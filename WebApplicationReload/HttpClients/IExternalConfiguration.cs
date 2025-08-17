using Refit;
using WebApplicationReload.Models;

namespace WebApplicationReload.HttpClients;

public interface IExternalConfiguration
{
    [Get("/config")]
    Task<TimeoutConfiguration?> GetValueAsync(
        CancellationToken cancellationToken);
}