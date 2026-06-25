namespace ClientProfiles.Application.DTOs.Responses
{
	public record PersonResponse(
		Guid ClientId,
		string FirstName,
		string? MiddleName,
		string LastName,
		DateTime DateOfBirth,
		int IdType,
		string IdentificationNumber);

	public record OrganizationResponse(
	Guid ClientId,
	string Name,
	int Industry,
	string IdentificationNumber);

	public record AddressResponse(
		Guid Id,
		string Street,
		string City,
		string State,
		string PostalCode,
		string Country,
		bool IsPrimary);

	public record PhoneResponse(
		Guid Id,
		string Number,
		int Type,
		bool IsPrimary);

	public record ClientResponse(
		Guid Id,
		int Type,
		DateTime CreatedAt,
		PersonResponse? Person,
		OrganizationResponse? Organization,
		IEnumerable<AddressResponse> Addresses,
		IEnumerable<PhoneResponse> Phones);
}
