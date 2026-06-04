using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Infrastructure.RabbitMQ
{
	public enum RabbitMqConnectionMode
	{
		FromEnvironment,	//Aspire
		FromConfiguration				//Production
	}

	public class RabbitMqConnectionOptions
	{
		public RabbitMqConnectionMode Mode { get; set; }
		//Aspire: "amqps://user:pass@host:port/vhost"
		public string? ConnectionString { get; set; }

		//Production: manual parameters
		public string? HostName { get; set; }
		public int Port { get; set; } = 5672;
		public string? UserName { get; set; }
		public string? Password { get; set; }
	}
}
