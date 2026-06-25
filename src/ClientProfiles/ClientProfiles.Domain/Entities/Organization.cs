namespace ClientProfiles.Domain.Entities
{
	
	public class Organization
	{
		public Guid ClientId { get; private set; } // Foreign key to Client
		public string Name { get; private set; } = string.Empty;
		public IndustryType Industry { get; private set; }
		public string IdentificationNumber { get; private set; } = string.Empty;
		public DateTime? UpdatedAt { get; private set; }

		// Navigation property
		public Client Client { get; private set; } = null!; // Navigation property

		private Organization()
		{
		}

		public static Organization CreateOrganization(Guid clientId, string name, IndustryType industry, string identificationNumber)
		{			
			return new Organization
			{
				ClientId = clientId,
				Name = name,
				Industry = industry,
				IdentificationNumber = identificationNumber,
			};
		}
		
		public void UpdateOrganization(string name, IndustryType industry, string identificationNumber)
		{
			Name = name;
			Industry = industry;
			IdentificationNumber = identificationNumber;
			UpdatedAt = DateTime.UtcNow;
		}

	}
}
