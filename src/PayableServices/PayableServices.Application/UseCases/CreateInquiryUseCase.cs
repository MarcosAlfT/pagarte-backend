using System.Diagnostics.Metrics;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using PayableServices.Application.Abstractions;
using PayableServices.Application.Models;
using PayableServices.Domain.Entities;

namespace PayableServices.Application.UseCases;

public sealed class CreateInquiryUseCase(
    IInquiryRepository inquiries, IPayableServiceRepository services,
    IPaymentRouteRepository routes, IQuoteConfigurationRepository configuration,
    IAmountInquiryProvider provider, IUnitOfWork unitOfWork, IClock clock,
    ILogger<CreateInquiryUseCase> logger)
{
    private static readonly Meter Meter = new("Pagarte.Inquiries");
    private static readonly Counter<long> Outcomes = Meter.CreateCounter<long>("inquiry.outcomes");

    public async Task<InquiryResult> ExecuteAsync(CreateInquiryCommand command, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(command.ClientId) || command.ClientId.Length > 100 ||
            string.IsNullOrWhiteSpace(command.IdempotencyKey) || command.IdempotencyKey.Length > 100 ||
            command.IdempotencyKey.Any(c => c < 33 || c > 126) || command.ServiceId == Guid.Empty ||
            command.References is null || command.References.Any(x => x is null || x.Key is null || x.Value is null) ||
            command.References.Select(x => x.Key).Distinct(StringComparer.Ordinal).Count() != command.References.Count)
            return new(null, "INVALID_INQUIRY_REQUEST");

        // Fingerprint encoding is unambiguous, case sensitive, and preserves leading zeroes.
        var canonical = command.References.OrderBy(x => x.Key, StringComparer.Ordinal)
            .Select(x => new ReferenceValue(x.Key, x.Value.Trim())).ToArray();
        var fingerprint = Hash(JsonSerializer.Serialize(new { command.ServiceId, References = canonical }));
        AmountInquiry inquiry;
        await using (var transaction = await unitOfWork.BeginAsync("inquiry-key:" + Hash(command.ClientId + ":" + command.IdempotencyKey), cancellationToken))
        {
            var existing = await inquiries.FindAsync(command.ClientId, command.IdempotencyKey, cancellationToken);
            if (existing is not null)
                return existing.Fingerprint == fingerprint
                    ? new(InquiryDto.Map(existing, clock.UtcNow)) : new(null, "IDEMPOTENCY_CONFLICT");

            var service = await services.GetByIdAsync(command.ServiceId, cancellationToken);
            var route = await routes.GetActiveByPayableServiceIdAsync(command.ServiceId, cancellationToken);
            if (service is not { IsActive: true, AllowsQuote: true, AllowsPayment: true, Currency: "USD" } ||
                route?.InquiryProvider is null || string.IsNullOrWhiteSpace(route.ExternalServiceCode) ||
                !provider.IsConfigured(route.InquiryProvider) ||
                await services.GetDetailsAsync(command.ServiceId, cancellationToken) is null)
                return new(null, "INQUIRY_UNAVAILABLE");
            var fields = await configuration.GetReferenceFieldsAsync(service.Id, cancellationToken);
            if (!ReferenceValidation.TryNormalize(fields, command.References, out var references))
                return new(null, "INVALID_REFERENCES");

            inquiry = new AmountInquiry
            {
                Id = Guid.NewGuid(),
                ClientId = command.ClientId,
                ServiceId = service.Id,
                RouteId = route.Id,
                Provider = route.InquiryProvider,
                ExternalServiceCode = route.ExternalServiceCode!,
                IdempotencyKey = command.IdempotencyKey,
                Fingerprint = fingerprint,
                ProviderKey = Guid.NewGuid().ToString("N"),
                ReferenceSnapshot = JsonSerializer.Serialize(references),
                CreatedAt = clock.UtcNow,
                LeaseToken = Guid.NewGuid(),
                LeaseUntil = clock.UtcNow.AddMinutes(1)
            };
            inquiries.Add(inquiry);
            await unitOfWork.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }
        return new(InquiryDto.Map(await CompleteAsync(inquiry, cancellationToken), clock.UtcNow));
    }

    public async Task RecoverAsync(Guid id, CancellationToken cancellationToken = default)
    {
        AmountInquiry? inquiry;
        await using (var transaction = await unitOfWork.BeginAsync("inquiry:" + id, cancellationToken))
        {
            inquiry = await inquiries.GetAsync(id, cancellationToken);
            if (inquiry is null || inquiry.Outcome != InquiryOutcome.Pending || inquiry.LeaseUntil > clock.UtcNow) return;
            inquiry.LeaseToken = Guid.NewGuid();
            inquiry.LeaseUntil = clock.UtcNow.AddMinutes(1);
            await unitOfWork.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }
        await CompleteAsync(inquiry, cancellationToken);
    }

    private async Task<AmountInquiry> CompleteAsync(AmountInquiry reserved, CancellationToken cancellationToken)
    {
        var token = reserved.LeaseToken;
        var result = await provider.InquireAsync(new(reserved.Provider, reserved.ExternalServiceCode,
            reserved.References(), reserved.ProviderKey, "USD", reserved.CreatedAt), cancellationToken);

        await using var transaction = await unitOfWork.BeginAsync("inquiry:" + reserved.Id, cancellationToken);
        var inquiry = (await inquiries.GetAsync(reserved.Id, cancellationToken))!;
        if (inquiry.Outcome != InquiryOutcome.Pending || inquiry.LeaseToken != token) return inquiry;
        var balance = result.Outcome is InquiryOutcome.AmountDue or InquiryOutcome.NothingDue;
        if (balance && (result.Currency != "USD" || !ProviderBalanceValidation.Matches(result, inquiry.ExternalServiceCode,
            inquiry.References(), inquiry.CreatedAt, clock.UtcNow)))
            result = new(InquiryOutcome.InvalidProviderResponse);
        if (result.Outcome == InquiryOutcome.Pending || !Enum.IsDefined(result.Outcome))
            result = new(InquiryOutcome.InvalidProviderResponse);
        inquiry.Outcome = result.Outcome;
        if (result.Outcome is InquiryOutcome.AmountDue or InquiryOutcome.NothingDue)
        {
            inquiry.AmountMinorUnits = result.AmountMinorUnits;
            inquiry.Currency = result.Currency;
            inquiry.ProviderInquiryId = result.ProviderInquiryId;
            inquiry.ProviderResultAt = result.ResultAt;
            inquiry.ExpiresAt = result.ExpiresAt < result.ResultAt!.Value.AddMinutes(15)
                ? result.ExpiresAt : result.ResultAt.Value.AddMinutes(15);
            inquiry.DueDate = result.DueDate;
            inquiry.BillingPeriod = result.BillingPeriod is { Length: 7 } ? result.BillingPeriod : null;
        }
        else
        {
            inquiry.ErrorCode = result.Outcome switch
            {
                InquiryOutcome.ReferenceNotFound => "REFERENCE_NOT_FOUND",
                InquiryOutcome.TemporarilyUnavailable => "PROVIDER_UNAVAILABLE",
                _ => "INVALID_PROVIDER_RESPONSE"
            };
        }
        await unitOfWork.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        Outcomes.Add(1, new KeyValuePair<string, object?>("outcome", inquiry.Outcome.ToString()));
        logger.LogInformation("Inquiry {InquiryId} client {ClientId} service {ServiceId} provider inquiry {ProviderInquiryId} key hash {KeyHash} outcome {Outcome}; references redacted",
            inquiry.Id, inquiry.ClientId, inquiry.ServiceId, inquiry.ProviderInquiryId, Hash(inquiry.IdempotencyKey), inquiry.Outcome);
        return inquiry;
    }

    private static string Hash(string value) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value)));
}
