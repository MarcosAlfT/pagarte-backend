namespace ClientProfiles.Domain.Entities
{
	public class Client
	{
		public Guid Id { get; private set; }
		public string UserId { get; private set; } = string.Empty; // from Identity.Client
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

		public static Client CreateClient(string userId, ClientType type)
		{
			return new Client
			{
				Id = Guid.NewGuid(),
				UserId = userId,
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
