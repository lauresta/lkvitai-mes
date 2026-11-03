namespace LKvitai.MES.Modules.Shopfloor.WebUI.Pages;

// Stage 1 fixtures. These objects never call legacy SQL or the Shopfloor API.
internal sealed class GamybaItem
{
    public required int Id { get; init; }
    public required string Barcode { get; init; }
    public required string Order { get; init; }
    public required string Branch { get; init; }
    public required string Due { get; init; }
    public required string Priority { get; init; }
    public required string Type { get; init; }
    public required string Fabric { get; init; }
    public required int Width { get; init; }
    public required int Height { get; init; }
    public required int Quantity { get; init; }
    public string Note { get; init; } = "";
    public string Stage { get; set; } = "patvirtintas";
    public string? Gu { get; set; }
    public string? PreviousGu { get; init; }
    public bool NoMaterial { get; init; }
}

internal sealed class GamybaPortion
{
    public int Number { get; init; }
    public string At { get; init; } = "";
    public string By { get; init; } = "Rūta M.";
    public List<int> ItemIds { get; init; } = [];
}

internal sealed class GamybaTask
{
    public required string Number { get; init; }
    public required string Created { get; init; }
    public string Author { get; init; } = "Rūta M.";
    public List<GamybaPortion> Portions { get; } = [];
    public List<string> Pdfs { get; } = [];
    public string? WriteOff { get; set; }
}

internal sealed class GamybaScan
{
    public required int ItemId { get; init; }
    public required string At { get; init; }
    public required string Before { get; init; }
    public required string After { get; init; }
    public bool Locked { get; init; }
}

internal sealed class GamybaDocument
{
    public required string Number { get; init; }
    public required string Operation { get; init; }
    public required string Created { get; init; }
    public string Author { get; init; } = "Rūta M.";
    public List<GamybaScan> Scans { get; } = [];
}

internal sealed class GamybaRepair
{
    public required string Number { get; init; }
    public required int ItemId { get; init; }
    public required string Reason { get; init; }
    public required string Claimed { get; init; }
    public string Fact { get; set; } = "";
    public bool Done { get; set; }
}

internal sealed class GamybaLabelJob
{
    public required string Number { get; init; }
    public required string Task { get; init; }
    public required int PhysicalLabels { get; init; }
    public int Sent { get; set; }
    public string State { get; set; } = "Pending";
    public string Mode { get; init; } = "GU";
    public string? SourceBarcode { get; init; }
    public string? ReprintOf { get; init; }
    public List<int> ReprintNumbers { get; init; } = [];
    public string Note { get; set; } = "";
}

internal sealed class GamybaTemplateColumn
{
    public required string Field { get; init; }
    public required string Label { get; set; }
    public required int Width { get; set; }
}

internal static class GamybaFixtures
{
    public static List<GamybaItem> Items() =>
    [
        new() { Id=1, Barcode="250611000121", Order="KVT-261028-012", Branch="Vilnius", Due="2026-11-05", Priority="normal", Type="Rolėtai", Fabric="Screen 3% Grey", Width=1200, Height=1600, Quantity=2 },
        new() { Id=2, Barcode="250611000122", Order="KVT-261028-012", Branch="Vilnius", Due="2026-11-05", Priority="normal", Type="Rolėtai", Fabric="Screen 3% Grey", Width=900, Height=1600, Quantity=1, Note="Montavimas 11-07" },
        new() { Id=3, Barcode="250611000123", Order="KVT-261028-012", Branch="Vilnius", Due="2026-11-05", Priority="normal", Type="Vertikalios", Fabric="Linen 127 Sand", Width=2400, Height=2200, Quantity=1 },
        new() { Id=4, Barcode="250611000131", Order="KVT-261029-004", Branch="Kaunas", Due="2026-11-03", Priority="urgent", Type="Plise", Fabric="Crush Ivory", Width=600, Height=1100, Quantity=4, Note="Skubus · klientas laukia" },
        new() { Id=5, Barcode="250611000132", Order="KVT-261029-004", Branch="Kaunas", Due="2026-11-03", Priority="urgent", Type="Plise", Fabric="Crush Ivory", Width=600, Height=900, Quantity=2 },
        new() { Id=6, Barcode="250611000140", Order="KVT-261030-021", Branch="Klaipėda", Due="2026-11-10", Priority="vip", Type="Romanetės", Fabric="Velvet Moss", Width=1400, Height=1800, Quantity=1, Note="VIP · ★" },
        new() { Id=7, Barcode="250611000141", Order="KVT-261030-021", Branch="Klaipėda", Due="2026-11-10", Priority="vip", Type="Tinkleliai", Fabric="Mesh Anthracite", Width=800, Height=1200, Quantity=3 },
        new() { Id=8, Barcode="250611000150", Order="KVT-261031-007", Branch="Vilnius", Due="2026-11-04", Priority="repair", Type="Horizontalios", Fabric="Alu 25 Silver", Width=1000, Height=1400, Quantity=1, Note="Garantinis remontas · R-0233", PreviousGu="GU-2610-0133" },
        new() { Id=9, Barcode="250611000151", Order="KVT-261031-009", Branch="Vilnius", Due="2026-11-06", Priority="normal", Type="Apsauginės", Fabric="Alu Roll 39 White", Width=2100, Height=2300, Quantity=1, NoMaterial=true },
        new() { Id=10, Barcode="250611000152", Order="KVT-261031-009", Branch="Vilnius", Due="2026-11-06", Priority="normal", Type="Grotos", Fabric="Steel Grey", Width=1800, Height=2100, Quantity=1 },
        new() { Id=11, Barcode="250611000101", Order="KVT-261027-003", Branch="Vilnius", Due="2026-11-03", Priority="normal", Type="Rolėtai", Fabric="Blackout Graphite", Width=1100, Height=1500, Quantity=2, Stage="gaminamas", Gu="GU-2611-0147" },
        new() { Id=12, Barcode="250611000102", Order="KVT-261027-003", Branch="Vilnius", Due="2026-11-03", Priority="normal", Type="Rolėtai", Fabric="Blackout Graphite", Width=1100, Height=1500, Quantity=2, Stage="gaminamas", Gu="GU-2611-0147" },
        new() { Id=13, Barcode="250611000103", Order="KVT-261027-003", Branch="Vilnius", Due="2026-11-03", Priority="normal", Type="Rolėtai", Fabric="Screen 5% White", Width=700, Height=1300, Quantity=1, Stage="gaminamas", Gu="GU-2611-0147" },
        new() { Id=14, Barcode="250611000110", Order="KVT-261027-008", Branch="Kaunas", Due="2026-11-04", Priority="normal", Type="Plise", Fabric="Crush Ivory", Width=500, Height=800, Quantity=6, Stage="gaminamas", Gu="GU-2611-0147" },
        new() { Id=15, Barcode="250611000111", Order="KVT-261027-008", Branch="Kaunas", Due="2026-11-04", Priority="normal", Type="Plise", Fabric="Crush Ivory", Width=500, Height=800, Quantity=2, Stage="gaminamas", Gu="GU-2611-0147" },
        new() { Id=16, Barcode="250611000112", Order="KVT-261027-008", Branch="Kaunas", Due="2026-11-04", Priority="normal", Type="Romanetės", Fabric="Velvet Moss", Width=1200, Height=1600, Quantity=1, Stage="gaminamas", Gu="GU-2611-0147" },
        new() { Id=17, Barcode="250611000091", Order="KVT-261024-015", Branch="Vilnius", Due="2026-11-02", Priority="normal", Type="Rolėtai", Fabric="Screen 3% Grey", Width=1000, Height=1400, Quantity=1, Stage="atiduotas", Gu="GU-2611-0146" },
        new() { Id=18, Barcode="250611000092", Order="KVT-261024-015", Branch="Vilnius", Due="2026-11-02", Priority="normal", Type="Rolėtai", Fabric="Screen 3% Grey", Width=1000, Height=1400, Quantity=1, Stage="pagamintas", Gu="GU-2611-0146" },
        new() { Id=19, Barcode="250611000093", Order="KVT-261024-015", Branch="Vilnius", Due="2026-11-02", Priority="normal", Type="Vertikalios", Fabric="Linen 127 Sand", Width=2000, Height=2400, Quantity=1, Stage="gaminamas", Gu="GU-2611-0146" },
        new() { Id=20, Barcode="250611000094", Order="KVT-261024-015", Branch="Vilnius", Due="2026-11-02", Priority="normal", Type="Rolėtai", Fabric="Blackout Graphite", Width=1000, Height=1400, Quantity=1, Stage="gaminamas", Gu="GU-2611-0146" }
    ];

    public static List<GamybaTask> Tasks()
    {
        var a = new GamybaTask { Number="GU-2611-0147", Created="2026-11-02 09:12" };
        a.Portions.Add(new() { Number=1, At="09:12", ItemIds=[11,12,13] });
        a.Portions.Add(new() { Number=2, At="10:41", ItemIds=[14,15,16] });
        a.Pdfs.Add("Rolėtai · v1 · 09:14");
        var b = new GamybaTask { Number="GU-2611-0146", Created="2026-11-01 14:05", Author="Jonas K.", WriteOff="SM-261101-07" };
        b.Portions.Add(new() { Number=1, At="14:05", By="Jonas K.", ItemIds=[17,18,19,20] });
        b.Pdfs.AddRange(["Rolėtai · v1 · 14:07", "Vertikalios · v1 · 14:07"]);
        return [a,b];
    }

    public static List<GamybaDocument> Documents()
    {
        var current = new GamybaDocument { Number="GT-261102-003", Operation="pagaminta", Created="2026-11-02", Author="Rūta M." };
        current.Scans.Add(new() { ItemId=17, At="08:52:10", Before="gaminamas", After="pagamintas", Locked=true });
        current.Scans.Add(new() { ItemId=18, At="08:52:31", Before="gaminamas", After="pagamintas" });
        return [current, new() { Number="GT-261101-011", Operation="pagaminta", Created="2026-11-01", Author="Jonas K." }, new() { Number="GT-261028-004", Operation="filialui", Created="2026-10-28", Author="Rūta M." }];
    }

    public static List<GamybaRepair> Repairs() =>
    [
        new() { Number="R-0233", ItemId=8, Reason="Mechanizmas", Claimed="Neveikia grandinėlė" },
        new() { Number="R-0229", ItemId=17, Reason="Audinys", Claimed="Dėmė ant audinio", Fact="Gamybinis defektas — pakeistas audinys", Done=true },
        new() { Number="R-0231", ItemId=19, Reason="Montavimas", Claimed="Kreivai sumontuota" }
    ];

    public static readonly string[] Reports =
    [
        "Apsauginės", "Garantiniai remontai", "Grotos", "Horizontalios",
        "Panel Trackai", "Pleated Blinds", "Remontai / Priedai", "Rolėtai",
        "Romanetės", "Nature Concept", "Tinkleliai", "Vertikalios", "XL Plise"
    ];

    public static List<GamybaTemplateColumn> Columns() =>
    [
        new() { Field="Barcode", Label="Kodas", Width=16 },
        new() { Field="OrderNo", Label="Užsakymas", Width=16 },
        new() { Field="Fabric", Label="Audinys", Width=24 },
        new() { Field="Width", Label="Plotis", Width=10 },
        new() { Field="Height", Label="Aukštis", Width=10 },
        new() { Field="Qty", Label="Kiekis", Width=8 },
        new() { Field="Note", Label="Pastaba", Width=16 }
    ];
}
