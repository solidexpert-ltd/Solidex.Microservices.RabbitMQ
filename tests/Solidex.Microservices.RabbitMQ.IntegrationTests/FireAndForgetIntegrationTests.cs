using System;
using System.Collections.Concurrent;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using Solidex.Microservices.RabbitMQ.IntegrationTests.Fakes;
using Xunit;

namespace Solidex.Microservices.RabbitMQ.IntegrationTests
{
    /// <summary>
    /// Integration tests for fire-and-forget: Publish and Send (no response expected).
    /// </summary>
    [Collection(RabbitMqCollection.Name)]
    public sealed class FireAndForgetIntegrationTests(RabbitMqFixture fixture)
    {
        [Fact]
        public async Task Publish_message_reaches_consumer_on_bound_queue()
        {
            const string exchange = "test-publish-exchange";
            const string routeKey = "test.publish";
            var queueName = "test-publish-queue-" + Guid.NewGuid().ToString("N");
            var received = new BlockingCollection<TestEvent>();
            var ready = new TaskCompletionSource();

            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(15));
            StartConsumer(fixture.ConnectionString, exchange, "direct", routeKey, queueName, received, ready, cts.Token);
            await ready.Task.WaitAsync(TimeSpan.FromSeconds(5), cts.Token);

            var message = new TestEvent { Id = "e1", Sequence = 1 };
            fixture.Manager.Publish(message, exchange, "direct", routeKey);

            var got = await Task.Run(() => received.Take(cts.Token), cts.Token);
            Assert.NotNull(got);
            Assert.Equal("e1", got.Id);
            Assert.Equal(1, got.Sequence);
        }

        [Fact]
        public async Task Send_with_RabbitQuery_attribute_publishes_to_configured_exchange_and_route()
        {
            const string exchange = "test-query-exchange";
            const string routeKey = "test.query";
            var queueName = "test-query-queue-" + Guid.NewGuid().ToString("N");
            var received = new BlockingCollection<TestQueryMessage>();
            var ready = new TaskCompletionSource();

            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(15));
            StartConsumerForType<TestQueryMessage>(fixture.ConnectionString, exchange, "direct", routeKey, queueName, received, ready, cts.Token);
            await ready.Task.WaitAsync(TimeSpan.FromSeconds(5), cts.Token);

            var message = new TestQueryMessage { Payload = "hello-query" };
            fixture.Manager.Send(message);

            var got = await Task.Run(() => received.Take(cts.Token), cts.Token);
            Assert.NotNull(got);
            Assert.Equal("hello-query", got.Payload);
        }

        private static void StartConsumer(
            string connectionString,
            string exchange,
            string exchangeType,
            string routeKey,
            string queueName,
            BlockingCollection<TestEvent> received,
            TaskCompletionSource ready,
            CancellationToken cancellationToken)
        {
            _ = Task.Run(async () =>
            {
                var factory = new ConnectionFactory { Uri = new Uri(connectionString) };
                await using var connection = await factory.CreateConnectionAsync(cancellationToken);
                await using var channel = await connection.CreateChannelAsync(null, cancellationToken);

                await channel.ExchangeDeclareAsync(exchange, exchangeType, true, false, null, false, false, cancellationToken);
                await channel.QueueDeclareAsync(queueName, true, false, false, null, false, false, cancellationToken);
                await channel.QueueBindAsync(queueName, exchange, routeKey, null, false, cancellationToken);

                var consumer = new AsyncEventingBasicConsumer(channel);
                consumer.ReceivedAsync += async (_, ea) =>
                {
                    var body = Encoding.UTF8.GetString(ea.Body.Span);
                    var msg = JsonConvert.DeserializeObject<TestEvent>(body);
                    await channel.BasicAckAsync(ea.DeliveryTag, false, CancellationToken.None);
                    if (msg != null)
                        received.Add(msg, cancellationToken);
                    await Task.CompletedTask;
                };

                await channel.BasicConsumeAsync(queueName, false, string.Empty, false, false, null, consumer, cancellationToken);
                ready.TrySetResult();

                try { await Task.Delay(Timeout.Infinite, cancellationToken); }
                catch (OperationCanceledException) { }
            }, cancellationToken);
        }

        private static void StartConsumerForType<T>(
            string connectionString,
            string exchange,
            string exchangeType,
            string routeKey,
            string queueName,
            BlockingCollection<T> received,
            TaskCompletionSource ready,
            CancellationToken cancellationToken) where T : class
        {
            _ = Task.Run(async () =>
            {
                var factory = new ConnectionFactory { Uri = new Uri(connectionString) };
                await using var connection = await factory.CreateConnectionAsync(cancellationToken);
                await using var channel = await connection.CreateChannelAsync(null, cancellationToken);

                await channel.ExchangeDeclareAsync(exchange, exchangeType, true, false, null, false, false, cancellationToken);
                await channel.QueueDeclareAsync(queueName, true, false, false, null, false, false, cancellationToken);
                await channel.QueueBindAsync(queueName, exchange, routeKey, null, false, cancellationToken);

                var consumer = new AsyncEventingBasicConsumer(channel);
                consumer.ReceivedAsync += async (_, ea) =>
                {
                    var body = Encoding.UTF8.GetString(ea.Body.Span);
                    var msg = JsonConvert.DeserializeObject<T>(body);
                    await channel.BasicAckAsync(ea.DeliveryTag, false, CancellationToken.None);
                    if (msg != null)
                        received.Add(msg, cancellationToken);
                    await Task.CompletedTask;
                };

                await channel.BasicConsumeAsync(queueName, false, string.Empty, false, false, null, consumer, cancellationToken);
                ready.TrySetResult();

                try { await Task.Delay(Timeout.Infinite, cancellationToken); }
                catch (OperationCanceledException) { }
            }, cancellationToken);
        }
    }
}
