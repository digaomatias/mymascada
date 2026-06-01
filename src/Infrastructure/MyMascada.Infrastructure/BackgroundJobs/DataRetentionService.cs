using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using MyMascada.Application.BackgroundJobs;
using MyMascada.Domain.Common;
using MyMascada.Infrastructure.Data;

namespace MyMascada.Infrastructure.BackgroundJobs;

public class DataRetentionService : IDataRetentionService
{
    private readonly ApplicationDbContext _context;
    private readonly IConfiguration _configuration;
    private readonly ILogger<DataRetentionService> _logger;

    private const int DefaultChatRetentionDays = 90;
    private const int BatchSize = 500;

    public DataRetentionService(
        ApplicationDbContext context,
        IConfiguration configuration,
        ILogger<DataRetentionService> logger)
    {
        _context = context;
        _configuration = configuration;
        _logger = logger;
    }

    public async Task CleanupExpiredChatMessagesAsync()
    {
        try
        {
            var retentionDays = _configuration.GetValue<int?>("DATA_RETENTION_CHAT_DAYS") ?? DefaultChatRetentionDays;
            var cutoff = DateTimeProvider.UtcNow.AddDays(-retentionDays);

            _logger.LogInformation(
                "Data retention: starting chat message cleanup for messages older than {RetentionDays} days (cutoff: {Cutoff})",
                retentionDays, cutoff);

            var totalDeleted = 0;
            int batchDeleted;

            do
            {
                // Soft-delete in batches to avoid long-running transactions
                var expiredMessages = await _context.ChatMessages
                    .Where(m => m.CreatedAt < cutoff)
                    .Take(BatchSize)
                    .ToListAsync();

                batchDeleted = expiredMessages.Count;

                if (batchDeleted > 0)
                {
                    var now = DateTimeProvider.UtcNow;
                    foreach (var message in expiredMessages)
                    {
                        message.IsDeleted = true;
                        message.DeletedAt = now;
                    }

                    await _context.SaveChangesAsync();
                    totalDeleted += batchDeleted;
                }
            } while (batchDeleted == BatchSize);

            if (totalDeleted > 0)
            {
                _logger.LogInformation(
                    "Data retention: soft-deleted {Count} chat messages older than {Cutoff}",
                    totalDeleted, cutoff);
            }
            else
            {
                _logger.LogDebug("Data retention: no expired chat messages found");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Data retention: failed to cleanup expired chat messages");
        }
    }
}
