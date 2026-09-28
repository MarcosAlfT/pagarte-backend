using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using PaymentSwitch.Messaging;
using PaymentSwitch.Messaging.Messages;
using PaymentSwitch.Worker.Interfaces;

namespace PaymentSwitch.Worker.Services
{
    /// <summary>
    /// Updates payment status in PaymentDb via raw SQL.
    /// Worker does not reference PaymentSwitch.Processor, avoiding a circular dependency.
    /// Only updates, never reads complex object graphs.
    /// </summary>
    public class PaymentStatusRepository(
        IConfiguration configuration,
        IClock clock) : IPaymentStatusRepository
    {
        private readonly string _connectionString =
            configuration.GetConnectionString("PaymentDb")
            ?? throw new InvalidOperationException("PaymentDb not configured.");
        private readonly IClock _clock = clock;

        public async Task UpdateStatusAsync(
            PaymentTransactionStatus status,
            Guid paymentId,
            string? companyReference = null, string? errorMessage = null)
        {
            using var connection = new SqlConnection(_connectionString);
            await connection.OpenAsync();

            var sql = @"
                UPDATE Payments
                SET Status = @Status,
                    CompanyReference = COALESCE(@CompanyReference, CompanyReference),
                    ErrorMessage = COALESCE(@ErrorMessage, ErrorMessage),
                    LastUpdatedAt = @UpdatedAt,
                    ProcessedAt = CASE
                        WHEN @IsTerminal = 1
                        THEN @UpdatedAt ELSE ProcessedAt END
                WHERE Id = @PaymentId";

            using var cmd = new SqlCommand(sql, connection);
            cmd.Parameters.AddWithValue("@Status", (int)status);
            cmd.Parameters.AddWithValue("@IsTerminal", IsTerminal(status));
            cmd.Parameters.AddWithValue("@CompanyReference",
                (object?)companyReference ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@ErrorMessage",
                (object?)errorMessage ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@UpdatedAt", _clock.UtcNow);
            cmd.Parameters.AddWithValue("@PaymentId", paymentId);

            await cmd.ExecuteNonQueryAsync();
        }

        private static int IsTerminal(PaymentTransactionStatus status)
            => status is PaymentTransactionStatus.Completed
                or PaymentTransactionStatus.CompanyPaymentFailed
                or PaymentTransactionStatus.Failed
                or PaymentTransactionStatus.Refunded
                or PaymentTransactionStatus.RefundFailed
                ? 1
                : 0;

        public async Task ScheduleRefundRetryAsync(
            Guid paymentId,
            DateTime nextRetryAt)
        {
            using var connection = new SqlConnection(_connectionString);
            await connection.OpenAsync();

            var sql = @"
                UPDATE Payments
				SET RefundRetryCount = RefundRetryCount + 1,
					NextRefundRetryAt = @NextRetryAt,
                    LastUpdatedAt = @UpdatedAt
                WHERE Id = @PaymentId";

            using var cmd = new SqlCommand(sql, connection);
            cmd.Parameters.AddWithValue("@NextRetryAt", nextRetryAt);
            cmd.Parameters.AddWithValue("@UpdatedAt", _clock.UtcNow);
            cmd.Parameters.AddWithValue("@PaymentId", paymentId);

            await cmd.ExecuteNonQueryAsync();
        }

        public async Task<IReadOnlyCollection<RefundRequestMessage>> GetDueRefundRequestsAsync(
            DateTime utcNow,
            int maxRetries,
            int batchSize)
        {
            using var connection = new SqlConnection(_connectionString);
            await connection.OpenAsync();

            var sql = @"
                SELECT TOP (@BatchSize)
                    p.Id,
                    p.OperatorProvider,
                    p.OperatorPaymentId,
					p.TotalMinorUnits,
                    p.Currency,
                    COALESCE(p.ErrorMessage, 'Refund retry requested') AS Reason,
					p.RefundRetryCount
                FROM Payments p
                WHERE p.Status = @RefundingStatus
					AND p.NextRefundRetryAt IS NOT NULL
					AND p.NextRefundRetryAt <= @UtcNow
					AND p.RefundRetryCount < @MaxRetries
                    AND p.OperatorPaymentId IS NOT NULL
                    AND p.OperatorPaymentId <> ''
				ORDER BY p.NextRefundRetryAt";

            using var cmd = new SqlCommand(sql, connection);
            cmd.Parameters.AddWithValue("@BatchSize", batchSize);
            cmd.Parameters.AddWithValue("@RefundingStatus",
                (int)PaymentTransactionStatus.Refunding);
            cmd.Parameters.AddWithValue("@UtcNow", utcNow);
            cmd.Parameters.AddWithValue("@MaxRetries", maxRetries);

            var messages = new List<RefundRequestMessage>();
            using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                messages.Add(new RefundRequestMessage
                {
                    PaymentId = reader.GetGuid(0),
                    OperatorProvider = reader.GetString(1),
                    OperatorPaymentId = reader.GetString(2),
					MinorUnits = reader.GetInt64(3),
                    Currency = reader.GetString(4),
                    Reason = reader.GetString(5),
                    RetryCount = reader.GetInt32(6),
                    CreatedAt = utcNow
                });
            }

            return messages;
        }

        public async Task MarkRefundRetryDispatchedAsync(Guid paymentId)
        {
            using var connection = new SqlConnection(_connectionString);
            await connection.OpenAsync();

            var sql = @"
                UPDATE Payments
				SET NextRefundRetryAt = NULL,
                    LastUpdatedAt = @UpdatedAt
                WHERE Id = @PaymentId";

            using var cmd = new SqlCommand(sql, connection);
            cmd.Parameters.AddWithValue("@UpdatedAt", _clock.UtcNow);
            cmd.Parameters.AddWithValue("@PaymentId", paymentId);

            await cmd.ExecuteNonQueryAsync();
        }
    }
}
