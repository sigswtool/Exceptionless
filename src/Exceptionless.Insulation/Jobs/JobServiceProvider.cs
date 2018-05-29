using System;
using Exceptionless.Core;
using Exceptionless.Core.Extensions;
using Exceptionless.Insulation.Configuration;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using LogLevel = Exceptionless.Logging.LogLevel;
using Serilog;
using Serilog.Events;
using Serilog.Sinks.Exceptionless;
using System.Threading;

namespace Exceptionless.Insulation.Jobs {
    public class JobServiceProvider {
        public static IServiceProvider GetServiceProvider(CancellationToken cancellationToken = default) {
            AppDomain.CurrentDomain.SetDataDirectory();

            string environment = Environment.GetEnvironmentVariable("AppMode");
            if (String.IsNullOrWhiteSpace(environment))
                environment = "Production";

            string currentDirectory = AppContext.BaseDirectory;
            var config = new ConfigurationBuilder()
                .SetBasePath(currentDirectory)
                .AddYamlFile("appsettings.yml", optional: true, reloadOnChange: true)
                .AddYamlFile($"appsettings.{environment}.yml", optional: true, reloadOnChange: true)
                .AddEnvironmentVariables()
                .Build();

            var appConfig = AppConfiguration.Load(config, environment);
            appConfig.DisableIndexConfiguration = true;

            var loggerConfig = new LoggerConfiguration().ReadFrom.Configuration(config);

            if (!String.IsNullOrEmpty(appConfig.ExceptionlessApiKey) && !String.IsNullOrEmpty(appConfig.ExceptionlessServerUrl)) {
                var client = ExceptionlessClient.Default;
                client.Configuration.SetDefaultMinLogLevel(LogLevel.Warn);
                client.Configuration.UseLogger(new SelfLogLogger());
                client.Configuration.SetVersion(appConfig.Version);
                client.Configuration.UseInMemoryStorage();

                if (String.IsNullOrEmpty(appConfig.InternalProjectId))
                    client.Configuration.Enabled = false;

                client.Configuration.ServerUrl = appConfig.ExceptionlessServerUrl;
                client.Startup(appConfig.ExceptionlessApiKey);

                loggerConfig.WriteTo.Sink(new ExceptionlessSink(), LogEventLevel.Verbose);
            }

            Log.Logger = loggerConfig.CreateLogger();
            Log.Information("Bootstrapping {AppMode} mode job ({InformationalVersion}) on {MachineName} using {@Settings} loaded from {Folder}", environment, appConfig.InformationalVersion, Environment.MachineName, appConfig, currentDirectory);

            var services = new ServiceCollection();
            services.AddLogging(b => b.AddSerilog(Log.Logger));
            services.AddSingleton<IConfiguration>(config);
            services.AddSingleton<AppConfiguration>(appConfig);
            Core.Bootstrapper.RegisterServices(services);
            Bootstrapper.RegisterServices(services, true);

            var container = services.BuildServiceProvider();

            Core.Bootstrapper.LogConfiguration(container, container.GetRequiredService<ILoggerFactory>());
            if (appConfig.EnableBootstrapStartupActions)
                container.RunStartupActionsAsync(cancellationToken).GetAwaiter().GetResult();

            return container;
        }
    }
}
