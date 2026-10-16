namespace Nexo.Domain;

public interface IDatabaseProbe
{
    Task<bool> CanConnectAsync(CancellationToken cancellationToken = default);
}
