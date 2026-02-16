using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Solidex.Microservices.RabbitMQ.Attributes;

namespace Solidex.Microservices.RabbitMQ.Chain
{
    /// <summary>
    /// Extension methods for ChainMQ(request).ThenBy(...).ExecuteAsync pattern.
    /// </summary>
    public static class RabbitChainExtensions
    {
        /// <summary>
        /// Start a chain: the initial request must have [RabbitMessage]. Use .ThenBy(...) to add steps, then .ExecuteAsync(ct).
        /// </summary>
        public static IRabbitChain<TRequest> ChainMQ<TRequest>(this IRabbitManager manager, TRequest request)
            where TRequest : class
        {
            if (manager == null)
                throw new ArgumentNullException(nameof(manager));
            if (request == null)
                throw new ArgumentNullException(nameof(request));

            var attr = typeof(TRequest).GetCustomAttributes(typeof(RabbitMessageAttribute), false)
                .OfType<RabbitMessageAttribute>()
                .FirstOrDefault();

            if (attr == null)
                throw new InvalidOperationException(
                    $"Type {typeof(TRequest).Name} must be marked with [RabbitMessage(Exchange, RouteKey, ...)] for ChainMQ.");

            return new RabbitChainBuilder<TRequest>(manager, request, typeof(TRequest), new List<ChainStep>());
        }
    }
}
