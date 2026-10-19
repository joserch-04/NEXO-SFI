using System.Text.RegularExpressions;
using Nexo.Domain;
namespace Nexo.Infrastructure;

public sealed class ProductImageFiles(string root):IProductImageFiles
{
 private string Resolve(string name)
 {
  if(!Regex.IsMatch(name,"\\A[a-f0-9]{32}\\.png\\z"))throw new CatalogError(400,"invalid_image_reference");
  return Path.Combine(root,name);
 }
 public async Task<string> Write(byte[] bytes)
 {
  Directory.CreateDirectory(root);var name=Guid.NewGuid().ToString("N")+".png";
  await using var stream=new FileStream(Resolve(name),FileMode.CreateNew,FileAccess.Write,FileShare.None);
  await stream.WriteAsync(bytes);await stream.FlushAsync();return name;
 }
 public async Task<byte[]?> Read(string name)=>File.Exists(Resolve(name))?await File.ReadAllBytesAsync(Resolve(name)):null;
 public Task Delete(string name){File.Delete(Resolve(name));return Task.CompletedTask;}
}
