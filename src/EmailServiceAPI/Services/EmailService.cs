using EmailServiceAPI.Models;

namespace EmailServiceAPI.Services
{
    public class EmailService : IEmailService
    {
        private readonly ILogger<EmailService> _logger;

        public EmailService(ILogger<EmailService> logger)
        {
            _logger = logger;
        }

        public async Task<EmailResult> SendLoginEmailAsync(string emailAddress)
        {
            try
            {
                // Currently simulating email sending
                // This will be replaced with AWS SES integration later
                _logger.LogInformation("Simulating email send to: {Email}", emailAddress);
                
                // Simulate some processing time
                await Task.Delay(500);
                
                // Simulate successful email sending
                var messageId = Guid.NewGuid().ToString();
                
                _logger.LogInformation("Email simulation completed for: {Email} with MessageId: {MessageId}", 
                    emailAddress, messageId);

                return new EmailResult
                {
                    Success = true,
                    MessageId = messageId
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error simulating email send to: {Email}", emailAddress);
                return new EmailResult
                {
                    Success = false,
                    ErrorMessage = ex.Message
                };
            }
        }
    }
}