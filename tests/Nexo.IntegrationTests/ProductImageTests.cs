using Nexo.Application;
using Nexo.Domain;
using Nexo.Infrastructure;
using Xunit;
namespace Nexo.IntegrationTests;
public class ProductImageTests
{
 [MySqlFact]
 public async Task Upload_replace_remove_reject_content_stale_version_and_reopen_storage()
 {
  var factory=new MySqlConnectionFactory(Environment.GetEnvironmentVariable("NEXO_TEST_MYSQL")!);
  var actor=await new MySqlSecurityStore(factory).Execute(async db=>(await db.Users(0,1)).First().Id);
  var store=new MySqlCatalogStore(factory);var catalog=new CatalogService(store);
  var category=(await catalog.Masters("categorias")).First(x=>x.Active).Id;
  var id=await catalog.SaveProduct(actor,new(0,"IMG-"+Guid.NewGuid().ToString("N"),null,"Imagen F7",category,null,10,1,"UNIDAD",0,false,true,0,[]));
  var root=Path.Combine(Path.GetTempPath(),"nexo-image-test-"+Guid.NewGuid().ToString("N"));
  try
  {
   var files=new ProductImageFiles(root);var service=new ProductImages(store,files);var product=(await catalog.Product(id))!;
   var first=await service.Save(actor,id,new(product.Version,BusinessProfileApiTests.IconFixture));Assert.NotNull(first.ImageRef);Assert.DoesNotContain(root,first.ImageRef);Assert.EndsWith(".png",first.ImageRef);
   Assert.Equal(Convert.FromBase64String(BusinessProfileApiTests.IconFixture.Split(',')[1]),await service.Read(id));
   // Nueva instancia: representa el reinicio del proceso con el mismo almacenamiento.
   Assert.NotNull(await new ProductImages(store,new ProductImageFiles(root)).Read(id));
   var second=await service.Save(actor,id,new(first.Version,BusinessProfileApiTests.IconFixture));Assert.NotEqual(first.ImageRef,second.ImageRef);
   Assert.Equal("stale_version",(await Assert.ThrowsAsync<CatalogError>(()=>service.Save(actor,id,new(first.Version,null)))).Code);
   Assert.Throws<CatalogError>(()=>files.Read("../evil.exe").GetAwaiter().GetResult());
   await Assert.ThrowsAsync<SecurityError>(()=>service.Save(actor,id,new(second.Version,"data:image/png;base64,TVqQAAMAAAAEAAAA")));
   await Assert.ThrowsAsync<SecurityError>(()=>service.Save(actor,id,new(second.Version,"data:image/svg+xml;base64,PHN2Zy8+")));
   await Assert.ThrowsAsync<SecurityError>(()=>service.Save(actor,id,new(second.Version,"data:image/png;base64,"+new string('A',180000))));
   var removed=await service.Save(actor,id,new(second.Version,null));Assert.Null(removed.ImageRef);Assert.Null(await service.Read(id));
  }
  finally
  {
   // Solo directorio temporal aleatorio creado por esta prueba.
   if(Directory.Exists(root))Directory.Delete(root,true);
   var product=(await catalog.Product(id))!;await catalog.SaveProduct(actor,product with{Active=false});
  }
 }
}
