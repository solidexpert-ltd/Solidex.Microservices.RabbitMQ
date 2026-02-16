using System;

namespace Solidex.Microservices.RabbitMQ.Chain
{
    /// <summary>
    /// Internal representation of a single step in a chain (response type, map to next request, next request type).
    /// </summary>
    internal sealed class ChainStep
    {
        public Type ResponseType { get; set; } = null!;
        public Type NextRequestType { get; set; } = null!;
        public Func<object, object> Map { get; set; } = null!;
    }
}
