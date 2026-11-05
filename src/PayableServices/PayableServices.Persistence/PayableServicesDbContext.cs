using Microsoft.EntityFrameworkCore;
using PayableServices.Domain.Entities;

namespace PayableServices.Persistence;

public sealed class PayableServicesDbContext(DbContextOptions<PayableServicesDbContext> options)
    : DbContext(options)
{
    public DbSet<AmountInquiry> AmountInquiries => Set<AmountInquiry>();
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
    public DbSet<QuoteReferenceValue> QuoteReferenceValues => Set<QuoteReferenceValue>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.Entity<AmountInquiry>(entity =>
        {
            entity.HasKey(x => x.Id);
            entity.Property(x => x.CreatedAt).HasConversion(v => v, v => DateTime.SpecifyKind(v, DateTimeKind.Utc));
            entity.Property(x => x.LeaseUntil).HasConversion(v => v, v => DateTime.SpecifyKind(v, DateTimeKind.Utc));
            entity.Property(x => x.ProviderResultAt).HasConversion(v => v, v => v.HasValue ? DateTime.SpecifyKind(v.Value, DateTimeKind.Utc) : (DateTime?)null);
            entity.Property(x => x.ExpiresAt).HasConversion(v => v, v => v.HasValue ? DateTime.SpecifyKind(v.Value, DateTimeKind.Utc) : (DateTime?)null);
            entity.Property(x => x.DueDate).HasConversion(v => v, v => v.HasValue ? DateTime.SpecifyKind(v.Value, DateTimeKind.Utc) : (DateTime?)null);
            entity.Property(x => x.ClientId).HasMaxLength(100).UseCollation("Latin1_General_100_BIN2");
            entity.Property(x => x.IdempotencyKey).HasMaxLength(100).UseCollation("Latin1_General_100_BIN2");
            entity.Property(x => x.ProviderKey).HasMaxLength(100);
            entity.Property(x => x.Fingerprint).HasMaxLength(64);
            entity.Property(x => x.Provider).HasMaxLength(100);
            entity.Property(x => x.ExternalServiceCode).HasMaxLength(100);
            entity.Property(x => x.ProviderInquiryId).HasMaxLength(200);
            entity.Property(x => x.BillingPeriod).HasMaxLength(7);
            entity.Property(x => x.Currency).HasMaxLength(3);
            entity.Property(x => x.ErrorCode).HasMaxLength(100);
            entity.HasIndex(x => new { x.ClientId, x.IdempotencyKey }).IsUnique();
            entity.HasIndex(x => x.ProviderKey).IsUnique();
            entity.HasIndex(x => new { x.Outcome, x.LeaseUntil });
        });

        modelBuilder.Entity<Quote>(entity =>
        {
            entity.Property(x => x.SubmittedAt).HasConversion(v => v, v => v.HasValue ? DateTime.SpecifyKind(v.Value, DateTimeKind.Utc) : (DateTime?)null);
            entity.Property(x => x.SubmissionLeaseUntil).HasConversion(v => v, v => v.HasValue ? DateTime.SpecifyKind(v.Value, DateTimeKind.Utc) : (DateTime?)null);
            entity.Property(x => x.SubmissionNextAttemptAt).HasConversion(v => v, v => v.HasValue ? DateTime.SpecifyKind(v.Value, DateTimeKind.Utc) : (DateTime?)null);
            entity.Property(x => x.SubmissionCompletedAt).HasConversion(v => v, v => v.HasValue ? DateTime.SpecifyKind(v.Value, DateTimeKind.Utc) : (DateTime?)null);
            entity.Property(x => x.SubmissionFingerprint).HasMaxLength(64);
            entity.HasIndex(x => x.SubmissionId).IsUnique().HasFilter("[SubmissionId] IS NOT NULL");
            entity.HasIndex(x => new { x.SubmissionNextAttemptAt, x.SubmissionLeaseUntil });
            entity.Property(x => x.CreatedAt).HasConversion(v => v, v => DateTime.SpecifyKind(v, DateTimeKind.Utc));
            entity.Property(x => x.ExpiresAt).HasConversion(v => v, v => DateTime.SpecifyKind(v, DateTimeKind.Utc));
            entity.HasIndex(x => x.InquiryId).IsUnique().HasFilter("[InquiryId] IS NOT NULL");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.ClientId).IsRequired();
            entity.Property(e => e.Currency).IsRequired().HasMaxLength(3);
            entity.Property(e => e.CountryCode).IsRequired().HasMaxLength(2);
            entity.Property(e => e.TotalAmount).HasPrecision(38, 9);
            entity.Property(e => e.ServiceName).HasMaxLength(200);
            entity.Property(e => e.RouteType).IsRequired().HasMaxLength(100);
            entity.Property(e => e.ExternalSourceCode).HasMaxLength(100);
            entity.Property(e => e.ExternalServiceCode).IsRequired().HasMaxLength(100);
            entity.Property(e => e.CompanyPaymentEndpoint).IsRequired().HasMaxLength(500);
            entity.Property(e => e.CompanyRollbackEndpoint).IsRequired().HasMaxLength(500);
            entity.Property(e => e.CompanyStatusEndpoint).HasMaxLength(500);
            entity.Property(e => e.CompanyCredentialReference).IsRequired().HasMaxLength(200);
            entity.Property(e => e.RoundingMode).IsRequired().HasMaxLength(50);
            entity.Property(e => e.ExecutionErrorCode).HasMaxLength(100);
            entity.Property(e => e.ExecutionErrorMessage).HasMaxLength(1000);
            entity.HasIndex(e => e.ClientId);
            entity.HasIndex(e => e.Status);
            entity.HasIndex(e => e.ExpiresAt);
            entity.HasMany(e => e.Items)
                .WithOne()
                .HasForeignKey(e => e.QuoteId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasMany(e => e.ReferenceValues)
                .WithOne()
                .HasForeignKey(e => e.QuoteId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<QuoteItem>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Description).IsRequired().HasMaxLength(500);
            entity.Property(e => e.ComponentType).IsRequired().HasMaxLength(100);
            entity.Property(e => e.Source).HasMaxLength(100);
            entity.Property(e => e.Amount).HasPrecision(38, 9);
            entity.Property(e => e.Currency).IsRequired().HasMaxLength(3);
            entity.HasIndex(e => e.QuoteId);
        });

        modelBuilder.Entity<QuoteReferenceValue>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Key).IsRequired().HasMaxLength(100);
            entity.Property(e => e.Value).IsRequired().HasMaxLength(1000);
            entity.HasIndex(e => new { e.QuoteId, e.Key }).IsUnique();
        });

        modelBuilder.Entity<PayableService>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Name).IsRequired().HasMaxLength(200);
            entity.Property(e => e.Description).HasMaxLength(1000);
            entity.Property(e => e.Status).IsRequired().HasMaxLength(50);
            entity.Property(e => e.SearchKeywords).HasMaxLength(1000);
            entity.Property(e => e.BaseAmount).HasPrecision(38, 9);
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
            entity.Property(x => x.InquiryProvider).HasMaxLength(100);
            entity.HasKey(e => e.Id);
            entity.Property(e => e.RouteType).IsRequired().HasMaxLength(100);
            entity.Property(e => e.Status).IsRequired().HasMaxLength(50);
            entity.Property(e => e.ExternalSourceCode).HasMaxLength(100);
            entity.Property(e => e.ExternalServiceCode).HasMaxLength(100);
            entity.Property(e => e.CompanyPaymentEndpoint).IsRequired().HasMaxLength(500);
            entity.Property(e => e.CompanyRollbackEndpoint).IsRequired().HasMaxLength(500);
            entity.Property(e => e.CompanyStatusEndpoint).HasMaxLength(500);
            entity.Property(e => e.CompanyCredentialReference).IsRequired().HasMaxLength(200);
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
            entity.HasIndex(x => x.Name).IsUnique();
            entity.Property(x => x.SnapshotFingerprint).HasMaxLength(64);
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
            entity.Property(e => e.Amount).HasPrecision(38, 9);
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
