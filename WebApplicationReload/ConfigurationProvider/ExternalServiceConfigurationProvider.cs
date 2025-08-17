using Microsoft.Extensions.Logging.Abstractions;
using WebApplicationReload.Models;
using WebApplicationReload.Services;

namespace WebApplicationReload.ConfigurationProvider;

public class ExternalServiceConfigurationProvider : Microsoft.Extensions.Configuration.ConfigurationProvider, IDisposable
{
    private readonly Func<IServiceProvider> _serviceProviderFactory;
    private readonly TimeSpan _reloadInterval;
    private IServiceScope _scope;
    private IExternalConfigService _configService;
    private ILogger<ExternalServiceConfigurationProvider> _logger;
    private Timer _reloadTimer;
    private TimeoutConfiguration _lastKnownConfig;

    public ExternalServiceConfigurationProvider(
        Func<IServiceProvider> serviceProviderFactory,
        TimeSpan reloadInterval)
    {
        _serviceProviderFactory =
            serviceProviderFactory ?? throw new ArgumentNullException(nameof(serviceProviderFactory));
        _reloadInterval = reloadInterval;
    }

    public override void Load()
    {
        LoadAsync().GetAwaiter().GetResult();
    }

    private async Task LoadAsync()
    {
        try
        {
            // Cria ServiceProvider apenas quando necessário
            var serviceProvider = _serviceProviderFactory();

            // Cria scope para gerenciar lifecycle
            _scope = serviceProvider.CreateScope();

            // Resolve dependências
            _logger = _scope.ServiceProvider.GetService<ILogger<ExternalServiceConfigurationProvider>>()
                      ?? NullLogger<ExternalServiceConfigurationProvider>.Instance;

            _configService = _scope.ServiceProvider.GetRequiredService<IExternalConfigService>();

            // Carrega configuração inicial
            await ReloadConfigurationAsync();

            // Inicia timer para reload periódico (como AWS SSM faz)
            _reloadTimer = new Timer(async _ => await CheckAndReloadAsync(), null, _reloadInterval, _reloadInterval);

            _logger.LogInformation("External configuration provider initialized with {ReloadInterval} reload interval",
                _reloadInterval);
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to initialize configuration provider");

            // Fallback configuration
            UpdateConfigurationData(new TimeoutConfiguration());
        }
    }

    /// <summary>
    /// Verifica se há mudanças e recarrega se necessário
    /// </summary>
    private async Task CheckAndReloadAsync()
    {
        try
        {
            var currentConfig = await _configService.
                GetTimeoutConfigurationAsync(CancellationToken.None);

            // Verifica se houve mudança
            if (_lastKnownConfig == null || !ConfigurationEquals(_lastKnownConfig, currentConfig))
            {
                _logger.LogInformation(
                    "Configuration change detected, triggering reload. Old: {OldTimeout}s, New: {NewTimeout}s",
                    _lastKnownConfig?.TimeoutSeconds ?? 0, currentConfig.TimeoutSeconds);

                _lastKnownConfig = currentConfig;
                UpdateConfigurationData(currentConfig);

                // Notifica o sistema de configuração sobre a mudança
                OnReload();
            }
            else
            {
                _logger.LogDebug("No configuration changes detected");
            }
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Error during configuration reload check");
        }
    }

    /// <summary>
    /// Força um reload da configuração
    /// </summary>
    private async Task ReloadConfigurationAsync()
    {
        try
        {
            var config = await _configService
                .GetTimeoutConfigurationAsync(CancellationToken.None);
            _lastKnownConfig = config;
            UpdateConfigurationData(config);

            _logger.LogInformation("Configuration loaded: {TimeoutSeconds}s", config.TimeoutSeconds);
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to reload configuration");

            // Mantém configuração anterior ou usa fallback
            if (_lastKnownConfig == null)
            {
                UpdateConfigurationData(new TimeoutConfiguration());
            }
        }
    }

    private void UpdateConfigurationData(TimeoutConfiguration config)
    {
        Data = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["TimeoutSettings:TimeoutSeconds"] = config.TimeoutSeconds.ToString(),
        };
    }

    private static bool ConfigurationEquals(TimeoutConfiguration config1, TimeoutConfiguration config2)
    {
        return config1.TimeoutSeconds == config2.TimeoutSeconds;
    }

    public void Dispose()
    {
        try
        {
            _reloadTimer?.Dispose();
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Error disposing reload timer");
        }
        finally
        {
            _scope?.Dispose();
        }
    }
}