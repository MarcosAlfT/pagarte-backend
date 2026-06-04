namespace ClientProfiles.Api.Domain.Entities
{
	public class Phone
	{
		public Guid Id { get; set; }
		public Guid ClientId { get; set; }
		public string Number { get; set; } = string.Empty!;
		public PhoneType Type { get; set; } // e.g., Mobile, Home, Work
		public bool IsPrimary { get; set; } = false;
		public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
		public  DateTime? UpdatedAt { get; set; }
		public bool IsDeleted { get; set; } = false;
		public DateTime? DeletedAt { get; set; }

		// Navigation property
		public Client Client { get; set; } = null!;

		public static Phone CreatePhone(Guid clientId, string number, PhoneType type, bool isPrimary)
		{
			return new Phone
			{
				Id = Guid.NewGuid(),
				ClientId = clientId,
				Number = number,
				Type = type,
				IsPrimary = isPrimary,
				CreatedAt = DateTime.UtcNow
			};
		}
		public void UpdatePhone(string number, PhoneType type, bool isPrimary)
		{
			Number = number;
			Type = type;
			IsPrimary = isPrimary;
			UpdatedAt = DateTime.UtcNow;
		}
		public void DeletePhone()
		{
			IsDeleted = true;
			DeletedAt = DateTime.UtcNow;
		}
	}
}
