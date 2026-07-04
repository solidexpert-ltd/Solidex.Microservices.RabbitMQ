using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Solidex.Microservices.RabbitMQ.IntegrationTests.Fakes;
using Xunit;

namespace Solidex.Microservices.RabbitMQ.IntegrationTests
{
    /// <summary>
    /// Integration tests for AddRabbitMqEndpoints / MapEndpoint / WithBinding:
    /// queue declared, bound to exchange with routing key, messages dispatched to MediatR.
    /// </summary>
    [Collection(RabbitMqCollection.Name)]
    public sealed class EndpointMappingIntegrationTests(RabbitMqFixture fixture)
    {
        [Fact]
        public async Task MapEndpoint_WithBinding_receives_published_message_and_dispatches_to_MediatR()
        {
            const string exchange = "service.notification";
            const string routingKey = "telegram";
            var queue = "service.notification.telegram:send-message-" + Guid.NewGuid().ToString("N");
            var received = new ConcurrentBag<EndpointTestRequest>();

            var services = new ServiceCollection();
            var uri = new Uri(fixture.ConnectionString);
            var userParts = uri.UserInfo?.Split(new[] { ':' }, 2, StringSplitOptions.None) ?? Array.Empty<string>();

            services.Configure<RabbitMqConfiguration>(c =>
            {
                c.Hostname = uri.Host;
                c.Port = uri.Port > 0 ? uri.Port : 5672;
                c.UserName = userParts.Length > 0 ? userParts[0] : "guest";
                c.Password = userParts.Length > 1 ? userParts[1] : "guest";
                c.VHost = string.IsNullOrEmpty(uri.AbsolutePath) || uri.AbsolutePath == "/" ? "/" : uri.AbsolutePath.TrimStart('/');
            });

            services.AddSingleton(received);
            services.AddMediatR(cfg => cfg.RegisterServicesFromAssembly(typeof(EndpointTestRequestHandler).Assembly));

            var endpointsConfig = new EndpointsConfiguration();
            endpointsConfig
                .MapEndpoint<EndpointTestRequest>(queue: queue, durable: true, exclusive: false, autoDelete: false, arguments: null)
                .WithBinding(exchange, routingKey);
            services.AddSingleton<IEndpointsConfiguration>(endpointsConfig);

            await using var provider = services.BuildServiceProvider();
            var options = provider.GetRequiredService<IOptions<RabbitMqConfiguration>>();
            var bus = endpointsConfig.Endpoints[0].BuildWrapper(provider, options);

            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(25));
            _ = Task.Run(() => bus.ExecuteAsync(cts.Token), cts.Token);
            await Task.Delay(4000, cts.Token);

            var message = new EndpointTestRequest { Text = "hello-telegram", Value = 42 };
            fixture.Manager.Publish(message, exchange, "direct", routingKey);

            for (int i = 0; i < 30 && received.IsEmpty; i++)
                await Task.Delay(500, cts.Token);

            Assert.True(!received.IsEmpty, "No message received by endpoint. Ensure RabbitMQ is running and the consumer started.");
            var got = Assert.Single(received);
            Assert.Equal("hello-telegram", got.Text);
            Assert.Equal(42, got.Value);
        }

        [Fact]
        public async Task MapEndpoint_with_different_binding_receives_on_correct_routing_key()
        {
            const string exchange = "service.user";
            const string routingKey = "account.deleted";
            var queue = "service.notification.telegram:delete-chat-" + Guid.NewGuid().ToString("N");
            var received = new ConcurrentBag<EndpointTestRequest>();

            var services = new ServiceCollection();
            var uri = new Uri(fixture.ConnectionString);
            var userParts = uri.UserInfo?.Split(new[] { ':' }, 2, StringSplitOptions.None) ?? Array.Empty<string>();

            services.Configure<RabbitMqConfiguration>(c =>
            {
                c.Hostname = uri.Host;
                c.Port = uri.Port > 0 ? uri.Port : 5672;
                c.UserName = userParts.Length > 0 ? userParts[0] : "guest";
                c.Password = userParts.Length > 1 ? userParts[1] : "guest";
                c.VHost = string.IsNullOrEmpty(uri.AbsolutePath) || uri.AbsolutePath == "/" ? "/" : uri.AbsolutePath.TrimStart('/');
            });

            services.AddSingleton(received);
            services.AddMediatR(cfg => cfg.RegisterServicesFromAssembly(typeof(EndpointTestRequestHandler).Assembly));

            var endpointsConfig = new EndpointsConfiguration();
            endpointsConfig
                .MapEndpoint<EndpointTestRequest>(queue: queue, durable: true, exclusive: false, autoDelete: false, arguments: null)
                .WithBinding("service.user", "account.deleted");
            services.AddSingleton<IEndpointsConfiguration>(endpointsConfig);

            await using var provider = services.BuildServiceProvider();
            var options = provider.GetRequiredService<IOptions<RabbitMqConfiguration>>();
            var bus = endpointsConfig.Endpoints[0].BuildWrapper(provider, options);

            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(25));
            _ = Task.Run(() => bus.ExecuteAsync(cts.Token), cts.Token);
            await Task.Delay(4000, cts.Token);

            var message = new EndpointTestRequest { Text = "account-deleted", Value = 1 };
            fixture.Manager.Publish(message, exchange, "direct", routingKey);

            for (int i = 0; i < 30 && received.IsEmpty; i++)
                await Task.Delay(500, cts.Token);

            Assert.True(!received.IsEmpty, "No message received by endpoint. Ensure RabbitMQ is running and the consumer started.");
            var got = Assert.Single(received);
            Assert.Equal("account-deleted", got.Text);
            Assert.Equal(1, got.Value);
        }
    }
}
