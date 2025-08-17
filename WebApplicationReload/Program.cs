using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;
using Polly;
using Polly.Timeout;
using WebApplicationReload.ConfigurationProvider;
using WebApplicationReload.Extensions;
using WebApplicationReload.Factories;
using WebApplicationReload.HttpClients;
using WebApplicationReload.Models;
using WebApplicationReload.Services;

namespace WebApplicationReload;

public class Program
{
    public static void Main(string[] args)
    {
        var builder = WebApplication.CreateBuilder(args);

        builder.Services.AddHttpClientWithDelay<IExternalQuotation>(
            builder.Configuration);

        builder.Services.AddHttpClientWithStandard<IExternalConfiguration>(
            builder.Configuration);

        builder.Services.TryAddScoped<IExternalConfigService, ExternalConfigService>();

        builder.Services.TryAddScoped<IPipelineExecutionFactory, PipelineExecutionFactory>();

        // 2. Adiciona configuration provider - ELE cuida do reload!
        builder.Configuration.AddExternalServiceByEnvironment(
            () => builder.Services.BuildServiceProvider(),
            builder.Environment);

        // 3. Configura options para bind automático
        builder.Services.Configure<TimeoutConfiguration>(
            builder.Configuration.GetSection("TimeoutSettings"));

        // 4. Configura pipeline Polly com dynamic reload
        builder.Services.AddResiliencePipeline("timeout-pipeline", (pipelineBuilder, context) =>
        {
            // Habilita reloads automáticos - Polly detecta mudanças do provider
            context.EnableReloads<TimeoutConfiguration>();

            var timeoutConfig = context.GetOptions<TimeoutConfiguration>();

            var strategyOptions = new TimeoutStrategyOptions
            {
                Timeout = TimeSpan.FromSeconds(timeoutConfig.TimeoutSeconds)
            };

            pipelineBuilder.AddTimeout(strategyOptions);

            var logger = context.ServiceProvider.GetRequiredService<ILogger<Program>>();
            logger.LogInformation("Polly pipeline configured with timeout: {TimeoutSeconds}s",
                timeoutConfig.TimeoutSeconds);
        });

        var app = builder.Build();

        // Config atual
        app.MapGet("/config/current", (IOptionsSnapshot<TimeoutConfiguration> options) =>
        {
            return Results.Ok(new
            {
                Config = options.Value
            });
        });

        // Simulate
        app.MapGet("/simulate-pipeline-delay/{delay:int}", async (
            int delay,
            IPipelineExecutionFactory pipelineFactory,
            CancellationToken cancellationToken) =>
        {
            try
            {
                var result = await pipelineFactory.ExecuteAsync("get-quotation-direct",
                    async t =>
                    {
                        await Task.Delay(TimeSpan.FromSeconds(delay), t);

                        return delay;
                    },
                    cancellationToken);

                return Results.Ok($"Ok - {result}");
            }
            catch (TimeoutRejectedException)
            {
                return Results.BadRequest("Operation timed out");
            }
        });

        // Simulate
        app.MapGet("/quotation/{ticker:required}", async (
            string ticker,
            IExternalQuotation service,
            CancellationToken cancellationToken) =>
        {
            try
            {
                var result = await service
                    .GetValueAsync(ticker, cancellationToken);

                return Results.Ok($"Ok - {result.Value}");
            }
            catch (TimeoutRejectedException)
            {
                return Results.BadRequest("Operation timed out");
            }
        });

        // Simulate
        app.MapGet("/quotation-pipeline/{ticker:required}", async (
            string ticker,
            IPipelineExecutionFactory pipelineFactory,
            IExternalQuotation service,
            CancellationToken cancellationToken) =>
        {
            try
            {
                var result = await pipelineFactory.ExecuteAsync("get-quotation-direct",
                    async _ => await service.GetValueAsync(
                        ticker, cancellationToken),
                    cancellationToken);

                return Results.Ok($"Ok - {result.Value}");
            }
            catch (TimeoutRejectedException)
            {
                return Results.BadRequest("Operation timed out");
            }
        });

        app.Run();
    }
}