using Nexo.Domain;
namespace Nexo.Application;

public sealed class BusinessProfileService(IBusinessProfileStore store)
{
    public Task<BusinessProfile> Read(CancellationToken ct = default) => store.Read(ct);
    public async Task<BusinessProfile> Save(long actor, BusinessProfile input, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(input.Name) || input.Name.Trim().Length > 120 || input.Tagline is null || input.Tagline.Trim().Length > 160
            || input.Name.Any(char.IsControl) || input.Tagline.Any(char.IsControl))
            throw new SecurityError(400, "invalid_business_profile");
        BusinessIcon.Validate(input.IconDataUrl);
        var profile = input with { Name = input.Name.Trim(), Tagline = input.Tagline.Trim() };
        return await store.Save(actor, profile, ct) ?? throw new SecurityError(409, "stale_version");
    }
}
