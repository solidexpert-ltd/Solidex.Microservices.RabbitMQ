using System;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.ObjectPool;
using Microsoft.Extensions.Options;
using Newtonsoft.Json;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using Solidex.Microservices.RabbitMQ.Attributes;

namespace Solidex.Microservices.RabbitMQ.Infrastructure
{
    /// <summary>
    /// Default implementation of <see cref="IRabbitManager"/> using a channel pool.
    /// </summary>
    public class RabbitManager : IRabbitManager
    {
        private readonly DefaultObjectPool<IChannel> _objectPool;
        private readonly RabbitMqConfiguration _options;

        internal IServiceProvider ServiceProvider { get; }

        public RabbitManager(
            IPooledObjectPolicy<IChannel> objectPolicy,
            IOptions<RabbitMqConfiguration> options,
            IServiceProvider serviceProvider)
        {
            _objectPool = new DefaultObjectPool<IChannel>(objectPolicy, Environment.ProcessorCount * 2);
            _options = options.Value;
            ServiceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
        }

        /// <inheritdoc />
        public async Task Publish<T>(T message, CancellationToken ct = default) where T : class
        {
            if (message == null)
                throw new ArgumentNullException(nameof(message));

            var attr = GetRabbitMessageAttribute<T>();
            await PublishRaw(message, attr.Exchange, attr.ExchangeType, attr.RouteKey, ct);
        }

        /// <inheritdoc />
        public void Publish<T>(T message, string exchangeName, string exchangeType, string routeKey) where T : class
        {
            if (message == null)
                throw new ArgumentNullException(nameof(message));

            PublishRaw(message, exchangeName, exchangeType, routeKey, CancellationToken.None).GetAwaiter().GetResult();
        }

        /// <inheritdoc />
        public async Task<TResponse> Send<TRequest, TResponse>(TRequest message, CancellationToken ct = default)
            where TRequest : class
            where TResponse : class
        {
            if (message == null)
                throw new ArgumentNullException(nameof(message));

            var attr = GetRabbitMessageAttribute<TRequest>();
            var replyQueue = string.IsNullOrEmpty(attr.ReplyQueue) ? "reply-" + Guid.NewGuid().ToString("N") : attr.ReplyQueue;
            var correlationId = Guid.NewGuid().ToString("N");

            await EnsureReplyQueueExists(replyQueue, ct);
            var responseTask = WaitForResponseAsync<TResponse>(replyQueue, correlationId, ct);
            await PublishWithReplyTo(message, attr.Exchange, attr.ExchangeType, attr.RouteKey, replyQueue, correlationId, ct);
            return await responseTask;
        }

        /// <inheritdoc />
        public async Task Subscribe<T>(string queueName, Func<T, CancellationToken, Task> onMessage,
            CancellationToken ct = default) where T : class
        {
            var factory = new ConnectionFactory
            {
                HostName = _options.Hostname,
                UserName = _options.UserName,
                Password = _options.Password,
                Port = _options.Port > 0 ? _options.Port : 5672,
                VirtualHost = string.IsNullOrEmpty(_options.VHost) ? "/" : _options.VHost
            };

            await using var connection = await factory.CreateConnectionAsync(ct);
            await using var channel = await connection.CreateChannelAsync(null, ct);

            await channel.QueueDeclareAsync(queueName, true, false, false, null, false, false, ct);

            var tcs = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            ct.Register(() => tcs.TrySetResult());

            var consumer = new AsyncEventingBasicConsumer(channel);
            consumer.ReceivedAsync += async (_, ea) =>
            {
                if (ct.IsCancellationRequested)
                    return;

                var content = Encoding.UTF8.GetString(ea.Body.Span);
                var msg = JsonConvert.DeserializeObject<T>(content);
                try
                {
                    if (msg != null)
                        await onMessage(msg, ct);
                }
                finally
                {
                    await channel.BasicAckAsync(ea.DeliveryTag, false, CancellationToken.None);
                }
                await Task.CompletedTask;
            };

            await channel.BasicConsumeAsync(queueName, false, string.Empty, false, false, null, consumer, ct);
            await tcs.Task;
        }

        /// <inheritdoc />
        public async Task PublishRaw<T>(T message, string exchange, string exchangeType, string routeKey,
            CancellationToken ct = default) where T : class
        {
            if (message == null)
                return;

            var channel = _objectPool.Get();
            try
            {
                await channel.ExchangeDeclareAsync(exchange, exchangeType, true, false, null, false, false, ct);
                var sendBytes = Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(message));
                var properties = new BasicProperties
                {
                    Persistent = true,
                    Type = message.GetType().Name
                };
                await channel.BasicPublishAsync(exchange, routeKey, false, properties, sendBytes, ct);
            }
            finally
            {
                _objectPool.Return(channel);
            }
        }

        /// <inheritdoc />
        public async Task PublishWithReplyTo<T>(T message, string exchange, string exchangeType, string routeKey,
            string replyToQueue, string correlationId, CancellationToken ct = default) where T : class
        {
            if (message == null)
                return;

            var channel = _objectPool.Get();
            try
            {
                await channel.ExchangeDeclareAsync(exchange, exchangeType, true, false, null, false, false, ct);
                var sendBytes = Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(message));
                var properties = new BasicProperties
                {
                    Persistent = true,
                    Type = message.GetType().Name,
                    ReplyTo = replyToQueue,
                    CorrelationId = correlationId
                };
                await channel.BasicPublishAsync(exchange, routeKey, false, properties, sendBytes, ct);
            }
            finally
            {
                _objectPool.Return(channel);
            }
        }

        /// <inheritdoc />
        public async Task<T> WaitForResponseAsync<T>(string replyQueueName, string correlationId,
            CancellationToken ct = default) where T : class
        {
            var tcs = new TaskCompletionSource<T>();
            var channel = _objectPool.Get();

            try
            {
                await channel.QueueDeclareAsync(replyQueueName, true, false, false, null, false, false, ct);

                var consumer = new AsyncEventingBasicConsumer(channel);
                consumer.ReceivedAsync += async (ch, ea) =>
                {
                    var msgCorrelationId = ea.BasicProperties?.CorrelationId;
                    if (string.IsNullOrEmpty(msgCorrelationId) || msgCorrelationId != correlationId)
                        return;

                    await channel.BasicCancelAsync(ea.ConsumerTag, false);

                    var content = Encoding.UTF8.GetString(ea.Body.Span);
                    var message = JsonConvert.DeserializeObject<T>(content);
                    await channel.BasicAckAsync(ea.DeliveryTag, false, CancellationToken.None);
                    if (message != null)
                        tcs.TrySetResult(message);
                    await Task.CompletedTask;
                };

                await channel.BasicConsumeAsync(replyQueueName, false, string.Empty, false, false, null, consumer, ct);
                ct.Register(() => tcs.TrySetCanceled());

                return await tcs.Task;
            }
            finally
            {
                _objectPool.Return(channel);
            }
        }

        /// <inheritdoc />
        public async Task EnsureReplyQueueExists(string replyQueueName, CancellationToken ct = default)
        {
            var channel = _objectPool.Get();
            try
            {
                await channel.QueueDeclareAsync(replyQueueName, true, false, false, null, false, false, ct);
            }
            finally
            {
                _objectPool.Return(channel);
            }
        }

        /// <summary>
        /// Internal: send with runtime types for chain execution.
        /// </summary>
        internal async Task<object> SendObjectAsync(object request, Type requestType, Type responseType, CancellationToken ct)
        {
            var sendMethod = typeof(IRabbitManager).GetMethod(nameof(Send))!.MakeGenericMethod(requestType, responseType);
            var task = sendMethod.Invoke(this, new object[] { request!, ct })!;
            await ((Task)task).ConfigureAwait(false);
            return task.GetType().GetProperty("Result")!.GetValue(task)!;
        }

        private static RabbitMessageAttribute GetRabbitMessageAttribute<T>()
        {
            var attr = typeof(T).GetCustomAttributes(typeof(RabbitMessageAttribute), false)
                .OfType<RabbitMessageAttribute>()
                .FirstOrDefault();
            if (attr == null)
                throw new InvalidOperationException(
                    $"Type {typeof(T).Name} must be marked with [RabbitMessage(Exchange, RouteKey, ...)].");
            return attr;
        }
    }
}
