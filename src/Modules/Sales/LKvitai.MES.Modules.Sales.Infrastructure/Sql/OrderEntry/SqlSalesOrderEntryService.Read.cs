using System.Data;
using System.Security.Cryptography;
using System.Text.Json;
using LKvitai.MES.Modules.Sales.Contracts.OrderEntry;
using Microsoft.Data.SqlClient;
namespace LKvitai.MES.Modules.Sales.Infrastructure.Sql;
public sealed partial class SqlSalesOrderEntryService
{
    private async Task<OrderEditorSnapshot?> ReadAsync(SqlConnection c,SqlTransaction tx,long id,CancellationToken ct)
    {
        var headerRows=await Rows(c,tx,"""
            SELECT u.*,s.UzsakymoSerijaPav SeriesCode,s.FilialoID SeriesBranchId,
                customer.UzsakovoPavadinimas CustomerName,place.VietaPavadinimas StoreName,
                manager.DarbuotojoPavadinimas ManagerName,consultant.DarbuotojoPavadinimas ConsultantName,
                state.Busena HeaderStateName,state.bAllowEditOrder HeaderCanEdit,
                CAST(ROUND(((ISNULL(u.mOrderDiscountableBlindsCost,0)+ISNULL(u.mOrderAccessoriesCost,0))/100)*(100-ISNULL(u.Nuolaida,0))+ISNULL(u.mOrderNONDiscountableBlindsCost,0),2) AS money) CurrentFinalTotal
            FROM dbo.Uzsakymai u WITH(UPDLOCK,HOLDLOCK)
            JOIN dbo.Zinynas_UzsakymoSerijos s ON s.UzsakymoSerijaID=u.UzsakymoSerija
            LEFT JOIN dbo.Zinynas_uzsakovai customer ON customer.UzsakovoID=u.UzsakovoID
            LEFT JOIN dbo.Zinynas_vieta place ON place.VietaID=u.VietaID
            LEFT JOIN dbo.Zinynas_darbuotojai manager ON manager.DarbuotojoID=u.VadybininkoID
            LEFT JOIN dbo.Zinynas_darbuotojai consultant ON consultant.DarbuotojoID=u.KonsultantoID
            LEFT JOIN dbo.Zinynas_busenos state ON state.BusenosID=u.StateID
            WHERE u.UzsakymasID=@id AND EXISTS(SELECT 1 FROM dbo.Loc_leidimas() access WHERE access.UKS=s.FilialoID);
            """,ct,("@id",id));
        if(headerRows.Count==0)return null;
        var h=headerRows.Single();
        var noteHistory=await LegacyOrderNoteHistory.ReadAsync(c,tx,id,30,ct);
        var productRows=await Rows(c,tx,"""
            SELECT pg.*,t.TipoPavadinimas TypeName,t.Rusis CategoryId,r.RusiesPavadinimas CategoryName,
                t.Gamininamas IsManufactured,t.KainosMatas PriceUnitId,
                colour.sCodeInternal ColourCode,state.Busena ProductStateName,
                state.bAllowEditBlindFlag ProductCanEdit,state.GalimaRedaguotiGamini ProductCanDelete
            FROM dbo.ParduotiGaminiai pg WITH(UPDLOCK,HOLDLOCK)
            LEFT JOIN dbo.Zinynas_tipai t ON t.TipoID=pg.ParduotoGaminioTipoID
            LEFT JOIN dbo.Zinynas_rusys r ON r.RusiesID=t.Rusis
            LEFT JOIN dbo.TBD_Components colour ON colour.iComponentID=pg.Spalva
            LEFT JOIN dbo.Zinynas_busenos state ON state.BusenosID=pg.BusenosID
            WHERE pg.UzsakymoID=@id ORDER BY pg.ParduotoGaminioID;
            """,ct,("@id",id));
        var accessories=await Rows(c,tx,"""
            SELECT p.*,ref.PriedoPavadinimas DefinitionName,unit.Matas UnitName
            FROM dbo.ParduotiPriedai p WITH(UPDLOCK,HOLDLOCK)
            JOIN dbo.ParduotiGaminiai pg ON pg.ParduotoGaminioID=p.ParduotoGaminioID
            LEFT JOIN dbo.Zinynas_priedai ref ON ref.PriedoID=p.PrduotoPriedoID
            LEFT JOIN dbo.Zinynas_matai unit ON unit.MatoID=ref.PriedoMatas
            WHERE pg.UzsakymoID=@id ORDER BY p.ParduotoGaminioID,p.PrduotoPriedoID;
            """,ct,("@id",id));
        var addresses=await Rows(c,tx,"SELECT * FROM dbo.TCR_OrderAddresses WITH(UPDLOCK,HOLDLOCK) WHERE iOrderID=@id",ct,("@id",id));
        var duties=await Rows(c,tx,"""
            SELECT assignment.*,d.sName DutyName,e.DarbuotojoPavadinimas EmployeeName
            FROM dbo.TCR_OrdersDutiesEmployees assignment WITH(UPDLOCK,HOLDLOCK)
            LEFT JOIN dbo.TBD_Duties d ON d.iDutyID=assignment.iDutyID
            LEFT JOIN dbo.Zinynas_darbuotojai e ON e.DarbuotojoID=assignment.iEmployeeID
            WHERE assignment.iOrderID=@id ORDER BY assignment.iDutyID,assignment.iEmployeeID;
            """,ct,("@id",id));
        var payments=await Rows(c,tx,"""
            SELECT payment.*,currency.ValiutosKodas CurrencyCode,method.MokejimoTipas PaymentMethodName,
                employee.DarbuotojoPavadinimas PaymentEmployeeName,register.EKA_NAME PaymentRegisterName
            FROM dbo.Mokejimai payment WITH(UPDLOCK,HOLDLOCK)
            LEFT JOIN dbo.Zinynas_valiutos currency ON currency.ValiutosID=payment.ValiutosID
            LEFT JOIN dbo.Zinynas_mokejimotipai method ON method.MokejimoTipasID=payment.MokeijimoTipasID
            LEFT JOIN dbo.Zinynas_darbuotojai employee ON employee.DarbuotojoID=payment.EmployeeID
            LEFT JOIN dbo.Zinynas_EKA register ON register.EKA_ID=payment.EKA_ID
            WHERE payment.UzsakymasID=@id ORDER BY payment.MokejimoID;
            """,ct,("@id",id));
        var revision=Convert.ToHexString(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(new{Version=1,Header=h,Products=productRows,Accessories=accessories,Addresses=addresses,Duties=duties,Payments=payments})));
        var discountable=(D(h,"mOrderDiscountableBlindsCost")??0)+(D(h,"mOrderAccessoriesCost")??0);
        var total=D(h,"CurrentFinalTotal")!.Value;
        var header=new OrderHeaderSnapshot(id,$"{S(h,"SeriesCode")}-{I(h,"UzsakymoNumeris")}-{S(h,"UzsakymoGrupe")}",I(h,"UzsakymoSerija")!.Value,I(h,"SeriesBranchId")!.Value,
            Date(h,"UzsakymoData"),Date(h,"UzsakymoTerminas"),I(h,"UzsakovoID"),S(h,"CustomerName"),I(h,"VietaID"),S(h,"StoreName"),I(h,"VadybininkoID"),S(h,"ManagerName"),I(h,"KonsultantoID"),S(h,"ConsultantName"),I(h,"PriorityID"),Date(h,"Montavimo_data"),S(h,"sNotes"),D(h,"Nuolaida"),I(h,"StateID"),S(h,"HeaderStateName"),B(h,"Sugadintas"),!B(h,"Sugadintas")&&B(h,"HeaderCanEdit"),D(h,"UzsakymoVerteSuNuolaida"),total,D(h,"Paid")??0,discountable,D(h,"mOrderNONDiscountableBlindsCost")??0,noteHistory);
        var products=productRows.Select(p=>new ProductSnapshot(L(p,"ParduotoGaminioID")!.Value,I(p,"ParduotoGaminioTipoID"),I(p,"CategoryId"),S(p,"TypeName"),S(p,"CategoryName"),I(p,"Plotis"),I(p,"Aukstis"),D(p,"Kiekis"),I(p,"Spalva")!.Value,S(p,"ColourCode"),S(p,"Valdymas"),D(p,"Kaina"),S(p,"Pastabos"),Enumerable.Range(1,12).Select(i=>S(p,$"PPP{i:D2}")).ToArray(),I(p,"BusenosID"),S(p,"ProductStateName"),!header.Cancelled&&B(p,"ProductCanEdit"),!header.Cancelled&&B(p,"ProductCanDelete") && I(p,"BusenosID") is 4 or 7,p["bIsTrustedPriceFlag"] is null?null:B(p,"bIsTrustedPriceFlag"),D(p,"mBlindFinalCost"),accessories.Where(a=>L(a,"ParduotoGaminioID")==L(p,"ParduotoGaminioID")).Select(a=>new AccessorySnapshot(I(a,"PrduotoPriedoID")!.Value,L(a,"ParduotoGaminioID")!.Value,S(a,"DefinitionName"),S(a,"UnitName"),D(a,"ParduotoPriedoKiekis"),D(a,"ParduotoPriedoKaina"))).ToArray(),D(p,"mBlindAccessoryFinalCost"),p["IsManufactured"] is null?null:B(p,"IsManufactured"),I(p,"PriceUnitId"))).ToArray();
        var address=addresses.SingleOrDefault();
        var paymentMethodIds=(await Rows(c,tx,"SELECT MokejimoTipasID FROM dbo.Zinynas_mokejimotipai WITH(HOLDLOCK) ORDER BY MokejimoTipas,MokejimoTipasID",ct)).Select(m=>I(m,"MokejimoTipasID")!.Value).ToArray();
        return new OrderEditorSnapshot(header,products,address is null?null:new OrderAddressInput(I(address,"iStreetID")!.Value,S(address,"sBuildingNo"),S(address,"sRoomNo"),S(address,"sPhoneNo"),S(address,"sNotes")),duties.Select(d=>new DutySnapshot(I(d,"iDutyID")!.Value,I(d,"iEmployeeID")!.Value,S(d,"DutyName"),S(d,"EmployeeName"),DT(d,"dtDate")!.Value,S(d,"sNotes"))).ToArray(),revision,Scope(header.BranchId,header.SeriesId),[],
            Scope(header.BranchId,header.SeriesId)&&options.PaymentsEnabled&&options.PaymentCurrencyId.HasValue,
            Scope(header.BranchId,header.SeriesId)&&options.ProductionConfirmationEnabled&&!string.IsNullOrWhiteSpace(options.ProductionDocumentPrefix),
            paymentMethodIds,
            payments.Select(p=>new OrderPaymentSnapshot(L(p,"MokejimoID")!.Value,Date(p,"Data"),D(p,"Suma")!.Value,I(p,"MokeijimoTipasID"),I(p,"EmployeeID"),I(p,"BankTransferNR"),S(p,"PPKKvitasNR")?.TrimEnd(),I(p,"EKA_ID"),S(p,"Pastabos"),I(p,"ValiutosID"),S(p,"CurrencyCode"),S(p,"PaymentMethodName"),S(p,"PaymentEmployeeName"),S(p,"PaymentRegisterName"))).ToArray(),
            await CancellationAuditUnavailableReason(c,tx,id,ct));
    }
}
