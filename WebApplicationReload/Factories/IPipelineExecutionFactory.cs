namespace WebApplicationReload.Factories;

public interface IPipelineExecutionFactory
{
    Task<T> ExecuteAsync<T>(string pipelineName, Func<CancellationToken, ValueTask<T>> operation, CancellationToken cancellationToken = default);
}