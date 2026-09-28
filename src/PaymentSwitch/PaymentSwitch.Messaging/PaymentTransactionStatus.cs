namespace PaymentSwitch.Messaging;

public enum PaymentTransactionStatus
{
	Accepted = 20,
	CheckingCard = 21,
	CardCheckRetryScheduled = 22,
	CardReady = 23,
	PayingCompany = 24,
	CompanyPaymentRetryScheduled = 25,
	CompanyPaymentOutcomeUnknown = 26,
	CompanyPaid = 27,
	ChargingCard = 28,
	ChargeRetryScheduled = 29,
	ChargeOutcomeUnknown = 30,
	RollingBackCompany = 31,
	RollbackRetryScheduled = 32,
	CompanyRolledBack = 33,
	Completed = 34,
	Failed = 35,
	ManualIntervention = 36,

	// Legacy values remain readable during migration but are not used by the
	// confirmed-quote execution saga.
	Confirmed = 0,
	LegacyChargingCard = 1,
	CardCharged = 2,
	SendingPaymentToCompany = 3,
	LegacyCompleted = 4,
	LegacyFailed = 5,
	Refunding = 6,
	Refunded = 7,
	RefundFailed = 8,
	CompanyPaymentFailed = 9
}
