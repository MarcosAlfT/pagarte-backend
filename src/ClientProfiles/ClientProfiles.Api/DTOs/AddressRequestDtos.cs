using ClientProfiles.Api.Domain;

namespace ClientProfiles.Api.DTOs
{
	public record CreateAddressRequest(
		string Street,
		string City,
		string State,
		string PostalCode,
		string Country,
		bool IsPrimary);

	public record UpdateAddressRequest(
		string Street,
		string City,
		string State,
		string PostalCode,
		string Country,
		bool IsPrimary);
}