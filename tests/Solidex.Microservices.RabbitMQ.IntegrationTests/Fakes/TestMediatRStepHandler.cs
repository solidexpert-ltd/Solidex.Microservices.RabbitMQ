using System.Threading;
using System.Threading.Tasks;
using MediatR;

namespace Solidex.Microservices.RabbitMQ.IntegrationTests.Fakes
{
    public class TestMediatRStepHandler : IRequestHandler<TestMediatRStepRequest, TestMediatRStepResponse>
    {
        public Task<TestMediatRStepResponse> Handle(TestMediatRStepRequest request, CancellationToken cancellationToken)
        {
            return Task.FromResult(new TestMediatRStepResponse { Squared = request.Value * request.Value });
        }
    }
}
