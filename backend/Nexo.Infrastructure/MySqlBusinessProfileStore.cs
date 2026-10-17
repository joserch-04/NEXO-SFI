using Dapper;
using Nexo.Domain;
namespace Nexo.Infrastructure;

public sealed class MySqlBusinessProfileStore(MySqlConnectionFactory factory) : IBusinessProfileStore
{
    private const string ReadSql = "SELECT nombre Name,lema Tagline,version Version,icono_data_url IconDataUrl FROM configuracion_negocio WHERE id=1";
    public async Task<BusinessProfile> Read(CancellationToken ct = default)
    {
        await using var connection = await factory.OpenAsync(ct);
        return await connection.QuerySingleAsync<BusinessProfile>(new CommandDefinition(ReadSql, cancellationToken: ct));
    }
    public async Task<BusinessProfile?> Save(long actor, BusinessProfile profile, CancellationToken ct = default)
    {
        await using var connection = await factory.OpenAsync(ct);
        await using var tx = await connection.BeginTransactionAsync(ct);
        var current = await connection.QuerySingleAsync<BusinessProfile>(new CommandDefinition(ReadSql + " FOR UPDATE", transaction: tx, cancellationToken: ct));
        if (current.Version != profile.Version) return null;
        await connection.ExecuteAsync(new CommandDefinition("""
            UPDATE configuracion_negocio SET nombre=@Name,lema=@Tagline,icono_data_url=@IconDataUrl,version=version+1,
            actualizado_por=@actor,actualizado_en=UTC_TIMESTAMP(6) WHERE id=1;
            INSERT INTO auditoria(actor_id,accion,recurso_tipo,recurso_id,fecha,resultado,correlacion,resumen)
            VALUES(@actor,'CONFIGURACION_NEGOCIO_GUARDAR','configuracion_negocio',1,UTC_TIMESTAMP(6),'EXITO',UUID(),'Identidad del negocio actualizada');
            """, new { actor, profile.Name, profile.Tagline, profile.IconDataUrl }, tx, cancellationToken: ct));
        await tx.CommitAsync(ct);
        return profile with { Version = current.Version + 1 };
    }
}
