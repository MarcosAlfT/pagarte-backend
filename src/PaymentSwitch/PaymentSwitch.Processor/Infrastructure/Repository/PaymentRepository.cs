using Microsoft.EntityFrameworkCore;
using PaymentSwitch.Processor.Domain.Entities;
using PaymentSwitch.Processor.Interfaces;
using Microsoft.Data.SqlClient;

namespace PaymentSwitch.Processor.Infrastructure.Repository;

public sealed class PaymentRepository(PaymentDbContext context) : IPaymentRepository
{
	public async Task<BillReservationStatus> CheckBillReservationAsync(
		string billKey, DateTime checkedAt, CancellationToken cancellationToken)
	{
		if (context.Database.CurrentTransaction is null)
			throw new InvalidOperationException("Bill reservation requires an acceptance transaction.");

		var resource = "bill:" + billKey;
		await context.Database.ExecuteSqlInterpolatedAsync(
			$"DECLARE @result int; EXEC @result = sys.sp_getapplock @Resource={resource}, @LockMode='Exclusive', @LockOwner='Transaction', @LockTimeout=10000; IF @result < 0 THROW 51000, 'Bill reservation lock unavailable.', 1;",
			cancellationToken);
		var billPayments = context.Payments.Where(payment => payment.BillKey == billKey);
		if (await billPayments.AnyAsync(payment => !payment.BillReservationReleased, cancellationToken))
			return BillReservationStatus.Reserved;
		return await billPayments.AnyAsync(
			payment => payment.BillReservationReleased && payment.ProcessedAt > checkedAt, cancellationToken)
			? BillReservationStatus.VerificationPredatesRelease
			: BillReservationStatus.Available;
	}

	public Task<Payment?> GetByIdAsync(
		Guid id,
		CancellationToken cancellationToken = default) =>
		Query().FirstOrDefaultAsync(payment => payment.Id == id, cancellationToken);

	public Task<Payment?> GetBySourceQuoteIdAsync(
		Guid sourceQuoteId,
		CancellationToken cancellationToken = default) =>
		Query().FirstOrDefaultAsync(
			payment => payment.SourceQuoteId == sourceQuoteId,
			cancellationToken);

	public async Task<IReadOnlyCollection<Payment>> GetByClientIdAsync(
		string clientId,
		int page,
		int pageSize,
		CancellationToken cancellationToken = default) =>
		await Query()
			.Where(payment => payment.ClientId == clientId)
			.OrderByDescending(payment => payment.CreatedAt)
			.Skip((Math.Max(page, 1) - 1) * Math.Clamp(pageSize, 1, 100))
			.Take(Math.Clamp(pageSize, 1, 100))
			.ToArrayAsync(cancellationToken);

	public Task<int> GetCountByClientIdAsync(
		string clientId,
		CancellationToken cancellationToken = default) =>
		context.Payments.CountAsync(
			payment => payment.ClientId == clientId,
			cancellationToken);

	public async Task<IReadOnlyCollection<Payment>> GetDueAsync(
		DateTime utcNow,
		int batchSize,
		CancellationToken cancellationToken = default) =>
		await Query()
			.Where(payment => payment.NextAttemptAt != null
				&& payment.NextAttemptAt <= utcNow
				&& payment.Status != PaymentSwitch.Messaging.PaymentTransactionStatus.Completed
				&& payment.Status != PaymentSwitch.Messaging.PaymentTransactionStatus.Compensated
				&& payment.Status != PaymentSwitch.Messaging.PaymentTransactionStatus.Failed
				&& payment.Status != PaymentSwitch.Messaging.PaymentTransactionStatus.CompanyRolledBack
				&& payment.Status != PaymentSwitch.Messaging.PaymentTransactionStatus.ManualIntervention)
			.OrderBy(payment => payment.NextAttemptAt)
			.Take(Math.Clamp(batchSize, 1, 100))
			.ToArrayAsync(cancellationToken);

	public async Task<PaymentInsertResult> InsertIfSourceQuoteAbsentAsync(
		Payment payment,
		CancellationToken cancellationToken = default)
	{
		context.Payments.Add(payment);
		try
		{
			await context.SaveChangesAsync(cancellationToken);
			return new PaymentInsertResult(true, payment);
		}
		catch (DbUpdateException exception) when (IsUniqueConstraintViolation(exception))
		{
			context.ChangeTracker.Clear();
			var existing = await GetBySourceQuoteIdAsync(
				payment.SourceQuoteId,
				cancellationToken);
			if (existing is null)
			{
				throw;
			}

			return new PaymentInsertResult(false, existing);
		}
	}

	public Task UpdateAsync(
		Payment payment,
		CancellationToken cancellationToken = default)
	{
		context.Payments.Update(payment);
		return Task.CompletedTask;
	}

	private IQueryable<Payment> Query() =>
		context.Payments
			.Include(payment => payment.Details)
			.Include(payment => payment.ReferenceValues)
			.Include(payment => payment.CreditCard);

	private static bool IsUniqueConstraintViolation(DbUpdateException exception) =>
		exception.InnerException is SqlException { Number: 2601 or 2627 };
}
