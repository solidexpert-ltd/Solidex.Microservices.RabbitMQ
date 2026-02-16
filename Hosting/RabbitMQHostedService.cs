using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace Solidex.Microservices.RabbitMQ.Hosting
{
    /// <summary>
    /// Hosted service that runs all registered RabbitMQ endpoint consumers.
    /// </summary>
    public class RabbitMqHostedService : BackgroundService
    {
        private readonly IList<IBus> _wrappers;

        public RabbitMqHostedService(
            IServiceProvider services,
            IOptions<RabbitMqConfiguration> options,
            IEndpointsConfiguration configuration)
        {
            _wrappers = configuration.Endpoints.Select(x => x.BuildWrapper(services, options)).ToList();
        }

        protected override Task ExecuteAsync(CancellationToken stoppingToken)
        {
            return Task.WhenAll(_wrappers.Select(x => x.ExecuteAsync(stoppingToken)));
        }
    }
}
