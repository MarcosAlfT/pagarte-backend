using ClientProfiles.Domain;

namespace ClientProfiles.Application.DTOs
{
	public record CreatePhoneRequest(
		string Number,
		PhoneType Type);

	public record UpdatePhoneRequest(
		string Number,
		PhoneType Type);
}
