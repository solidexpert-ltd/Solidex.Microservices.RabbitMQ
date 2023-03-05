using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Reflection;
using System.Text;
using Microsoft.Extensions.ObjectPool;
using Newtonsoft.Json;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using Solidex.Core.Base.Abstraction;
using Solidex.Microservices.RabbitMQ.Attributes;

namespace Solidex.Microservices.RabbitMQ
{
    public class RabbitManager : IRabbitManager
    {
        private readonly DefaultObjectPool<IModel> _objectPool;


        public RabbitManager(IPooledObjectPolicy<IModel> objectPolicy)
        {
            _objectPool = new DefaultObjectPool<IModel>(objectPolicy, Environment.ProcessorCount * 2);
        }

        public void Publish<T>(T message, string exchangeName, string exchangeType, string routeKey)
            where T : class
        {
            if (message == null)
                return;

            var channel = _objectPool.Get();

            try
            {
                channel.ExchangeDeclare(exchangeName, exchangeType, true, false, null);

                var sendBytes = Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(message));

                var properties = channel.CreateBasicProperties();
                properties.Persistent = true;
                properties.Type = message.GetType().Name;

                channel.BasicPublish(exchangeName, routeKey, properties, sendBytes);
            }
            catch (Exception ex)
            {
                throw ex;
            }
            finally
            {
                _objectPool.Return(channel);
            }
        }

        public T Subscribe<T>(string queueName, Guid id) where T : IEntity
        {
            var respQueue = new BlockingCollection<T>();

            var channel = _objectPool.Get();
            channel.QueueDeclare(queueName, true, false, false, null);

            var consumer = new EventingBasicConsumer(channel);
            consumer.Received += (ch, ea) =>
            {
                var content = Encoding.UTF8.GetString(ea.Body.ToArray());

                var message = JsonConvert.DeserializeObject<T>(content);
                channel.BasicAck(ea.DeliveryTag, false);
                if (message.Id != id) return;
                respQueue.Add(message);
                respQueue.CompleteAdding();
            };

            channel.BasicConsume(queueName, false, consumer);

            return respQueue.Take();
        }

        public void Send<T>(T message) where T : class
        {
            if (message == null)
                return;

            var channel = _objectPool.Get();

            var attribute = typeof(T).GetCustomAttributes(typeof(RabbitQueryAttribute)).FirstOrDefault() as RabbitQueryAttribute;

            if (attribute is null)
                throw new Exception($"The {nameof(RabbitQueryAttribute)} attribute is not exist");

            try
            {
                channel.ExchangeDeclare(attribute.ExchangeName, attribute.ExchangeType, true, false, null);

                var sendBytes = Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(message));

                var properties = channel.CreateBasicProperties();
                properties.Persistent = true;

                channel.BasicPublish(attribute.ExchangeName, attribute.RouteKey, properties, sendBytes);
            }
            catch (Exception ex)
            {
                throw;
            }
            finally
            {
                _objectPool.Return(channel);
            }
        }

        public void Consume<T, TE>(Func<T, TE> lambda)
        { 
            var attribute = typeof(T).GetCustomAttributes(typeof(RabbitQueryAttribute)).FirstOrDefault() as RabbitQueryAttribute;

            if (attribute is null)
                throw new Exception($"The {nameof(RabbitQueryAttribute)} attribute is not exist");
            
            var channel = _objectPool.Get();
            channel.QueueDeclare(Assembly.GetExecutingAssembly().FullName + nameof(T), true, false, false, null);

            var consumer = new EventingBasicConsumer(channel);
            
            consumer.Received += async (ch, ea) =>
            {
                var content = Encoding.UTF8.GetString(ea.Body.ToArray());
                try
                {
                    var message = JsonConvert.DeserializeObject<T>(content);
                    var res = lambda.Invoke(message);
                }
                finally
                {
                    channel.BasicAck(ea.DeliveryTag, false);
                }
            };
        }
    }
}