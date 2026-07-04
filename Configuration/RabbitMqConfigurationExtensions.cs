using System;
using RabbitMQ.Client;

namespace Solidex.Microservices.RabbitMQ
{
    /// <summary>
    /// Extension methods for <see cref="RabbitMqConfiguration"/> to create a RabbitMQ.Client <see cref="ConnectionFactory"/>.
    /// Use when configuring health checks or any code that needs a factory with the same endpoint (including Uri).
    /// </summary>
    public static class RabbitMqConfigurationExtensions
    {
        /// <summary>
        /// Creates a <see cref="ConnectionFactory"/> from this configuration.
        /// When <see cref="RabbitMqConfiguration.Uri"/> is set, it is used; otherwise Hostname, UserName, Password, Port, VHost are used.
        /// </summary>
        public static ConnectionFactory CreateConnectionFactory(this RabbitMqConfiguration options)
        {
            var factory = new ConnectionFactory();

            if (!string.IsNullOrWhiteSpace(options.Uri))
            {
                factory.Uri = new Uri(options.Uri);
            }
            else
            {
                factory.HostName = options.Hostname;
                factory.UserName = options.UserName;
                factory.Password = options.Password;
                factory.Port = options.Port > 0 ? options.Port : AmqpTcpEndpoint.UseDefaultPort;
                factory.VirtualHost = string.IsNullOrEmpty(options.VHost) ? "/" : options.VHost;
            }

            return factory;
        }
    }
}
