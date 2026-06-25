using ClientProfiles.Application.Abstractions;
using ClientProfiles.Persistence.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace ClientProfiles.Persistence
{
	public static class DependencyInjection
	{
		public static IServiceCollection AddClientProfilesPersistence(this IServiceCollection services, IConfiguration configuration)
		{
			services.AddDbContext<ClientProfilesDbContext>(options =>
				options.UseSqlServer(configuration.GetConnectionString("ClientProfilesDb")));

			services.AddScoped<IUnitOfWork>(provider => provider.GetRequiredService<ClientProfilesDbContext>());
			services.AddScoped<IClientRepository, ClientRepository>();
			services.AddScoped<IPersonRepository, PersonRepository>();
			services.AddScoped<IOrganizationRepository, OrganizationRepository>();
			services.AddScoped<IAddressRepository, AddressRepository>();
			services.AddScoped<IPhoneRepository, PhoneRepository>();

			return services;
		}
	}
}
