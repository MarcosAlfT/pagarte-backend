using PaymentSwitch.Processor.Application.Models;
using PaymentSwitch.Processor.Domain.Entities;

namespace PaymentSwitch.Processor.Application.Services;

internal static class BillVerificationPolicy
{
    public const string RefreshMessage = "The bill must be verified again before payment.";

    public static (string Code, string Message)? Validate(ConfirmedQuoteExecution execution, DateTime utcNow)
    {
        if (!execution.RequiresBillVerification) return null;

        if (execution.BillKey is not { Length: 64 } key ||
            key.Any(c => c is not (>= '0' and <= '9' or >= 'a' and <= 'f')))
            return ("BILL_IDENTITY_REQUIRED", "The quote has no supported bill identity. Consult again.");

        var evidence = execution.BillVerification;
        switch (evidence?.ErrorCode)
        {
            case "BILL_CHANGED":
                return ("BILL_CHANGED", "The bill changed. Consult again for a new quote.");
            case "BILL_NO_LONGER_DUE":
                return ("BILL_NO_LONGER_DUE", "This bill is no longer due.");
            case "BILL_ROUTE_UNAVAILABLE":
                return ("BILL_ROUTE_UNAVAILABLE", "The original payment route is unavailable. Consult again.");
        }

        var serviceAmounts = execution.Components
            .Where(component => component.Source == "AmountInquiry" && component.ComponentType == "ServiceAmount")
            .ToArray();
        if (evidence is null || evidence.ErrorCode is not null || evidence.ObservedBillKey != key ||
            evidence.ProviderInquiryId.Length is 0 or > 200 || string.IsNullOrWhiteSpace(evidence.ProviderInquiryId) ||
            serviceAmounts.Length != 1 || evidence.AmountMinorUnits <= 0 ||
            evidence.AmountMinorUnits != serviceAmounts[0].MinorUnits || evidence.Currency != execution.Currency ||
            evidence.CheckedAt is not { Kind: DateTimeKind.Utc } checkedAt || checkedAt > utcNow ||
            execution.SubmittedAt is not { } submittedAt || checkedAt < submittedAt ||
            evidence.ExpiresAt is not { Kind: DateTimeKind.Utc } expiresAt ||
            expiresAt <= checkedAt || expiresAt - checkedAt > TimeSpan.FromMinutes(1))
            return ("BILL_VERIFICATION_UNAVAILABLE", "The bill could not be verified. Consult again.");

        return expiresAt <= utcNow
            ? (Payment.StaleBillVerification, RefreshMessage)
            : null;
    }
}
