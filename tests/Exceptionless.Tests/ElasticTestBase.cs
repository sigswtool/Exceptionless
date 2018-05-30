using System;
using Exceptionless.Core;
using Exceptionless.Core.Repositories.Configuration;
using Foundatio.Utility;
using Xunit.Abstractions;

namespace Exceptionless.Tests {
    public class ElasticsearchFixture : IDisposable {
        private readonly Lazy<AppConfiguration> _config;

        public ElasticsearchFixture(ITestOutputHelper output) {
            TestLog = new TestLoggerFactory(output) {
                MinimumLevel = LogLevel.Information
            };
            TestLog.SetLogLevel<ScheduledTimer>(LogLevel.Warning);

            _config = new Lazy<AppConfiguration>(() => Services.GetRequiredService<AppConfiguration>());
        }

        public ExceptionlessElasticConfiguration Config { get; }
        public IServiceProvider Services => null;
        public IAppService App => Config.App;

        public void Dispose() { }
    }

    public class TestWithElasticsearch : TestWithServer {
        protected readonly ExceptionlessElasticConfiguration _configuration;

        public TestWithElasticsearch(TestServerFixture fixture) : base(fixture) {
            _configuration = GetService<ExceptionlessElasticConfiguration>();
            _configuration.DeleteIndexesAsync().GetAwaiter().GetResult();
            _configuration.ConfigureIndexesAsync(beginReindexingOutdated: false).GetAwaiter().GetResult();
        }

        public override void Dispose() {
            _configuration.Dispose();
            base.Dispose();
        }
    }
}