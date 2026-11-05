using PaymentSwitch.Processor.Application.Models;
using PaymentSwitch.Processor.Domain.Entities;

namespace PaymentSwitch.Processor.Application.Abstractions;

public interface IChargeFirstPaymentGateway
{
    string? ResolveNotificationEndpoint(ConfirmedQuoteExecution execution, string operatorProvider);
    Task<PaymentOperationResult> NotifyAsync(Payment payment, bool recover, CancellationToken cancellationToken);
    Task<PaymentOperationResult> RefundAsync(Payment payment, CancellationToken cancellationToken);
}
