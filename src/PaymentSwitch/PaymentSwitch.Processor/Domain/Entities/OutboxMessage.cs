namespace PaymentSwitch.Processor.Domain.Entities
{
	public class OutboxMessage
	{
		public Guid Id { get; set; }
		public string MessageType { get; set; } = string.Empty;
		public string Payload { get; set; } = string.Empty;
		public string Exchange { get; set; } = string.Empty;
		public string RoutingKey { get; set; } = string.Empty;
		public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
		public DateTime? PublishedAt { get; set; }
		public DateTime? LastAttemptAt { get; set; }
		public DateTime NextAttemptAt { get; set; } = DateTime.UtcNow;
		public int Attempts { get; set; }
		public string? ErrorMessage { get; set; }

		public static OutboxMessage Create(
			string messageType,
			string payload,
			string exchange,
			string routingKey)
		{
			return new OutboxMessage
			{
				Id = Guid.NewGuid(),
				MessageType = messageType,
				Payload = payload,
				Exchange = exchange,
				RoutingKey = routingKey,
				CreatedAt = DateTime.UtcNow,
				NextAttemptAt = DateTime.UtcNow
			};
		}

		public void MarkPublished()
		{
			PublishedAt = DateTime.UtcNow;
			LastAttemptAt = DateTime.UtcNow;
			ErrorMessage = null;
		}

		public void MarkFailed(string errorMessage)
		{
			Attempts++;
			LastAttemptAt = DateTime.UtcNow;
			ErrorMessage = errorMessage;

			var retryDelayMinutes = Math.Min(Attempts, 10);
			NextAttemptAt = DateTime.UtcNow.AddMinutes(retryDelayMinutes);
		}
	}
}
