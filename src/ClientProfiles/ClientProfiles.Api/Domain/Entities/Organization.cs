using System.Data;

namespace ClientProfiles.Api.Domain.Entities
{
	
	public class Organization
	{
		public Guid ClientId { get; set; } // Foreign key to Client
		public string Name { get; set; } = string.Empty;
		public IndustryType Industry { get; set; }
		public string IdentificationNumber { get; set; } = string.Empty;
		public DateTime? UpdatedAt { get; set; }

		// Navigation property
		public Client Client { get; set; } = null!; // Navigation property

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
