using System;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.ObjectPool;
using RabbitMQ.Client;

namespace Solidex.Microservices.RabbitMQ
{
    public static class RabbitServiceCollectionExtensions
    {
        public static IServiceCollection AddRabbit(this IServiceCollection services, IConfiguration configuration)
        {
            services.Configure<RabbitMqConfiguration>(configuration.GetSection("RabbitMQ"));

            services.AddSingleton<ObjectPoolProvider, DefaultObjectPoolProvider>();
            services.AddSingleton<IPooledObjectPolicy<IChannel>, RabbitModelPooledObjectPolicy>();

            services.AddSingleton<IRabbitManager, RabbitManager>();

            return services;
        }
        
        public static void AddRabbitMqEndpoints(this IServiceCollection services,
            Action<EndpointsConfiguration> configuration)
        {
            var cfg = new EndpointsConfiguration();
            configuration(cfg);
            services.AddSingleton<IEndpointsConfiguration>(cfg);
            services.AddHostedService<RabbitMQHostedService>();
        }
    }
}