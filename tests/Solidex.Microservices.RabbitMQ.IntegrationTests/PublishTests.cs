using System;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using Solidex.Microservices.RabbitMQ.IntegrationTests.Fakes;
using Solidex.Microservices.RabbitMQ.IntegrationTests.Infrastructure;
using Xunit;

namespace Solidex.Microservices.RabbitMQ.IntegrationTests
{
    [Collection("RabbitMQ")]
    public class PublishTests
    {
        private readonly RabbitMqFixture _fixture;

        public PublishTests(RabbitMqFixture fixture)
        {
            _fixture = fixture;
        }

        [Fact]
        public async Task Publish_WithValidAttribute_MessageArrivesOnQueue()
        {
            var manager = _fixture.GetManager();
            var queueName = "test-publish-queue-" + Guid.NewGuid().ToString("N")[..8];
            var evt = new TestEvent { Id = "e1", Sequence = 42 };

            using var connection = await _fixture.CreateConnectionAsync();
            await using var channel = await connection.CreateChannelAsync();
            await channel.ExchangeDeclareAsync("test-events", "direct", true, false, null);
            await channel.QueueDeclareAsync(queueName, true, false, false, null);
            await channel.QueueBindAsync(queueName, "test-events", "test.event", null);

            await manager.Publish(evt);

            var tcs = new TaskCompletionSource<TestEvent>();
            var consumer = new AsyncEventingBasicConsumer(channel);
            consumer.ReceivedAsync += (_, ea) =>
            {
                var body = Encoding.UTF8.GetString(ea.Body.Span);
                var received = JsonConvert.DeserializeObject<TestEvent>(body);
                if (received != null)
                    tcs.TrySetResult(received);
                return Task.CompletedTask;
            };
            await channel.BasicConsumeAsync(queueName, true, string.Empty, false, false, null, consumer);

            var result = await Task.WhenAny(tcs.Task, Task.Delay(5000));
            Assert.True(result == tcs.Task, "Expected message within 5 seconds");
            var msg = await tcs.Task;
            Assert.Equal("e1", msg.Id);
            Assert.Equal(42, msg.Sequence);
        }

        [Fact]
        public async Task Publish_WithoutAttribute_ThrowsInvalidOperationException()
        {
            var manager = _fixture.GetManager();
            var message = new TestRequest { Value = 1 };

            await Assert.ThrowsAsync<InvalidOperationException>(() => manager.Publish(message));
        }

        [Fact]
        public async Task Publish_NullMessage_ThrowsArgumentNullException()
        {
            var manager = _fixture.GetManager();

            await Assert.ThrowsAsync<ArgumentNullException>(() => manager.Publish((TestEvent)null!));
        }
    }
}
