namespace Nexo.Domain;

// Perfil compartido de la única tienda de esta instalación; NEXO es el software.
public sealed record BusinessProfile(string Name, string Tagline, uint Version)
{
    public string? IconDataUrl { get; init; }
    public BusinessProfile() : this("", "", 0) { }
}
public interface IBusinessProfileStore
{
    Task<BusinessProfile> Read(CancellationToken ct = default);
    // null significa que otro administrador guardó una versión más reciente.
    Task<BusinessProfile?> Save(long actor, BusinessProfile profile, CancellationToken ct = default);
}
