using Grpc.Core;
using PaymentSwitch.Contracts;
using PaymentSwitch.Processor.Application.UseCases;

namespace PaymentSwitch.Processor.GrpcServices;

public sealed class PaymentGrpcService(
	GetPaymentUseCase getPaymentUseCase,
	GetPaymentHistoryUseCase getPaymentHistoryUseCase)
	: PaymentService.PaymentServiceBase
{
	public override async Task<GetPaymentResponse> GetPayment(
		GetPaymentRequest request,
		ServerCallContext context)
	{
		var payment = await getPaymentUseCase.ExecuteAsync(
			request.ClientId,
			Guid.Parse(request.PaymentId));
		return payment is null
			? new GetPaymentResponse { Found = false }
			: new GetPaymentResponse { Found = true, Payment = MapPayment(payment) };
	}

	public override async Task<GetPaymentHistoryResponse> GetPaymentHistory(
		GetPaymentHistoryRequest request,
		ServerCallContext context)
	{
		var result = await getPaymentHistoryUseCase.ExecuteAsync(
			request.ClientId,
			request.Page,
			request.PageSize);
		var response = new GetPaymentHistoryResponse { Total = result.Total };
		response.Payments.AddRange(result.Payments.Select(MapPayment));
		return response;
	}

	private static PaymentDto MapPayment(Domain.Entities.Payment payment)
	{
		var dto = new PaymentDto
		{
			Id = payment.Id.ToString(),
			SourceQuoteId = payment.SourceQuoteId.ToString(),
			Reference = payment.Reference,
			Status = payment.Status.ToString(),
			Total = new Money
			{
				CurrencyCode = payment.Currency,
				MinorUnits = payment.TotalMinorUnits
			},
			CreatedAt = payment.CreatedAt.ToString("O"),
			ProcessedAt = payment.ProcessedAt?.ToString("O") ?? string.Empty,
			ErrorCode = payment.ErrorCode ?? string.Empty,
			ErrorMessage = payment.ErrorMessage ?? string.Empty,
			ServiceName = payment.ServiceName,
			OperatorProvider = payment.OperatorProvider
		};
		dto.Details.AddRange(payment.Details
			.OrderBy(detail => detail.DisplayOrder)
			.Select(detail => new PaymentDetailDto
			{
				Type = detail.ComponentType,
				Description = detail.Description,
				Source = detail.Source,
				Money = new Money
				{
					CurrencyCode = detail.Currency,
					MinorUnits = detail.MinorUnits
				}
			}));
		return dto;
	}
}
