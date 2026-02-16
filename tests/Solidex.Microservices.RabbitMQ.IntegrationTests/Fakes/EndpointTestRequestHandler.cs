using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;
using MediatR;

namespace Solidex.Microservices.RabbitMQ.IntegrationTests.Fakes
{
    /// <summary>
    /// MediatR handler that records received requests for endpoint mapping tests.
    /// </summary>
    public class EndpointTestRequestHandler : IRequestHandler<EndpointTestRequest, Unit>
    {
        private readonly ConcurrentBag<EndpointTestRequest> _received;

        public EndpointTestRequestHandler(ConcurrentBag<EndpointTestRequest> received)
        {
            _received = received;
        }

        public Task<Unit> Handle(EndpointTestRequest request, CancellationToken cancellationToken)
        {
            _received.Add(request);
            return Task.FromResult(Unit.Value);
        }
    }
}
