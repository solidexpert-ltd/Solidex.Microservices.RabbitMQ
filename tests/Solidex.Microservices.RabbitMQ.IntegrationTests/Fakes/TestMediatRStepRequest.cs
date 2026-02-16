using MediatR;

namespace Solidex.Microservices.RabbitMQ.IntegrationTests.Fakes
{
    /// <summary>
    /// MediatR request without [RabbitMessage] for chain mixed routing tests.
    /// </summary>
    public class TestMediatRStepRequest : IRequest<TestMediatRStepResponse>
    {
        public int Value { get; set; }
    }
}
