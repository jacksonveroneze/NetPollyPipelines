namespace WebApplicationReload.ConfigurationProvider;

public static class ExternalServiceConfigurationExtensions
{
    /// <summary>
    /// Adiciona configuration provider que carrega de serviço externo com reload automático
    /// </summary>
    /// <param name="builder">Configuration builder</param>
    /// <param name="serviceProviderFactory">Factory que cria ServiceProvider quando necessário</param>
    /// <param name="reloadInterval">Intervalo para verificar mudanças (padrão: 1 minuto)</param>
    /// <returns>Configuration builder para chaining</returns>
    private static IConfigurationBuilder AddExternalService(
        this IConfigurationBuilder builder,
        Func<IServiceProvider> serviceProviderFactory,
        TimeSpan? reloadInterval = null)
    {
        var interval = reloadInterval ?? TimeSpan.FromMinutes(1);
        return builder.Add(new ExternalServiceConfigurationSource(serviceProviderFactory, interval));
    }

    /// <summary>
    /// Adiciona configuration provider com diferentes intervalos por ambiente
    /// </summary>
    public static IConfigurationBuilder AddExternalServiceByEnvironment(
        this IConfigurationBuilder builder,
        Func<IServiceProvider> serviceProviderFactory,
        IWebHostEnvironment environment)
    {
        var reloadInterval = environment.IsDevelopment()
            ? TimeSpan.FromSeconds(15) // Dev: reload rápido
            : TimeSpan.FromMinutes(5); // Prod: reload moderado

        return builder.AddExternalService(serviceProviderFactory, reloadInterval);
    }
}