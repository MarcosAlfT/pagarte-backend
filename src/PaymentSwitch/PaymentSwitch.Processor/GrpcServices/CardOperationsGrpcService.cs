using Grpc.Core;
using Microsoft.EntityFrameworkCore;
using Microsoft.Data.SqlClient;
using PaymentSwitch.Contracts;
using PaymentSwitch.Processor.Application.Models;
using PaymentSwitch.Processor.Application.UseCases;

namespace PaymentSwitch.Processor.GrpcServices;

public sealed class CardOperationsGrpcService(ReviewCardOperationUseCase reviews) : CardOperationsService.CardOperationsServiceBase
{
	public override Task<ListCardReviewsResponse> ListReviews(ListCardReviewsRequest request, ServerCallContext context) => Run(async () =>
	{
		var response = new ListCardReviewsResponse();
		response.Items.AddRange((await reviews.ListAsync(request.Limit == 0 ? 50 : request.Limit, context.CancellationToken))
			.Select(r => new CardReviewItemDto { Id = r.Id.ToString(), Kind = r.Kind, State = r.State, ClientId = r.ClientId, Attempts = r.Attempts, CreatedAt = r.CreatedAt.ToString("O") }));
		return response;
	});
	public override Task<CardReviewDto> GetReview(GetCardReviewRequest request, ServerCallContext context) =>
		Run(async () => Map(await reviews.GetAsync(Id(request.Id), context.CancellationToken)));
	public override Task<CardReviewDto> ReviewOperation(ReviewCardOperationRequest request, ServerCallContext context) =>
		Run(async () => Map(await reviews.ExecuteAsync(new(request.Actor, request.IdempotencyKey, Id(request.TargetId),
			request.Kind, request.Action, request.ExpectedState, request.ReasonCode), context.CancellationToken)));
	private static Guid Id(string value) => Guid.TryParse(value, out var id) && id != Guid.Empty ? id : throw new CardOperationException("INVALID_ID", "Invalid ID.");
	private static CardReviewDto Map(CardReviewView value) => new() { Id = value.Id.ToString(), TargetId = value.TargetId.ToString(),
		Kind = value.Kind, Action = value.Action, Actor = value.Actor, ReasonCode = value.ReasonCode, BeforeState = value.BeforeState,
		AfterState = value.AfterState, Outcome = value.Outcome, CreatedAt = value.CreatedAt.ToString("O"), CompletedAt = value.CompletedAt?.ToString("O") ?? "" };
	private static async Task<T> Run<T>(Func<Task<T>> action)
	{
		try { return await action(); }
		catch (CardOperationException error) { throw new RpcException(new Status(error.HttpStatus switch { 404 => StatusCode.NotFound, 409 => StatusCode.AlreadyExists, _ => StatusCode.InvalidArgument }, error.Code)); }
		catch (DbUpdateException error) when (error.InnerException is SqlException { Number: 2601 or 2627 }) { throw new RpcException(new Status(StatusCode.AlreadyExists, "CONCURRENT_REVIEW")); }
		catch (DbUpdateConcurrencyException) { throw new RpcException(new Status(StatusCode.Aborted, "CONCURRENT_REVIEW")); }
	}
}
