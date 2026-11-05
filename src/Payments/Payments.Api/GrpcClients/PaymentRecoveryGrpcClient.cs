using Grpc.Core;
using PaymentSwitch.Contracts;

namespace Payments.Api.GrpcClients;

public sealed class PaymentRecoveryGrpcClient(PaymentRecoveryService.PaymentRecoveryServiceClient client, IConfiguration configuration)
{
    public async Task<ListPaymentRecoveriesResponse> ListAsync(int limit, CancellationToken cancellationToken) =>
        await client.ListAsync(new() { Limit = limit }, Headers(), cancellationToken: cancellationToken);

    public async Task<PaymentRecoveryAuditDto> GetAuditAsync(string id, CancellationToken cancellationToken) =>
        await client.GetAuditAsync(new() { Id = id }, Headers(), cancellationToken: cancellationToken);

    public async Task<PaymentRecoveryAuditDto> ScheduleAsync(SchedulePaymentRecoveryRequest request, CancellationToken cancellationToken) =>
        await client.ScheduleAsync(request, Headers(), cancellationToken: cancellationToken);

    private Metadata Headers()
    {
        var key = configuration["PaymentRecoveryAuth:ApiKey"];
        if (string.IsNullOrWhiteSpace(key) || key == configuration["WorkloadAuth:ApiKey"] || key == configuration["CardOperationsAuth:ApiKey"])
            throw new RpcException(new Status(StatusCode.Unavailable, "PAYMENT_RECOVERY_UNAVAILABLE"));
        return new() { { "x-workload-api-key", key } };
    }
}
