using System;
using Microsoft.Extensions.Options;

namespace Solidex.Microservices.RabbitMQ{

public interface IEndpointConfiguration
{
    void WithBinding(string exchange, string routingKey);
    IBus BuildWrapper(IServiceProvider services, IOptions<RabbitMqConfiguration> options);
}
}