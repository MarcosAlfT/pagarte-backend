using Microsoft.EntityFrameworkCore;
using PaymentSwitch.Processor.Domain.Entities;
using PaymentSwitch.Processor.Interfaces;


namespace PaymentSwitch.Processor.Infrastructure
{
	public class PaymentDbContext : DbContext, IUnitOfWork
	{
		public PaymentDbContext(DbContextOptions<PaymentDbContext> options) : base(options) { }

		public DbSet<CreditCard> CreditCards { get; set; }
		public DbSet<Payment> Payments { get; set; }
		public DbSet<PaymentDetail> PaymentDetails { get; set; }
		public DbSet<PaymentReferenceValue> PaymentReferenceValues { get; set; }
		public DbSet<Service> Services { get; set; }
		public DbSet<Company> Companies { get; set; }
		public DbSet<PaymentOperator> PaymentOperators { get; set; }
		public DbSet<OutboxMessage> OutboxMessages { get; set; }

		protected override void OnModelCreating(ModelBuilder modelBuilder)
		{
			base.OnModelCreating(modelBuilder);

			modelBuilder.Entity<CreditCard>(entity =>
			{
				entity.HasKey(e => e.Id);
				entity.Property(e => e.ClientId).IsRequired();
				entity.Property(e => e.OperatorProvider).IsRequired().HasMaxLength(100);
				entity.Property(e => e.OperatorCardToken).IsRequired().HasMaxLength(500);
				entity.Property(e => e.CardNumber).IsRequired().HasMaxLength(25);
				entity.Property(e => e.CardHolderName).IsRequired().HasMaxLength(200);
				entity.Property(e => e.Last4Digits).IsRequired().HasMaxLength(4);
				entity.HasIndex(e => e.ClientId);
			});

			modelBuilder.Entity<Payment>(entity =>
			{
				entity.HasKey(e => e.Id);
				entity.Property(e => e.ClientId).IsRequired();
				entity.Property(e => e.PackageFingerprint).IsRequired().HasMaxLength(64);
				entity.Property(e => e.ServiceName).IsRequired().HasMaxLength(200);
				entity.Property(e => e.CountryCode).IsRequired().HasMaxLength(2);
				entity.Property(e => e.RouteType).IsRequired().HasMaxLength(100);
				entity.Property(e => e.ExternalSourceCode).HasMaxLength(100);
				entity.Property(e => e.ExternalServiceCode).IsRequired().HasMaxLength(100);
				entity.Property(e => e.CompanyPaymentEndpoint).IsRequired().HasMaxLength(500);
				entity.Property(e => e.CompanyRollbackEndpoint).IsRequired().HasMaxLength(500);
				entity.Property(e => e.CompanyStatusEndpoint).HasMaxLength(500);
				entity.Property(e => e.CompanyCredentialReference).IsRequired().HasMaxLength(200);
				entity.Property(e => e.OperatorProvider).IsRequired().HasMaxLength(100);
				entity.Property(e => e.Reference).IsRequired().HasMaxLength(50);
				entity.Property(e => e.Currency).IsRequired().HasMaxLength(3);
				entity.Property(e => e.RoundingMode).IsRequired().HasMaxLength(50);
				entity.Property(e => e.ErrorCode).HasMaxLength(100);
				entity.Property(e => e.ErrorMessage).HasMaxLength(1000);
				entity.Property(e => e.CompanyPaymentIdempotencyKey).IsRequired().HasMaxLength(42);
				entity.Property(e => e.CardChargeIdempotencyKey).IsRequired().HasMaxLength(42);
				entity.Property(e => e.CompanyRollbackIdempotencyKey).IsRequired().HasMaxLength(42);
				entity.Property(e => e.Version).IsRowVersion();
				entity.HasIndex(e => e.Reference).IsUnique();
				entity.HasIndex(e => e.SourceQuoteId).IsUnique();
				entity.HasIndex(e => e.ClientId);
				entity.HasIndex(e => e.Status);
				entity.HasIndex(e => new { e.NextAttemptAt, e.Status });
				entity.HasOne(e => e.CreditCard)
					.WithMany(c => c.Payments)
					.HasForeignKey(e => e.CreditCardId);
			});

			modelBuilder.Entity<PaymentDetail>(entity =>
			{
				entity.HasKey(e => e.Id);
				entity.Property(e => e.ComponentType).IsRequired().HasMaxLength(100);
				entity.Property(e => e.Description).IsRequired().HasMaxLength(500);
				entity.Property(e => e.Source).HasMaxLength(100);
				entity.Property(e => e.Currency).IsRequired().HasMaxLength(3);
				entity.HasOne(e => e.Payment)
					.WithMany(p => p.Details)
					.HasForeignKey(e => e.PaymentId)
					.OnDelete(DeleteBehavior.Cascade);
			});

			modelBuilder.Entity<PaymentReferenceValue>(entity =>
			{
				entity.HasKey(e => e.Id);
				entity.Property(e => e.Key).IsRequired().HasMaxLength(100);
				entity.Property(e => e.Value).IsRequired().HasMaxLength(1000);
				entity.HasIndex(e => new { e.PaymentId, e.Key }).IsUnique();
				entity.HasOne(e => e.Payment)
					.WithMany(p => p.ReferenceValues)
					.HasForeignKey(e => e.PaymentId)
					.OnDelete(DeleteBehavior.Cascade);
			});

			modelBuilder.Entity<Service>(entity =>
			{
				entity.HasKey(e => e.Id);
				entity.Property(e => e.Name).IsRequired().HasMaxLength(200);
				entity.Property(e => e.BaseAmount).HasPrecision(38, 9);
				entity.Property(e => e.Currency).IsRequired().HasMaxLength(3);
				entity.HasQueryFilter(e => e.IsActive);
				entity.HasOne(e => e.Company)
					.WithMany(c => c.Services)
					.HasForeignKey(e => e.CompanyId);
			});

			modelBuilder.Entity<Company>(entity =>
			{
				entity.HasKey(e => e.Id);
				entity.Property(e => e.Name).IsRequired().HasMaxLength(200);
				entity.Property(e => e.ApiEndpoint).IsRequired().HasMaxLength(500);
				entity.HasQueryFilter(e => e.IsActive);
			});

			modelBuilder.Entity<PaymentOperator>(entity =>
			{
				entity.HasKey(e => e.Id);
				entity.Property(e => e.Code).IsRequired().HasMaxLength(100);
				entity.Property(e => e.Name).IsRequired().HasMaxLength(200);
				entity.HasIndex(e => e.Code).IsUnique();
				entity.HasIndex(e => new { e.Scope, e.IsActive, e.Priority });
			});

			modelBuilder.Entity<OutboxMessage>(entity =>
			{
				entity.HasKey(e => e.Id);
				entity.Property(e => e.MessageType).IsRequired().HasMaxLength(500);
				entity.Property(e => e.Payload).IsRequired();
				entity.Property(e => e.Exchange).IsRequired().HasMaxLength(200);
				entity.Property(e => e.RoutingKey).IsRequired().HasMaxLength(200);
				entity.Property(e => e.ErrorMessage).HasMaxLength(1000);
				entity.HasIndex(e => new { e.PublishedAt, e.NextAttemptAt });
			});
		}
	}
}
