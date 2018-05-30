using System;
using Foundatio.Repositories.Elasticsearch.Configuration;
using Nest;

namespace Exceptionless.Core.Repositories.Configuration {
    public sealed class StackIndex : VersionedIndex {
        private readonly IAppService _app;

        public StackIndex(ExceptionlessElasticConfiguration configuration) : base(configuration, configuration.App.Config.AppScopePrefix + "stacks", 1) {
            _app = configuration.App;
            AddType(Stack = new StackIndexType(this));
        }

        public StackIndexType Stack { get; }

        public override CreateIndexDescriptor ConfigureIndex(CreateIndexDescriptor idx) {
            return base.ConfigureIndex(idx.Settings(s => s
                .NumberOfShards(_app.Config.ElasticsearchNumberOfShards)
                .NumberOfReplicas(_app.Config.ElasticsearchNumberOfReplicas)
                .Priority(5)));
        }
    }
}