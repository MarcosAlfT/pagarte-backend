using PaymentSwitch.Messaging.Messages;
using PaymentSwitch.Worker.Interfaces;

namespace PaymentSwitch.Worker.Application.UseCases
{
	public sealed class SendPaymentEmailUseCase(
		IEmailSenderService emailSender,
		ILogger<SendPaymentEmailUseCase> logger)
	{
		private readonly IEmailSenderService _emailSender = emailSender;
		private readonly ILogger<SendPaymentEmailUseCase> _logger = logger;

		public async Task ExecuteAsync(
			EmailMessage message,
			CancellationToken cancellationToken = default)
		{
			if (string.IsNullOrEmpty(message.To))
			{
				_logger.LogWarning("Email skipped - no recipient");
				return;
			}

			await _emailSender.SendAsync(
				message.To,
				message.Subject,
				message.Body,
				message.IsHtml);
		}
	}
}
