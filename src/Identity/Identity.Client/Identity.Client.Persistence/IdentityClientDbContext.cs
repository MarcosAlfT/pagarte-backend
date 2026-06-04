using Identity.Client.Application.Abstractions;
using Identity.Client.Domain.Passkeys;
using Identity.Client.Domain.Tokens;
using Identity.Client.Domain.Users;
using Microsoft.EntityFrameworkCore;

namespace Identity.Client.Persistence;

public sealed class IdentityClientDbContext(DbContextOptions<IdentityClientDbContext> options) : DbContext(options), IUnitOfWork
{
    public DbSet<User> Users => Set<User>();
    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();
    public DbSet<EmailConfirmationToken> EmailConfirmationTokens => Set<EmailConfirmationToken>();
    public DbSet<PasswordResetToken> PasswordResetTokens => Set<PasswordResetToken>();
    public DbSet<UserPasskey> UserPasskeys => Set<UserPasskey>();

    async Task IUnitOfWork.SaveChangesAsync(CancellationToken cancellationToken)
    {
        await base.SaveChangesAsync(cancellationToken);
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.UseOpenIddict();

        ConfigureUsers(modelBuilder);
        ConfigureRefreshTokens(modelBuilder);
        ConfigureEmailConfirmationTokens(modelBuilder);
        ConfigurePasswordResetTokens(modelBuilder);
        ConfigurePasskeys(modelBuilder);
    }

    private static void ConfigureUsers(ModelBuilder modelBuilder)
    {
        var entity = modelBuilder.Entity<User>();
        entity.ToTable("Users");
        entity.HasKey(user => user.Id);
        entity.Property(user => user.Email).HasMaxLength(320).IsRequired();
        entity.Property(user => user.NormalizedEmail).HasMaxLength(320).IsRequired();
        entity.Property(user => user.PasswordHash).HasMaxLength(512).IsRequired();
        entity.Property(user => user.Status).HasConversion<string>().HasMaxLength(64).IsRequired();
        entity.Property(user => user.SecurityStamp).HasMaxLength(64).IsRequired();
        entity.Property(user => user.ConcurrencyStamp).HasMaxLength(64).IsRequired();
        entity.Property(user => user.CreatedBy).HasMaxLength(256);
        entity.Property(user => user.UpdatedBy).HasMaxLength(256);
        entity.Property(user => user.DeletedBy).HasMaxLength(256);
        entity.HasIndex(user => user.NormalizedEmail).IsUnique();
        entity.HasQueryFilter(user => user.DeletedAt == null);
        entity.HasMany(user => user.Passkeys)
            .WithOne()
            .HasForeignKey(passkey => passkey.UserId)
            .OnDelete(DeleteBehavior.Restrict);
        entity.Navigation(user => user.Passkeys).UsePropertyAccessMode(PropertyAccessMode.Field);
    }

    private static void ConfigureRefreshTokens(ModelBuilder modelBuilder)
    {
        var entity = modelBuilder.Entity<RefreshToken>();
        entity.ToTable("RefreshTokens");
        entity.HasKey(token => token.Id);
        entity.Property(token => token.TokenHash).HasMaxLength(256).IsRequired();
        entity.Property(token => token.DeviceId).HasMaxLength(128);
        entity.Property(token => token.DeviceName).HasMaxLength(256);
        entity.Property(token => token.UserAgent).HasMaxLength(512);
        entity.Property(token => token.CreatedByIp).HasMaxLength(64);
        entity.Property(token => token.RevokedByIp).HasMaxLength(64);
        entity.Property(token => token.CreatedBy).HasMaxLength(256);
        entity.Property(token => token.UpdatedBy).HasMaxLength(256);
        entity.Property(token => token.DeletedBy).HasMaxLength(256);
        entity.Property(token => token.RevokedBy).HasMaxLength(256);
        entity.HasIndex(token => token.TokenHash).IsUnique();
        entity.HasIndex(token => token.UserId);
        entity.HasQueryFilter(token => token.DeletedAt == null);
    }

    private static void ConfigureEmailConfirmationTokens(ModelBuilder modelBuilder)
    {
        var entity = modelBuilder.Entity<EmailConfirmationToken>();
        entity.ToTable("EmailConfirmationTokens");
        entity.HasKey(token => token.Id);
        entity.Property(token => token.TokenHash).HasMaxLength(256).IsRequired();
        entity.Property(token => token.CreatedBy).HasMaxLength(256);
        entity.Property(token => token.UpdatedBy).HasMaxLength(256);
        entity.Property(token => token.DeletedBy).HasMaxLength(256);
        entity.Property(token => token.RevokedBy).HasMaxLength(256);
        entity.HasIndex(token => token.TokenHash).IsUnique();
        entity.HasIndex(token => token.UserId);
        entity.HasQueryFilter(token => token.DeletedAt == null);
    }

    private static void ConfigurePasswordResetTokens(ModelBuilder modelBuilder)
    {
        var entity = modelBuilder.Entity<PasswordResetToken>();
        entity.ToTable("PasswordResetTokens");
        entity.HasKey(token => token.Id);
        entity.Property(token => token.TokenHash).HasMaxLength(256).IsRequired();
        entity.Property(token => token.CreatedBy).HasMaxLength(256);
        entity.Property(token => token.UpdatedBy).HasMaxLength(256);
        entity.Property(token => token.DeletedBy).HasMaxLength(256);
        entity.Property(token => token.RevokedBy).HasMaxLength(256);
        entity.HasIndex(token => token.TokenHash).IsUnique();
        entity.HasIndex(token => token.UserId);
        entity.HasQueryFilter(token => token.DeletedAt == null);
    }

    private static void ConfigurePasskeys(ModelBuilder modelBuilder)
    {
        var entity = modelBuilder.Entity<UserPasskey>();
        entity.ToTable("UserPasskeys");
        entity.HasKey(passkey => passkey.Id);
        entity.Property(passkey => passkey.CredentialId).HasMaxLength(512).IsRequired();
        entity.Property(passkey => passkey.PublicKey).HasMaxLength(4096).IsRequired();
        entity.Property(passkey => passkey.DeviceName).HasMaxLength(256);
        entity.Property(passkey => passkey.CreatedBy).HasMaxLength(256);
        entity.Property(passkey => passkey.UpdatedBy).HasMaxLength(256);
        entity.Property(passkey => passkey.DeletedBy).HasMaxLength(256);
        entity.HasIndex(passkey => passkey.CredentialId).IsUnique();
        entity.HasIndex(passkey => passkey.UserId);
        entity.HasQueryFilter(passkey => passkey.DeletedAt == null);
    }
}
