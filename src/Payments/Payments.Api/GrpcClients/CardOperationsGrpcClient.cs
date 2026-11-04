using Grpc.Core;
using PaymentSwitch.Contracts;

namespace Payments.Api.GrpcClients;

public sealed class CardOperationsGrpcClient(CardOperationsService.CardOperationsServiceClient client, IConfiguration configuration)
{
	private Metadata Headers()
	{
		var key = configuration["CardOperationsAuth:ApiKey"];
		if (string.IsNullOrWhiteSpace(key) || key == configuration["WorkloadAuth:ApiKey"])
			throw new InvalidOperationException("Configure a separate card-operations workload key.");
		return new() { { "x-workload-api-key", key } };
	}
	public async Task<ListCardReviewsResponse> ListAsync(int limit, CancellationToken ct) => await client.ListReviewsAsync(new() { Limit = limit }, Headers(), cancellationToken: ct);
	public async Task<CardReviewDto> GetAsync(string id, CancellationToken ct) => await client.GetReviewAsync(new() { Id = id }, Headers(), cancellationToken: ct);
	public async Task<CardReviewDto> ReviewAsync(ReviewCardOperationRequest request, CancellationToken ct) => await client.ReviewOperationAsync(request, Headers(), cancellationToken: ct);
}
