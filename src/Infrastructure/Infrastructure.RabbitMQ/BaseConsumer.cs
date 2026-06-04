using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using System.Text;
using System.Text.Json;

namespace Infrastructure.RabbitMQ
{
	public abstract class BaseConsumer<T> : BackgroundService where T : class
	{
		private readonly RabbitMqConnectionFactory _connectionFactory;
		protected readonly ILogger _logger;
		private IChannel? _channel;

		protected abstract string QueueName { get; }
		protected virtual ushort PrefetchCount => 1;

		protected BaseConsumer(RabbitMqConnectionFactory connectionFactory, ILogger logger)
		{
			_connectionFactory = connectionFactory;
			_logger = logger;
		}

		protected override async Task ExecuteAsync(CancellationToken stoppingToken)
		{
			var connection = await _connectionFactory.GetConnectionAsync();
			_channel = await connection.CreateChannelAsync();
			await _channel.BasicQosAsync(0, PrefetchCount, false);

			var consumer = new AsyncEventingBasicConsumer(_channel);
			consumer.ReceivedAsync += async (_, ea) =>
			{
				try
				{
					var body = Encoding.UTF8.GetString(ea.Body.ToArray());
					var message = JsonSerializer.Deserialize<T>(body);

					if (message != null)
						await HandleAsync(message, stoppingToken);

					await _channel.BasicAckAsync(ea.DeliveryTag, false);
				}
				catch (Exception ex)
				{
					_logger.LogError(ex, "Error processing message from {Queue}", QueueName);
					await _channel.BasicNackAsync(ea.DeliveryTag, false, requeue: false);
				}
			};

			await _channel.BasicConsumeAsync(QueueName, autoAck: false, consumer: consumer);

			// Keep alive until cancelled
			await Task.Delay(Timeout.Infinite, stoppingToken);
		}

		protected abstract Task HandleAsync(T message, CancellationToken cancellationToken);

		public override void Dispose()
		{
			_channel?.CloseAsync().GetAwaiter().GetResult();
			_channel?.Dispose();
			base.Dispose();
		}
	}
}