using MediatR;

namespace Solidex.Microservices.RabbitMQ.IntegrationTests.Fakes
{
    /// <summary>
    /// Request type used by endpoint mapping integration test (MapEndpoint + WithBinding).
    /// </summary>
    public class EndpointTestRequest : IRequest<Unit>
    {
        public string Text { get; set; } = string.Empty;
        public int Value { get; set; }
    }
}
