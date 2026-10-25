using Microsoft.EntityFrameworkCore;
using MimeKit;
using PTO_Management_System.Data;
using PTO_Management_System.Interfaces;

namespace PTO_Management_System.Services
{
    /// <summary>Every 30 seconds: emails each notification that has not been emailed yet.</summary>
    public class EmailWorker : BackgroundService
    {
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly IEmailSender _email;
        private readonly ILogger<EmailWorker> _logger;

        public EmailWorker(IServiceScopeFactory scopeFactory, IEmailSender email, ILogger<EmailWorker> logger)
        {
            _scopeFactory = scopeFactory;
            _email = email;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            if (!_email.IsEnabled)
            {
                _logger.LogWarning("Email is OFF. Set Email:Enabled=true and the Email:Username / Email:Password secrets.");
                return;
            }

            try { await Task.Delay(TimeSpan.FromSeconds(15), stoppingToken); }
            catch (OperationCanceledException) { return; }

            while (!stoppingToken.IsCancellationRequested)
            {
                try { await SendPendingAsync(stoppingToken); }
                catch (Exception ex) { _logger.LogError(ex, "Email worker failed."); }

                try { await Task.Delay(TimeSpan.FromSeconds(30), stoppingToken); }
                catch (OperationCanceledException) { break; }
            }
        }

        private async Task SendPendingAsync(CancellationToken ct)
        {
            using (var scope = _scopeFactory.CreateScope())
            {
                var context = scope.ServiceProvider.GetRequiredService<PtoDbContext>();
                var cutoff = DateTime.UtcNow.AddDays(-2);

                var pending = await context.Notifications
                    .Include(n => n.Beneficiary)
                    .Where(n => n.EmailSentAt == null && n.DateSent >= cutoff)
                    .OrderBy(n => n.DateSent)
                    .Take(20)
                    .ToListAsync(ct);

                foreach (var n in pending)
                {
                    var to = n.Beneficiary?.Email;
                    if (string.IsNullOrWhiteSpace(to) || !MailboxAddress.TryParse(to, out _))
                    {
                        n.EmailSentAt = DateTime.UtcNow; // nothing to send to
                        _logger.LogWarning("Notification {Id}: no valid email address, skipped.", n.Id);
                        continue;
                    }

                    try
                    {
                        await _email.SendAsync(to, n.Beneficiary.FullName, "myPTO: " + n.Title, n.Title, n.Message);
                        n.EmailSentAt = DateTime.UtcNow;
                        _logger.LogInformation("Email sent to {To}: {Title}", to, n.Title);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Could not email notification {Id}. Will retry.", n.Id);
                        break; // stop this round, retry in 30 seconds
                    }
                }

                await context.SaveChangesAsync(ct);
            }
        }
    }
}