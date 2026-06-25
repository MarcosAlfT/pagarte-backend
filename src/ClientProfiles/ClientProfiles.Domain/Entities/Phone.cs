namespace ClientProfiles.Domain.Entities
{
	public class Phone
	{
		public Guid Id { get; private set; }
		public Guid ClientId { get; private set; }
		public string Number { get; private set; } = string.Empty!;
		public PhoneType Type { get; private set; } // e.g., Mobile, Home, Work
		public bool IsPrimary { get; private set; } = false;
		public DateTime CreatedAt { get; private set; } = DateTime.UtcNow;
		public DateTime? UpdatedAt { get; private set; }
		public bool IsDeleted { get; private set; } = false;
		public DateTime? DeletedAt { get; private set; }

		// Navigation property
		public Client Client { get; private set; } = null!;

		private Phone()
		{
		}

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
		public void UpdatePhone(string number, PhoneType type)
		{
			Number = number;
			Type = type;
			UpdatedAt = DateTime.UtcNow;
		}
		public void SetPrimary(bool isPrimary)
		{
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
