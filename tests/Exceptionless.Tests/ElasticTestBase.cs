using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Exceptionless.Core;
using Exceptionless.Core.Repositories.Configuration;
using Foundatio.Utility;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Nest;
using Xunit.Abstractions;
using Exceptionless.Insulation.Configuration;
using Foundatio.Logging.Xunit;
using Microsoft.Extensions.Logging;
using Exceptionless.Core.Mail;
using Exceptionless.Core.Authentication;
using Exceptionless.Tests.Authentication;
using Exceptionless.Tests.Mail;
using Exceptionless.Core.Extensions;

namespace Exceptionless.Tests {
    public class TestWithElasticsearch : TestWithLoggingBase, IDisposable {
        protected ExceptionlessElasticConfiguration _configuration;
        private readonly IDisposable _testSystemClock = TestSystemClock.Install();
        private Lazy<IServiceProvider> _serviceProvider;
        private readonly List<Action<IServiceCollection>> _serviceConfigurations = new List<Action<IServiceCollection>>();

        private static object _lock = new object();
        private static Settings _baseSettings;
        private static ServiceCollection _baseServices;

        public TestWithElasticsearch(ITestOutputHelper output) : base(output) {
            AddServicesConfiguration(s => {
                s.AddSingleton<ILoggerFactory>(Log);
                s.AddSingleton(typeof(ILogger<>), typeof(Logger<>));
                Web.Bootstrapper.RegisterServices(s, Log);
                s.AddSingleton<IMailer, NullMailer>();
                s.AddSingleton<IDomainLoginProvider, TestDomainLoginProvider>();
            });

            if (_baseSettings == null || _baseServices == null) {
                lock (_lock) {
                    _baseSettings = Settings.ReadFromConfiguration(GetConfiguration(), "Development");
                    _baseServices = GetServices();
                }
            }

            Settings = _baseSettings.DeepClone();
            Settings.AppScope = "test-" + Guid.NewGuid().ToString("N").Substring(0, 10);
            Log.MinimumLevel = Microsoft.Extensions.Logging.LogLevel.Information;
            Log.SetLogLevel<ScheduledTimer>(Microsoft.Extensions.Logging.LogLevel.Warning);

            _serviceProvider = new Lazy<IServiceProvider>(() => {
                _baseServices.ReplaceSingleton(Settings);
                var serviceProvider = _baseServices.BuildServiceProvider();
                _configuration = serviceProvider.GetRequiredService<ExceptionlessElasticConfiguration>();
                ConfigureIndexesAsync().GetAwaiter().GetResult();
                return serviceProvider;
            });
        }

        public IServiceProvider Services => _serviceProvider.Value;
        public Settings Settings { get; }
        protected TService GetService<TService>() => Services.GetRequiredService<TService>();

        public void AddServicesConfiguration(Action<IServiceCollection> configuration) {
            _serviceConfigurations.Add(configuration);
        }

        private IConfigurationRoot GetConfiguration() {
            var config = new ConfigurationBuilder()
                .SetBasePath(AppContext.BaseDirectory)
                .AddYamlFile("appsettings.yml", optional: true, reloadOnChange: true)
                .AddEnvironmentVariables()
                .Build();

            return config;
        }

        private ServiceCollection GetServices() {
            var services = new ServiceCollection();

            foreach (var configurator in _serviceConfigurations)
                configurator(services);

            return services;
        }

        private async Task ConfigureIndexesAsync() {
            var oldLoggingLevel = Log.MinimumLevel;
            Log.MinimumLevel = Microsoft.Extensions.Logging.LogLevel.Warning;

            await _configuration.ConfigureIndexesAsync();

            Log.MinimumLevel = oldLoggingLevel;
        }

        public void Dispose() {
            _testSystemClock.Dispose();
            _configuration.Client.DeleteIndexAsync(new DeleteIndexRequest(Settings.AppScopePrefix + "*"));
            _configuration.Dispose();
        }
    }
}