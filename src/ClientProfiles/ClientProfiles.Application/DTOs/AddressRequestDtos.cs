using ClientProfiles.Domain;

namespace ClientProfiles.Application.DTOs
{
	public record CreateAddressRequest(
		string Street,
		string City,
		string State,
		string PostalCode,
		string Country);

	public record UpdateAddressRequest(
		string Street,
		string City,
		string State,
		string PostalCode,
		string Country);
}
