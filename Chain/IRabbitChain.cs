using System;
using System.Threading;
using System.Threading.Tasks;

namespace Solidex.Microservices.RabbitMQ.Chain
{
    /// <summary>
    /// Fluent builder for a multi-step pipeline. Each step can route to RabbitMQ (if request has [RabbitMessage]) or MediatR.
    /// </summary>
    /// <typeparam name="TCurrentRequest">Type of the current/last request in the chain.</typeparam>
    public interface IRabbitChain<TCurrentRequest>
        where TCurrentRequest : class
    {
        /// <summary>
        /// Add a step: map the previous response to the next request. Next step runs via MQ or MediatR based on [RabbitMessage] on TNextRequest.
        /// </summary>
        IRabbitChain<TNextRequest> ThenBy<TResponse, TNextRequest>(Func<TResponse, TNextRequest> map)
            where TResponse : class
            where TNextRequest : class;

        /// <summary>
        /// Execute the chain and return the final response.
        /// </summary>
        Task<TResponse> ExecuteAsync<TResponse>(CancellationToken ct = default)
            where TResponse : class;
    }
}
