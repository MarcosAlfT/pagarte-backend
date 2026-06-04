using PaymentSwitch.Processor.Domain.Entities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PaymentSwitch.Processor.Interfaces
{
	public interface IFeeConfigurationRepository
	{
		Task<IEnumerable<FeeConfiguration>> GetActiveFeesAsync();
	}
}
