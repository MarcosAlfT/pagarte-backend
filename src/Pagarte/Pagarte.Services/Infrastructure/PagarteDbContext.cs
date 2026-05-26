using Microsoft.EntityFrameworkCore;
using Pagarte.Services.Domain.Entities;


namespace Pagarte.Services.Infrastructure
{
	public class PagarteDbContext : DbContext
	{
		public PagarteDbContext(DbContextOptions<PagarteDbContext> options) : base(options) { }

		public DbSet<CreditCard> CreditCards { get; set; }
		public DbSet<Payment> Payments { get; set; }
		public DbSet<PaymentDetail> PaymentDetails { get; set; }
		public DbSet<PaymentQuote> PaymentQuotes { get; set; }
		public DbSet<PaymentQuoteDetail> PaymentQuoteDetails { get; set; }
		public DbSet<Service> Services { get; set; }
		public DbSet<Company> Companies { get; set; }
		public DbSet<FeeConfiguration> FeeConfigurations { get; set; }
		public DbSet<PaymentOperator> PaymentOperators { get; set; }

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
				entity.HasQueryFilter(e => !e.IsDeleted);
			});

			modelBuilder.Entity<Payment>(entity =>
			{
				entity.HasKey(e => e.Id);
				entity.Property(e => e.ClientId).IsRequired();
				entity.Property(e => e.OperatorProvider).IsRequired().HasMaxLength(100);
				entity.Property(e => e.Reference).IsRequired().HasMaxLength(50);
				entity.Property(e => e.Currency).IsRequired().HasMaxLength(3);
				entity.HasIndex(e => e.Reference).IsUnique();
				entity.HasIndex(e => e.ClientId);
				entity.HasIndex(e => e.Status);
				entity.HasOne(e => e.CreditCard)
					.WithMany(c => c.Payments)
					.HasForeignKey(e => e.CreditCardId);
				entity.HasOne(e => e.Quote)
					.WithMany(q => q.Payments)
					.HasForeignKey(e => e.QuoteId)
					.OnDelete(DeleteBehavior.Restrict);
				entity.HasOne(e => e.Service)
					.WithMany(s => s.Payments)
					.HasForeignKey(e => e.ServiceId);
			});

			modelBuilder.Entity<PaymentDetail>(entity =>
			{
				entity.HasKey(e => e.Id);
				entity.Property(e => e.Amount).HasPrecision(18, 2);
				entity.Property(e => e.Currency).IsRequired().HasMaxLength(3);
				entity.HasOne(e => e.Payment)
					.WithMany(p => p.Details)
					.HasForeignKey(e => e.PaymentId);
			});

			modelBuilder.Entity<PaymentQuote>(entity =>
			{
				entity.HasKey(e => e.Id);
				entity.Property(e => e.ClientId).IsRequired();
				entity.Property(e => e.Currency).IsRequired().HasMaxLength(3);
				entity.Property(e => e.TotalAmount).HasPrecision(18, 2);
				entity.HasIndex(e => e.ClientId);
				entity.HasIndex(e => e.Status);
				entity.HasIndex(e => e.ExpiresAt);
				entity.HasOne(e => e.Service)
					.WithMany()
					.HasForeignKey(e => e.ServiceId)
					.OnDelete(DeleteBehavior.Restrict);
			});

			modelBuilder.Entity<PaymentQuoteDetail>(entity =>
			{
				entity.HasKey(e => e.Id);
				entity.Property(e => e.Description).IsRequired().HasMaxLength(500);
				entity.Property(e => e.Amount).HasPrecision(18, 2);
				entity.Property(e => e.Currency).IsRequired().HasMaxLength(3);
				entity.HasOne(e => e.PaymentQuote)
					.WithMany(q => q.Details)
					.HasForeignKey(e => e.PaymentQuoteId);
			});

			modelBuilder.Entity<Service>(entity =>
			{
				entity.HasKey(e => e.Id);
				entity.Property(e => e.Name).IsRequired().HasMaxLength(200);
				entity.Property(e => e.BaseAmount).HasPrecision(18, 2);
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

			modelBuilder.Entity<FeeConfiguration>(entity =>
			{
				entity.HasKey(e => e.Id);
				entity.Property(e => e.Value).HasPrecision(18, 4);
				entity.Property(e => e.Currency).IsRequired().HasMaxLength(3);
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
		}
	}
}
