using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Exceptionless.Core.Extensions;
using Exceptionless.Core.Models.WorkItems;
using Exceptionless.Core.Repositories;
using Foundatio.Jobs;
using Foundatio.Lock;
using Foundatio.Repositories;
using Microsoft.Extensions.Logging;
using Stripe;

namespace Exceptionless.Core.Jobs.WorkItemHandlers {
    public class RemoveOrganizationWorkItemHandler : WorkItemHandlerBase {
        private readonly ILockProvider _lockProvider;
        private readonly IAppService _app;
        private readonly IDatabase _db;

        public RemoveOrganizationWorkItemHandler(IAppService app, IDatabase db) : base(app.LoggerFactory) {
            _db = db;
            _lockProvider = new CacheLockProvider(_app.Cache, app.MessageBus);
        }

        public override Task<ILock> GetWorkItemLockAsync(object workItem, CancellationToken cancellationToken = new CancellationToken()) {
            string cacheKey = $"{nameof(RemoveOrganizationWorkItemHandler)}:{((RemoveOrganizationWorkItem)workItem).OrganizationId}";
            return _lockProvider.AcquireAsync(cacheKey, TimeSpan.FromMinutes(15), new CancellationToken(true));
        }

        public override async Task HandleItemAsync(WorkItemContext context) {
            var wi = context.GetData<RemoveOrganizationWorkItem>();
            using (Log.BeginScope(new ExceptionlessState().Organization(wi.OrganizationId))) {
                Log.LogInformation("Received remove organization work item for: {organization}", wi.OrganizationId);

                await context.ReportProgressAsync(0, "Starting deletion...").AnyContext();
                var organization = await _db.Organizations.GetByIdAsync(wi.OrganizationId).AnyContext();
                if (organization == null) {
                    await context.ReportProgressAsync(100, "Organization deleted").AnyContext();
                    return;
                }

                await context.ReportProgressAsync(10, "Removing subscriptions").AnyContext();
                if (!String.IsNullOrEmpty(organization.StripeCustomerId)) {
                    Log.LogInformation("Canceling stripe subscription for the organization {OrganizationName} with Id: {organization}.", organization.Name, organization.Id);

                    var subscriptionService = new StripeSubscriptionService(_app.Config.StripeApiKey);
                    var subscriptions = (await subscriptionService.ListAsync(new StripeSubscriptionListOptions { CustomerId = organization.StripeCustomerId }).AnyContext()).Where(s => !s.CanceledAt.HasValue);
                    foreach (var subscription in subscriptions)
                        await subscriptionService.CancelAsync(subscription.Id).AnyContext();
                }

                await context.ReportProgressAsync(20, "Removing users").AnyContext();
                var users = await _db.Users.GetByOrganizationIdAsync(organization.Id).AnyContext();
                foreach (var user in users.Documents) {
                    // delete the user if they are not associated to any other organizations and they are not the current user
                    if (user.OrganizationIds.All(oid => String.Equals(oid, organization.Id)) && !String.Equals(user.Id, wi.CurrentUserId)) {
                        Log.LogInformation("Removing user {user} as they do not belong to any other organizations.", user.Id);
                        await _db.Users.RemoveAsync(user.Id).AnyContext();
                    } else {
                        Log.LogInformation("Removing user {user} from organization {OrganizationName} with Id: {organization}", user.Id, organization.Name, organization.Id);
                        user.OrganizationIds.Remove(organization.Id);
                        await _db.Users.SaveAsync(user, o => o.Cache()).AnyContext();
                    }
                }

                await context.ReportProgressAsync(30, "Removing tokens").AnyContext();
                await _db.Tokens.RemoveAllByOrganizationIdAsync(organization.Id).AnyContext();

                await context.ReportProgressAsync(40, "Removing web hooks").AnyContext();
                await _db.WebHooks.RemoveAllByOrganizationIdAsync(organization.Id).AnyContext();

                await context.ReportProgressAsync(50, "Removing projects").AnyContext();
                var projects = await _db.Projects.GetByOrganizationIdAsync(organization.Id).AnyContext();
                if (wi.IsGlobalAdmin && projects.Total > 0) {
                    int completed = 1;
                    foreach (var project in projects.Documents) {
                        using (Log.BeginScope(new ExceptionlessState().Organization(wi.OrganizationId).Project(project.Id))) {
                            Log.LogInformation("Resetting all project data for project {ProjectName} with Id: {project}.", project.Name, project.Id);
                            await _db.Events.RemoveAllByProjectIdAsync(organization.Id, project.Id).AnyContext();
                            await _db.Stacks.RemoveAllByProjectIdAsync(organization.Id, project.Id).AnyContext();
                            await context.ReportProgressAsync(CalculateProgress(projects.Total, completed++, 51, 89), "Removing projects...").AnyContext();
                        }
                    }

                    Log.LogInformation("Deleting all projects for organization {OrganizationName} with Id: {organization}.", organization.Name, organization.Id);
                    await _db.Projects.RemoveAsync(projects.Documents).AnyContext();
                }

                Log.LogInformation("Deleting organization {OrganizationName} with Id: {organization}.", organization.Name, organization.Id);
                await context.ReportProgressAsync(90, "Removing organization").AnyContext();
                await _db.Organizations.RemoveAsync(organization.Id).AnyContext();

                await context.ReportProgressAsync(100, "Organization deleted").AnyContext();
            }
        }
    }
}
