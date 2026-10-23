namespace EvalLab.Api.Evaluation;

/// <summary>Processes queued runs one at a time, saving the run after each status change.</summary>
public sealed class RunProcessor(
    RunQueue queue,
    IRunStore store,
    IServiceScopeFactory scopeFactory,
    TimeProvider timeProvider,
    ILogger<RunProcessor> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await foreach (var runId in queue.ReadAllAsync(stoppingToken))
        {
            try
            {
                await ProcessAsync(runId, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                // Shutting down: the run stays "running" on disk and is not resumed (see README).
                return;
            }
            catch (Exception ex)
            {
                // An unhandled exception in a BackgroundService stops the whole host, so keep processing.
                logger.LogError(ex, "Run {RunId} could not be processed.", runId);
            }
        }
    }

    private async Task ProcessAsync(Guid runId, CancellationToken stoppingToken)
    {
        var run = await store.GetAsync(runId, stoppingToken);
        if (run is null)
        {
            logger.LogWarning("Run {RunId} was queued but not found in the store.", runId);
            return;
        }

        run = run with { Status = RunStatus.Running, StartedAt = timeProvider.GetUtcNow() };
        await store.SaveAsync(run, stoppingToken);

        try
        {
            // EvaluationRunner depends on the typed HttpClient, which is transient: resolve it per run.
            using var scope = scopeFactory.CreateScope();
            var runner = scope.ServiceProvider.GetRequiredService<EvaluationRunner>();
            run = await runner.RunAsync(run, stoppingToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException || !stoppingToken.IsCancellationRequested)
        {
            // Expected failures (dataset or classifier) are already recorded by the runner; this is a safety net.
            logger.LogError(ex, "Run {RunId} failed unexpectedly.", runId);
            run = run with
            {
                Status = RunStatus.Failed,
                CompletedAt = timeProvider.GetUtcNow(),
                FailureReason = $"Unexpected error: {ex.Message}",
            };
        }

        await store.SaveAsync(run, stoppingToken);
        logger.LogInformation("Run {RunId} finished with status {Status}.", runId, run.Status);
    }
}
