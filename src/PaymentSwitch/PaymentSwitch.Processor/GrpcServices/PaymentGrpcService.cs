using PaymentSwitch.Contracts;
using PaymentSwitch.Processor.Application.Models;
using PaymentSwitch.Processor.Application.UseCases;
using Grpc.Core;

namespace PaymentSwitch.Processor.GrpcServices
{
	public class PaymentGrpcService(
			CreatePaymentQuoteUseCase createPaymentQuoteUseCase,
			ConfirmPaymentQuoteUseCase confirmPaymentQuoteUseCase,
			GetPaymentUseCase getPaymentUseCase,
			GetPaymentHistoryUseCase getPaymentHistoryUseCase,
			ILogger<PaymentGrpcService> logger)
			: PaymentSwitch.Contracts.PaymentService.PaymentServiceBase
	{
		private readonly CreatePaymentQuoteUseCase _createPaymentQuoteUseCase =
			createPaymentQuoteUseCase;
		private readonly ConfirmPaymentQuoteUseCase _confirmPaymentQuoteUseCase =
			confirmPaymentQuoteUseCase;
		private readonly GetPaymentUseCase _getPaymentUseCase = getPaymentUseCase;
		private readonly GetPaymentHistoryUseCase _getPaymentHistoryUseCase =
			getPaymentHistoryUseCase;
		private readonly ILogger<PaymentGrpcService> _logger = logger;

		public override async Task<CreatePaymentQuoteResponse> CreatePaymentQuote(
			CreatePaymentQuoteRequest request, ServerCallContext context)
		{
			_logger.LogInformation("Creating payment quote for client {ClientId}", request.ClientId);

			var result = await _createPaymentQuoteUseCase.ExecuteAsync(
				new CreatePaymentQuoteCommand(
					request.ClientId,
					Guid.Parse(request.ServiceId),
					request.Currency),
				context.CancellationToken);

			if (!result.Success || result.Quote == null)
			{
				return new CreatePaymentQuoteResponse
				{
					Success = false,
					ErrorMessage = result.ErrorMessage ?? string.Empty
				};
			}

			return new CreatePaymentQuoteResponse
			{
				Success = true,
				Quote = MapQuote(result.Quote)
			};
		}

		public override async Task<ProcessPaymentResponse> ConfirmPayment(
			ConfirmPaymentRequest request, ServerCallContext context)
		{
			_logger.LogInformation("Confirming payment quote {QuoteId} for client {ClientId}",
				request.QuoteId, request.ClientId);

			var result = await _confirmPaymentQuoteUseCase.ExecuteAsync(
				new ConfirmPaymentCommand(
					request.ClientId,
					Guid.Parse(request.QuoteId),
					Guid.Parse(request.CreditCardId)),
				context.CancellationToken);

			return new ProcessPaymentResponse
			{
				Success = result.Success,
				PaymentId = result.PaymentId?.ToString() ?? string.Empty,
				Reference = result.Reference ?? string.Empty,
				Status = result.Status ?? string.Empty,
				ErrorMessage = result.ErrorMessage ?? string.Empty
			};
		}

		public override async Task<GetPaymentResponse> GetPayment(
			GetPaymentRequest request, ServerCallContext context)
		{
			var payment = await _getPaymentUseCase.ExecuteAsync(
				request.ClientId,
				Guid.Parse(request.PaymentId));

			if (payment == null)
			{
				return new GetPaymentResponse { Found = false };
			}

			return new GetPaymentResponse { Found = true, Payment = MapPayment(payment) };
		}

		public override async Task<GetPaymentHistoryResponse> GetPaymentHistory(
			GetPaymentHistoryRequest request, ServerCallContext context)
		{
			var result = await _getPaymentHistoryUseCase.ExecuteAsync(
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
				Reference = payment.Reference,
				Status = payment.Status.ToString(),
				Currency = payment.Currency,
				TotalAmount = (double)payment.Details.Sum(d => d.Amount),
				CreatedAt = payment.CreatedAt.ToString("O"),
				ProcessedAt = payment.ProcessedAt?.ToString("O") ?? string.Empty,
				ErrorMessage = payment.ErrorMessage ?? string.Empty,
				ServiceName = payment.Service?.Name ?? string.Empty,
				OperatorProvider = payment.OperatorProvider
			};

			dto.Details.AddRange(payment.Details.Select(d => new PaymentDetailDto
			{
				Type = d.Type.ToString(),
				Description = d.Description,
				Amount = (double)d.Amount,
				Currency = d.Currency
			}));

			return dto;
		}

		private static PaymentQuoteDto MapQuote(Domain.Entities.PaymentQuote quote)
		{
			var dto = new PaymentQuoteDto
			{
				Id = quote.Id.ToString(),
				Status = quote.Status.ToString(),
				Currency = quote.Currency,
				TotalAmount = (double)quote.TotalAmount,
				CreatedAt = quote.CreatedAt.ToString("O"),
				ExpiresAt = quote.ExpiresAt.ToString("O"),
				ServiceId = quote.ServiceId.ToString(),
				ServiceName = quote.Service?.Name ?? string.Empty
			};

			dto.Details.AddRange(quote.Details.Select(d => new PaymentDetailDto
			{
				Type = d.Type.ToString(),
				Description = d.Description,
				Amount = (double)d.Amount,
				Currency = d.Currency
			}));

			return dto;
		}
	}
}
