using System;
using System.Collections.Concurrent;
using System.Threading.Tasks;
using System.Net.Mail;
using Microsoft.Extensions.Configuration;

namespace CVAPI.Services
{
    public class OneTimeLink
    {
        public string Id { get; set; }
        public string Email { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime ExpiresAt { get; set; }
        public bool IsUsed { get; set; }
    }

    public class OneTimeLinkService
    {
        private readonly ConcurrentDictionary<string, OneTimeLink> _activeLinks = new ConcurrentDictionary<string, OneTimeLink>();
        private readonly IConfiguration _configuration;

        public OneTimeLinkService(IConfiguration configuration)
        {
            _configuration = configuration;
        }

        public async Task GenerateAndSendOneTimeLink(string email)
        {
            // Generate a unique token
            string token = Guid.NewGuid().ToString();

            // Create the one-time link
            var oneTimeLink = new OneTimeLink
            {
                Id = token,
                Email = email,
                CreatedAt = DateTime.UtcNow,
                ExpiresAt = DateTime.UtcNow.AddMinutes(15), // Link expires in 15 minutes
                IsUsed = false
            };

            // Store the link in memory
            _activeLinks[token] = oneTimeLink;

            // Send email with the link
            await SendEmailWithLink(email, token);
        }

        public async Task<bool> ValidateOneTimeLink(string token)
        {
            // Check if link exists and is valid
            if (_activeLinks.TryGetValue(token, out var link))
            {
                // Check if link is expired or already used
                if (link.ExpiresAt < DateTime.UtcNow || link.IsUsed)
                {
                    _activeLinks.TryRemove(token, out _);
                    return false;
                }

                // Mark link as used
                link.IsUsed = true;
                return true;
            }

            return false;
        }

        public async Task SendEmailAsync(string email, string subject, string body)
        {
            try
            {
                using (var client = new SmtpClient())
                {
                    var mailMessage = new MailMessage
                    {
                        From = new MailAddress(_configuration["Email:FromAddress"]),
                        Subject = subject,
                        Body = body,
                        IsBodyHtml = true
                    };
                    mailMessage.To.Add(email);

                    await client.SendMailAsync(mailMessage);
                }
            }
            catch (Exception ex)
            {
                // Log the exception
                Console.WriteLine($"Error sending email: {ex.Message}");
                throw;
            }
        }

        private async Task SendEmailWithLink(string email, string token)
        {
            try
            {
                string baseUrl = _configuration["AppSettings:BaseUrl"];
                string oneTimeLinkUrl = $"{baseUrl}/validate/{token}";

                // Configure your email sending logic here
                await SendEmailAsync(email, "Your One-Time Link", $"Click the following link to proceed: {oneTimeLinkUrl}");
            }
            catch (Exception ex)
            {
                // Log the exception
                Console.WriteLine($"Error sending email: {ex.Message}");
                throw;
            }
        }
    }
}