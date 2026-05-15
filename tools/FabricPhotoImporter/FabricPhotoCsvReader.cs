using System.Globalization;
using CsvHelper;
using CsvHelper.Configuration;

namespace FabricPhotoImporter;

public static class FabricPhotoCsvReader
{
    public static IReadOnlyList<FabricPhotoImportRow> Read(string csvPath)
    {
        using var reader = File.OpenText(csvPath);
        using var csv = new CsvReader(reader, new CsvConfiguration(CultureInfo.InvariantCulture)
        {
            MissingFieldFound = null,
            HeaderValidated = null,
            TrimOptions = TrimOptions.Trim
        });

        return csv.GetRecords<FabricPhotoImportRow>().ToList();
    }
}
