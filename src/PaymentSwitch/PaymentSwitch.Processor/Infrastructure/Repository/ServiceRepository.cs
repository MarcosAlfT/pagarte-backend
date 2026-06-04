using PaymentSwitch.Processor.Domain.Entities;
using PaymentSwitch.Processor.Interfaces;
using Microsoft.EntityFrameworkCore;
using PaymentSwitch.Processor.Infrastructure;

namespace PaymentSwitch.Processor.Infrastructure.Repository
{
	public class ServiceRepository(PaymentDbContext context) : IServiceRepository
	{
		private readonly PaymentDbContext _context = context;

		public async Task<IEnumerable<Service>> GetAllActiveAsync(string? category = null)
		{
			var query = _context.Services.Include(s => s.Company).AsQueryable();
			if (!string.IsNullOrEmpty(category))
				query = query.Where(s => s.Category == category);
			return await query.ToListAsync();
		}

		public async Task<Service?> GetByIdAsync(Guid id)
			=> await _context.Services
				.Include(s => s.Company)
				.FirstOrDefaultAsync(s => s.Id == id);
	}
}