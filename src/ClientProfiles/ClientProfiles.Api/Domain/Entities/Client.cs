namespace ClientProfiles.Api.Domain.Entities
{
	public class Client
	{
		public Guid Id { get; set; }
		public string UserId { get; set; } = string.Empty; // from Identity.Client
		public ClientType Type { get; set; }
		public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
		public bool IsDeleted { get; set; } = false;
		public DateTime? DeletedAt { get; set; }

		// Navigation properties
		public Person? Person { get; set; }
		public Organization? Organization { get; set; }
		public ICollection<Address> Addresses { get; set; } = [];
		public ICollection<Phone> Phones { get; set; } = [];

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
