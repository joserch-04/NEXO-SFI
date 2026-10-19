using Nexo.Domain;
namespace Nexo.Application;

public sealed record ProductImageInput(uint Version, string? DataUrl);
public sealed class ProductImages(ICatalogStore store,IProductImageFiles files)
{
 public async Task<CatalogProduct> Save(long actor,long id,ProductImageInput input)
 {
  // Reutilizar la validación binaria PNG existente, independiente del nombre/MIME del cliente.
  BusinessIcon.Validate(input.DataUrl);
  var reference=input.DataUrl==null?null:await files.Write(Convert.FromBase64String(input.DataUrl["data:image/png;base64,".Length..]));
  // Si COMMIT pierde su respuesta, conservar el archivo es más seguro que borrar
  // una referencia que la BD pudo haber confirmado. Los huérfanos se depuran fuera del flujo.
  return await store.Execute(async db=>
   {
    var old=await db.Product(id)??throw new CatalogError(404,"catalog_not_found");
    if(old.Version!=input.Version)throw new CatalogError(409,"stale_version");
    await db.SetImage(id,reference);await db.Audit(actor,"productos.imagen",id);
    // Archivos reemplazados se conservan: borrar antes de COMMIT rompería referencias si hay rollback.
    return old with {ImageRef=reference,Version=old.Version+1};
  });
 }
 public async Task<byte[]?> Read(long id)=>await store.Execute(async db=>
 {
  var p=await db.Product(id)??throw new CatalogError(404,"catalog_not_found");
  return p.ImageRef==null?null:await files.Read(p.ImageRef);
 });
}
