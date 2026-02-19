using System;
using System.Threading;
using System.Threading.Tasks;
using CVAPI.Models;
using CVAPI.Repos;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace CVAPI.Services
{
    public class ApplicantCleanupService : BackgroundService
    {
        private readonly IServiceProvider _services;
        private readonly ILogger<ApplicantCleanupService> _logger;
        private readonly OneTimeLinkService _oneTimeLinkService;
        
        public ApplicantCleanupService(
            IServiceProvider services,
            ILogger<ApplicantCleanupService> logger,
            OneTimeLinkService oneTimeLinkService)
        {
            _services = services;
            _logger = logger;
            _oneTimeLinkService = oneTimeLinkService;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    await CheckApplicants();
                }
                catch (Exception ex)
                {
                    _logger.LogError($"Error checking applicants: {ex.Message}");
                }

                // Run check every 24 hours
                await Task.Delay(TimeSpan.FromHours(24), stoppingToken);
            }
        }

        private async Task CheckApplicants()
        {
            using var scope = _services.CreateScope();
            var userRepository = scope.ServiceProvider.GetRequiredService<UserRepository>();

            var applicants = await userRepository.GetAllApplicants("DK");
            _logger.LogInformation($"Checking {applicants.Count} applicants for cleanup");

            foreach (var applicant in applicants)
            {
                var threeMonthsAfterAdd = applicant.DateAdded.AddMonths(3);
                var twoWeeksAfterThreeMonths = threeMonthsAfterAdd.AddDays(14);

                if (DateTime.UtcNow >= threeMonthsAfterAdd && DateTime.UtcNow < twoWeeksAfterThreeMonths)
                {
                    _logger.LogInformation($"Sending reminder email to applicant {applicant.UserId} added on {applicant.DateAdded}");
                    await SendReminderEmail(applicant);
                }
                else if (DateTime.UtcNow >= twoWeeksAfterThreeMonths)
                {
                    _logger.LogInformation($"Deleting expired applicant {applicant.UserId} added on {applicant.DateAdded}");
                    await userRepository.DeleteApplicantAsync(applicant.UserId, "DK");
                }
            }
        }

        public async Task KeepApplicant(string userId, string region = "DK")
        {
            using var scope = _services.CreateScope();
            var userRepository = scope.ServiceProvider.GetRequiredService<UserRepository>();

            var applicant = await userRepository.GetApplicantAsync(userId, region);
            if (applicant != null)
            {
                applicant.DateAdded = DateTime.UtcNow; // Reset the DateAdded to current time
                await userRepository.UpdateApplicantAsync(applicant, region);
                _logger.LogInformation($"Reset DateAdded for applicant {userId} to {DateTime.UtcNow}");
            }
        }

        private async Task SendReminderEmail(Applicant applicant)
        {
            var subject = "Action Required: Confirm Your Application";
            var body = $@"Dear {applicant.FirstName},

Your application was submitted on {applicant.DateAdded:d}. It requires confirmation to remain active in our system.

If you do not confirm within the next 14 days, your application will be automatically removed from our database.

Please log in to confirm your application.

Best regards,
The VEXA Team";

            await _oneTimeLinkService.SendEmailAsync(applicant.Email, subject, body);
            _logger.LogInformation($"Sent reminder email to applicant {applicant.UserId}");
        }
    }
}
