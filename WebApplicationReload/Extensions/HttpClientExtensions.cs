using Polly;
using Polly.Timeout;
using Refit;
using WebApplicationReload.Models;

namespace WebApplicationReload.Extensions;

public static class HttpClientExtensions
{
    public static IServiceCollection AddHttpClientWithDelay<TClient>(
        this IServiceCollection services,
        IConfiguration configuration) where TClient : class
    {
        var url = configuration.GetValue<string>("QuotationServerUrl");

        services
            .AddRefitClient<TClient>()
            .ConfigureHttpClient(client => { client.BaseAddress = new Uri(url!); })
            .AddResilienceHandler("default", (resBuilder, resContext) =>
            {
                resContext.EnableReloads<TimeoutConfiguration>();

                var timeoutConfig = resContext
                    .GetOptions<TimeoutConfiguration>();

                var strategyOptions = new TimeoutStrategyOptions
                {
                    Timeout = TimeSpan.FromSeconds(
                        timeoutConfig.TimeoutSeconds)
                };

                resBuilder.AddTimeout(strategyOptions);
            });

        return services;
    }

    public static IServiceCollection AddHttpClientWithStandard<TClient>(
        this IServiceCollection services,
        IConfiguration configuration) where TClient : class
    {
        var url = configuration.GetValue<string>("ConfigServerUrl");

        services
            .AddRefitClient<TClient>()
            .ConfigureHttpClient(client => { client.BaseAddress = new Uri(url!); })
            .AddStandardResilienceHandler();

        return services;
    }
}