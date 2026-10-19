using Nexo.Application;
using Nexo.Domain;
using Xunit;
namespace Nexo.UnitTests;
public class PosRulesTests
{
 [Theory]
 [InlineData("EFECTIVO")] [InlineData("TARJETA")] [InlineData("TRANSFERENCIA")]
 public void Exact_payment(string method) {var p=Assert.Single(PosService.Allocate(25,[new(method,25,method=="EFECTIVO"?null:"00001234")]));Assert.Equal(25,p.Applied);Assert.Equal(0,p.Change);Assert.Equal(method=="EFECTIVO"?null:"00001234",p.Reference);}
 [Fact] public void Mixed_payment_change_only_in_cash()
 {var p=PosService.Allocate(75,[new("EFECTIVO",100),new("TARJETA",25,"NFC-001"),new("TRANSFERENCIA",10,"TRF-001")]);Assert.Equal(75,p.Sum(x=>x.Applied));Assert.Equal(60,p.Single(x=>x.Method=="EFECTIVO").Change);Assert.Equal("NFC-001",p.Single(x=>x.Method=="TARJETA").Reference);Assert.Equal("TRF-001",p.Single(x=>x.Method=="TRANSFERENCIA").Reference);}
 [Theory]
 [InlineData("EFECTIVO",24,"payment_incomplete")]
 [InlineData("TARJETA",30,"invalid_change")]
 [InlineData("TRANSFERENCIA",30,"invalid_change")]
 [InlineData("OTRO",25,"invalid_pos")]
 public void Invalid_payments(string method,decimal amount,string code)=>Assert.Equal(code,Assert.Throws<CatalogError>(()=>PosService.Allocate(25,[new(method,amount,method=="EFECTIVO"?null:"TEST-001")])).Code);
 [Fact] public void Rejects_duplicates_negative_and_excess_non_cash()
 {
  Assert.Throws<CatalogError>(()=>PosService.Allocate(20,[new("EFECTIVO",10),new("EFECTIVO",10)]));
  Assert.Throws<CatalogError>(()=>PosService.Allocate(20,[new("EFECTIVO",-10)]));
  Assert.Throws<CatalogError>(()=>PosService.Allocate(20,[new("EFECTIVO",10),new("TARJETA",25,"TEST-001")]));
 }
 [Fact] public void Rounding_uses_two_decimals_away_from_zero(){Assert.Equal(18.13m,PosService.Round(18.125m));Assert.False(PosService.Money(1.001m));}
 [Fact] public void Line_tax_uses_discounted_base_and_rounds_each_line()
 {
  var product=new CatalogProduct(1,"TEST",null,"Prueba aritmética",1,null,18.125m,0,"UNIDAD",0,false,true,1,[]);
  var line=PosService.CalculateLine(product,new(1,3,4.38m),.10m);
  Assert.Equal(54.38m,line.Subtotal);Assert.Equal(5,line.Tax);Assert.Equal(55,line.Total);
  Assert.Throws<CatalogError>(()=>PosService.CalculateLine(product,new(1,1,50),.10m));
  Assert.Throws<CatalogError>(()=>PosService.CalculateLine(product,new(1,1,0),1.1m));
 }
 [Theory]
 [InlineData("TARJETA",null)] [InlineData("TRANSFERENCIA",null)]
 [InlineData("TARJETA","")] [InlineData("TRANSFERENCIA","   ")]
 public void Non_cash_requires_reference(string method,string? reference)=>Assert.Equal("payment_reference_required",Assert.Throws<CatalogError>(()=>PosService.Allocate(25,[new(method,25,reference)])).Code);
 [Theory]
 [InlineData("TARJETA","REF\n123")] [InlineData("TRANSFERENCIA","<script>")]
 public void Invalid_reference_is_rejected(string method,string reference)=>Assert.Equal("invalid_payment_reference",Assert.Throws<CatalogError>(()=>PosService.Allocate(25,[new(method,25,reference)])).Code);
 [Fact] public void References_preserve_leading_zeroes_trim_and_enforce_length()
 {
  Assert.Equal("00001-AB/20:3",Assert.Single(PosService.Allocate(25,[new("TARJETA",25," 00001-AB/20:3 ")])).Reference);
  Assert.Throws<CatalogError>(()=>PosService.Allocate(25,[new("TRANSFERENCIA",25,new string('A',101))]));
  Assert.Equal("cash_reference_not_allowed",Assert.Throws<CatalogError>(()=>PosService.Allocate(25,[new("EFECTIVO",25,"REF-001")])).Code);
 }
 [Fact] public void Null_reference_preserves_legacy_serialization_for_idempotency()
 {
  var old=System.Text.Json.JsonSerializer.Serialize(new{Method="TARJETA",Received=25m});
  Assert.Equal(old,System.Text.Json.JsonSerializer.Serialize(new PaymentInput("TARJETA",25)));
 }
}
