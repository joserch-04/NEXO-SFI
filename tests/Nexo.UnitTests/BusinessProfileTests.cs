using Nexo.Application;
using Nexo.Domain;
using Xunit;
namespace Nexo.UnitTests;
public class BusinessProfileTests
{
    private sealed class Store : IBusinessProfileStore
    {
        public BusinessProfile Value = new("Mi tienda", "", 1);
        public Task<BusinessProfile> Read(CancellationToken ct = default) => Task.FromResult(Value);
        public Task<BusinessProfile?> Save(long actor, BusinessProfile profile, CancellationToken ct = default)
        {
            if (Value.Version != profile.Version) return Task.FromResult<BusinessProfile?>(null);
            Value = profile with { Version = Value.Version + 1 }; return Task.FromResult<BusinessProfile?>(Value);
        }
    }
    [Theory]
    [InlineData("", "")]
    [InlineData("   ", "")]
    [InlineData("Tienda\nNombre", "")]
    [InlineData("Tienda", "Lema\ncon salto")]
    public async Task Rejects_empty_or_control_characters(string name, string tagline)
    {
        var service = new BusinessProfileService(new Store());
        Assert.Equal("invalid_business_profile", (await Assert.ThrowsAsync<SecurityError>(() => service.Save(1, new(name, tagline, 1)))).Code);
    }
    [Fact]
    public async Task Trims_and_preserves_identity_across_reads()
    {
        var service = new BusinessProfileService(new Store());
        var saved = await service.Save(1, new("  Tienda El Encuentro  ", "  Cerca de vos  ", 1));
        Assert.Equal("Tienda El Encuentro", saved.Name); Assert.Equal("Cerca de vos", saved.Tagline);
        Assert.Equal(2u, saved.Version); Assert.Equal(saved, await service.Read());
    }
    [Fact]
    public async Task Rejects_stale_version_and_overlong_fields()
    {
        var service = new BusinessProfileService(new Store());
        Assert.Equal("stale_version", (await Assert.ThrowsAsync<SecurityError>(() => service.Save(1, new("Tienda", "", 0)))).Code);
        Assert.Equal("invalid_business_profile", (await Assert.ThrowsAsync<SecurityError>(() => service.Save(1, new(new string('a', 121), "", 1)))).Code);
        Assert.Equal("invalid_business_profile", (await Assert.ThrowsAsync<SecurityError>(() => service.Save(1, new("Tienda", new string('a', 161), 1)))).Code);
        Assert.Equal("Mi tienda", (await service.Read()).Name);
    }
}
