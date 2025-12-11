using Flipkart.Publisher;
using Microsoft.Extensions.Options;
using RabbitMQ.Client;
using System.Text;
using System.Text.Json;

public interface IFlipkartOrderMessagePublisher
{
    Task PublishOrderAsync(OrderCreatedMessage message);
}

public class FlipkartOrderMessagePublisher : IFlipkartOrderMessagePublisher, IDisposable
{
    private readonly RabbitMqSettings _settings;
    private readonly IConnection _connection;
    private readonly IModel _channel;

    public FlipkartOrderMessagePublisher(IOptions<RabbitMqSettings> options)
    {
        _settings = options.Value;

        var factory = new ConnectionFactory
        {
            HostName = _settings.HostName,
            Port = _settings.Port,
            UserName = _settings.UserName,
            Password = _settings.Password,
            VirtualHost = _settings.VirtualHost,
            DispatchConsumersAsync = true
        };

        _connection = factory.CreateConnection();
        _channel = _connection.CreateModel();

        // Main exchange
        _channel.ExchangeDeclare(_settings.ExchangeName, ExchangeType.Direct, durable: true);

        // Dead-letter exchange
        _channel.ExchangeDeclare(_settings.DeadLetterExchange, ExchangeType.Fanout, durable: true);

        // Main queue with DLX
        _channel.QueueDeclare(
            queue: _settings.QueueName,
            durable: true,
            exclusive: false,
            autoDelete: false,
            arguments: new Dictionary<string, object>
            {
                { "x-dead-letter-exchange", _settings.DeadLetterExchange }
            });

        // Dead-letter queue
        _channel.QueueDeclare(
            queue: _settings.DeadLetterQueue,
            durable: true,
            exclusive: false,
            autoDelete: false);

        _channel.QueueBind(_settings.QueueName, _settings.ExchangeName, _settings.RoutingKey);
        _channel.QueueBind(_settings.DeadLetterQueue, _settings.DeadLetterExchange, string.Empty);
    }

    public Task PublishOrderAsync(OrderCreatedMessage message)
    {
        var json = JsonSerializer.Serialize(message);
        var body = Encoding.UTF8.GetBytes(json);

        var props = _channel.CreateBasicProperties();
        props.Persistent = true;

        _channel.BasicPublish(
            exchange: _settings.ExchangeName,
            routingKey: _settings.RoutingKey,
            basicProperties: props,
            body: body);

        return Task.CompletedTask;
    }

    public void Dispose()
    {
        _channel?.Dispose();
        _connection?.Dispose();
    }
}
