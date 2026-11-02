using System.Text.Json;
using LKvitai.MES.Modules.Sales.Contracts.OrderEntry;
using Xunit;
namespace LKvitai.MES.Tests.Sales.Auth;
public sealed class CalculationRevisionCompatibilityTests
{
    [Fact]
    public void Older_pending_configuration_keeps_the_receipt_hash_input_shape()
    {
        var input=new ProductConfigurationInput(-1025240863,800,1500,1,6026,"D",20.8526m,true,null,new string?[12]);
        // v4's canonical serialized payload predates CalculationRevision. Reading
        // and reserializing it must not inject a null field into the receipt hash.
        var older=JsonSerializer.Serialize(new{input.TypeId,input.WidthMm,input.HeightMm,input.Quantity,input.ColourId,input.Control,input.UnitPrice,input.UseListPrice,input.Note,input.Parameters,input.RecalculateParameters,input.ManualParameterSlots});
        var decoded=JsonSerializer.Deserialize<ProductConfigurationInput>(older)!;
        Assert.Equal(older,JsonSerializer.Serialize(decoded));
        Assert.Contains("\"CalculationRevision\":\"reviewed\"",JsonSerializer.Serialize(decoded with{CalculationRevision="reviewed"}));
    }
}
