namespace ClientProfiles.Api.Domain.Entities
{
	public class Person
	{
		public Guid ClientId { get; set; } // Foreign key to Client
		public string FirstName { get; set; } = string.Empty;
		public string? MiddleName { get; set; }
		public string LastName { get; set; } = string.Empty;
		public DateTime DateOfBirth { get; set; }
		public IdentificationType IdType { get; set; }
		public string IdentificationNumber { get; set; } = string.Empty;
		public DateTime? UpdatedAt { get; set; }

		//Navigation property
		public Client Client { get; set; } = null!;
		public static Person CreatePerson(Guid clientId, string firstName, string? middleName, string lastName, DateTime dateOfBirth, IdentificationType idType, string identificationNumber)
		{
			return new Person
			{
				ClientId = clientId,
				FirstName = firstName,
				MiddleName = middleName,
				LastName = lastName,
				DateOfBirth = dateOfBirth,
				IdType = idType,
				IdentificationNumber = identificationNumber,
			};
		}

		public void UpdatePerson(string firstName, string? middleName, string lastName, DateTime dateOfBirth, IdentificationType idType, string identificationNumber)
		{
			FirstName = firstName;
			MiddleName = middleName;
			LastName = lastName;
			DateOfBirth = dateOfBirth;
			IdType = idType;
			IdentificationNumber = identificationNumber;
			UpdatedAt = DateTime.UtcNow;
		}
	}

}
