namespace ClientProfiles.Api.Domain.Entities
{
	public class Address
	{
		public Guid Id { get; set; }
		public Guid ClientId { get; set; }
		public string Street { get; set; } = string.Empty;
		public string City { get; set; } = string.Empty;
		public string State { get; set; } = string.Empty;
		public string PostalCode { get; set; } = string.Empty;
		public string Country { get; set; } = string.Empty;
		public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
		public DateTime? LastUpdatedAt { get; set; }
		public bool IsPrimary { get; set; } = false;
		public bool IsDeleted { get; set; } = false;
		public DateTime? DeletedAt { get; set; }

		// Navigation property
		public Client Client { get; set; } = null!;

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
		public void UpdateAddress(string street, string city, string state, string postalCode, string country, bool isPrimary)
		{
			Street = street;
			City = city;
			State = state;
			PostalCode = postalCode;
			Country = country;
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
