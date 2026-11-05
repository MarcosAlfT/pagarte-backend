using Grpc.Core;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using PaymentSwitch.Contracts;
using PaymentSwitch.Processor.Application.UseCases;
using PaymentSwitch.Processor.Domain.Entities;

namespace PaymentSwitch.Processor.GrpcServices;

public sealed class PaymentRecoveryGrpcService(ReviewPaymentRecoveryUseCase recovery) : PaymentRecoveryService.PaymentRecoveryServiceBase
{
    public override Task<ListPaymentRecoveriesResponse> List(ListPaymentRecoveriesRequest request, ServerCallContext context) =>
        RunAsync(async () =>
        {
            var response = new ListPaymentRecoveriesResponse();
            response.Items.AddRange((await recovery.ListAsync(request.Limit, context.CancellationToken)).Select(item =>
                new PaymentRecoveryItemDto
                {
                    PaymentId = item.PaymentId.ToString(), Reference = item.Reference, Stage = item.Stage.ToString(),
                    Version = item.Version, UpdatedAt = UtcTimestamp(item.UpdatedAt)
                }));
            return response;
        });

    public override Task<PaymentRecoveryAuditDto> GetAudit(GetPaymentRecoveryAuditRequest request, ServerCallContext context) =>
        RunAsync(async () => Map(await recovery.GetAsync(Id(request.Id), context.CancellationToken)));

    public override Task<PaymentRecoveryAuditDto> Schedule(SchedulePaymentRecoveryRequest request, ServerCallContext context) =>
        RunAsync(async () => Map(await recovery.ScheduleAsync(new(Id(request.PaymentId), request.Actor,
            request.IdempotencyKey, request.ExpectedVersion, request.ReasonCode), context.CancellationToken)));

    private static Guid Id(string value) => Guid.TryParse(value, out var id) && id != Guid.Empty
        ? id : throw new PaymentRecoveryException("INVALID_RECOVERY_ID", 400);

    private static PaymentRecoveryAuditDto Map(PaymentRecoveryAction action) => new()
    {
        Id = action.Id.ToString(), PaymentId = action.PaymentId.ToString(), Stage = action.Stage.ToString(),
        Actor = action.Actor, ReasonCode = action.ReasonCode, ScheduledAt = UtcTimestamp(action.ScheduledAt)
    };

    private static string UtcTimestamp(DateTime timestamp) =>
        DateTime.SpecifyKind(timestamp, DateTimeKind.Utc).ToString("O");

    private static async Task<T> RunAsync<T>(Func<Task<T>> operation)
    {
        try { return await operation(); }
        catch (PaymentRecoveryException error)
        {
            var code = error.Status switch { 404 => StatusCode.NotFound, 409 => StatusCode.Aborted, _ => StatusCode.InvalidArgument };
            throw new RpcException(new Status(code, error.Message));
        }
        catch (DbUpdateConcurrencyException)
        {
            throw new RpcException(new Status(StatusCode.Aborted, "CONCURRENT_RECOVERY"));
        }
        catch (DbUpdateException error) when (error.InnerException is SqlException { Number: 2601 or 2627 })
        {
            throw new RpcException(new Status(StatusCode.Aborted, "CONCURRENT_RECOVERY"));
        }
    }
}
