using System;

namespace Solidex.Microservices.RabbitMQ.Attributes;

public class RabbitQueryAttribute: Attribute
{
    public string ExchangeName { get; set; }
    public string ExchangeType { get; set; }
    public string RouteKey { get; set; }
}