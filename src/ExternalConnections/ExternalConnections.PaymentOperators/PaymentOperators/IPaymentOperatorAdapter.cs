namespace ExternalConnections.PaymentOperators.PaymentOperators;

public interface IPaymentOperatorAdapter
{
    string ProviderCode { get; }

    /// <summary>
    /// Registers a card with the payment operator.
    /// CVV is request-only and must not be persisted.
    /// </summary>
    Task<PaymentOperatorCardResult> RegisterCardAsync(
        string cardNumber, string cvv, string cardHolderName,
        int expiryMonth, int expiryYear);

    Task<PaymentOperatorChargeResult> ChargeAsync(
        string cardToken, decimal amount, string currency, string reference);

    Task<PaymentOperatorRefundResult> RefundAsync(
        string operatorPaymentId, decimal amount, string currency, string reason);
}

public record PaymentOperatorCardResult(
    bool Success,
    string? CardToken,
    string? Last4Digits,
    string? CardType,
    int ExpiryMonth,
    int ExpiryYear,
    string? ErrorMessage);

public record PaymentOperatorChargeResult(
    bool Success,
    string? OperatorPaymentId,
    string? ErrorMessage);

public record PaymentOperatorRefundResult(
    bool Success,
    string? RefundId,
    string? ErrorMessage);
