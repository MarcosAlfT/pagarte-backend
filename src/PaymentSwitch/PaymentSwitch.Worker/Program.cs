using ExternalConnections.PaymentOperators.Config;
using PaymentSwitch.Worker.Application.Abstractions;
using PaymentSwitch.Worker.Application.UseCases;
using PaymentSwitch.Worker.Consumers;
using PaymentSwitch.Worker.Interfaces;
using PaymentSwitch.Worker.Services;
using PaymentSwitch.Messaging;
using Infrastructure.RabbitMQ;

namespace PaymentSwitch.Worker
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
                    services.AddExternalConnections(configuration);

                    // Repository uses raw SQL to PaymentDb to avoid referencing Processor.
                    services.AddScoped<IPaymentStatusRepository, PaymentStatusRepository>();
                    services.AddSingleton<IClock, SystemClock>();

                    // Email service
                    services.AddScoped<IEmailSenderService, EmailSenderService>();
                    services.AddScoped<IRefundGateway, PaymentOperatorRefundGateway>();

                    // Application use cases
                    services.AddScoped<ProcessPaymentRequestUseCase>();
                    services.AddScoped<ProcessRefundRequestUseCase>();
                    services.AddScoped<SendPaymentEmailUseCase>();

                    // RabbitMQ
                    services.AddRabbitMq(configuration);
                    services.AddScoped<IMessagePublisher, RabbitMqPublisher>();

                    // Each consumer listens to one queue.
                    services.AddHostedService<PaymentRequestConsumer>();
                    services.AddHostedService<RefundConsumer>();
                    services.AddHostedService<EmailConsumer>();
                    services.AddHostedService<RefundRetryDispatcherService>();
                })
                .Build();

            var rabbitFactory = host.Services.GetRequiredService<RabbitMqConnectionFactory>();
            var connection = await rabbitFactory.GetConnectionAsync();
            var channel = await connection.CreateChannelAsync();
            await PaymentSwitchTopology.DeclareAllAsync(channel);

            host.Run();
        }
    }
}
