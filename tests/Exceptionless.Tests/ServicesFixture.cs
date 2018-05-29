using System;
using Exceptionless.Tests.Authentication;
using Exceptionless.Tests.Mail;
using Exceptionless.Core;
using Exceptionless.Core.Authentication;
using Exceptionless.Core.Mail;
using Exceptionless.Insulation.Configuration;
using Foundatio.Logging.Xunit;
using Foundatio.Utility;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Xunit.Abstractions;
using System.Collections.Generic;
using Xunit;

namespace Exceptionless.Tests {
    public class ServicesFixture : IDisposable {
        private readonly IDisposable _testSystemClock = TestSystemClock.Install();
        private Lazy<IServiceProvider> _serviceProvider;
        private readonly List<Action<IServiceCollection>> _serviceConfigurations = new List<Action<IServiceCollection>>();
        private readonly Lazy<AppConfiguration> _config;

        public ServicesFixture(ITestOutputHelper output) {
            TestLog = new TestLoggerFactory(output) {
                MinimumLevel = LogLevel.Information
            };
            TestLog.SetLogLevel<ScheduledTimer>(LogLevel.Warning);

            _serviceProvider = new Lazy<IServiceProvider>(() => GetServiceProvider());
            _config = new Lazy<AppConfiguration>(() => Services.GetRequiredService<AppConfiguration>());
        }

        private IServiceProvider GetServiceProvider() {
            var services = new ServiceCollection();

            var config = GetConfiguration();
            var appConfig = AppConfiguration.Load(config, "Development");
            services.AddSingleton<AppConfiguration>(appConfig);
            services.AddSingleton<ILoggerFactory>(TestLog);
            services.AddSingleton(typeof(ILogger<>), typeof(Logger<>));

            Web.Bootstrapper.RegisterServices(services, appConfig, TestLog);
            services.AddSingleton<IMailer, NullMailer>();
            services.AddSingleton<IDomainLoginProvider, TestDomainLoginProvider>();

            foreach (var configurator in _serviceConfigurations)
                configurator(services);

            return services.BuildServiceProvider();
        }
        
        public TestLoggerFactory TestLog { get; }
        public IServiceProvider Services => _serviceProvider.Value;
        public AppConfiguration Config => _config.Value;

        public void AddServicesConfiguration(Action<IServiceCollection> configuration) {
            _serviceConfigurations.Add(configuration);
        }

        protected virtual IConfigurationRoot GetConfiguration() {
            var config = new ConfigurationBuilder()
                .SetBasePath(AppContext.BaseDirectory)
                .AddYamlFile("appsettings.yml", optional: true, reloadOnChange: true)
                .AddEnvironmentVariables()
                .Build();

            return config;
        }

        public virtual void Dispose() {
            _testSystemClock.Dispose();
        }
    }

    public class TestWithServices : IClassFixture<ServicesFixture> {
        private readonly ServicesFixture _fixture;
        protected readonly ILogger _logger;

        public TestWithServices(ServicesFixture fixture) {
            _fixture = fixture;
            _logger = _fixture.TestLog.CreateLogger(GetType());
        }

        protected virtual TService GetService<TService>() => _fixture.Services.GetRequiredService<TService>();
        protected TestLoggerFactory TestLog => _fixture.TestLog;
        protected AppConfiguration Config => _fixture.Config;
    }
}