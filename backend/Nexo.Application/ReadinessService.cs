using Nexo.Domain;
namespace Nexo.Application;

public sealed record ReadinessResult(string Status);

public sealed class ReadinessService(IDatabaseProbe database)
{
    public async Task<ReadinessResult> CheckAsync(CancellationToken cancellationToken = default) =>
        new(await database.CanConnectAsync(cancellationToken) ? "ready" : "unavailable");
}
