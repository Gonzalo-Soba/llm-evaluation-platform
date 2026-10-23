using System.Threading.Channels;

namespace EvalLab.Api.Evaluation;

/// <summary>In-memory queue of run ids waiting to be processed. Its contents are lost when the process stops.</summary>
public sealed class RunQueue
{
    private readonly Channel<Guid> channel = Channel.CreateUnbounded<Guid>(new UnboundedChannelOptions { SingleReader = true });

    public ValueTask EnqueueAsync(Guid runId, CancellationToken cancellationToken) =>
        channel.Writer.WriteAsync(runId, cancellationToken);

    public IAsyncEnumerable<Guid> ReadAllAsync(CancellationToken cancellationToken) =>
        channel.Reader.ReadAllAsync(cancellationToken);
}
