using ClientProfiles.Api.Domain.Entities;
using Mapster;

namespace ClientProfiles.Api.DTOs.Responses
{
	public static class MappingConfig
	{
		public static void Configure()
		{
			TypeAdapterConfig<Client, ClientResponse>.NewConfig()
				.Map(dest => dest.Person, src => src.Person == null ? null : src.Person.Adapt<PersonResponse>())
				.Map(dest => dest.Organization, src => src.Organization == null ? null : src.Organization.Adapt<OrganizationResponse>())
				.Map(dest => dest.Addresses, src => src.Addresses.Adapt<IEnumerable<AddressResponse>>())
				.Map(dest => dest.Phones, src => src.Phones.Adapt<IEnumerable<PhoneResponse>>());
		}

	}
}
