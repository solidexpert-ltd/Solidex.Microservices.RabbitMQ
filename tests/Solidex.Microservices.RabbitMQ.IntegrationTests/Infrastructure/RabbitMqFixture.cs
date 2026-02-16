using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using RabbitMQ.Client;
using Solidex.Microservices.RabbitMQ;
using Testcontainers.RabbitMq;

namespace Solidex.Microservices.RabbitMQ.IntegrationTests.Infrastructure
{
    /// <summary>
    /// Shared fixture: starts a RabbitMQ container and provides a service provider with IRabbitManager and MediatR.
    /// </summary>
    public class RabbitMqFixture : IAsyncLifetime
    {
        private RabbitMqContainer _container;
        private IServiceProvider _serviceProvider;
        private bool _initialized;

        public RabbitMqFixture()
        {
            _container = new RabbitMqBuilder("rabbitmq:3-management").Build();
            _serviceProvider = null!;
        }

        public async Task InitializeAsync()
        {
            if (_initialized)
                return;
            await _container.StartAsync();
            await Task.Delay(TimeSpan.FromSeconds(2));
            var connectionString = _container.GetConnectionString();
            var uri = new Uri(connectionString);
            var userInfo = uri.UserInfo?.Split(':', 2) ?? Array.Empty<string>();
            var configData = new Dictionary<string, string>
            {
                ["RabbitMQ:Hostname"] = uri.Host,
                ["RabbitMQ:Port"] = (uri.Port > 0 ? uri.Port : 5672).ToString(),
                ["RabbitMQ:UserName"] = userInfo.Length > 0 ? userInfo[0] : "guest",
                ["RabbitMQ:Password"] = userInfo.Length > 1 ? userInfo[1] : "guest",
                ["RabbitMQ:VHost"] = string.IsNullOrEmpty(uri.AbsolutePath) || uri.AbsolutePath == "/" ? "/" : uri.AbsolutePath.TrimStart('/')
            };
            var configuration = new ConfigurationBuilder()
                .AddInMemoryCollection(configData)
                .Build();

            var services = new ServiceCollection();
            services.AddSingleton<IConfiguration>(configuration);
            services.AddRabbit(configuration);
            services.AddMediatR(cfg =>
            {
                cfg.RegisterServicesFromAssembly(typeof(RabbitMqFixture).Assembly);
                cfg.LicenseKey = "test";
            });

            _serviceProvider = services.BuildServiceProvider();
            _initialized = true;
        }

        public Task DisposeAsync() => Task.CompletedTask;

        public IRabbitManager GetManager() => _serviceProvider.GetRequiredService<IRabbitManager>();

        public IServiceProvider GetServiceProvider() => _serviceProvider;

        /// <summary>
        /// Create a connection for raw RabbitMQ client (e.g. to set up consumers or verify messages).
        /// </summary>
        public async Task<IConnection> CreateConnectionAsync()
        {
            var connectionString = _container.GetConnectionString();
            var factory = new ConnectionFactory { Uri = new Uri(connectionString) };
            return await factory.CreateConnectionAsync();
        }

        public string GetConnectionString() => _container.GetConnectionString();
    }
}
