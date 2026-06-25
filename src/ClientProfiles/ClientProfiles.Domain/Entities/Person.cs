namespace ClientProfiles.Domain.Entities
{
	public class Person
	{
		public Guid ClientId { get; private set; } // Foreign key to Client
		public string FirstName { get; private set; } = string.Empty;
		public string? MiddleName { get; private set; }
		public string LastName { get; private set; } = string.Empty;
		public DateTime DateOfBirth { get; private set; }
		public IdentificationType IdType { get; private set; }
		public string IdentificationNumber { get; private set; } = string.Empty;
		public DateTime? UpdatedAt { get; private set; }

		//Navigation property
		public Client Client { get; private set; } = null!;

		private Person()
		{
		}

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
