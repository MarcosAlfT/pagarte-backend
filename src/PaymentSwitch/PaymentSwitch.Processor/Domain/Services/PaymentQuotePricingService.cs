using PaymentSwitch.Processor.Application.Abstractions;
using PaymentSwitch.Processor.Domain.Entities;
using PaymentSwitch.Processor.Domain.Enums;

namespace PaymentSwitch.Processor.Domain.Services
{
	public sealed class PaymentQuotePricingService : IPaymentQuotePricingService
	{
		public IReadOnlyCollection<PaymentQuotePriceLine> Calculate(
			Service service,
			IReadOnlyCollection<FeeConfiguration> fees,
			string currency)
		{
			var details = new List<PaymentQuotePriceLine>
			{
				new(PaymentDetailType.ServiceAmount, service.Name, service.BaseAmount, currency)
			};

			foreach (var fee in fees)
			{
				var amount = fee.CalculationType == CalculationType.Percentage
					? service.BaseAmount * fee.Value / 100
					: fee.Value;

				var type = fee.Type switch
				{
					FeeType.PaymentOperator => PaymentDetailType.PaymentOperatorFee,
					FeeType.Company => PaymentDetailType.CompanyFee,
					FeeType.Platform => PaymentDetailType.PlatformFee,
					_ => PaymentDetailType.Tax
				};

				details.Add(new PaymentQuotePriceLine(
					type,
					$"{fee.Type} Fee",
					amount,
					currency));
			}

			return details;
		}
	}
}
