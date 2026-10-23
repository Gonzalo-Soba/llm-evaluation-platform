namespace EvalLab.Api.Evaluation;

public interface IRunStore
{
    /// <summary>Creates or replaces the run.</summary>
    Task SaveAsync(EvaluationRun run, CancellationToken cancellationToken);

    /// <summary>Returns the run, or null if it does not exist.</summary>
    Task<EvaluationRun?> GetAsync(Guid runId, CancellationToken cancellationToken);

    /// <summary>Returns all runs, most recently created first.</summary>
    Task<IReadOnlyList<EvaluationRun>> ListAsync(CancellationToken cancellationToken);
}
