namespace PayableServices.Domain.Enums;

public enum QuoteStatus
{
	Unpaid = 0,
	Paid = 1,
	Expired = 2,
	Cancelled = 3,
	PaymentFailed = 4
}
