using System;
using System.Threading;
using System.Threading.Tasks;
using Exceptionless.Core.Extensions;
using Exceptionless.Core.Models;
using Exceptionless.Core.Repositories;
using Exceptionless.DateTimeExtensions;
using Foundatio.Jobs;
using Foundatio.Lock;
using Foundatio.Repositories;
using Foundatio.Utility;
using Microsoft.Extensions.Logging;

namespace Exceptionless.Core.Jobs {
    [Job(Description = "Deletes old events that are outside of a plans retention period.", InitialDelay = "15m", Interval = "1h")]
    public class RetentionLimitsJob : JobWithLockBase {
        private readonly ILockProvider _lockProvider;
        private readonly IAppService _app;
        private readonly IDatabase _db;

        public RetentionLimitsJob(IAppService app, IDatabase database) : base(app.LoggerFactory) {
            _app = app;
            _db = database;
            _lockProvider = new ThrottlingLockProvider(app.Cache, 1, TimeSpan.FromDays(1));
        }

        protected override Task<ILock> GetLockAsync(CancellationToken cancellationToken = default) {
            return _lockProvider.AcquireAsync(nameof(RetentionLimitsJob), TimeSpan.FromHours(2), new CancellationToken(true));
        }

        protected override async Task<JobResult> RunInternalAsync(JobContext context) {
            var results = await _db.Organizations.GetByRetentionDaysEnabledAsync(o => o.SnapshotPaging().PageLimit(100)).AnyContext();
            while (results.Documents.Count > 0 && !context.CancellationToken.IsCancellationRequested) {
                foreach (var organization in results.Documents) {
                    using (_logger.BeginScope(new ExceptionlessState().Organization(organization.Id))) {
                        await EnforceEventCountLimitsAsync(organization).AnyContext();

                        // Sleep so we are not hammering the backend.
                        await SystemClock.SleepAsync(TimeSpan.FromSeconds(5)).AnyContext();
                    }
                }

                if (context.CancellationToken.IsCancellationRequested || !await results.NextPageAsync().AnyContext())
                    break;

                if (results.Documents.Count > 0)
                    await context.RenewLockAsync().AnyContext();
            }

            return JobResult.Success;
        }

        private async Task EnforceEventCountLimitsAsync(Organization organization) {
            _logger.LogInformation("Enforcing event count limits for organization {OrganizationName} with Id: {organization}", organization.Name, organization.Id);

            try {
                int retentionDays = organization.RetentionDays;
                if (_app.Config.MaximumRetentionDays > 0 && retentionDays > _app.Config.MaximumRetentionDays)
                    retentionDays = _app.Config.MaximumRetentionDays;

                var cutoff = SystemClock.UtcNow.Date.SubtractDays(retentionDays);
                await _db.Events.RemoveAllByDateAsync(organization.Id, cutoff).AnyContext();
            } catch (Exception ex) {
                _logger.LogError(ex, "Error enforcing limits: org={OrganizationName} id={organization} message={Message}", organization.Name, organization.Id, ex.Message);
            }
        }
    }
}
