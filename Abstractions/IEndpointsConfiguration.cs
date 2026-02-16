using System.Collections.Generic;

namespace Solidex.Microservices.RabbitMQ
{
    /// <summary>
    /// Configuration for RabbitMQ endpoint consumers.
    /// </summary>
    public interface IEndpointsConfiguration
    {
        List<IEndpointConfiguration> Endpoints { get; set; }
    }
}
