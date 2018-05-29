namespace Exceptionless.Core.Repositories {
    public interface IDatabase {
        IEventRepository Events { get; }
        IOrganizationRepository Organizations { get; }
        IProjectRepository Projects { get; }
        IStackRepository Stacks { get; }
        ITokenRepository Tokens { get; }
        IUserRepository Users { get; }
        IWebHookRepository WebHooks { get; }
    }
}
