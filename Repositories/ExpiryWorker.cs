using PTO_Management_System.Interfaces;

namespace PTO_Management_System.Repositories
{
    /// <summary>Runs the expiry check shortly after start-up and then every 6 hours.</summary>
    public class ExpiryWorker : BackgroundService
    {
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly ILogger<ExpiryWorker> _logger;

        public ExpiryWorker(IServiceScopeFactory scopeFactory, ILogger<ExpiryWorker> logger)
        {
            _scopeFactory = scopeFactory;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            try { await Task.Delay(TimeSpan.FromSeconds(30), stoppingToken); }
            catch (OperationCanceledException) { return; }

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    using (var scope = _scopeFactory.CreateScope())
                    {
                        var service = scope.ServiceProvider.GetRequiredService<IExpiryService>();
                        int changed = service.Refresh();
                        _logger.LogInformation("Expiry check finished: {Count} PTO(s) changed status.", changed);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Expiry check failed.");
                }

                try { await Task.Delay(TimeSpan.FromHours(6), stoppingToken); }
                catch (OperationCanceledException) { break; }
            }
        }
    }
}
