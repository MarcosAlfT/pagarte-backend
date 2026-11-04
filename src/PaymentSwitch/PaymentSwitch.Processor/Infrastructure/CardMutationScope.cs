using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using PaymentSwitch.Processor.Application.Abstractions;

namespace PaymentSwitch.Processor.Infrastructure;

public sealed class CardMutationScope(PaymentDbContext db) : ICardMutationScope
{
	public async Task<ICardMutationTransaction> BeginAsync(string clientId, CancellationToken ct)
	{
		var transaction = await db.Database.BeginTransactionAsync(ct);
		try
		{
			var resource = "card-owner:" + Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(clientId)));
			await db.Database.ExecuteSqlInterpolatedAsync($"DECLARE @result int; EXEC @result = sys.sp_getapplock @Resource={resource}, @LockMode='Exclusive', @LockOwner='Transaction', @LockTimeout=10000; IF @result < 0 THROW 51000, 'Card mutation lock unavailable.', 1;", ct);
			db.ChangeTracker.Clear();
			return new Transaction(db, transaction);
		}
		catch { await transaction.DisposeAsync(); throw; }
	}
	private sealed class Transaction(PaymentDbContext db, IDbContextTransaction transaction) : ICardMutationTransaction
	{
		public async Task CommitAsync(CancellationToken ct)
		{
			await db.SaveChangesAsync(ct);
			await transaction.CommitAsync(ct);
		}
		public ValueTask DisposeAsync() => transaction.DisposeAsync();
	}
}
