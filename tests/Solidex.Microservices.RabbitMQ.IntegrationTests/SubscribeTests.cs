using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Solidex.Microservices.RabbitMQ.IntegrationTests.Fakes;
using Solidex.Microservices.RabbitMQ.IntegrationTests.Infrastructure;
using Xunit;

namespace Solidex.Microservices.RabbitMQ.IntegrationTests
{
    [Collection("RabbitMQ")]
    public class SubscribeTests
    {
        private readonly RabbitMqFixture _fixture;

        public SubscribeTests(RabbitMqFixture fixture)
        {
            _fixture = fixture;
        }

        [Fact]
        public async Task Subscribe_ReceivesMultipleMessages()
        {
            var manager = _fixture.GetManager();
            var queueName = "subscribe-test-" + Guid.NewGuid().ToString("N")[..8];
            var received = new ConcurrentBag<TestEvent>();
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(15));

            using (var connection = await _fixture.CreateConnectionAsync())
            {
                await using var channel = await connection.CreateChannelAsync();
                await channel.ExchangeDeclareAsync("test-events", "direct", true, false, null);
                await channel.QueueDeclareAsync(queueName, true, false, false, null);
                await channel.QueueBindAsync(queueName, "test-events", "test.event", null);
            }

            var subscribeTask = manager.Subscribe<TestEvent>(queueName, async (evt, _) =>
            {
                received.Add(evt);
                await Task.CompletedTask;
            }, cts.Token);

            await Task.Delay(500);

            await manager.Publish(new TestEvent { Id = "a", Sequence = 1 }, cts.Token);
            await manager.Publish(new TestEvent { Id = "b", Sequence = 2 }, cts.Token);
            await manager.Publish(new TestEvent { Id = "c", Sequence = 3 }, cts.Token);

            await Task.Delay(2000, cts.Token);
            await cts.CancelAsync();

            await Task.WhenAny(subscribeTask, Task.Delay(3000, cts.Token));
            Assert.Equal(3, received.Count);
        }

        [Fact]
        public async Task Subscribe_CancellationStopsConsuming()
        {
            var manager = _fixture.GetManager();
            var queueName = "subscribe-cancel-" + Guid.NewGuid().ToString("N")[..8];
            using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(800));

            var subscribeTask = manager.Subscribe<TestEvent>(queueName, (_, _) => Task.CompletedTask, cts.Token);
            await subscribeTask;
        }

        [Fact]
        public async Task Subscribe_HandlerThrows_MessageStillAcked()
        {
            var manager = _fixture.GetManager();
            var queueName = "subscribe-throw-" + Guid.NewGuid().ToString("N")[..8];
            var received = new ConcurrentBag<TestEvent>();
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
            var first = true;

            using (var connection = await _fixture.CreateConnectionAsync())
            {
                await using var channel = await connection.CreateChannelAsync();
                await channel.ExchangeDeclareAsync("test-events", "direct", true, false, null);
                await channel.QueueDeclareAsync(queueName, true, false, false, null);
                await channel.QueueBindAsync(queueName, "test-events", "test.event", null);
            }

            var subscribeTask = manager.Subscribe<TestEvent>(queueName, async (evt, _) =>
            {
                if (first && evt.Sequence == 1)
                {
                    first = false;
                    throw new InvalidOperationException("Simulated handler failure");
                }
                received.Add(evt);
                await Task.CompletedTask;
            }, cts.Token);

            await Task.Delay(500);
            await manager.Publish(new TestEvent { Id = "x", Sequence = 1 });
            await manager.Publish(new TestEvent { Id = "y", Sequence = 2 });
            await Task.Delay(2000);
            cts.Cancel();
            await Task.WhenAny(subscribeTask, Task.Delay(3000));
            Assert.Single(received);
            Assert.Equal(2, received.Single().Sequence);
        }
    }
}
