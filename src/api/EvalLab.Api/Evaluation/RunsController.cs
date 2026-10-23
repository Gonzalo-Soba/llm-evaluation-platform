using EvalLab.Api.Classification;
using EvalLab.Api.Datasets;
using Microsoft.AspNetCore.Mvc;

namespace EvalLab.Api.Evaluation;

[ApiController]
[Route("api/runs")]
public sealed class RunsController(
    IRunStore store,
    RunQueue queue,
    DatasetResolver datasetResolver,
    TimeProvider timeProvider) : ControllerBase
{
    /// <summary>Queues an evaluation run. It is processed in the background; poll GET /api/runs/{id} for the result.</summary>
    [HttpPost]
    public async Task<ActionResult<CreateRunResponse>> Create(CreateRunRequest request, CancellationToken cancellationToken)
    {
        string path;
        try
        {
            path = datasetResolver.ResolvePath(request.Dataset);
        }
        catch (ArgumentException ex)
        {
            return InvalidRequest("dataset", ex.Message);
        }

        if (!System.IO.File.Exists(path))
        {
            return InvalidRequest("dataset", $"Dataset '{request.Dataset}' does not exist.");
        }

        if (request.Split is not null && !DatasetLoader.Splits.Contains(request.Split))
        {
            return InvalidRequest("split", $"Unknown split '{request.Split}'. Expected one of: {string.Join(", ", DatasetLoader.Splits)}.");
        }

        var run = EvaluationRun.CreateQueued(
            request.Dataset, request.Split, request.Config ?? ClassifierConfig.Default, timeProvider.GetUtcNow());

        // Saved before queuing, so the run can always be read once its id is returned.
        await store.SaveAsync(run, cancellationToken);
        await queue.EnqueueAsync(run.RunId, cancellationToken);

        return AcceptedAtAction(nameof(Get), new { id = run.RunId }, new CreateRunResponse(run.RunId, run.Status));
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<EvaluationRun>> Get(Guid id, CancellationToken cancellationToken)
    {
        var run = await store.GetAsync(id, cancellationToken);
        return run is null ? NotFound() : run;
    }

    /// <summary>Lists all runs, newest first, without predictions.</summary>
    [HttpGet]
    public async Task<IReadOnlyList<RunSummary>> List(CancellationToken cancellationToken)
    {
        var runs = await store.ListAsync(cancellationToken);
        return runs.Select(RunSummary.From).ToList();
    }

    private ActionResult InvalidRequest(string field, string message)
    {
        ModelState.AddModelError(field, message);
        return ValidationProblem(ModelState);
    }
}
