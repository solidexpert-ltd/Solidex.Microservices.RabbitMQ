using System;
using Microsoft.Extensions.Options;

namespace Solidex.Microservices.RabbitMQ
{
    /// <summary>
    /// Configuration for a single RabbitMQ endpoint (queue + optional exchange binding).
    /// </summary>
    public interface IEndpointConfiguration
    {
        void WithBinding(string exchange, string routingKey);

        IBus BuildWrapper(IServiceProvider services, IOptions<RabbitMqConfiguration> options);
    }
}
