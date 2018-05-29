using System;
using System.Linq;
using Exceptionless.Core;
using Exceptionless.Core.Extensions;
using Exceptionless.Core.Geo;
using Exceptionless.Core.Mail;
using Exceptionless.Core.Queues.Models;
using Exceptionless.Core.Utility;
using Exceptionless.Insulation.Geo;
using Exceptionless.Insulation.Mail;
using Exceptionless.Insulation.Redis;
using Foundatio.Caching;
using Foundatio.Jobs;
using Foundatio.Messaging;
using Foundatio.Metrics;
using Foundatio.Queues;
using Foundatio.Serializer;
using Foundatio.Storage;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Serilog.Sinks.Exceptionless;
using StackExchange.Redis;

namespace Exceptionless.Insulation {
    public class Bootstrapper {
        public static void RegisterServices(IServiceCollection container, AppConfiguration config, bool runMaintenanceTasks) {
            if (!String.IsNullOrEmpty(config.ExceptionlessApiKey) && !String.IsNullOrEmpty(config.ExceptionlessServerUrl)) {
                var client = ExceptionlessClient.Default;
                client.Configuration.ServerUrl = config.ExceptionlessServerUrl;
                client.Configuration.ApiKey = config.ExceptionlessApiKey;

                client.Configuration.SetDefaultMinLogLevel(Logging.LogLevel.Warn);
                client.Configuration.UseLogger(new SelfLogLogger());
                client.Configuration.SetVersion(config.Version);
                if (String.IsNullOrEmpty(config.InternalProjectId))
                    client.Configuration.Enabled = false;

                client.Configuration.UseInMemoryStorage();
                client.Configuration.UseReferenceIds();

                container.ReplaceSingleton<ICoreLastReferenceIdManager, ExceptionlessClientCoreLastReferenceIdManager>();
                container.AddSingleton<ExceptionlessClient>(client);
            }

            if (!String.IsNullOrEmpty(config.GoogleGeocodingApiKey))
                container.ReplaceSingleton<IGeocodeService>(s => new GoogleGeocodeService(config.GoogleGeocodingApiKey));

            if (config.EnableMetricsReporting)
                container.ReplaceSingleton<IMetricsClient>(s => new StatsDMetricsClient(new StatsDMetricsClientOptions { ServerName = config.MetricsServerName, Port = config.MetricsServerPort, Prefix = "ex", LoggerFactory = s.GetRequiredService<ILoggerFactory>() }));

            if (config.AppMode != AppMode.Development)
                container.ReplaceSingleton<IMailSender, MailKitMailSender>();

            if (!String.IsNullOrEmpty(config.RedisConnectionString)) {
                container.AddSingleton<ConnectionMultiplexer>(s => {
                    var multiplexer = ConnectionMultiplexer.Connect(config.RedisConnectionString);
                    multiplexer.PreserveAsyncOrder = false;
                    return multiplexer;
                });

                if (config.HasAppScope)
                    container.ReplaceSingleton<ICacheClient>(s => new ScopedCacheClient(CreateRedisCacheClient(s), config.AppScope));
                else
                    container.ReplaceSingleton<ICacheClient>(CreateRedisCacheClient);

                container.ReplaceSingleton<IConnectionMapping, RedisConnectionMapping>();
                container.ReplaceSingleton<IMessageBus>(s => new RedisMessageBus(new RedisMessageBusOptions {
                    Subscriber = s.GetRequiredService<ConnectionMultiplexer>().GetSubscriber(),
                    Topic = $"{config.AppScopePrefix}messages",
                    Serializer = s.GetRequiredService<ISerializer>(),
                    LoggerFactory = s.GetRequiredService<ILoggerFactory>()
                }));
            }

            if (!String.IsNullOrEmpty(config.AzureStorageQueueConnectionString)) {
                container.ReplaceSingleton(s => CreateAzureStorageQueue<EventPost>(s, config, retries: 1));
                container.ReplaceSingleton(s => CreateAzureStorageQueue<EventUserDescription>(s, config));
                container.ReplaceSingleton(s => CreateAzureStorageQueue<EventNotificationWorkItem>(s, config));
                container.ReplaceSingleton(s => CreateAzureStorageQueue<WebHookNotification>(s, config));
                container.ReplaceSingleton(s => CreateAzureStorageQueue<MailMessage>(s, config));
                container.ReplaceSingleton(s => CreateAzureStorageQueue<WorkItemData>(s, config, workItemTimeout: TimeSpan.FromHours(1)));
            } else if (!String.IsNullOrEmpty(config.RedisConnectionString)) {
                container.ReplaceSingleton(s => CreateRedisQueue<EventPost>(s, config, runMaintenanceTasks, retries: 1));
                container.ReplaceSingleton(s => CreateRedisQueue<EventUserDescription>(s, config, runMaintenanceTasks));
                container.ReplaceSingleton(s => CreateRedisQueue<EventNotificationWorkItem>(s, config, runMaintenanceTasks));
                container.ReplaceSingleton(s => CreateRedisQueue<WebHookNotification>(s, config, runMaintenanceTasks));
                container.ReplaceSingleton(s => CreateRedisQueue<MailMessage>(s, config, runMaintenanceTasks));
                container.ReplaceSingleton(s => CreateRedisQueue<WorkItemData>(s, config, runMaintenanceTasks, workItemTimeout: TimeSpan.FromHours(1)));
            }

            if (!String.IsNullOrEmpty(config.AzureStorageConnectionString)) {
                container.ReplaceSingleton<IFileStorage>(s => new AzureFileStorage(new AzureFileStorageOptions {
                    ConnectionString = config.AzureStorageConnectionString,
                    ContainerName =  $"{config.AppScopePrefix}ex-events",
                    Serializer = s.GetRequiredService<ITextSerializer>(),
                    LoggerFactory = s.GetRequiredService<ILoggerFactory>()
                }));
            } else if (!String.IsNullOrEmpty(config.AliyunStorageConnectionString)) {
                container.ReplaceSingleton<IFileStorage>(s => new AliyunFileStorage(new AliyunFileStorageOptions {
                    ConnectionString = config.AliyunStorageConnectionString,
                    Serializer = s.GetRequiredService<ITextSerializer>(),
                    LoggerFactory = s.GetRequiredService<ILoggerFactory>()
                }));
            } else if (!String.IsNullOrEmpty(config.MinioStorageConnectionString)) {
                container.ReplaceSingleton<IFileStorage>(s => new MinioFileStorage(new MinioFileStorageOptions {
                    ConnectionString = config.MinioStorageConnectionString,
                    Serializer = s.GetRequiredService<ITextSerializer>(),
                    LoggerFactory = s.GetRequiredService<ILoggerFactory>()
                }));
            }
        }

        private static IQueue<T> CreateAzureStorageQueue<T>(IServiceProvider container, AppConfiguration config, int retries = 2, TimeSpan? workItemTimeout = null) where T : class {
            return new AzureStorageQueue<T>(new AzureStorageQueueOptions<T> {
                ConnectionString = config.AzureStorageQueueConnectionString,
                Name = GetQueueName<T>(config).ToLowerInvariant(),
                Retries = retries,
                Behaviors = container.GetServices<IQueueBehavior<T>>().ToList(),
                WorkItemTimeout = workItemTimeout.GetValueOrDefault(TimeSpan.FromMinutes(5.0)),
                Serializer = container.GetRequiredService<ISerializer>(),
                LoggerFactory = container.GetRequiredService<ILoggerFactory>()
            });
        }

        private static IQueue<T> CreateRedisQueue<T>(IServiceProvider container, AppConfiguration config, bool runMaintenanceTasks, int retries = 2, TimeSpan? workItemTimeout = null) where T : class {
            return new RedisQueue<T>(new RedisQueueOptions<T> {
                ConnectionMultiplexer = container.GetRequiredService<ConnectionMultiplexer>(),
                Name = GetQueueName<T>(config),
                Retries = retries,
                Behaviors = container.GetServices<IQueueBehavior<T>>().ToList(),
                WorkItemTimeout = workItemTimeout.GetValueOrDefault(TimeSpan.FromMinutes(5.0)),
                RunMaintenanceTasks = runMaintenanceTasks,
                Serializer = container.GetRequiredService<ISerializer>(),
                LoggerFactory = container.GetRequiredService<ILoggerFactory>()
            });
        }

        private static string GetQueueName<T>(AppConfiguration config) {
            return String.Concat(config.QueueScopePrefix, typeof(T).Name);
        }

        private static RedisCacheClient CreateRedisCacheClient(IServiceProvider container) {
            return new RedisCacheClient(new RedisCacheClientOptions {
                ConnectionMultiplexer = container.GetRequiredService<ConnectionMultiplexer>(),
                Serializer = container.GetRequiredService<ISerializer>(),
                LoggerFactory = container.GetRequiredService<ILoggerFactory>()
            });
        }
    }
}