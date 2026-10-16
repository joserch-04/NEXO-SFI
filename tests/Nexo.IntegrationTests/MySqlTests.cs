using Nexo.Infrastructure;
using Xunit;
namespace Nexo.IntegrationTests;
public class MySqlTests
{
    [MySqlFact]
    [Trait("Category", "MySql")]
    public async Task Dapper_executes_parameterized_query_with_application_user()
    {
        var connectionString = Environment.GetEnvironmentVariable("NEXO_TEST_MYSQL")!;
        Assert.True(await new MySqlDatabaseProbe(new MySqlConnectionFactory(connectionString)).CanConnectAsync());
    }
}
public sealed class MySqlFactAttribute : FactAttribute
{
    public MySqlFactAttribute()
    {
        if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("NEXO_TEST_MYSQL")))
            Skip = "Defina NEXO_TEST_MYSQL para ejecutar contra MySQL real.";
    }
}
