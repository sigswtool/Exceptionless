using Foundatio.Caching;
using Foundatio.Lock;
using Foundatio.Messaging;
using Foundatio.Metrics;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;

namespace Exceptionless.Core {
    public interface IAppService {
        AppConfiguration Config { get; }
        ILoggerFactory LoggerFactory { get; }
        IMetricsClient Metrics { get; }
        ICacheClient Cache { get; }
        ILockProvider Locks { get; }
        IMessageBus MessageBus { get; }
        JsonSerializerSettings JsonSerializerSettings { get; }
    }

    public class AppService : IAppService {
        public AppService(AppConfiguration config, ILoggerFactory loggerFactory, IMetricsClient metrics,
            ICacheClient cache, ILockProvider locks, IMessageBus messageBus, JsonSerializerSettings jsonSerializerSettings) {
            Config = config;
            LoggerFactory = loggerFactory;
            Metrics = metrics;
            Cache = cache;
            Locks = locks;
            MessageBus = messageBus;
            JsonSerializerSettings = jsonSerializerSettings;
        }

        public AppConfiguration Config { get; }
        public ILoggerFactory LoggerFactory { get; }
        public IMetricsClient Metrics { get; }
        public ICacheClient Cache { get; }
        public ILockProvider Locks { get; }
        public IMessageBus MessageBus { get; }
        public JsonSerializerSettings JsonSerializerSettings { get; }
    }
}
