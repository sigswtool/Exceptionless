using System;
using System.Threading;
using System.Threading.Tasks;
using Exceptionless.Core.Extensions;
using Exceptionless.Core.Messaging.Models;
using Exceptionless.Core.Models;
using Exceptionless.Core.Repositories;
using Exceptionless.Core.Utility;
using Foundatio.Repositories.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Exceptionless.Core.Services {
    public class MessageService : IDisposable, IStartupAction {
        private readonly IDatabase _db;
        private readonly IConnectionMapping _connectionMapping;
        private readonly IAppService _app;
        private readonly ILogger _logger;

        public MessageService(IDatabase db, IConnectionMapping connectionMapping, IAppService app) {
            _db = db;
            _connectionMapping = connectionMapping;
            _logger = app.LoggerFactory?.CreateLogger<MessageService>() ?? NullLogger<MessageService>.Instance;
        }

        public Task RunAsync(CancellationToken shutdownToken = default) {
            if (!_app.Config.EnableRepositoryNotifications)
                return Task.CompletedTask;

            if (_db.Stacks is StackRepository sr)
                sr.BeforePublishEntityChanged.AddHandler(BeforePublishStackEntityChanged);
            if (_db.Events is EventRepository er)
                er.BeforePublishEntityChanged.AddHandler(BeforePublishEventEntityChanged);

            return Task.CompletedTask;
        }

        private async Task BeforePublishStackEntityChanged(object sender, BeforePublishEntityChangedEventArgs<Stack> args) {
            args.Cancel = await GetNumberOfListeners(args.Message).AnyContext() == 0;
            if (args.Cancel && _logger.IsEnabled(LogLevel.Trace))
                _logger.LogTrace("Cancelled Stack Entity Changed Message: {@Message}", args.Message);
        }

        private async Task BeforePublishEventEntityChanged(object sender, BeforePublishEntityChangedEventArgs<PersistentEvent> args) {
            args.Cancel = await GetNumberOfListeners(args.Message).AnyContext() == 0;
            if (args.Cancel && _logger.IsEnabled(LogLevel.Trace))
                _logger.LogTrace("Cancelled Persistent Event Entity Changed Message: {@Message}", args.Message);
        }

        private Task<int> GetNumberOfListeners(EntityChanged message) {
            var entityChanged = ExtendedEntityChanged.Create(message, false);
            if (String.IsNullOrEmpty(entityChanged.OrganizationId))
                return Task.FromResult(1); // Return 1 as we have no idea if people are listening.

            return _connectionMapping.GetGroupConnectionCountAsync(entityChanged.OrganizationId);
        }

        public void Dispose() {
            if (_db.Stacks is StackRepository sr)
                sr.BeforePublishEntityChanged.RemoveHandler(BeforePublishStackEntityChanged);
            if (_db.Events is EventRepository er)
                er.BeforePublishEntityChanged.RemoveHandler(BeforePublishEventEntityChanged);
        }
    }
}