using PaymentSwitch.Processor.Domain.Entities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PaymentSwitch.Processor.Interfaces
{
	public interface IServiceRepository
	{
		Task<IEnumerable<Service>> GetAllActiveAsync(string? category = null);
		Task<Service?> GetByIdAsync(Guid id);
	}
}
