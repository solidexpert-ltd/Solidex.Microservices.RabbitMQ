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
using RabbitMQ.Client.Exceptions;

namespace Solidex.Microservices.RabbitMQ.Endpoints
{
    /// <summary>
    /// RabbitMQ consumer wrapper that dispatches messages to MediatR.
    /// Exchanges and queues are declared automatically if they do not exist.
    /// </summary>
    public class RabbitMqWrapper<T> : IBus
    {
        private IChannel _channel;
        private readonly IConnection _connection;
        private readonly string _queue;
        private readonly IServiceProvider _serviceProvider;

        public RabbitMqWrapper(
            IOptions<RabbitMqConfiguration> options,
            EndpointConfiguration<T> configuration,
            IServiceProvider services)
        {
            _serviceProvider = services;
            var opts = options.Value;
            var factory = new ConnectionFactory
            {
                HostName = opts.Hostname,
                UserName = opts.UserName,
                Password = opts.Password,
                Port = opts.Port > 0 ? opts.Port : AmqpTcpEndpoint.UseDefaultPort,
                VirtualHost = string.IsNullOrEmpty(opts.VHost) ? "/" : opts.VHost
            };

            _connection = factory.CreateConnectionAsync(CancellationToken.None).GetAwaiter().GetResult();
            _channel = _connection.CreateChannelAsync(null, CancellationToken.None).GetAwaiter().GetResult();
            _queue = configuration.Queue;
            _channel.QueueDeclareAsync(_queue, configuration.Durable, configuration.Exclusive,
                    configuration.AutoDelete, configuration.Arguments, false, false, CancellationToken.None)
                .GetAwaiter().GetResult();
            if (!string.IsNullOrEmpty(configuration.Exchange))
            {
                EnsureExchangeAndBinding(configuration);
            }
        }

        private void EnsureExchangeAndBinding(EndpointConfiguration<T> configuration)
        {
            var exchangeType = string.IsNullOrEmpty(configuration.ExchangeType) ? "direct" : configuration.ExchangeType;
            try
            {
                _channel.ExchangeDeclareAsync(configuration.Exchange, exchangeType, true, false, null, false, false, CancellationToken.None)
                    .GetAwaiter().GetResult();
                _channel.QueueBindAsync(configuration.Queue, configuration.Exchange, configuration.RoutingKey, null, false, CancellationToken.None)
                    .GetAwaiter().GetResult();
            }
            catch (OperationInterruptedException ex) when (ex.ShutdownReason?.ReplyCode == 404)
            {
                // Exchange or queue not found - channel is now invalid; create new channel and declare exchange then bind
                try
                {
                    _channel.CloseAsync(200, "Recovering after NOT_FOUND", false, CancellationToken.None).GetAwaiter().GetResult();
                }
                catch
                {
                    /* channel already closed */
                }
                _channel = _connection.CreateChannelAsync(null, CancellationToken.None).GetAwaiter().GetResult();
                _channel.ExchangeDeclareAsync(configuration.Exchange, exchangeType, true, false, null, false, false, CancellationToken.None)
                    .GetAwaiter().GetResult();
                _channel.QueueBindAsync(configuration.Queue, configuration.Exchange, configuration.RoutingKey, null, false, CancellationToken.None)
                    .GetAwaiter().GetResult();
            }
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

                    var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();

                    var message = JsonConvert.DeserializeObject<T>(content);
                    
                    if (message != null)
                        await mediator.Send(message, stoppingToken);
                }
                finally
                {
                    await _channel.BasicAckAsync(ea.DeliveryTag, false, CancellationToken.None);
                }
            };

            _channel.BasicConsumeAsync(_queue, false, string.Empty, false, false, null, consumer, CancellationToken.None)
                .GetAwaiter().GetResult();
            return Task.CompletedTask;
        }

        public void Dispose()
        {
            _channel.CloseAsync(200, "Connection closed", false, CancellationToken.None).GetAwaiter().GetResult();
            _connection.Dispose();
        }
    }
}
