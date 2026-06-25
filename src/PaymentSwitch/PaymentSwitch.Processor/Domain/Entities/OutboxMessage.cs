namespace PaymentSwitch.Processor.Domain.Entities
{
	public class OutboxMessage
	{
		public Guid Id { get; set; }
		public string MessageType { get; set; } = string.Empty;
		public string Payload { get; set; } = string.Empty;
		public string Exchange { get; set; } = string.Empty;
		public string RoutingKey { get; set; } = string.Empty;
		public DateTime CreatedAt { get; set; }
		public DateTime? PublishedAt { get; set; }
		public DateTime? LastAttemptAt { get; set; }
		public DateTime NextAttemptAt { get; set; }
		public int Attempts { get; set; }
		public string? ErrorMessage { get; set; }

		public static OutboxMessage Create(
			string messageType,
			string payload,
			string exchange,
			string routingKey,
			DateTime utcNow)
		{
			return new OutboxMessage
			{
				Id = Guid.NewGuid(),
				MessageType = messageType,
				Payload = payload,
				Exchange = exchange,
				RoutingKey = routingKey,
				CreatedAt = utcNow,
				NextAttemptAt = utcNow
			};
		}

		public void MarkPublished(DateTime utcNow)
		{
			PublishedAt = utcNow;
			LastAttemptAt = utcNow;
			ErrorMessage = null;
		}

		public void MarkFailed(string errorMessage, DateTime utcNow)
		{
			Attempts++;
			LastAttemptAt = utcNow;
			ErrorMessage = errorMessage;

			var retryDelayMinutes = Math.Min(Attempts, 10);
			NextAttemptAt = utcNow.AddMinutes(retryDelayMinutes);
		}
	}
}
