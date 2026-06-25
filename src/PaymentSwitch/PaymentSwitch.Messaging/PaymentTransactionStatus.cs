namespace PaymentSwitch.Messaging
{
	public enum PaymentTransactionStatus
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
}
