using System;
using Foundatio.Repositories.Elasticsearch.Configuration;
using Nest;

namespace Exceptionless.Core.Repositories.Configuration {
    public sealed class OrganizationIndex : VersionedIndex {
        internal const string KEYWORD_LOWERCASE_ANALYZER = "keyword_lowercase";
        private readonly IAppService _app;

        public OrganizationIndex(ExceptionlessElasticConfiguration configuration) : base(configuration, configuration.App.Config.AppScopePrefix + "organizations", 1) {
            _app = configuration.App;
            AddType(Organization = new OrganizationIndexType(this));
            AddType(Project = new ProjectIndexType(this));
            AddType(Token = new TokenIndexType(this));
            AddType(User = new UserIndexType(this));
            AddType(WebHook = new WebHookIndexType(this));
        }

        public override CreateIndexDescriptor ConfigureIndex(CreateIndexDescriptor idx) {
            return base.ConfigureIndex(idx.Settings(s => s
                .Analysis(d => d.Analyzers(b => b.Custom(KEYWORD_LOWERCASE_ANALYZER, c => c.Filters("lowercase").Tokenizer("keyword"))))
                .NumberOfShards(_app.Config.ElasticsearchNumberOfShards)
                .NumberOfReplicas(_app.Config.ElasticsearchNumberOfReplicas)
                .Priority(10)));
        }

        public OrganizationIndexType Organization { get; }
        public ProjectIndexType Project { get; }
        public TokenIndexType Token { get; }
        public UserIndexType User { get; }
        public WebHookIndexType WebHook { get; }
    }
}