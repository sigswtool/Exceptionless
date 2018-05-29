using System;
using Foundatio.Repositories.Elasticsearch.Configuration;
using Nest;

namespace Exceptionless.Core.Repositories.Configuration {
    public sealed class StackIndex : VersionedIndex {
        public StackIndex(IElasticConfiguration configuration) : base(configuration, AppConfiguration.Current.AppScopePrefix + "stacks", 1) {
            AddType(Stack = new StackIndexType(this));
        }

        public StackIndexType Stack { get; }

        public override CreateIndexDescriptor ConfigureIndex(CreateIndexDescriptor idx) {
            return base.ConfigureIndex(idx.Settings(s => s
                .NumberOfShards(AppConfiguration.Current.ElasticsearchNumberOfShards)
                .NumberOfReplicas(AppConfiguration.Current.ElasticsearchNumberOfReplicas)
                .Priority(5)));
        }
    }
}