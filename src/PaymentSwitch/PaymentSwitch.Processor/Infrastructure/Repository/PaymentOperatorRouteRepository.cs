using Microsoft.EntityFrameworkCore;
using PaymentSwitch.Processor.Application.Abstractions;
using PaymentSwitch.Processor.Domain.Entities;

namespace PaymentSwitch.Processor.Infrastructure.Repository;

public sealed class PaymentOperatorRouteRepository(PaymentDbContext db) : IPaymentOperatorRouteRepository
{
	public async Task<IReadOnlyList<PaymentOperatorRoute>> PinAsync(IReadOnlyList<PaymentOperatorRoute> configured, CancellationToken ct)
	{
		await using var transaction = await db.Database.BeginTransactionAsync(ct);
		await db.Database.ExecuteSqlRawAsync("DECLARE @result int; EXEC @result = sys.sp_getapplock @Resource='payment-operator-routes', @LockMode='Exclusive', @LockOwner='Transaction', @LockTimeout=10000; IF @result < 0 THROW 51000, 'Operator route lock unavailable.', 1;", ct);
		var pinned = await db.PaymentOperatorRoutes.ToListAsync(ct);
		foreach (var route in configured)
		{
			var original = pinned.SingleOrDefault(r => r.IntegrationId == route.IntegrationId);
			if (original is not null)
			{
				if (original.CredentialReference != route.CredentialReference || original.Provider != route.Provider || original.Account != route.Account || original.Environment != route.Environment || original.Version != route.Version)
					throw new InvalidOperationException("An existing payment integration cannot be repointed. Configure a new integration ID.");
				continue;
			}
			// When upgrading, existing setup/card snapshots remain the source of truth.
			if (await db.CardRegistrationSessions.AnyAsync(s => s.IntegrationId == route.IntegrationId &&
				(s.Provider != route.Provider || s.ProviderAccount != route.Account || s.ProviderEnvironment != route.Environment || s.IntegrationVersion != route.Version), ct)
				|| await db.CreditCards.AnyAsync(c => c.OperatorProvider == route.IntegrationId && c.ProviderAccount != "" &&
					(c.AdapterProvider != route.Provider || c.ProviderAccount != route.Account || c.ProviderEnvironment != route.Environment || c.IntegrationVersion != route.Version), ct))
				throw new InvalidOperationException("The configured route conflicts with historical card registration identity.");
			db.PaymentOperatorRoutes.Add(route);
			pinned.Add(route);
		}
		await db.SaveChangesAsync(ct);
		await transaction.CommitAsync(ct);
		return pinned;
	}
}
