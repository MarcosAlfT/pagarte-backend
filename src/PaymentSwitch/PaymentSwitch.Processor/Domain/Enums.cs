namespace PaymentSwitch.Processor.Domain.Enums
{
	public enum TransactionStatus
	{
		Confirmed = 0,
		ChargingCard = 1,
		CardCharged = 2,
		SendingPaymentToCompany = 3,
		Completed = 4,
		CompanyPaymentFailed = 9,
		Failed = 5,
		Refunding = 6,
		Refunded = 7,
		RefundFailed = 8
	}

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
