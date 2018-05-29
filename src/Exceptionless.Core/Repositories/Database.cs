namespace Exceptionless.Core.Repositories {
    public class Database : IDatabase {
        public Database(IEventRepository events, IOrganizationRepository organizations, IProjectRepository projects,
            IStackRepository stacks, ITokenRepository tokens, IUserRepository users, IWebHookRepository webHooks) {
            Events = events;
            Organizations = organizations;
            Projects = projects;
            Stacks = stacks;
            Tokens = tokens;
            Users = users;
            WebHooks = webHooks;
        }

        public IEventRepository Events { get; }
        public IOrganizationRepository Organizations { get; }
        public IProjectRepository Projects { get; }
        public IStackRepository Stacks { get; }
        public ITokenRepository Tokens { get; }
        public IUserRepository Users { get; }
        public IWebHookRepository WebHooks { get; }
    }
}
