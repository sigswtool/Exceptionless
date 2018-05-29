using System;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using Exceptionless.Core.Models;
using Exceptionless.Core.Repositories.Configuration;
using FluentRest;
using Foundatio.Serializer;
using Xunit.Abstractions;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using Exceptionless.Core;
using Foundatio.Utility;
using Foundatio.Logging.Xunit;
using Microsoft.Extensions.Logging;

namespace Exceptionless.Tests {
    public class TestServerFixture : IDisposable {
        private readonly IDisposable _testSystemClock = TestSystemClock.Install();
        private readonly Lazy<AppConfiguration> _config;

        public TestServerFixture(ITestOutputHelper output) {
            TestLog = new TestLoggerFactory(output) {
                MinimumLevel = LogLevel.Information
            };
            TestLog.SetLogLevel<ScheduledTimer>(LogLevel.Warning);

            _config = new Lazy<AppConfiguration>(() => Services.GetRequiredService<AppConfiguration>());
        }

        public TestLoggerFactory TestLog { get; }
        public IServiceProvider Services => null;
        public AppConfiguration Config => _config.Value;

        public void Dispose() {}
    }

    public class TestWithServer : IClassFixture<TestServerFixture> {
        private readonly TestServerFixture _fixture;
        protected readonly ExceptionlessElasticConfiguration _configuration;
        protected readonly TestServer _server;
        protected readonly FluentClient _client;
        protected readonly HttpClient _httpClient;
        protected readonly ISerializer _serializer;
        protected readonly ILogger _logger;

        public TestWithServer(TestServerFixture fixture) {
            _fixture = fixture;
            _logger = _fixture.TestLog.CreateLogger(GetType());
        }

        protected virtual TService GetService<TService>() => _fixture.Services.GetRequiredService<TService>();
        protected TestLoggerFactory TestLog => _fixture.TestLog;
        protected AppConfiguration Config => _fixture.Config;

        protected Task<FluentResponse> SendRequest(Action<SendBuilder> configure) {
            var request = _client.CreateRequest();
            var builder = new SendBuilder(request);
            configure(builder);

            if (request.ContentData != null && !(request.ContentData is HttpContent)) {
                string mediaType = !String.IsNullOrEmpty(request.ContentType) ? request.ContentType : "application/json";
                request.ContentData = new StringContent(_serializer.SerializeToString(request.ContentData), Encoding.UTF8, mediaType);
            }

            return _client.SendAsync(request);
        }

        protected async Task<T> SendRequestAs<T>(Action<SendBuilder> configure) {
            var response = await SendRequest(configure);
            return await DeserializeResponse<T>(response);
        }

        protected Task<FluentResponse> SendTokenRequest(Token token, Action<SendBuilder> configure) {
            return SendTokenRequest(token.Id, configure);
        }

        protected Task<FluentResponse> SendTokenRequest(string token, Action<SendBuilder> configure) {
            return SendRequest(s => {
                s.BearerToken(token);
                configure(s);
            });
        }

        protected async Task<T> SendTokenRequestAs<T>(string token, Action<SendBuilder> configure) {
            var response = await SendTokenRequest(token, configure);
            return await DeserializeResponse<T>(response);
        }

        protected Task<FluentResponse> SendUserRequest(string username, string password, Action<SendBuilder> configure) {
            return SendRequest(s => {
                s.BasicAuthorization(username, password);
                configure(s);
            });
        }

        protected async Task<T> SendUserRequestAs<T>(string username, string password, Action<SendBuilder> configure) {
            var response = await SendUserRequest(username, password, configure);
            return await DeserializeResponse<T>(response);
        }

        protected async Task<T> DeserializeResponse<T>(FluentResponse response) {
            string json = await response.HttpContent.ReadAsStringAsync();
            response.EnsureSuccessStatusCode();
            return _serializer.Deserialize<T>(json);
        }
    }
}