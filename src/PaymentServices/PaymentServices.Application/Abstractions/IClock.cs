namespace PaymentServices.Application.Abstractions;

public interface IClock
{
	DateTime UtcNow { get; }
}
