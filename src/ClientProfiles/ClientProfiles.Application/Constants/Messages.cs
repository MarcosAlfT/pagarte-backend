namespace ClientProfiles.Application.Constants
{
	public class Messages
	{
		public static class Client
		{
			public const string NotFound = "Client not found.";
			public const string Created = "Client created successfully.";
			public const string Updated = "Client updated successfully.";
			public const string Deleted = "Client deleted successfully.";
			public const string Error = "An error occurred while processing the client.";
			public const string AlreadyExists = "A client profile already exists for this user.";
			public const string NotPerson = "Client is not a person.";
			public const string NotOrganization = "Client is not an organization.";
		}

		public static class Address
		{
			public const string NotFound = "Address not found.";
			public const string Created = "Address created successfully.";
			public const string Updated = "Address updated successfully.";
			public const string Deleted = "Address deleted successfully.";
			public const string Error = "An error occurred while processing the address.";
			public const string PrimaryAddressExists = "A primary address already exists.";
		}

		public static class Phone
		{
			public const string NotFound = "Phone not found.";
			public const string Created = "Phone created successfully.";
			public const string Updated = "Phone updated successfully.";
			public const string Deleted = "Phone deleted successfully.";
			public const string Error = "An error occurred while processing the phone.";
			public const string PrimaryPhoneExists = "A primary phone already exists.";
		}

		public static class Auth
		{
			public const string InvalidToken = "Invalid or missing token.";
			public const string Unauthorized = "Unauthorized access.";
		}
	}
}
