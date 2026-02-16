using System;
using System.Text;
using System.Threading;
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
    public class SendTests
    {
        private readonly RabbitMqFixture _fixture;

        public SendTests(RabbitMqFixture fixture)
        {
            _fixture = fixture;
        }

        [Fact]
        public async Task Send_WithResponder_ReturnsResponse()
        {
            var manager = _fixture.GetManager();
            var responderQueue = "send-test-responder-" + Guid.NewGuid().ToString("N")[..8];
            var cts = new CancellationTokenSource();
            StartRpcResponder(responderQueue, cts.Token);

            var request = new TestChainRequest { Value = 5 };
            TestResponse response = await manager.Send<TestChainRequest, TestResponse>(request);

            Assert.NotNull(response);
            Assert.Equal(10, response.Doubled);
            cts.Cancel();
        }

        [Fact]
        public async Task Send_CancellationRequested_ThrowsTaskCanceled()
        {
            var manager = _fixture.GetManager();
            using var cts = new CancellationTokenSource();
            cts.Cancel();
            var request = new TestChainRequest { Value = 1 };

            await Assert.ThrowsAsync<TaskCanceledException>(
                () => manager.Send<TestChainRequest, TestResponse>(request, cts.Token));
        }

        [Fact]
        public async Task Send_WithoutAttribute_ThrowsInvalidOperationException()
        {
            var manager = _fixture.GetManager();
            var request = new TestRequest { Value = 1 };

            await Assert.ThrowsAsync<InvalidOperationException>(
                () => manager.Send<TestRequest, TestResponse>(request));
        }

        [Fact]
        public async Task Send_ConcurrentCalls_CorrectCorrelationMatching()
        {
            var manager = _fixture.GetManager();
            var responderQueue = "send-concurrent-responder-" + Guid.NewGuid().ToString("N")[..8];
            var cts = new CancellationTokenSource();
            StartRpcResponder(responderQueue, cts.Token);

            var t1 = manager.Send<TestChainRequest, TestResponse>(new TestChainRequest { Value = 1 });
            var t2 = manager.Send<TestChainRequest, TestResponse>(new TestChainRequest { Value = 2 });
            var t3 = manager.Send<TestChainRequest, TestResponse>(new TestChainRequest { Value = 3 });

            var r1 = await t1;
            var r2 = await t2;
            var r3 = await t3;

            Assert.Equal(2, r1.Doubled);
            Assert.Equal(4, r2.Doubled);
            Assert.Equal(6, r3.Doubled);
            cts.Cancel();
        }

        private void StartRpcResponder(string queueName, CancellationToken ct)
        {
            _ = Task.Run(async () =>
            {
                var connection = await _fixture.CreateConnectionAsync();
                var channel = await connection.CreateChannelAsync();
                await channel.ExchangeDeclareAsync("test-chain-exchange", "direct", true, false, null);
                await channel.QueueDeclareAsync(queueName, true, false, false, null);
                await channel.QueueBindAsync(queueName, "test-chain-exchange", "chain.request", null);

                var consumer = new AsyncEventingBasicConsumer(channel);
                consumer.ReceivedAsync += async (_, ea) =>
                {
                    var body = Encoding.UTF8.GetString(ea.Body.Span);
                    var request = JsonConvert.DeserializeObject<TestChainRequest>(body);
                    var replyTo = ea.BasicProperties?.ReplyTo;
                    var correlationId = ea.BasicProperties?.CorrelationId;
                    await channel.BasicAckAsync(ea.DeliveryTag, false);
                    if (request != null && !string.IsNullOrEmpty(replyTo) && !string.IsNullOrEmpty(correlationId))
                    {
                        var response = new TestResponse { Doubled = request.Value * 2 };
                        var replyBytes = Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(response));
                        await channel.QueueDeclareAsync(replyTo, true, false, false, null);
                        var props = new global::RabbitMQ.Client.BasicProperties { CorrelationId = correlationId, Persistent = true };
                        await channel.BasicPublishAsync("", replyTo, false, props, replyBytes);
                    }
                    await Task.CompletedTask;
                };
                await channel.BasicConsumeAsync(queueName, false, string.Empty, false, false, null, consumer, ct);
                await Task.Delay(-1, ct);
            }, ct);
            Thread.Sleep(500);
        }
    }
}
