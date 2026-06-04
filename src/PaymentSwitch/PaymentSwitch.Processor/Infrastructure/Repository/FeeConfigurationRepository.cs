using PaymentSwitch.Processor.Domain.Entities;
using PaymentSwitch.Processor.Interfaces;
using Microsoft.EntityFrameworkCore;
using PaymentSwitch.Processor.Infrastructure;

namespace PaymentSwitch.Processor.Infrastructure.Repository
{
	public class FeeConfigurationRepository(PaymentDbContext context) : IFeeConfigurationRepository
	{
		private readonly PaymentDbContext _context = context;

		public async Task<IEnumerable<FeeConfiguration>> GetActiveFeesAsync()
			=> await _context.FeeConfigurations
				.Where(f => f.EffectiveDate <= DateTime.UtcNow
					&& (f.EndDate == null || f.EndDate >= DateTime.UtcNow))
				.ToListAsync();
	}
}
