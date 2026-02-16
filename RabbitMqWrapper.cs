using System;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Newtonsoft.Json;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using Solidex.Microservices.RabbitMQ;

namespace Solidex.Telegram.RabbitMQEndpointBinder
{
    public class RabbitMqWrapper<T> : IBus
    {
        private readonly IChannel _channel;
        private readonly IConnection _connection;
        private readonly string _queue;
        private readonly IServiceProvider _serviceProvider;

        public RabbitMqWrapper(IOptions<RabbitMqConfiguration> options,
            EndpointConfiguration<T> configuration, IServiceProvider services)
        {
            _serviceProvider = services;
            var factory = new ConnectionFactory
            {
                HostName = options.Value.Hostname,
                UserName = options.Value.UserName,
                Password = options.Value.Password,
                Port = AmqpTcpEndpoint.UseDefaultPort,
                VirtualHost = options.Value.VHost
            };

            _connection = factory.CreateConnectionAsync(CancellationToken.None).GetAwaiter().GetResult();
            _channel = _connection.CreateChannelAsync(null, CancellationToken.None).GetAwaiter().GetResult();
            _queue = configuration.Queue;
            _channel.QueueDeclareAsync(_queue, configuration.Durable, configuration.Exclusive,
                configuration.AutoDelete, configuration.Arguments, false, false, CancellationToken.None).GetAwaiter().GetResult();
            _channel.QueueBindAsync(configuration.Queue, configuration.Exchange, configuration.RoutingKey, null, false, CancellationToken.None).GetAwaiter().GetResult();
        }

        public Task ExecuteAsync(CancellationToken stoppingToken)
        {
            var consumer = new AsyncEventingBasicConsumer(_channel);
            consumer.ReceivedAsync += async (_, ea) =>
            {
                var content = Encoding.UTF8.GetString(ea.Body.Span);
                try
                {
                    using var scope = _serviceProvider.CreateScope();

                    var mediator =
                        scope.ServiceProvider
                            .GetRequiredService<IMediator>();

                    var message = JsonConvert.DeserializeObject<T>(content);
                    if (message != null)
                        await mediator.Send(message, stoppingToken);
                }
                finally
                {
                    await _channel.BasicAckAsync(ea.DeliveryTag, false, CancellationToken.None);
                }
            };

            _channel.BasicConsumeAsync(_queue, false, string.Empty, false, false, null, consumer, CancellationToken.None).GetAwaiter().GetResult();
            return Task.CompletedTask;
        }

        public void Dispose()
        {
            _channel.CloseAsync(200, "Connection closed", false, CancellationToken.None).GetAwaiter().GetResult();
            _connection.Dispose();
        }
    }
}