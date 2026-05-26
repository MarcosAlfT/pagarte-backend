using RabbitMQ.Client;

namespace Shared.RabbitMQ
{
    /// <summary>
    /// Manages a single persistent RabbitMQ connection with auto-recovery.
    /// Register as Singleton.
    /// </summary>
    public class RabbitMQConnectionFactory : IDisposable
    {
		private readonly RabbitMQConnentionOptions _connectionOptions;

        private IConnection? _connection;
        private readonly SemaphoreSlim _semaphore = new(1, 1);

        public RabbitMQConnectionFactory(RabbitMQConnentionOptions connOptions)
        {
            _connectionOptions = connOptions;
		}

        public async Task<IConnection> GetConnectionAsync()
        {
            await _semaphore.WaitAsync();
			try
            {
                if (_connection == null || !_connection.IsOpen)
                {
					var connectionString = BuildConnectionString();

					var factory = new ConnectionFactory
					{
						Uri = new Uri(connectionString),
						AutomaticRecoveryEnabled = true,
						NetworkRecoveryInterval = TimeSpan.FromSeconds(10)
					};
					_connection = await factory.CreateConnectionAsync();
                }
                return _connection;
            }
            finally
            {
                _semaphore.Release();
			}
		}

		private string BuildConnectionString()
		{
            var hostName = _connectionOptions.HostName;
            var userName = _connectionOptions.UserName;

			return _connectionOptions.Mode switch
			{
				RabbitMQConnentionMode.FromEnvironment =>
					_connectionOptions.ConnectionString
					?? throw new Exception("ConnectionString is null"),

				RabbitMQConnentionMode.FromConfiguration =>
					$"amqp://{userName}:{_connectionOptions.Password}@{hostName}:{_connectionOptions.Port}/",

				_ => throw new Exception("Unsupported RabbitMQ mode")
			};
		}

		public void Dispose()
        {
            _connection?.CloseAsync().GetAwaiter().GetResult();
            _connection?.Dispose();
        }
    }

}
