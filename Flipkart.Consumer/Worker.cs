using Flipkart.Publisher;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using System;
using System.Collections.Generic;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace Flipkart.Consumer
{
    // Local copy of the message contract used by publisher and consumer.
    // You may remove this if you share the model in a common project.
    public record OrderCreatedMessage(string OrderId, decimal Amount, DateTime CreatedAt);

    public class Worker : BackgroundService
    {
        private readonly ILogger<Worker> _logger;
        private readonly RabbitMqSettings _settings;
        private IConnection? _connection;
        private IModel? _channel;

        public Worker(ILogger<Worker> logger, IOptions<RabbitMqSettings> options)
        {
            _logger = logger;
            _settings = options.Value;
        }

        public override Task StartAsync(CancellationToken cancellationToken)
        {
            var factory = new ConnectionFactory
            {
                HostName = _settings.HostName,
                Port = _settings.Port,
                UserName = _settings.UserName,
                Password = _settings.Password,
                VirtualHost = _settings.VirtualHost,
                DispatchConsumersAsync = true
            };

            _logger.LogInformation("Connecting to RabbitMQ {Host}:{Port} vhost={Vhost}",
                _settings.HostName, _settings.Port, _settings.VirtualHost);

            _connection = factory.CreateConnection();
            _channel = _connection.CreateModel();

            // Ensure exchange, queue and DLQ exist (idempotent)
            DeclareExchangesAndQueues();

            // Back-pressure: max 10 unacked messages per consumer
            _channel.BasicQos(0, 10, false);

            _logger.LogInformation("Flipkart.Consumer connected to RabbitMQ and ready to consume from queue '{QueueName}'.",
                _settings.QueueName);

            return base.StartAsync(cancellationToken);
        }

        private void DeclareExchangesAndQueues()
        {
            if (_channel == null) throw new InvalidOperationException("Channel is not initialized.");

            // Main exchange (direct)
            _channel.ExchangeDeclare(_settings.ExchangeName, ExchangeType.Direct, durable: true, autoDelete: false);

            // Dead-letter exchange (fanout)
            _channel.ExchangeDeclare(_settings.DeadLetterExchange, ExchangeType.Fanout, durable: true, autoDelete: false);

            // Main queue with DLX argument
            var queueArgs = new Dictionary<string, object>
            {
                { "x-dead-letter-exchange", _settings.DeadLetterExchange }
            };

            _channel.QueueDeclare(
                queue: _settings.QueueName,
                durable: true,
                exclusive: false,
                autoDelete: false,
                arguments: queueArgs);

            // Dead-letter queue
            _channel.QueueDeclare(
                queue: _settings.DeadLetterQueue,
                durable: true,
                exclusive: false,
                autoDelete: false,
                arguments: null);

            // Bind main queue to exchange with routing key
            _channel.QueueBind(_settings.QueueName, _settings.ExchangeName, _settings.RoutingKey);

            // Bind DLQ to DLX
            _channel.QueueBind(_settings.DeadLetterQueue, _settings.DeadLetterExchange, string.Empty);
        }

        protected override Task ExecuteAsync(CancellationToken stoppingToken)
        {
            if (_channel == null)
                throw new InvalidOperationException("Channel is not initialized.");

            var consumer = new AsyncEventingBasicConsumer(_channel);

            consumer.Received += async (model, ea) =>
            {
                // Respect cancellation
                if (stoppingToken.IsCancellationRequested)
                {
                    _logger.LogInformation("Cancellation requested. Not processing new messages.");
                    return;
                }

                var body = ea.Body.ToArray();
                var json = Encoding.UTF8.GetString(body);

                try
                {
                    var order = JsonSerializer.Deserialize<OrderCreatedMessage>(json);
                    if (order is null)
                        throw new Exception("Invalid message payload");

                    _logger.LogInformation("Processing Flipkart Order {OrderId}, Amount {Amount}",
                        order.OrderId, order.Amount);

                    // Simulate business logic:
                    // - Reserve stock
                    // - Charge payment
                    // - Send confirmation email/SMS
                    await Task.Delay(500, stoppingToken);

                    _channel.BasicAck(ea.DeliveryTag, multiple: false);
                    _logger.LogInformation("Order {OrderId} processed successfully.", order.OrderId);
                }
                catch (OperationCanceledException)
                {
                    // If the app is shutting down, requeue the message so another consumer can pick it up.
                    _logger.LogWarning("Operation cancelled while processing message. Requeuing.");
                    _channel.BasicNack(ea.DeliveryTag, multiple: false, requeue: true);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to process message. Sending to DLQ (no requeue).");
                    // Send to DLQ by nack without requeue (queue must have x-dead-letter-exchange)
                    _channel.BasicNack(ea.DeliveryTag, multiple: false, requeue: false);
                }
            };

            // Start consuming (autoAck: false -> manual ack)
            _channel.BasicConsume(
                queue: _settings.QueueName,
                autoAck: false,
                consumer: consumer);

            return Task.CompletedTask;
        }

        public override void Dispose()
        {
            try
            {
                _channel?.Close();
                _connection?.Close();
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Exception while closing RabbitMQ connection.");
            }

            _channel?.Dispose();
            _connection?.Dispose();

            base.Dispose();
        }
    }
}
