namespace Infrastructure.RabbitMQ
{
    /// <summary>
    /// Contract for publishing messages to RabbitMQ.
    /// Implemented by RabbitMqPublisher.
    /// </summary>
    public interface IMessagePublisher
    {
        Task PublishAsync<T>(T message, string exchange, string routingKey) where T : class;
        Task PublishJsonAsync(string json, string exchange, string routingKey);
    }
}
