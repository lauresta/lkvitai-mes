using System.Data;
using LKvitai.MES.Modules.Sales.Contracts.Orders;
using Microsoft.Data.SqlClient;

namespace LKvitai.MES.Modules.Sales.Infrastructure.Sql;

internal static class LegacyOrderNoteHistory
{
    // Call only after the caller's scoped parent read. History is read-only;
    // keep duplicate/current-note entries exactly as recorded by legacy triggers.
    internal static async Task<IReadOnlyList<OrderNoteHistoryEntry>> ReadAsync(
        SqlConnection connection, SqlTransaction? transaction, long orderId, int timeout, CancellationToken ct)
    {
        await using var command = new SqlCommand("""
            SELECT NoteID,Note,DateTimeInsert,UserNameInsert
            FROM dbo.UzsakymuPastabos WHERE OrderID=@id ORDER BY NoteID;
            """, connection, transaction) { CommandTimeout = timeout };
        command.Parameters.Add("@id", SqlDbType.BigInt).Value = orderId;
        await using var reader = await command.ExecuteReaderAsync(ct);
        var result = new List<OrderNoteHistoryEntry>();
        while (await reader.ReadAsync(ct))
            result.Add(new(reader.GetInt64(0), reader.GetString(1),
                reader.IsDBNull(2) ? null : reader.GetDateTime(2), reader.IsDBNull(3) ? null : reader.GetString(3)));
        return result;
    }
}
