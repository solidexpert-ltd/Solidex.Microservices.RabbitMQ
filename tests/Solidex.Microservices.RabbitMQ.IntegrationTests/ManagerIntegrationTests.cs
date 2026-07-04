using System;
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
    /// Integration tests for IRabbitManager helpers and edge cases.
    /// </summary>
    [Collection(RabbitMqCollection.Name)]
    public sealed class ManagerIntegrationTests(RabbitMqFixture fixture)
    {
        [Fact]
        public async Task EnsureReplyQueueExists_creates_queue_so_messages_can_be_delivered()
        {
            var replyQueue = "test-ensure-reply-" + Guid.NewGuid().ToString("N");
            fixture.Manager.EnsureReplyQueueExists(replyQueue);

            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
            var responseTask = fixture.Manager.WaitForResponseAsync<TestResponse>(replyQueue, "corr-1", cts.Token);

            _ = Task.Run(async () =>
            {
                var factory = new ConnectionFactory { Uri = new Uri(fixture.ConnectionString) };
                await using var connection = await factory.CreateConnectionAsync(cts.Token);
                await using var channel = await connection.CreateChannelAsync(null, cts.Token);
                var body = Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(new TestResponse { Doubled = 99 }));
                var props = new BasicProperties { CorrelationId = "corr-1" };
                await channel.BasicPublishAsync("", replyQueue, false, props, body, cts.Token);
            }, cts.Token);

            var response = await responseTask;
            Assert.NotNull(response);
            Assert.Equal(99, response.Doubled);
        }

        [Fact]
        public async Task WaitForResponseAsync_returns_only_single_response_when_responder_sends_multiple_with_same_correlation()
        {
            const string exchange = "test-single-exchange";
            const string routeKey = "test.single";
            var requestQueue = "test-single-request-" + Guid.NewGuid().ToString("N");
            var replyQueue = "test-single-reply-" + Guid.NewGuid().ToString("N");
            var correlationId = Guid.NewGuid().ToString("N");
            var readyQueue = "ready-single-" + Guid.NewGuid().ToString("N");

            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(15));

            StartResponderThatSendsTwice(fixture.ConnectionString, exchange, routeKey, requestQueue, readyQueue, cts.Token);
            await WaitForOneMessageAsync(fixture.ConnectionString, readyQueue, TimeSpan.FromSeconds(8), cts.Token);

            fixture.Manager.EnsureReplyQueueExists(replyQueue);
            var responseTask = fixture.Manager.WaitForResponseAsync<TestResponse>(replyQueue, correlationId, cts.Token);
            fixture.Manager.PublishWithReplyTo(
                new TestRequest { Value = 5 },
                exchange,
                "direct",
                routeKey,
                replyQueue,
                correlationId);

            var response = await responseTask;
            Assert.NotNull(response);
            // Responder sends 5*2=10 first, then 99; we must get exactly one (the first)
            Assert.Equal(10, response.Doubled);
        }

        private static async Task WaitForOneMessageAsync(string connectionString, string queueName, TimeSpan timeout, CancellationToken cancellationToken)
        {
            var tcs = new TaskCompletionSource<bool>();
            using var timeoutCts = new CancellationTokenSource(timeout);
            using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeoutCts.Token);

            var factory = new ConnectionFactory { Uri = new Uri(connectionString) };
            await using var connection = await factory.CreateConnectionAsync(linked.Token);
            await using var channel = await connection.CreateChannelAsync(null, linked.Token);
            await channel.QueueDeclareAsync(queueName, true, false, false, null, false, false, linked.Token);

            var consumer = new AsyncEventingBasicConsumer(channel);
            consumer.ReceivedAsync += async (_, ea) =>
            {
                await channel.BasicAckAsync(ea.DeliveryTag, false, CancellationToken.None);
                tcs.TrySetResult(true);
                await Task.CompletedTask;
            };
            await channel.BasicConsumeAsync(queueName, false, string.Empty, false, false, null, consumer, linked.Token);

            try { await tcs.Task.WaitAsync(linked.Token); }
            catch (OperationCanceledException) when (timeoutCts.IsCancellationRequested)
            {
                throw new InvalidOperationException($"Ready signal not received within {timeout.TotalSeconds}s.");
            }
        }

        private static void StartResponderThatSendsTwice(
            string connectionString,
            string exchange,
            string routeKey,
            string requestQueue,
            string? readyQueue,
            CancellationToken cancellationToken)
        {
            _ = Task.Run(async () =>
            {
                var factory = new ConnectionFactory { Uri = new Uri(connectionString) };
                await using var connection = await factory.CreateConnectionAsync(cancellationToken);
                await using var channel = await connection.CreateChannelAsync(null, cancellationToken);

                await channel.ExchangeDeclareAsync(exchange, "direct", true, false, null, false, false, cancellationToken);
                await channel.QueueDeclareAsync(requestQueue, true, false, false, null, false, false, cancellationToken);
                await channel.QueueBindAsync(requestQueue, exchange, routeKey, null, false, cancellationToken);

                var consumer = new AsyncEventingBasicConsumer(channel);
                consumer.ReceivedAsync += async (_, ea) =>
                {
                    var replyTo = ea.BasicProperties?.ReplyTo;
                    var corrId = ea.BasicProperties?.CorrelationId;
                    if (string.IsNullOrEmpty(replyTo) || string.IsNullOrEmpty(corrId))
                        return;

                    var body = Encoding.UTF8.GetString(ea.Body.Span);
                    var request = JsonConvert.DeserializeObject<TestRequest>(body);
                    await channel.BasicAckAsync(ea.DeliveryTag, false, cancellationToken);

                    var first = new TestResponse { Doubled = (request?.Value ?? 0) * 2 };
                    var firstBytes = Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(first));
                    var props = new BasicProperties { CorrelationId = corrId };
                    await channel.BasicPublishAsync("", replyTo, false, props, firstBytes, cancellationToken);

                    // Intentionally send a second response with same correlation (caller should still receive only one)
                    var second = new TestResponse { Doubled = 99 };
                    var secondBytes = Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(second));
                    await channel.BasicPublishAsync("", replyTo, false, props, secondBytes, cancellationToken);
                };

                await channel.BasicConsumeAsync(requestQueue, false, string.Empty, false, false, null, consumer, cancellationToken);

                if (!string.IsNullOrEmpty(readyQueue))
                {
                    await channel.QueueDeclareAsync(readyQueue, true, false, false, null, false, false, cancellationToken);
                    await channel.BasicPublishAsync("", readyQueue, false, new BasicProperties(), Encoding.UTF8.GetBytes("ready"), cancellationToken);
                }

                try { await Task.Delay(Timeout.Infinite, cancellationToken); }
                catch (OperationCanceledException) { }
            }, cancellationToken);
        }
    }
}
