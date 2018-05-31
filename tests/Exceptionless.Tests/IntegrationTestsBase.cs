using System;
using System.Net.Http;
using System.Threading.Tasks;
using Exceptionless.Tests.Utility;
using Exceptionless.Core.Repositories.Configuration;
using FluentRest;
using Foundatio.Serializer;
using Xunit.Abstractions;
using Microsoft.Extensions.DependencyInjection;
using Exceptionless.Web;
using Newtonsoft.Json;
using Exceptionless.Tests.Extensions;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;
using System.Threading;
using Nest;
using System.Linq;
using Foundatio.Caching;
using Foundatio.Storage;
using Foundatio.Queues;
using Foundatio.Jobs;
using Exceptionless.Core.Queues.Models;
using Microsoft.Extensions.Logging;
using Exceptionless.Core.Mail;
using Exceptionless.Tests.Mail;
using Exceptionless.Core.Authentication;
using Exceptionless.Tests.Authentication;
using Microsoft.AspNetCore.Hosting;
using Exceptionless.Core.Extensions;

namespace Exceptionless.Tests {
    public class AppServerFixture : WebApplicationFactory<Startup> {
        protected override void ConfigureWebHost(IWebHostBuilder builder) {
            builder.ConfigureServices(s => {
                s.ReplaceSingleton<IMailer, NullMailer>();
                s.ReplaceSingleton<IDomainLoginProvider, TestDomainLoginProvider>();
            });
        }
    }

    public class TestWithServer : TestWithServices, IClassFixture<AppServerFixture>, IDisposable {
        protected readonly AppServerFixture _fixture;
        protected readonly FluentClient _client;
        protected readonly ISerializer _serializer;
        protected readonly ExceptionlessElasticConfiguration _configuration;
        private readonly IServiceProvider _serviceProvider;

        public TestWithServer(AppServerFixture appServerFixture, ServicesFixture fixture, ITestOutputHelper output) : base(fixture, output) {
            _fixture = appServerFixture;

            var options = new WebApplicationFactoryClientOptions {
                BaseAddress = new Uri("http://localhost/api/v2")
            };

            var factory = appServerFixture.WithWebHostBuilder(b => {
                b.ConfigureServices(s => {
                    s.ReplaceSingleton<ILoggerFactory>(Log);
                });
            });
            var httpClient = factory.CreateClient(options);
            _serviceProvider = factory.Server.Host.Services;

            var loggers = _serviceProvider.GetServices<ILoggerFactory>();
            var mailers = _serviceProvider.GetServices<IMailer>();
            var logins = _serviceProvider.GetServices<IDomainLoginProvider>();
            Assert.Same(GetService<ILoggerFactory>(), Log);
            var settings = GetService<JsonSerializerSettings>();
            _client = new FluentClient(httpClient, new JsonContentSerializer(settings));
            _configuration = GetService<ExceptionlessElasticConfiguration>();

            ResetAllAsync().GetAwaiter().GetResult();
        }

        protected override TService GetService<TService>() {
            return _serviceProvider.GetRequiredService<TService>();
        }

        protected async Task<HttpResponseMessage> SendRequest(Action<AppSendBuilder> configure) {
            var request = new HttpRequestMessage(HttpMethod.Get, _client.HttpClient.BaseAddress);
            var builder = new AppSendBuilder(request);
            configure(builder);

            var response = await _client.SendAsync(request);

            var expectedStatus = request.GetExpectedStatus();
            if (expectedStatus.HasValue && expectedStatus.Value != response.StatusCode) {
                string content = await response.Content.ReadAsStringAsync();
                if (content.Length > 1000)
                    content = content.Substring(0, 1000);

                throw new HttpRequestException($"Expected status code {expectedStatus.Value} but received status code {response.StatusCode} ({response.ReasonPhrase}).\n" + content);
            }

            return response;
        }

        protected async Task<T> SendRequestAs<T>(Action<AppSendBuilder> configure) {
            var response = await SendRequest(configure);
            return await response.DeserializeAsync<T>();
        }

        protected async Task<HttpResponseMessage> SendGlobalAdminRequest(Action<AppSendBuilder> configure) {
            return await SendRequest(b => {
                b.AsGlobalAdminUser();
                configure(b);
            });
        }

        protected async Task<T> SendGlobalAdminRequestAs<T>(Action<AppSendBuilder> configure) {
            var response = await SendGlobalAdminRequest(configure);
            return await response.DeserializeAsync<T>();
        }

        protected async Task<T> DeserializeResponse<T>(HttpResponseMessage response) {
            return await response.DeserializeAsync<T>();
        }

        private static bool _indexesHaveBeenConfigured = false;
        private static readonly SemaphoreSlim _semaphoreSlim = new SemaphoreSlim(1, 1);
        protected async Task ResetAllAsync() {
            await _semaphoreSlim.WaitAsync();
            try {
                var oldLoggingLevel = Log.MinimumLevel;
                Log.MinimumLevel = Microsoft.Extensions.Logging.LogLevel.Warning;

                if (!_indexesHaveBeenConfigured) {
                    await _configuration.Client.DeleteIndexAsync(new DeleteIndexRequest(Settings.AppScopePrefix + "*"));
                    await _configuration.ConfigureIndexesAsync();
                    _indexesHaveBeenConfigured = true;
                } else {
                    await _configuration.Client.DeleteByQueryAsync(new DeleteByQueryRequest(Settings.AppScopePrefix + "*") {
                        Query = new MatchAllQuery(),
                        IgnoreUnavailable = true,
                        Refresh = true
                    });
                }

                var cacheClient = GetService<ICacheClient>();
                await cacheClient.RemoveAllAsync();

                var fileStorage = GetService<IFileStorage>();
                await fileStorage.DeleteFilesAsync(await fileStorage.GetFileListAsync());

                await GetService<IQueue<WorkItemData>>().DeleteQueueAsync();
                await GetService<IQueue<EventPost>>().DeleteQueueAsync();

                Log.MinimumLevel = oldLoggingLevel;
            }
            finally {
                _semaphoreSlim.Release();
            }
        }

        public void Dispose() {
            _configuration.Dispose();
        }
    }
}