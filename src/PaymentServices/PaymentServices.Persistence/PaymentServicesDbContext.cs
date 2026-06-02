using Microsoft.EntityFrameworkCore;
using PaymentServices.Domain.Entities;

namespace PaymentServices.Persistence;

public sealed class PaymentServicesDbContext(DbContextOptions<PaymentServicesDbContext> options)
	: DbContext(options)
{
	public DbSet<Country> Countries => Set<Country>();
	public DbSet<Category> Categories => Set<Category>();
	public DbSet<Subcategory> Subcategories => Set<Subcategory>();
	public DbSet<Provider> Providers => Set<Provider>();
	public DbSet<PaymentNetwork> PaymentNetworks => Set<PaymentNetwork>();
	public DbSet<PayableService> PayableServices => Set<PayableService>();
	public DbSet<ReferenceFieldDefinition> ReferenceFieldDefinitions => Set<ReferenceFieldDefinition>();
	public DbSet<PaymentRoute> PaymentRoutes => Set<PaymentRoute>();
	public DbSet<ExternalCatalogueSource> ExternalCatalogueSources => Set<ExternalCatalogueSource>();
	public DbSet<ExternalCatalogueItem> ExternalCatalogueItems => Set<ExternalCatalogueItem>();
	public DbSet<ExternalCatalogueMapping> ExternalCatalogueMappings => Set<ExternalCatalogueMapping>();
	public DbSet<AmountComposition> AmountCompositions => Set<AmountComposition>();
	public DbSet<AmountComponent> AmountComponents => Set<AmountComponent>();
	public DbSet<Quote> Quotes => Set<Quote>();
	public DbSet<QuoteItem> QuoteItems => Set<QuoteItem>();

	protected override void OnModelCreating(ModelBuilder modelBuilder)
	{
		base.OnModelCreating(modelBuilder);

		modelBuilder.Entity<Quote>(entity =>
		{
			entity.HasKey(e => e.Id);
			entity.Property(e => e.ClientId).IsRequired();
			entity.Property(e => e.Currency).IsRequired().HasMaxLength(3);
			entity.Property(e => e.TotalAmount).HasPrecision(18, 2);
			entity.Property(e => e.ServiceName).HasMaxLength(200);
			entity.HasIndex(e => e.ClientId);
			entity.HasIndex(e => e.Status);
			entity.HasIndex(e => e.ExpiresAt);
			entity.HasMany(e => e.Items)
				.WithOne()
				.HasForeignKey(e => e.QuoteId)
				.OnDelete(DeleteBehavior.Cascade);
		});

		modelBuilder.Entity<QuoteItem>(entity =>
		{
			entity.HasKey(e => e.Id);
			entity.Property(e => e.Description).IsRequired().HasMaxLength(500);
			entity.Property(e => e.Amount).HasPrecision(18, 2);
			entity.Property(e => e.Currency).IsRequired().HasMaxLength(3);
			entity.HasIndex(e => e.QuoteId);
		});

		modelBuilder.Entity<PayableService>(entity =>
		{
			entity.HasKey(e => e.Id);
			entity.Property(e => e.Name).IsRequired().HasMaxLength(200);
			entity.Property(e => e.Description).HasMaxLength(1000);
			entity.Property(e => e.Status).IsRequired().HasMaxLength(50);
			entity.Property(e => e.SearchKeywords).HasMaxLength(1000);
			entity.Property(e => e.Currency).IsRequired().HasMaxLength(3);
			entity.HasIndex(e => new { e.CountryId, e.CategoryId, e.SubcategoryId, e.AllowsQuote });
		});

		modelBuilder.Entity<Category>(entity =>
		{
			entity.HasKey(e => e.Id);
			entity.Property(e => e.Name).IsRequired().HasMaxLength(200);
			entity.Property(e => e.Description).HasMaxLength(1000);
			entity.Property(e => e.SearchKeywords).HasMaxLength(1000);
		});

		modelBuilder.Entity<Subcategory>(entity =>
		{
			entity.HasKey(e => e.Id);
			entity.Property(e => e.Name).IsRequired().HasMaxLength(200);
			entity.Property(e => e.Description).HasMaxLength(1000);
			entity.Property(e => e.SearchKeywords).HasMaxLength(1000);
		});

		modelBuilder.Entity<Provider>(entity =>
		{
			entity.HasKey(e => e.Id);
			entity.Property(e => e.Name).IsRequired().HasMaxLength(200);
		});

		modelBuilder.Entity<PaymentNetwork>(entity =>
		{
			entity.HasKey(e => e.Id);
			entity.Property(e => e.Name).IsRequired().HasMaxLength(200);
		});

		modelBuilder.Entity<PaymentRoute>(entity =>
		{
			entity.HasKey(e => e.Id);
			entity.Property(e => e.RouteType).IsRequired().HasMaxLength(100);
			entity.Property(e => e.Status).IsRequired().HasMaxLength(50);
			entity.Property(e => e.ExternalSourceCode).HasMaxLength(100);
			entity.Property(e => e.ExternalServiceCode).HasMaxLength(100);
			entity.HasIndex(e => new { e.PayableServiceId, e.IsActive });
		});

		modelBuilder.Entity<ReferenceFieldDefinition>(entity =>
		{
			entity.HasKey(e => e.Id);
			entity.Property(e => e.FieldKey).IsRequired().HasMaxLength(100);
			entity.Property(e => e.DisplayLabel).IsRequired().HasMaxLength(200);
			entity.Property(e => e.DataType).IsRequired().HasMaxLength(50);
			entity.Property(e => e.ValidationRule).HasMaxLength(500);
		});

		modelBuilder.Entity<ExternalCatalogueSource>(entity =>
		{
			entity.HasKey(e => e.Id);
			entity.Property(e => e.Name).IsRequired().HasMaxLength(200);
			entity.Property(e => e.SourceType).IsRequired().HasMaxLength(100);
		});

		modelBuilder.Entity<ExternalCatalogueItem>(entity =>
		{
			entity.HasKey(e => e.Id);
			entity.Property(e => e.ExternalCategory).IsRequired().HasMaxLength(200);
			entity.Property(e => e.ExternalSubcategory).HasMaxLength(200);
			entity.Property(e => e.ExternalName).IsRequired().HasMaxLength(200);
			entity.Property(e => e.ExternalCode).IsRequired().HasMaxLength(100);
			entity.Property(e => e.ExternalStatus).IsRequired().HasMaxLength(50);
			entity.Property(e => e.RawReference).HasMaxLength(2000);
			entity.HasIndex(e => new { e.ExternalCatalogueSourceId, e.ExternalCode }).IsUnique();
		});

		modelBuilder.Entity<ExternalCatalogueMapping>(entity =>
		{
			entity.HasKey(e => e.Id);
			entity.Property(e => e.ReviewReason).HasMaxLength(1000);
			entity.HasIndex(e => e.ExternalCatalogueItemId).IsUnique();
		});

		modelBuilder.Entity<AmountComposition>(entity =>
		{
			entity.HasKey(e => e.Id);
			entity.HasMany(e => e.Components)
				.WithOne()
				.HasForeignKey(e => e.AmountCompositionId)
				.OnDelete(DeleteBehavior.Cascade);
		});

		modelBuilder.Entity<AmountComponent>(entity =>
		{
			entity.HasKey(e => e.Id);
			entity.Property(e => e.ComponentType).IsRequired().HasMaxLength(100);
			entity.Property(e => e.Description).HasMaxLength(500);
			entity.Property(e => e.Amount).HasPrecision(18, 2);
			entity.Property(e => e.Currency).IsRequired().HasMaxLength(3);
			entity.Property(e => e.Source).HasMaxLength(100);
			entity.Property(e => e.AppliesTo).HasMaxLength(100);
		});

		modelBuilder.Entity<Country>(entity =>
		{
			entity.HasKey(e => e.Id);
			entity.Property(e => e.Code).IsRequired().HasMaxLength(10);
			entity.Property(e => e.Name).IsRequired().HasMaxLength(200);
			entity.Property(e => e.Currency).IsRequired().HasMaxLength(3);
		});
	}
}
