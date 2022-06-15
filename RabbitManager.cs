using System;
using System.Collections.Concurrent;
using System.Text;
using Microsoft.Extensions.ObjectPool;
using Newtonsoft.Json;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using Solidex.Core.Base.Abstraction;

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
    }
}