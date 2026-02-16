using System;

namespace Solidex.Microservices.RabbitMQ.Attributes
{
    /// <summary>
    /// Marks a type for RabbitMQ routing. Used by Publish (fire-and-forget), Send (RPC), and ChainMQ.
    /// </summary>
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
    public class RabbitMessageAttribute : Attribute
    {
        /// <summary>Target exchange name.</summary>
        public string Exchange { get; set; } = string.Empty;

        /// <summary>Exchange type (e.g. "direct", "topic").</summary>
        public string ExchangeType { get; set; } = "direct";

        /// <summary>Routing key for the message.</summary>
        public string RouteKey { get; set; } = string.Empty;

        /// <summary>Reply queue name for RPC. If null or empty, a unique name is generated per request.</summary>
        public string ReplyQueue { get; set; } = string.Empty;
    }
}
