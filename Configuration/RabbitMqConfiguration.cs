namespace Solidex.Microservices.RabbitMQ
{
    /// <summary>
    /// Configuration options for RabbitMQ connection.
    /// When <see cref="Uri"/> is set, it takes precedence over Hostname/UserName/Password/Port/VHost.
    /// Use amqp:// or amqps:// (e.g. amqp://guest:guest@rabbitmq:5672/).
    /// </summary>
    public class RabbitMqConfiguration
    {
        /// <summary>
        /// AMQP connection URI (e.g. amqp://user:password@host:5672/vhost).
        /// When set, overrides Hostname, UserName, Password, Port, VHost. Ideal for containers via env (e.g. RabbitMQ__Uri).
        /// </summary>
        public string Uri { get; set; } = string.Empty;

        public string Hostname { get; set; } = string.Empty;

        public string QueueName { get; set; } = string.Empty;

        public string UserName { get; set; } = string.Empty;

        public string Password { get; set; } = string.Empty;

        public int Port { get; set; }

        public string VHost { get; set; } = string.Empty;
    }
}
