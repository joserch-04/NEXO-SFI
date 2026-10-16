using Nexo.Application;
using Nexo.Domain;
using Xunit;
namespace Nexo.UnitTests;
public class ReadinessTests
{
    [Theory]
    [InlineData(true, "ready")]
    [InlineData(false, "unavailable")]
    public async Task Maps_database_availability(bool available, string expected)
    {
        var result = await new ReadinessService(new Probe(available)).CheckAsync();
        Assert.Equal(expected, result.Status);
    }
    [Fact]
    public async Task Propagates_cancellation()
    {
        using var source = new CancellationTokenSource();
        source.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            new ReadinessService(new Probe(true)).CheckAsync(source.Token));
    }
    private sealed class Probe(bool available) : IDatabaseProbe
    {
        public Task<bool> CanConnectAsync(CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(available);
        }
    }
}
