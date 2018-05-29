using System;
using Exceptionless.Core.Repositories.Configuration;
using Xunit.Abstractions;

namespace Exceptionless.Tests {
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