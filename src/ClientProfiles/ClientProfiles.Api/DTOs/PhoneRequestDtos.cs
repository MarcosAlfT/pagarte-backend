using ClientProfiles.Api.Domain;

namespace ClientProfiles.Api.DTOs
{
	public record CreatePhoneRequest(
		string Number,
		PhoneType Type,
		bool IsPrimary);

	public record UpdatePhoneRequest(
		string Number,
		PhoneType Type,
		bool IsPrimary);
}
