using RabbitMQ.Client;
using Shared.RabbitMQ;
using System.Text;
using System.Text.Json;

public class RabbitMQPublisher : IMessagePublisher, IDisposable
{
	private readonly RabbitMQConnectionFactory _connectionFactory;
	private IChannel? _channel;

	public RabbitMQPublisher(RabbitMQConnectionFactory connectionFactory)
	{
		_connectionFactory = connectionFactory;
	}

	private async Task<IChannel> GetChannelAsync()
	{
		if (_channel == null || _channel.IsClosed)
		{
			var connection = await _connectionFactory.GetConnectionAsync();
			_channel = await connection.CreateChannelAsync();
		}
		return _channel;
	}

	public async Task PublishAsync<T>(T message, string exchange, string routingKey) where T : class
	{
		var json = JsonSerializer.Serialize(message);
		await PublishJsonAsync(json, exchange, routingKey);
	}

	public async Task PublishJsonAsync(string json, string exchange, string routingKey)
	{
		var channel = await GetChannelAsync();
		var body = Encoding.UTF8.GetBytes(json);
		var properties = new BasicProperties
		{
			Persistent = true,
			ContentType = "application/json",
			MessageId = Guid.NewGuid().ToString(),
			Timestamp = new AmqpTimestamp(DateTimeOffset.UtcNow.ToUnixTimeSeconds())
		};

		await channel.BasicPublishAsync(
			exchange: exchange,
			routingKey: routingKey,
			mandatory: false,
			basicProperties: properties,
			body: body);
	}

	public void Dispose()
	{
		_channel?.CloseAsync().GetAwaiter().GetResult();
		_channel?.Dispose();
	}
}
