using EmailServiceAPI.Models;

namespace EmailServiceAPI.Services
{
    public interface IEmailService
    {
        Task<EmailResult> SendLoginEmailAsync(string emailAddress);
    }
}