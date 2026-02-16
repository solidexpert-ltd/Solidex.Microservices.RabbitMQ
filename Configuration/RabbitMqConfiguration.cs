namespace Solidex.Microservices.RabbitMQ
{
    /// <summary>
    /// Configuration options for RabbitMQ connection.
    /// </summary>
    public class RabbitMqConfiguration
    {
        public string Hostname { get; set; } = string.Empty;

        public string QueueName { get; set; } = string.Empty;

        public string UserName { get; set; } = string.Empty;

        public string Password { get; set; } = string.Empty;

        public int Port { get; set; }

        public string VHost { get; set; } = string.Empty;
    }
}
