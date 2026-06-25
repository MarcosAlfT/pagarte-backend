using Microsoft.EntityFrameworkCore;
using PaymentSwitch.Processor.Domain.Entities;
using PaymentSwitch.Processor.Domain.Enums;
using PaymentSwitch.Processor.Interfaces;

namespace PaymentSwitch.Processor.Infrastructure.Repository
{
	public class PaymentOperatorRepository(PaymentDbContext context) : IPaymentOperatorRepository
	{
		private readonly PaymentDbContext _context = context;

		public async Task<PaymentOperator?> GetActiveAsync(PaymentOperatorScope scope)
			=> await _context.PaymentOperators
				.Where(o => o.IsActive && o.Scope == scope)
				.OrderBy(o => o.Priority)
				.FirstOrDefaultAsync();

		public async Task<PaymentOperator?> GetByCodeAsync(string code)
			=> await _context.PaymentOperators
				.FirstOrDefaultAsync(o => o.Code == code);

		public Task<PaymentOperator> CreateAsync(PaymentOperator paymentOperator)
		{
			_context.PaymentOperators.Add(paymentOperator);
			return Task.FromResult(paymentOperator);
		}
	}
}
