using ClientProfiles.Api.Domain;

namespace ClientProfiles.Api.DTOs
{
	public record CreatePersonRequest(
		string FirstName,
		string? MiddleName,
		string LastName,
		DateTime DateOfBirth,
		IdentificationType IdType,
		string IdentificationNumber);

	public record CreateOrganizationRequest(
		string Name,
		IndustryType Industry,
		string IdentificationNumber);

	public record UpdatePersonRequest(
		string FirstName,
		string? MiddleName,
		string LastName,
		DateTime DateOfBirth,
		IdentificationType IdType,
		string IdentificationNumber);

	public record UpdateOrganizationRequest(
		string Name,
		IndustryType Industry,
		string IdentificationNumber);
}