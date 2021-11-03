using System;


namespace solidex.microcervices.rabbitMQ
{
    public class RabbitMqConfiguration
    {
        public string Hostname { get; set; }

        public string QueueName { get; set; }

        public string UserName { get; set; }

        public string Password { get; set; }
        public int Port { get; set; }
        public string VHost { get; set; }
    }
}