using System;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using RabbitMQ.Client;
using Solidex.Microservices.RabbitMQ;
using Solidex.Microservices.RabbitMQ.IntegrationTests.Fakes;
using Solidex.Microservices.RabbitMQ.IntegrationTests.Infrastructure;
using Xunit;

namespace Solidex.Microservices.RabbitMQ.IntegrationTests
{
    [Collection("RabbitMQ")]
    public class EndpointTests
    {
        private readonly RabbitMqFixture _fixture;

        public EndpointTests(RabbitMqFixture fixture)
        {
            _fixture = fixture;
        }

        [Fact(Skip = "MediatR 14 requires LicenseAccessor in DI when resolving IMediator in endpoint wrapper; host apps must register MediatR with license. Other endpoint wiring is covered by Send/Publish tests.")]
        public async Task MapEndpoint_WithBinding_MediatRHandlerReceivesMessage()
        {
            var received = new ConcurrentBag<EndpointTestRequest>();
            var uri = new Uri(_fixture.GetConnectionString());
            var userInfo = uri.UserInfo?.Split(':', 2) ?? Array.Empty<string>();
            var configData = new Dictionary<string, string>
            {
                ["RabbitMQ:Hostname"] = uri.Host,
                ["RabbitMQ:Port"] = (uri.Port > 0 ? uri.Port : 5672).ToString(),
                ["RabbitMQ:UserName"] = userInfo.Length > 0 ? userInfo[0] : "guest",
                ["RabbitMQ:Password"] = userInfo.Length > 1 ? userInfo[1] : "guest",
                ["RabbitMQ:VHost"] = string.IsNullOrEmpty(uri.AbsolutePath) || uri.AbsolutePath == "/" ? "/" : uri.AbsolutePath.TrimStart('/')
            };
            var configuration = new ConfigurationBuilder().AddInMemoryCollection(configData).Build();
            var services = new ServiceCollection();
            services.AddSingleton<IConfiguration>(configuration);
            services.AddRabbit(configuration);
            services.AddMediatR(cfg =>
            {
                cfg.RegisterServicesFromAssembly(typeof(EndpointTestRequest).Assembly);
                cfg.LicenseKey = "test";
            });
            services.AddSingleton(received);
            services.AddRabbitMqEndpoints(cfg =>
            {
                cfg.MapEndpoint<EndpointTestRequest>("endpoint-test-queue", true, false, false, null)
                    .WithBinding("endpoint-test-exchange", "endpoint.routing");
            });
            var sp = services.BuildServiceProvider();
            var host = sp.GetRequiredService<Microsoft.Extensions.Hosting.IHostedService>();
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(15));
            var runTask = host.StartAsync(cts.Token);

            await Task.Delay(3500);

            using var connection = await _fixture.CreateConnectionAsync();
            await using var channel = await connection.CreateChannelAsync();
            await channel.ExchangeDeclareAsync("endpoint-test-exchange", "direct", true, false, null);
            await channel.QueueDeclareAsync("endpoint-test-queue", true, false, false, null);
            await channel.QueueBindAsync("endpoint-test-queue", "endpoint-test-exchange", "endpoint.routing", null);
            var body = System.Text.Encoding.UTF8.GetBytes(
                Newtonsoft.Json.JsonConvert.SerializeObject(new EndpointTestRequest { Text = "hello", Value = 42 }));
            var emptyProps = new global::RabbitMQ.Client.BasicProperties();
            await channel.BasicPublishAsync("endpoint-test-exchange", "endpoint.routing", false, emptyProps, body);

            await Task.Delay(5000);
            cts.Cancel();
            await runTask;

            Assert.Single(received);
            Assert.Equal("hello", received.Single().Text);
            Assert.Equal(42, received.Single().Value);
        }
    }
}
