using Microsoft.EntityFrameworkCore;
using ReservationService.Clients;
using ReservationService.Data;
using ReservationService.Models;
using ReservationService.Services;

namespace ReservationService.BackgroundJobs;

public class WaitlistExpiryBackgroundService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<WaitlistExpiryBackgroundService> _logger;
    private readonly TimeSpan _interval;

    public WaitlistExpiryBackgroundService(
        IServiceScopeFactory scopeFactory,
        ILogger<WaitlistExpiryBackgroundService> logger,
        IConfiguration configuration)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;

        var intervalMinutes = configuration.GetValue<int?>("WaitlistExpiry:IntervalMinutes") ?? 60;
        _interval = TimeSpan.FromMinutes(intervalMinutes);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            await ProcessExpiredEntriesAsync(stoppingToken);

            try
            {
                await Task.Delay(_interval, stoppingToken);
            }
            catch (TaskCanceledException)
            {
                break;
            }
        }
    }

    private async Task ProcessExpiredEntriesAsync(CancellationToken stoppingToken)
    {
        using var scope = _scopeFactory.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ReservationServiceContext>();
        var cascadeService = scope.ServiceProvider.GetRequiredService<WaitlistCascadeService>();
        var catalogServiceClient = scope.ServiceProvider.GetRequiredService<CatalogServiceClient>();

        var now = DateTime.UtcNow;
        var expiredEntries = await context.WaitlistEntries
            .Where(w => w.Status == WaitlistStatus.Notified && w.ClaimDeadline != null && w.ClaimDeadline < now)
            .ToListAsync(stoppingToken);

        if (expiredEntries.Count == 0)
        {
            _logger.LogInformation("Waitlist expiry sweep found no expired claims");
            return;
        }

        _logger.LogInformation("Waitlist expiry sweep found {Count} expired claim(s)", expiredEntries.Count);

        foreach (var entry in expiredEntries)
        {
            entry.Status = WaitlistStatus.Expired;
            await context.SaveChangesAsync(stoppingToken);

            var claimed = await cascadeService.OfferCopyToNextEligibleAsync(entry.BookId, entry.BookTitle, entry.BookAuthor);
            if (!claimed)
            {
                await catalogServiceClient.UpdateAvailabilityAsync(entry.BookId, 1);
                _logger.LogInformation(
                    "Waitlist entry {WaitlistId} expired with no eligible successor; copy for book {BookId} released to general availability",
                    entry.WaitlistId, entry.BookId);
            }
            else
            {
                _logger.LogInformation(
                    "Waitlist entry {WaitlistId} expired; copy for book {BookId} cascaded to next eligible entry",
                    entry.WaitlistId, entry.BookId);
            }
        }
    }
}
