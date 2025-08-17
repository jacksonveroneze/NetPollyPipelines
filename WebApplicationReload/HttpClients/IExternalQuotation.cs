using Refit;
using WebApplicationReload.Models;

namespace WebApplicationReload.HttpClients;

public interface IExternalQuotation
{
    [Get("/quotations")]
    Task<QuotationValue> GetValueAsync(
        [AliasAs("ticker")] string ticker,
        CancellationToken cancellationToken);
}