using EmailServiceAPI.Services;
using EmailServiceAPI.Models;
using Microsoft.Extensions.Logging;
using Moq;
using FluentAssertions;
using Xunit;

namespace EmailServiceAPI.Tests
{
    public class EmailServiceTests
    {
        private readonly Mock<ILogger<EmailService>> _loggerMock;
        private readonly EmailService _emailService;

        public EmailServiceTests()
        {
            _loggerMock = new Mock<ILogger<EmailService>>();
            _emailService = new EmailService(_loggerMock.Object);
        }

        [Fact]
        public async Task SendLoginEmailAsync_ValidEmail_ReturnsSuccess()
        {
            // Arrange
            var email = "test@example.com";

            // Act
            var result = await _emailService.SendLoginEmailAsync(email);

            // Assert
            result.Should().NotBeNull();
            result.Success.Should().BeTrue();
            result.MessageId.Should().NotBeNull();
            result.ErrorMessage.Should().BeNull();
        }

        [Theory]
        [InlineData("user@domain.com")]
        [InlineData("test.email@company.co.uk")]
        [InlineData("123@test.io")]
        public async Task SendLoginEmailAsync_VariousValidEmails_ReturnsSuccess(string email)
        {
            // Act
            var result = await _emailService.SendLoginEmailAsync(email);

            // Assert
            result.Success.Should().BeTrue();
        }
    }
}