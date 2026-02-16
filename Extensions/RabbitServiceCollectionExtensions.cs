using System;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.ObjectPool;
using RabbitMQ.Client;
using Solidex.Microservices.RabbitMQ.Hosting;
using Solidex.Microservices.RabbitMQ.Infrastructure;

namespace Solidex.Microservices.RabbitMQ
{
    /// <summary>
    /// Extension methods for registering RabbitMQ services.
    /// </summary>
    public static class RabbitServiceCollectionExtensions
    {
        /// <summary>
        /// Adds core RabbitMQ services (configuration, channel pool, IRabbitManager).
        /// </summary>
        public static IServiceCollection AddRabbit(this IServiceCollection services, IConfiguration configuration)
        {
            services.Configure<RabbitMqConfiguration>(configuration.GetSection("RabbitMQ"));

            services.AddSingleton<ObjectPoolProvider, DefaultObjectPoolProvider>();
            services.AddSingleton<IPooledObjectPolicy<IChannel>, RabbitModelPooledObjectPolicy>();
            services.AddSingleton<IRabbitManager, RabbitManager>();

            return services;
        }

        /// <summary>
        /// Adds RabbitMQ consumer endpoints (hosted service that runs all registered endpoints).
        /// </summary>
        public static IServiceCollection AddRabbitMqEndpoints(
            this IServiceCollection services,
            Action<EndpointsConfiguration> configuration)
        {
            var cfg = new EndpointsConfiguration();
            configuration(cfg);
            services.AddSingleton<IEndpointsConfiguration>(cfg);
            services.AddHostedService<RabbitMqHostedService>();

            return services;
        }
    }
}
