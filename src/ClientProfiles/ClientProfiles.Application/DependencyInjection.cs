using ClientProfiles.Application.UseCases.Addresses;
using ClientProfiles.Application.UseCases.Clients;
using ClientProfiles.Application.UseCases.Phones;
using Microsoft.Extensions.DependencyInjection;

namespace ClientProfiles.Application
{
	public static class DependencyInjection
	{
		public static IServiceCollection AddClientProfilesApplication(this IServiceCollection services)
		{
			services.AddScoped<GetClientByUserIdUseCase>();
			services.AddScoped<GetClientsUseCase>();
			services.AddScoped<CreatePersonClientUseCase>();
			services.AddScoped<CreateOrganizationClientUseCase>();
			services.AddScoped<UpdatePersonClientUseCase>();
			services.AddScoped<UpdateOrganizationClientUseCase>();
			services.AddScoped<DeleteClientUseCase>();

			services.AddScoped<GetAddressesByClientUseCase>();
			services.AddScoped<CreateAddressUseCase>();
			services.AddScoped<UpdateAddressUseCase>();
			services.AddScoped<SetPrimaryAddressUseCase>();
			services.AddScoped<DeleteAddressUseCase>();

			services.AddScoped<GetPhonesByClientUseCase>();
			services.AddScoped<CreatePhoneUseCase>();
			services.AddScoped<UpdatePhoneUseCase>();
			services.AddScoped<SetPrimaryPhoneUseCase>();
			services.AddScoped<DeletePhoneUseCase>();

			return services;
		}
	}
}
