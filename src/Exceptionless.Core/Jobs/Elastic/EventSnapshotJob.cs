using System.Threading;
using System.Threading.Tasks;
using Exceptionless.Core.Repositories.Configuration;
using Foundatio.Jobs;
using Foundatio.Repositories.Elasticsearch.Jobs;

namespace Exceptionless.Core.Jobs.Elastic {
    [Job(Description = "Takes an Elasticsearch events index snapshot ", IsContinuous = false)]
    public class EventSnapshotJob : SnapshotJob {
        private readonly IAppService _app;

        public EventSnapshotJob(ExceptionlessElasticConfiguration configuration, IAppService app) : base(configuration.Client, app.Locks, app.LoggerFactory) {
            _app = app;
            Repository = app.Config.AppScopePrefix + "ex_events";
            IncludedIndexes.Add("events*");
        }

        public override Task<JobResult> RunAsync(CancellationToken cancellationToken = new CancellationToken()) {
            if (!_app.Config.EnableSnapshotJobs)
                return Task.FromResult(JobResult.Success);

            return base.RunAsync(cancellationToken);
        }
    }
}