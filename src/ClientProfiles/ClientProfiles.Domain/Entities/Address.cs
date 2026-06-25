namespace ClientProfiles.Domain.Entities
{
	public class Address
	{
		public Guid Id { get; private set; }
		public Guid ClientId { get; private set; }
		public string Street { get; private set; } = string.Empty;
		public string City { get; private set; } = string.Empty;
		public string State { get; private set; } = string.Empty;
		public string PostalCode { get; private set; } = string.Empty;
		public string Country { get; private set; } = string.Empty;
		public DateTime CreatedAt { get; private set; } = DateTime.UtcNow;
		public DateTime? LastUpdatedAt { get; private set; }
		public bool IsPrimary { get; private set; } = false;
		public bool IsDeleted { get; private set; } = false;
		public DateTime? DeletedAt { get; private set; }

		// Navigation property
		public Client Client { get; private set; } = null!;

		private Address()
		{
		}

		public static Address CreateAddress(Guid clientId, string street, string city, string state, string postalCode, string country, bool isPrimary)
		{
			return new Address
			{
				Id = Guid.NewGuid(),
				ClientId = clientId,
				Street = street,
				City = city,
				State = state,
				PostalCode = postalCode,
				Country = country,
				IsPrimary = isPrimary,
				CreatedAt = DateTime.UtcNow
			};
		}
		public void UpdateAddress(string street, string city, string state, string postalCode, string country)
		{
			Street = street;
			City = city;
			State = state;
			PostalCode = postalCode;
			Country = country;
			LastUpdatedAt = DateTime.UtcNow;
		}
		public void SetPrimary(bool isPrimary)
		{
			IsPrimary = isPrimary;
			LastUpdatedAt = DateTime.UtcNow;
		}
		public void DeleteAddress()
		{
			IsDeleted = true;
			DeletedAt = DateTime.UtcNow;
		}
	}
}
