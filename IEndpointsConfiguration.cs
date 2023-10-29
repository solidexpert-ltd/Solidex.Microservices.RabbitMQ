using System.Collections.Generic;

namespace Solidex.Microservices.RabbitMQ
{

    public interface IEndpointsConfiguration
    {
        List<IEndpointConfiguration> Endpoints { get; set; }
    }
}