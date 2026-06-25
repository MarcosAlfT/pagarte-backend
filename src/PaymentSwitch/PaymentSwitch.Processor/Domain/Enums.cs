namespace PaymentSwitch.Processor.Domain.Enums
{
	public enum PaymentQuoteStatus
	{
		Unpaid,
		Paid
	}

	public enum PaymentOperatorScope
	{
		International,
		Local
	}

	public enum CardType {
		Visa, 
		Mastercard, 
		Amex, 
		Other 
	}

	public enum FeeType {
		PaymentOperator,
		Company, 
		Platform
	}

	public enum CalculationType { 
		Percentage,
		FixedAmount 
	}

	public enum PaymentDetailType
	{
		ServiceAmount,
		PaymentOperatorFee,
		CompanyFee,
		PlatformFee,
		Tax
	}
}
