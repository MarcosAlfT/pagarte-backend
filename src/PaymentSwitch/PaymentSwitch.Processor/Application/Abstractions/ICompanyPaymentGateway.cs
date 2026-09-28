using PaymentSwitch.Processor.Domain.Entities;

namespace PaymentSwitch.Processor.Application.Abstractions;

public interface ICompanyPaymentGateway
{
	Task<PaymentOperationResult> PayServiceAsync(
		Payment payment,
		CancellationToken cancellationToken = default);

	Task<PaymentOperationResult> ReconcilePaymentAsync(
		Payment payment,
		CancellationToken cancellationToken = default);

	Task<PaymentOperationResult> RollbackAsync(
		Payment payment,
		CancellationToken cancellationToken = default);
}
