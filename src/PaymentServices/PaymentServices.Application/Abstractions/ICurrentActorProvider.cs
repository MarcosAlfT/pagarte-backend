namespace PaymentServices.Application.Abstractions;

public interface ICurrentActorProvider
{
	string? ActorId { get; }
	string? IpAddress { get; }
}
