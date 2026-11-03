namespace ClientProfiles.Domain.Entities
{
	public class Client
	{
		public Guid Id { get; private set; }
		// UserId is retained only for pre-migration records. It is never used for authorization.
		public string UserId { get; private set; } = string.Empty;
		public string? Issuer { get; private set; }
		public string? Subject { get; private set; }
		public ClientType Type { get; private set; }
		public DateTime CreatedAt { get; private set; } = DateTime.UtcNow;
		public bool IsDeleted { get; private set; } = false;
		public DateTime? DeletedAt { get; private set; }

		// Navigation properties
		public Person? Person { get; private set; }
		public Organization? Organization { get; private set; }
		public ICollection<Address> Addresses { get; private set; } = [];
		public ICollection<Phone> Phones { get; private set; } = [];

		private Client()
		{
		}

		public static Client CreateClient(string issuer, string subject, ClientType type)
		{
			if (string.IsNullOrWhiteSpace(issuer) || string.IsNullOrWhiteSpace(subject))
				throw new ArgumentException("Issuer and subject are required.");
			return new Client
			{
				Id = Guid.NewGuid(),
				Issuer = issuer,
				Subject = subject,
				Type = type,
				CreatedAt = DateTime.UtcNow
			};
		}
		public void Delete()
		{
			IsDeleted = true;
			DeletedAt = DateTime.UtcNow;
		}
	}
}
