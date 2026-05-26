using Pagarte.Connections.Config;
using Pagarte.Engine.Consumers;
using Pagarte.Engine.Interfaces;
using Pagarte.Engine.Services;
using Pagarte.Messaging;
using Shared.RabbitMQ;

namespace Pagarte.Engine
{
    public class Program
    {
        public static async Task Main(string[] args)
        {
            var host = Host.CreateDefaultBuilder(args)
                .ConfigureServices((context, services) =>
                {
                    var configuration = context.Configuration;

                    // External connections (payment operator, companies) with Polly resilience.
                    services.AddPagarteConnections(configuration);

                    // Repository uses raw SQL to PagarteDb to avoid referencing Worker.
                    services.AddScoped<IPaymentStatusRepository, PaymentStatusRepository>();

                    // Email service
                    services.AddScoped<IEmailSenderService, EmailSenderService>();

                    // RabbitMQ
                    services.AddRabbitMq(configuration);
                    services.AddScoped<IMessagePublisher, RabbitMQPublisher>();

                    // Each consumer listens to one queue.
                    services.AddHostedService<PaymentRequestConsumer>();
                    services.AddHostedService<RefundConsumer>();
                    services.AddHostedService<EmailConsumer>();
                })
                .Build();

            var rabbitFactory = host.Services.GetRequiredService<RabbitMQConnectionFactory>();
            var connection = await rabbitFactory.GetConnectionAsync();
            var channel = await connection.CreateChannelAsync();
            await PagarteTopology.DeclareAllAsync(channel);

            host.Run();
        }
    }
}
