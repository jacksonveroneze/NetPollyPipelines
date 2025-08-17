using WebApplicationReload.Models;

namespace WebApplicationReload.Services;

public interface IExternalConfigService
{
    Task<TimeoutConfiguration> GetTimeoutConfigurationAsync(
        CancellationToken cancellationToken);
}