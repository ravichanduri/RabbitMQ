namespace Flipkart.Publisher
{
    public class RabbitMqSettings
    {
        public string HostName { get; set; } = "localhost";
        public int Port { get; set; } = 5672;
        public string UserName { get; set; } = "guest";
        public string Password { get; set; } = "guest";
        public string VirtualHost { get; set; } = "app_vhost";

        public string ExchangeName { get; set; } = "orders.exchange";
        public string QueueName { get; set; } = "orders.queue";
        public string RoutingKey { get; set; } = "orders.created";
        public string DeadLetterExchange { get; set; } = "orders.dlx";
        public string DeadLetterQueue { get; set; } = "orders.dlq";
    }

}
