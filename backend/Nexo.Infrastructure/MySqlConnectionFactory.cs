using MySqlConnector;
namespace Nexo.Infrastructure;

// Cada operación posee y libera su conexión; MySqlConnector administra el pool.
public sealed class MySqlConnectionFactory(string connectionString)
{
    public async Task<MySqlConnection> OpenAsync(CancellationToken cancellationToken = default)
    {
        var connection = new MySqlConnection(connectionString);
        try
        {
            await connection.OpenAsync(cancellationToken);
            return connection;
        }
        catch
        {
            await connection.DisposeAsync();
            throw;
        }
    }
}
