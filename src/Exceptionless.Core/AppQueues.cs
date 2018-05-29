using Exceptionless.Core.Queues.Models;
using Foundatio.Jobs;
using Foundatio.Queues;

namespace Exceptionless.Core {
    public interface IAppQueues {
        IQueue<EventPost> EventPosts { get; }
        IQueue<EventUserDescription> EventUserDescriptions { get; }
        IQueue<EventNotificationWorkItem> EventNotificationWorkItems { get; }
        IQueue<WebHookNotification> WebHookNotifications { get; }
        IQueue<MailMessage> MailMessages { get; }
        IQueue<WorkItemData> WorkItems { get; }
    }

    public class AppQueues : IAppQueues {
        public AppQueues(IQueue<EventPost> eventPosts, IQueue<EventUserDescription> eventUserDescriptions, IQueue<EventNotificationWorkItem> eventNotificationWorkItems,
            IQueue<WebHookNotification> webHookNotifications, IQueue<MailMessage> mailMessages, IQueue<WorkItemData> workItems) {
            EventPosts = eventPosts;
            EventUserDescriptions = eventUserDescriptions;
            EventNotificationWorkItems = eventNotificationWorkItems;
            WebHookNotifications = webHookNotifications;
            WorkItems = workItems;
        }

        public IQueue<EventPost> EventPosts { get; }
        public IQueue<EventUserDescription> EventUserDescriptions { get; }
        public IQueue<EventNotificationWorkItem> EventNotificationWorkItems { get; }
        public IQueue<WebHookNotification> WebHookNotifications { get; }
        public IQueue<MailMessage> MailMessages { get; }
        public IQueue<WorkItemData> WorkItems { get; }
    }
}
