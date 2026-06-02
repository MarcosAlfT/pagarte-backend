using Microsoft.Extensions.Logging;
using System.Text.RegularExpressions;

namespace ExternalConnections.CardOperators.PaymentOperators;

public partial class MockPaymentOperatorAdapter(
    ILogger<MockPaymentOperatorAdapter> logger) : IPaymentOperatorAdapter
{
    private readonly ILogger<MockPaymentOperatorAdapter> _logger = logger;
    public string ProviderCode => "Mock";

    public Task<PaymentOperatorCardResult> RegisterCardAsync(
        string cardNumber, string cvv, string cardHolderName,
        int expiryMonth, int expiryYear)
    {
        var digits = DigitsOnlyRegex().Replace(cardNumber, string.Empty);
        var last4 = digits.Length >= 4 ? digits[^4..] : "4242";
        var brand = InferBrand(digits);
        var token = $"mock_card_{Guid.NewGuid():N}";

        _logger.LogInformation(
            "Mock payment operator registered {Brand} card ending in {Last4} for {CardHolderName}.",
            brand, last4, cardHolderName);

        return Task.FromResult(new PaymentOperatorCardResult(
            true,
            token,
            last4,
            brand,
            expiryMonth,
            expiryYear,
            null));
    }

    public Task<PaymentOperatorChargeResult> ChargeAsync(
        string cardToken, decimal amount, string currency, string reference)
    {
        var paymentId = $"mock_payment_{Guid.NewGuid():N}";
        _logger.LogInformation(
            "Mock payment operator charged {Amount} {Currency} to card token {CardToken} for {Reference}.",
            amount, currency, cardToken, reference);

        return Task.FromResult(new PaymentOperatorChargeResult(true, paymentId, null));
    }

    public Task<PaymentOperatorRefundResult> RefundAsync(
        string operatorPaymentId, decimal amount, string currency, string reason)
    {
        var refundId = $"mock_refund_{Guid.NewGuid():N}";
        _logger.LogInformation(
            "Mock payment operator refunded {Amount} {Currency} for payment {PaymentId}.",
            amount, currency, operatorPaymentId);

        return Task.FromResult(new PaymentOperatorRefundResult(true, refundId, null));
    }

    private static string InferBrand(string digits)
    {
        if (digits.StartsWith('4'))
        {
            return "Visa";
        }

        if (digits.Length >= 2
            && int.TryParse(digits[..2], out var firstTwo)
            && firstTwo is >= 51 and <= 55)
        {
            return "Mastercard";
        }

        if (digits.StartsWith("34") || digits.StartsWith("37"))
        {
            return "Amex";
        }

        return "Other";
    }

    [GeneratedRegex("\\D")]
    private static partial Regex DigitsOnlyRegex();
}
