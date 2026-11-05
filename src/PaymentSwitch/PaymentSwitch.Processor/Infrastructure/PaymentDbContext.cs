using Microsoft.EntityFrameworkCore;
using PaymentSwitch.Processor.Domain.Entities;
using PaymentSwitch.Processor.Interfaces;


namespace PaymentSwitch.Processor.Infrastructure
{
	public class PaymentDbContext : DbContext, IUnitOfWork
	{
		public PaymentDbContext(DbContextOptions<PaymentDbContext> options) : base(options) { }

		public DbSet<CardReviewOperation> CardReviewOperations { get; set; }
		public DbSet<PaymentOperatorRoute> PaymentOperatorRoutes { get; set; }
		public DbSet<CardSetupNotification> CardSetupNotifications { get; set; }
		public DbSet<CardRegistrationSession> CardRegistrationSessions { get; set; }
		public DbSet<CreditCard> CreditCards { get; set; }
		public DbSet<Payment> Payments { get; set; }
		public DbSet<QuoteSubmissionOutcome> QuoteSubmissionOutcomes { get; set; }
		public DbSet<PaymentDetail> PaymentDetails { get; set; }
		public DbSet<PaymentReferenceValue> PaymentReferenceValues { get; set; }
		public DbSet<Service> Services { get; set; }
		public DbSet<Company> Companies { get; set; }
		public DbSet<PaymentOperator> PaymentOperators { get; set; }
		public DbSet<OutboxMessage> OutboxMessages { get; set; }

		protected override void OnModelCreating(ModelBuilder modelBuilder)
		{
			base.OnModelCreating(modelBuilder);

            modelBuilder.Entity<QuoteSubmissionOutcome>(entity =>
            {
                entity.HasKey(x => x.SourceQuoteId);
                entity.HasIndex(x => x.SubmissionId).IsUnique();
                entity.Property(x => x.ClientId).HasMaxLength(100).IsRequired();
                entity.Property(x => x.PackageFingerprint).HasMaxLength(64).IsRequired();
                entity.Property(x => x.ErrorCode).HasMaxLength(100);
                entity.Property(x => x.ErrorMessage).HasMaxLength(1000);
                entity.Property(x => x.RecordedAt).HasConversion(v => v, v => DateTime.SpecifyKind(v, DateTimeKind.Utc));
            });

			modelBuilder.Entity<CardReviewOperation>(entity =>
			{
				entity.HasKey(r => r.Id);
				entity.Property(r => r.Actor).HasMaxLength(300).UseCollation("Latin1_General_100_BIN2").IsRequired();
				entity.Property(r => r.RequestKey).HasMaxLength(100).UseCollation("Latin1_General_100_BIN2").IsRequired();
				entity.Property(r => r.Fingerprint).HasMaxLength(64).IsRequired();
				entity.Property(r => r.Kind).HasMaxLength(30).IsRequired();
				entity.Property(r => r.Action).HasMaxLength(30).IsRequired();
				entity.Property(r => r.ReasonCode).HasMaxLength(50).IsRequired();
				entity.Property(r => r.BeforeState).HasMaxLength(40).IsRequired();
				entity.Property(r => r.AfterState).HasMaxLength(40).IsRequired();
				entity.Property(r => r.Outcome).HasMaxLength(40).IsRequired();
				entity.HasIndex(r => new { r.Actor, r.RequestKey }).IsUnique();
				entity.HasIndex(r => new { r.TargetId, r.CreatedAt });
			});

			modelBuilder.Entity<PaymentOperatorRoute>(entity =>
			{
				entity.HasKey(r => r.IntegrationId);
				entity.Property(r => r.IntegrationId).HasMaxLength(100);
				entity.Property(r => r.CredentialReference).HasMaxLength(100).IsRequired();
				entity.Property(r => r.Provider).HasMaxLength(100).IsRequired();
				entity.Property(r => r.Account).HasMaxLength(100).IsRequired();
				entity.Property(r => r.Environment).HasMaxLength(30).IsRequired();
				entity.Property(r => r.Version).HasMaxLength(50).IsRequired();
			});

			modelBuilder.Entity<CreditCard>(entity =>
			{
				entity.HasKey(e => e.Id);
				entity.Property(e => e.ClientId).IsRequired();
				entity.Property(e => e.OperatorProvider).IsRequired().HasMaxLength(100);
				entity.Property(e => e.OperatorCardToken).IsRequired().HasMaxLength(500);
				entity.Property(e => e.ClientId).HasMaxLength(100);
				entity.Property(e => e.AdapterProvider).HasMaxLength(100);
				entity.Property(e => e.DetachmentState).HasMaxLength(30).IsRequired();
				entity.HasIndex(e => e.DetachmentNextAttemptAt);
				entity.Property(e => e.ProviderAccount).HasMaxLength(100);
				entity.Property(e => e.ProviderEnvironment).HasMaxLength(30);
				entity.Property(e => e.IntegrationVersion).HasMaxLength(50);
				entity.Property(e => e.ProviderReferenceDigest).HasMaxLength(64);
				entity.HasIndex(e => e.ClientId).IsUnique().HasDatabaseName("UX_CreditCards_ActiveDefault")
					.HasFilter("[IsDefault] = 1 AND [IsDeleted] = 0");
				entity.HasIndex(e => new { e.ClientId, e.ProviderReferenceDigest }).IsUnique()
					.HasFilter("[ProviderReferenceDigest] IS NOT NULL AND [IsDeleted] = 0");
				entity.Property(e => e.CardHolderName).IsRequired().HasMaxLength(200);
				entity.Property(e => e.Last4Digits).IsRequired().HasMaxLength(4);
			});

			modelBuilder.Entity<CardSetupNotification>(entity =>
			{
				entity.HasKey(e => e.Id);
				entity.Property(e => e.IntegrationId).HasMaxLength(100).IsRequired();
				entity.Property(e => e.EventId).HasMaxLength(100).UseCollation("Latin1_General_100_BIN2").IsRequired();
				entity.HasIndex(e => new { e.IntegrationId, e.EventId }).IsUnique();
			});

			modelBuilder.Entity<CardRegistrationSession>(entity =>
			{
				entity.HasKey(s => s.Id);
				entity.Ignore(s => s.IsTerminal);
				entity.Property(s => s.ClientId).HasMaxLength(100).IsRequired();
				entity.Property(s => s.CreationKey).HasMaxLength(100).UseCollation("Latin1_General_100_BIN2").IsRequired();
				entity.Property(s => s.CreationFingerprint).HasMaxLength(64).IsRequired();
				entity.Property(s => s.CompletionKey).HasMaxLength(100).UseCollation("Latin1_General_100_BIN2");
				entity.Property(s => s.TokenDigest).HasMaxLength(64);
				entity.Property(s => s.ProtectedToken).HasMaxLength(6000);
				entity.Property(s => s.IntegrationId).HasMaxLength(100).IsRequired();
				entity.Property(s => s.Provider).HasMaxLength(100).IsRequired();
				entity.Property(s => s.ProviderAccount).HasMaxLength(100).IsRequired();
				entity.Property(s => s.ProviderEnvironment).HasMaxLength(30).IsRequired();
				entity.Property(s => s.IntegrationVersion).HasMaxLength(50).IsRequired();
				entity.Property(s => s.CardHolderName).HasMaxLength(200).IsRequired();
				entity.Property(s => s.ConsentVersion).HasMaxLength(100);
				entity.Property(s => s.CleanupState).HasMaxLength(30).IsRequired();
				entity.Property(s => s.NextAction).HasMaxLength(100);
				entity.Property(s => s.ErrorCode).HasMaxLength(100);
				entity.Property(s => s.Version).IsRowVersion();
				entity.HasIndex(s => new { s.ClientId, s.CreationKey }).IsUnique();
				entity.HasIndex(s => new { s.ClientId, s.CompletionKey }).IsUnique().HasFilter("[CompletionKey] IS NOT NULL");
				entity.HasIndex(s => s.TokenDigest).IsUnique().HasFilter("[TokenDigest] IS NOT NULL");
				entity.HasIndex(s => s.NextAttemptAt);
			});

			modelBuilder.Entity<Payment>(entity =>
			{
				entity.HasKey(e => e.Id);
				entity.Property(e => e.ClientId).IsRequired();
				entity.Property(e => e.PackageFingerprint).IsRequired().HasMaxLength(64);
				entity.Property(e => e.BillKey).HasMaxLength(64);
				entity.HasIndex(e => e.BillKey).IsUnique()
					.HasFilter("[BillKey] IS NOT NULL AND [BillReservationReleased] = 0");
				entity.HasIndex(e => new { e.BillKey, e.ProcessedAt })
					.HasFilter("[BillKey] IS NOT NULL AND [BillReservationReleased] = 1");
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
