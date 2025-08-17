using System.Collections.Concurrent;
using Polly;
using Polly.Retry;
using Polly.Timeout;
using WebApplicationReload.Services;

namespace WebApplicationReload.Factories;

public class PipelineExecutionFactory : IPipelineExecutionFactory
{
    private readonly IExternalConfigService _configService;
    private readonly ILogger<PipelineExecutionFactory> _logger;

    private static readonly ConcurrentDictionary<string, (ResiliencePipeline Pipeline, DateTime Created, int TimeoutSeconds)>
        Cache = new();

    private readonly TimeSpan _cacheExpiry = TimeSpan.FromSeconds(30);

    public PipelineExecutionFactory(IExternalConfigService configService, ILogger<PipelineExecutionFactory> logger)
    {
        _configService = configService;
        _logger = logger;
    }

    public async Task<T> ExecuteAsync<T>(string pipelineName, Func<CancellationToken, ValueTask<T>> operation,
        CancellationToken cancellationToken = default)
    {
        var pipeline = await GetOrCreatePipelineAsync(pipelineName);

        _logger.LogDebug("Executing operation '{PipelineName}'", pipelineName);

        try
        {
            return await pipeline.ExecuteAsync(operation, cancellationToken);
        }
        catch (TimeoutRejectedException ex)
        {
            _logger.LogWarning("Operation '{PipelineName}' timed out after {Timeout}s",
                pipelineName, ex.Timeout.TotalSeconds);
            throw;
        }
    }

    public async Task ExecuteAsync(string pipelineName, Func<CancellationToken, ValueTask> operation,
        CancellationToken cancellationToken = default)
    {
        var pipeline = await GetOrCreatePipelineAsync(pipelineName);

        _logger.LogDebug("Executing operation '{PipelineName}'", pipelineName);

        try
        {
            await pipeline.ExecuteAsync(operation, cancellationToken);
        }
        catch (TimeoutRejectedException ex)
        {
            _logger.LogWarning("Operation '{PipelineName}' timed out after {Timeout}s",
                pipelineName, ex.Timeout.TotalSeconds);
            throw;
        }
    }

    private async Task<ResiliencePipeline> GetOrCreatePipelineAsync(string pipelineName)
    {
        var cacheKey = $"{pipelineName}_nongeneric";

        // 1. Busca configuração atual
        var currentConfig = await _configService.GetTimeoutConfigurationAsync(CancellationToken.None);

        // 2. Verifica cache
        if (Cache.TryGetValue(cacheKey, out var cached))
        {
            var isExpired = DateTime.UtcNow - cached.Created > _cacheExpiry;
            var configChanged = cached.TimeoutSeconds != currentConfig.TimeoutSeconds;

            if (!isExpired && !configChanged && cached.Pipeline is ResiliencePipeline cachedPipeline)
            {
                _logger.LogDebug("Using cached pipeline '{PipelineName}' with timeout {Timeout}s",
                    pipelineName, cached.TimeoutSeconds);
                return cachedPipeline;
            }

            if (configChanged)
            {
                _logger.LogInformation("Configuration changed for '{PipelineName}': {OldTimeout}s → {NewTimeout}s",
                    pipelineName, cached.TimeoutSeconds, currentConfig.TimeoutSeconds);
            }
        }

        // 3. Cria nova pipeline
        _logger.LogInformation("Creating pipeline '{PipelineName}' with timeout: {Timeout}s",
            pipelineName, currentConfig.TimeoutSeconds);

        var newPipeline = new ResiliencePipelineBuilder()
            .AddTimeout(new TimeoutStrategyOptions
            {
                Timeout = TimeSpan.FromSeconds(currentConfig.TimeoutSeconds)
            })
            .AddRetry(new RetryStrategyOptions
            {
                MaxRetryAttempts = 2,
                BackoffType = DelayBackoffType.Exponential,
                UseJitter = true,
                ShouldHandle = new PredicateBuilder()
                    .Handle<HttpRequestException>()
                    .Handle<TaskCanceledException>()
            })
            .Build();

        // 4. Atualiza cache
        Cache.AddOrUpdate(cacheKey,
            (newPipeline, DateTime.UtcNow, currentConfig.TimeoutSeconds),
            (key, old) => (newPipeline, DateTime.UtcNow, currentConfig.TimeoutSeconds));

        return newPipeline;
    }
}