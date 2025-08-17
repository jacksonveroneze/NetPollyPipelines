using WebApplicationReload.HttpClients;
using WebApplicationReload.Models;

namespace WebApplicationReload.Services;

public class ExternalConfigService(
    IExternalConfiguration service,
    ILogger<ExternalConfigService> logger)
    : IExternalConfigService
{
    public async Task<TimeoutConfiguration> GetTimeoutConfigurationAsync(
        CancellationToken cancellationToken)
    {
        try
        {
            var config = await service.GetValueAsync(cancellationToken);

            if (config != null)
            {
                logger.LogDebug("Configuration retrieved: {TimeoutSeconds}s", config.TimeoutSeconds);

                return config;
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to get timeout configuration from external service");
        }

        // Fallback configuration
        return new TimeoutConfiguration();
    }
}