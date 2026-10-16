using Dapper;
using MySqlConnector;
using Nexo.Domain;
namespace Nexo.Infrastructure;

public sealed class MySqlDatabaseProbe(MySqlConnectionFactory connections, Action<int>? reportFailure = null) : IDatabaseProbe
{
    public async Task<bool> CanConnectAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            await using var connection = await connections.OpenAsync(cancellationToken);
            var command = new CommandDefinition("SELECT @value", new { value = 1 },
                commandTimeout: 3, cancellationToken: cancellationToken);
            return await connection.ExecuteScalarAsync<int>(command) == 1;
        }
        catch (MySqlException exception) { reportFailure?.Invoke(exception.Number); return false; }
        catch (TimeoutException) { reportFailure?.Invoke(-1); return false; }
    }
}
