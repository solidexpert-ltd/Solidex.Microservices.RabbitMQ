using System;
using System.Threading;
using System.Threading.Tasks;

namespace Solidex.Microservices.RabbitMQ
{
    /// <summary>
    /// Abstraction for publishing and subscribing to RabbitMQ messages.
    /// 1. Fire-and-forget: <see cref="Publish{T}"/> — uses [RabbitMessage] on T.
    /// 2. RPC: <see cref="Send{TRequest,TResponse}"/> — uses [RabbitMessage] on TRequest, waits for TResponse.
    /// 3. Subscribe: <see cref="Subscribe{T}"/> — receive all events from a queue until cancelled.
    /// </summary>
    public interface IRabbitManager
    {
        /// <summary>
        /// Fire-and-forget: publish using [RabbitMessage] on T. Does not wait for any response.
        /// </summary>
        Task Publish<T>(T message, CancellationToken ct = default) where T : class;

        /// <summary>
        /// RPC: send request using [RabbitMessage] on TRequest, wait for TResponse.
        /// </summary>
        Task<TResponse> Send<TRequest, TResponse>(TRequest message, CancellationToken ct = default)
            where TRequest : class
            where TResponse : class;

        /// <summary>
        /// Subscribe: receive all events from the queue. Runs until cancellation.
        /// Calls <paramref name="onMessage"/> for each message; acks after the handler completes.
        /// </summary>
        Task Subscribe<T>(string queueName, Func<T, CancellationToken, Task> onMessage,
            CancellationToken ct = default) where T : class;

        /// <summary>
        /// Publish to explicit exchange/route (low-level; used by chain/endpoints).
        /// </summary>
        Task PublishRaw<T>(T message, string exchange, string exchangeType, string routeKey,
            CancellationToken ct = default) where T : class;

        /// <summary>
        /// Publish with ReplyTo and CorrelationId for request-response pattern.
        /// </summary>
        Task PublishWithReplyTo<T>(T message, string exchange, string exchangeType, string routeKey,
            string replyToQueue, string correlationId, CancellationToken ct = default) where T : class;

        /// <summary>
        /// Wait for a single response on the given reply queue with the given correlation id.
        /// </summary>
        Task<T> WaitForResponseAsync<T>(string replyQueueName, string correlationId,
            CancellationToken ct = default) where T : class;

        /// <summary>
        /// Ensure a reply queue exists (declare it). Call before PublishWithReplyTo.
        /// </summary>
        Task EnsureReplyQueueExists(string replyQueueName, CancellationToken ct = default);
    }
}
