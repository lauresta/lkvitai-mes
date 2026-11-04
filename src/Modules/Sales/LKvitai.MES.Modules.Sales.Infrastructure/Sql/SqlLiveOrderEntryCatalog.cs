using LKvitai.MES.Modules.Sales.Infrastructure.Sql.Security;
using System.Data;
using LKvitai.MES.Modules.Sales.Application.OrderEntry;
using LKvitai.MES.Modules.Sales.Application.Ports;
using LKvitai.MES.Modules.Sales.Contracts.OrderEntry;
using Microsoft.Data.SqlClient;

namespace LKvitai.MES.Modules.Sales.Infrastructure.Sql;

/// <summary>Fresh live reference read; selected columns only, no credentials, financial balances or customer contact data.</summary>
public sealed class SqlLiveOrderEntryCatalog(SalesSqlOptions options, ILegacySqlConnectionFactory connections) : ILiveOrderEntryCatalog
{
    public async Task<CatalogUsageDto> GetUsageAsync(int storeId, int days, CancellationToken ct)
    {
        await using var lease = await connections.OpenAsync(ct).ConfigureAwait(false);
        await using var command = new SqlCommand("""
            SELECT t.TipoID, t.Rusis, COUNT_BIG(*) AS Uses
            FROM dbo.Uzsakymai u
            JOIN dbo.Zinynas_UzsakymoSerijos s ON s.UzsakymoSerijaID=u.UzsakymoSerija
            JOIN dbo.ParduotiGaminiai pg ON pg.UzsakymoID=u.UzsakymasID
            JOIN dbo.Zinynas_tipai t ON t.TipoID=pg.ParduotoGaminioTipoID
            WHERE u.VietaID=@storeId
              AND u.UzsakymoData>=DATEADD(day,1-@days,CONVERT(date,GETDATE()))
              AND EXISTS(SELECT 1 FROM dbo.Loc_leidimas() access WHERE access.UKS=s.FilialoID)
            GROUP BY t.TipoID,t.Rusis
            """, lease.Connection)
        { CommandTimeout = options.CommandTimeoutSeconds > 0 ? options.CommandTimeoutSeconds : 30 };
        command.Parameters.Add("@storeId", SqlDbType.Int).Value = storeId;
        command.Parameters.Add("@days", SqlDbType.Int).Value = days;
        var types = new List<TypeUsageDto>();
        var categories = new Dictionary<int, int>();
        await using var reader = await command.ExecuteReaderAsync(ct).ConfigureAwait(false);
        while (await reader.ReadAsync(ct).ConfigureAwait(false))
        {
            var typeId = reader.GetInt32(0);
            var categoryId = reader.GetInt32(1);
            var uses = (int)Math.Min(reader.GetInt64(2), int.MaxValue);
            types.Add(new TypeUsageDto(typeId, uses));
            categories[categoryId] = (int)Math.Min(int.MaxValue, (long)categories.GetValueOrDefault(categoryId) + uses);
        }
        return new CatalogUsageDto(categories.Select(x => new CategoryUsageDto(x.Key, x.Value)).ToArray(), types);
    }

    public async Task<LiveOrderEntryCatalogDto> GetAsync(CancellationToken ct)
    {
        await using var lease = await connections.OpenAsync(ct).ConfigureAwait(false);
        var connection = lease.Connection;
        async Task<List<T>> Read<T>(string sql, Func<SqlDataReader,T> map)
        {
            await using var command = new SqlCommand(sql, connection) { CommandTimeout = options.CommandTimeoutSeconds > 0 ? options.CommandTimeoutSeconds : 30 };
            await using var reader = await command.ExecuteReaderAsync(ct).ConfigureAwait(false);
            var result = new List<T>();
            while(await reader.ReadAsync(ct).ConfigureAwait(false)) result.Add(map(reader));
            return result;
        }
        var series=await Read("""
            SELECT s.UzsakymoSerijaID,s.UzsakymoSerijaPav,s.FilialoID,c.UzsakovoID,c.UzsakovoPavadinimas,
                   v.VietaID,v.VietaPavadinimas,m.DarbuotojoID,m.DarbuotojoPavadinimas AS ManagerName,
                   k.DarbuotojoID,k.DarbuotojoPavadinimas AS ConsultantName
            FROM dbo.Zinynas_UzsakymoSerijos s
            LEFT JOIN dbo.Zinynas_uzsakovai c ON c.UzsakovoID=s.UzaskovoID AND EXISTS(SELECT 1 FROM dbo.Loc_leidimas() access WHERE access.UKS=c.PriskirtasFilialui)
            LEFT JOIN dbo.Zinynas_vieta v ON v.VietaID=s.VietaID AND EXISTS(SELECT 1 FROM dbo.Loc_leidimas() access WHERE access.UKS=v.Filialas)
            LEFT JOIN dbo.Zinynas_darbuotojai m ON m.DarbuotojoID=s.Vad_DarbuotojasID AND EXISTS(SELECT 1 FROM dbo.Loc_leidimas() access WHERE access.UKS=m.FilialasID)
            LEFT JOIN dbo.Zinynas_darbuotojai k ON k.DarbuotojoID=s.Kons_DarbuotojoID AND EXISTS(SELECT 1 FROM dbo.Loc_leidimas() access WHERE access.UKS=k.FilialasID)
            WHERE ISNULL(s.bHideInApplicationFlag,0)=0 AND EXISTS(SELECT 1 FROM dbo.Loc_leidimas() access WHERE access.UKS=s.FilialoID) ORDER BY s.UzsakymoSerijaPav,s.UzsakymoSerijaID
            """,r=>new LiveSeriesDto(I(r,0),S(r,1)??"",NI(r,2),NI(r,3),S(r,4),NI(r,5),S(r,6),NI(r,7),S(r,8),NI(r,9),S(r,10)));
        var categories=await Read("""
            SELECT RusiesID,RusiesPavadinimas,Naudojamas,TeikiamaNuolaida,KainosIvestis,
                   P01,P02,P03,P04,P05,P06,P07,P08,P09,P10,P11,P12
            FROM dbo.Zinynas_rusys ORDER BY RRusiavimas,RusiesPavadinimas,RusiesID
            """,r=>new LiveCategoryDto(I(r,0),S(r,1)??$"#{I(r,0)}",B(r,2),B(r,3),LegacyPriceEntryRules.NormalizeMode(S(r,4)),Enumerable.Range(5,12).Select(i=>S(r,i)).ToArray()));
        var types=await Read("""
            SELECT t.TipoID,t.Rusis,t.TipoPavadinimas,t.Naudojamas,t.Gamininamas,t.KainosMatas,
                   t.MinKainosPlotis,t.MinKainosAukstis,t.MinKainosPlotas,c.KainosIvestis,
                   t.NPP01,t.NPP02,t.NPP03,t.NPP04,t.NPP05,t.NPP06,t.NPP07,t.NPP08,t.NPP09,t.NPP10,t.NPP11,t.NPP12
            FROM dbo.Zinynas_tipai t JOIN dbo.Zinynas_rusys c ON c.RusiesID=t.Rusis
            WHERE t.Naudojamas<>0 ORDER BY c.RRusiavimas,t.Rusiavimas,t.TipoPavadinimas,t.TipoID
            """,r=>
            {
                var formulas=Enumerable.Range(10,12).Select(i=>S(r,i)).ToArray();
                return new LiveTypeDto(I(r,0),I(r,1),S(r,2)??$"#{I(r,0)}",B(r,3),B(r,4),NI(r,5),ND(r,6),ND(r,7),ND(r,8),
                    LegacyPriceEntryRules.NormalizeMode(S(r,9)),LegacyPriceEntryRules.SupportsManualUnitPriceForType(I(r,0),S(r,9)),
                    formulas,LegacyProductCalculator.FindUnsupportedReason(formulas));
            });
        var colourRows=await Read("""
            SELECT DISTINCT c.iComponentID,c.sCodeInternal,c.sName,m.iCategoryID,
                   vr.Laisva,vr.[Sandėlyje],vr.[Gamyboje],vr.Bendrai,vr.Rezervuota,u.sName
            FROM dbo.view_Remains vr
            JOIN dbo.TBD_Components c ON c.iComponentID=vr.iComponentID
            JOIN dbo.TCR_CategoriesColours m ON m.iComponentCategoryID=c.iComponentCategoryID
            LEFT JOIN dbo.DIC_Units u ON u.iUnitID=c.iUnitID
            WHERE c.iComponentID<>0 ORDER BY c.iComponentID,m.iCategoryID
            """,r=>(Id:I(r,0),Code:S(r,1)??"",Name:S(r,2)??"",Category:I(r,3),Free:ND(r,4),Warehouse:ND(r,5),Production:ND(r,6),Total:ND(r,7),Reserved:ND(r,8),Unit:S(r,9)));
        var colours=colourRows.GroupBy(x=>x.Id).Select(g=>new LiveColourDto(g.Key,g.First().Code,g.First().Name,g.Select(x=>x.Category).Distinct().ToArray(),g.First().Free,g.First().Warehouse,g.First().Production,g.First().Total,g.First().Reserved,g.First().Unit)).ToArray();
        var accessoryRows=await Read("""
            SELECT DISTINCT p.PriedoID,p.PriedoPavadinimas,p.PriedoMatas,m.Matas,p.Kaina,c.iBlindCategoryID
            FROM dbo.Zinynas_priedai p LEFT JOIN dbo.Zinynas_matai m ON m.MatoID=p.PriedoMatas
            LEFT JOIN dbo.TCR_BlindCategoryAccessoryCategory c ON c.iAccessoryCategoryID=p.iAccessoryCategoryID
            ORDER BY p.PriedoID,c.iBlindCategoryID
            """,r=>(Id:I(r,0),Name:S(r,1)??$"Priedas #{I(r,0)}",UnitId:NI(r,2),Unit:S(r,3)??"",Price:ND(r,4),Category:NI(r,5)));
        var accessories=accessoryRows.GroupBy(x=>x.Id).Select(g=>new LiveAccessoryDto(g.Key,g.First().Name,g.First().UnitId,g.First().Unit,g.First().Price,g.Where(x=>x.Category.HasValue).Select(x=>x.Category!.Value).Distinct().ToArray())).ToArray();
        var customers=await Read("SELECT c.UzsakovoID,c.UzsakovoPavadinimas FROM dbo.Zinynas_uzsakovai c WHERE ISNULL(c.bHideInApplicationFlag,0)=0 AND EXISTS(SELECT 1 FROM dbo.Loc_leidimas() access WHERE access.UKS=c.PriskirtasFilialui) ORDER BY UzsakovoPavadinimas,UzsakovoID",r=>new LiveNamedReferenceDto(I(r,0),S(r,1)??$"#{I(r,0)}"));
        var employees=await Read("SELECT e.DarbuotojoID,e.DarbuotojoPavadinimas FROM dbo.Zinynas_darbuotojai e WHERE ISNULL(e.bHideInApplicationFlag,0)=0 AND EXISTS(SELECT 1 FROM dbo.Loc_leidimas() access WHERE access.UKS=e.FilialasID) ORDER BY DarbuotojoPavadinimas,DarbuotojoID",r=>new LiveNamedReferenceDto(I(r,0),S(r,1)??$"#{I(r,0)}"));
        var stores=await Read("SELECT v.VietaID,v.VietaPavadinimas FROM dbo.Zinynas_vieta v WHERE EXISTS(SELECT 1 FROM dbo.Loc_leidimas() access WHERE access.UKS=v.Filialas) ORDER BY VietaPavadinimas,VietaID",r=>new LiveNamedReferenceDto(I(r,0),S(r,1)??$"#{I(r,0)}"));
        var duties=await Read("SELECT iDutyID,sName FROM dbo.TBD_Duties ORDER BY iSortingID,iDutyID",r=>new LiveNamedReferenceDto(I(r,0),S(r,1)??$"#{I(r,0)}"));
        var priorities=await Read("SELECT PriorityID,sPriority FROM dbo.DIC_Priorities ORDER BY PriorityID",r=>new LiveNamedReferenceDto(I(r,0),S(r,1)??$"#{I(r,0)}"));
        return new LiveOrderEntryCatalogDto(DateTimeOffset.UtcNow,series,categories,types,colours,accessories,customers,employees,stores,duties,priorities);
    }
    private static int I(SqlDataReader r,int i)=>r.GetInt32(i);
    private static int? NI(SqlDataReader r,int i)=>r.IsDBNull(i)?null:r.GetInt32(i);
    private static string? S(SqlDataReader r,int i)=>r.IsDBNull(i)?null:r.GetString(i);
    private static bool B(SqlDataReader r,int i)=>!r.IsDBNull(i)&&r.GetBoolean(i);
    private static decimal? ND(SqlDataReader r,int i)=>r.IsDBNull(i)?null:r.GetDecimal(i);
}
