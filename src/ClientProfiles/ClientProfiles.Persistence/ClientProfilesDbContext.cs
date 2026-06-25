using Microsoft.EntityFrameworkCore;
using ClientProfiles.Application.Abstractions;
using ClientProfiles.Domain.Entities;

namespace ClientProfiles.Persistence
{
	public class ClientProfilesDbContext: DbContext, IUnitOfWork
	{
		public ClientProfilesDbContext(DbContextOptions<ClientProfilesDbContext> options) : base(options)
		{
		}
		public DbSet<Client> Clients { get; set; }
		public DbSet<Person> Persons { get; set; }
		public DbSet<Organization> Organizations { get; set; }
		public DbSet<Address> Addresses { get; set; }
		public DbSet<Phone> Phones { get; set; }

		async Task IUnitOfWork.SaveChangesAsync(CancellationToken cancellationToken)
		{
			await base.SaveChangesAsync(cancellationToken);
		}

		protected override void OnModelCreating(ModelBuilder modelBuilder)
		{
			base.OnModelCreating(modelBuilder);
			modelBuilder.Entity<Client>(entity =>
			{
				entity.HasKey(e => e.Id);
				entity.Property(e => e.Type).IsRequired();
				entity.Property(e => e.CreatedAt).IsRequired();
				entity.Property(e => e.IsDeleted).IsRequired();
				entity.Property(e => e.DeletedAt).IsRequired(false);
				entity.HasQueryFilter(e => !e.IsDeleted);
			});
			modelBuilder.Entity<Person>(entity =>
			{
				entity.HasKey(e => e.ClientId);
				entity.Property(e => e.FirstName).IsRequired().HasMaxLength(100);
				entity.Property(e => e.MiddleName).HasMaxLength(100);
				entity.Property(e => e.LastName).IsRequired().HasMaxLength(100);
				entity.Property(e => e.DateOfBirth).IsRequired();
				entity.Property(e => e.IdType).IsRequired();
				entity.Property(e => e.IdentificationNumber).IsRequired().HasMaxLength(100);
				entity.Property(e => e.UpdatedAt).IsRequired(false);
				entity.HasOne(e => e.Client)
					.WithOne(c => c.Person)
					.HasForeignKey<Person>(p => p.ClientId);
				entity.HasQueryFilter(p => !p.Client.IsDeleted);
			});
			modelBuilder.Entity<Organization>(entity =>
			{
				entity.HasKey(e => e.ClientId);
				entity.Property(e => e.Name).IsRequired().HasMaxLength(200);
				entity.Property(e => e.Industry).IsRequired();
				entity.Property(e => e.IdentificationNumber).IsRequired().HasMaxLength(100);
				entity.Property(e => e.UpdatedAt).IsRequired(false);
				entity.HasOne(e => e.Client)
					.WithOne(c => c.Organization)
					.HasForeignKey<Organization>(o => o.ClientId);
				entity.HasQueryFilter(o => !o.Client.IsDeleted);
			});

			modelBuilder.Entity<Address>(entity =>
			{
				entity.HasKey(e => e.Id);
				entity.Property(e => e.ClientId).IsRequired();
				entity.Property(e => e.Street).IsRequired().HasMaxLength(200);
				entity.Property(e => e.City).IsRequired().HasMaxLength(100);
				entity.Property(e => e.State).IsRequired().HasMaxLength(100);
				entity.Property(e => e.PostalCode).IsRequired().HasMaxLength(20);
				entity.Property(e => e.Country).IsRequired().HasMaxLength(100);
				entity.Property(e => e.CreatedAt).IsRequired();
				entity.Property(e => e.LastUpdatedAt).IsRequired(false);
				entity.Property(e => e.IsPrimary).IsRequired();
				entity.Property(e => e.IsDeleted).IsRequired();
				entity.Property(e => e.DeletedAt).IsRequired(false);
				entity.HasOne(e => e.Client)
					.WithMany(c => c.Addresses)
					.HasForeignKey(a => a.ClientId);
				entity.HasIndex(e => e.ClientId)
					.IsUnique()
					.HasFilter("[IsPrimary] = 1 AND [IsDeleted] = 0");
				entity.HasQueryFilter(a => !a.Client.IsDeleted && !a.IsDeleted);
			});
			modelBuilder.Entity<Phone>(entity =>
			{
				entity.HasKey(e => e.Id);
				entity.Property(e => e.ClientId);
				entity.Property(e => e.Number).IsRequired().HasMaxLength(20);
				entity.Property(e => e.Type).IsRequired();
				entity.Property(e => e.IsPrimary).IsRequired();
				entity.Property(e => e.CreatedAt).IsRequired();
				entity.Property(e => e.UpdatedAt).IsRequired(false);
				entity.Property(e => e.IsDeleted).IsRequired();
				entity.Property(e => e.DeletedAt).IsRequired(false);
				entity.HasOne(e => e.Client)
					.WithMany(c => c.Phones)
					.HasForeignKey(p => p.ClientId);
				entity.HasIndex(e => e.ClientId)
					.IsUnique()
					.HasFilter("[IsPrimary] = 1 AND [IsDeleted] = 0");
				entity.HasQueryFilter(p => !p.Client.IsDeleted && !p.IsDeleted);
			});
		}
	}
}
