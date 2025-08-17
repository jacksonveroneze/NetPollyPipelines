namespace WebApplicationReload.ConfigurationProvider;

public class ExternalServiceConfigurationSource(
    Func<IServiceProvider> serviceProviderFactory,
    TimeSpan reloadInterval)
    : IConfigurationSource
{
    private readonly Func<IServiceProvider> _serviceProviderFactory =
        serviceProviderFactory
        ?? throw new ArgumentNullException(nameof(serviceProviderFactory));

    public IConfigurationProvider Build(IConfigurationBuilder builder)
    {
        return new ExternalServiceConfigurationProvider(
            _serviceProviderFactory, reloadInterval);
    }
}