namespace PaymentServices.Domain.Enums;

public enum ExternalCatalogueMappingStatus
{
	Unmapped = 0,
	Mapped = 1,
	Ignored = 2,
	ReviewRequired = 3,
	InactiveExternal = 4
}
