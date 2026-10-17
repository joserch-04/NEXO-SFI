using Nexo.Domain;
using Xunit;
namespace Nexo.UnitTests;
public class InventoryRulesTests
{
    [Theory]
    [InlineData(0, false)] [InlineData(-1, false)] [InlineData(0.001, true)] [InlineData(1.0001, false)]
    public void Quantities_have_at_most_three_decimals(decimal value, bool valid) => Assert.Equal(valid, InventoryRules.ValidQuantity(value));
    [Fact]
    public void Stock_cannot_be_negative() => Assert.Throws<InvalidOperationException>(() => InventoryRules.Balance(10, -11));
    [Fact]
    public void Transfer_conserves_total() => Assert.Equal(40, InventoryRules.Balance(10, -4) + InventoryRules.Balance(30, 4));
}
