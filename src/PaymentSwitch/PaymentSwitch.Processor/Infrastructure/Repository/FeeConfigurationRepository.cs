using PaymentSwitch.Processor.Domain.Entities;
using PaymentSwitch.Processor.Interfaces;
using Microsoft.EntityFrameworkCore;
using PaymentSwitch.Processor.Infrastructure;
using PaymentSwitch.Messaging;

namespace PaymentSwitch.Processor.Infrastructure.Repository
{
	public class FeeConfigurationRepository(
		PaymentDbContext context,
		IClock clock) : IFeeConfigurationRepository
	{
		private readonly PaymentDbContext _context = context;
		private readonly IClock _clock = clock;

		public async Task<IEnumerable<FeeConfiguration>> GetActiveFeesAsync()
			=> await _context.FeeConfigurations
				.Where(f => f.EffectiveDate <= _clock.UtcNow
					&& (f.EndDate == null || f.EndDate >= _clock.UtcNow))
				.ToListAsync();
	}
}
