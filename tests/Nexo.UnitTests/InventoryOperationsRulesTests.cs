using Nexo.Application;
using Nexo.Domain;
using Xunit;
namespace Nexo.UnitTests;
public class InventoryOperationsRulesTests
{
    private static InventoryBatchInput Input()=>new(Guid.NewGuid().ToString(),"ENTRADA","RECEPCION","Recepción",1,"DOC-1",[new(1,1,null,5,10)]);
    [Fact]
    public void Accepts_valid_receipt_transfer_and_classified_exit()
    {
        InventoryOperationsService.ValidateBatch(Input());
        InventoryOperationsService.ValidateBatch(Input() with{Type="TRASLADO",ReasonCode="TRASLADO",SupplierId=null,Lines=[new(1,1,2,5)]});
        foreach(var reason in InventoryOperationsService.ExitReasons)InventoryOperationsService.ValidateBatch(Input() with{Type="SALIDA",ReasonCode=reason,SupplierId=null,Lines=[new(1,1,null,1)]});
    }
    [Theory]
    [InlineData("supplier")][InlineData("reference")][InlineData("reason")][InlineData("duplicate")][InlineData("quantity")][InlineData("cost")][InlineData("destination")][InlineData("empty")]
    public void Rejects_invalid_document(string defect)
    {
        var input=Input();input=defect switch{
            "supplier"=>input with{SupplierId=null},"reference"=>input with{Reference=""},"reason"=>input with{ReasonCode="DESCONOCIDO"},
            "duplicate"=>input with{Lines=[new(1,1,null,1,10),new(1,1,null,1,10)]},"quantity"=>input with{Lines=[new(1,1,null,0.0001m,10)]},
            "cost"=>input with{Lines=[new(1,1,null,1,-1)]},"destination"=>input with{Type="TRASLADO",ReasonCode="TRASLADO",SupplierId=null,Lines=[new(1,1,1,1)]},_=>input with{Lines=[]}};
        Assert.Equal("invalid_inventory_operation",Assert.Throws<SecurityError>(()=>InventoryOperationsService.ValidateBatch(input)).Code);
    }
}
