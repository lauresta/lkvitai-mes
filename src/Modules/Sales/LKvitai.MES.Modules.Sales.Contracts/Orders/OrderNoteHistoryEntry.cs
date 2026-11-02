namespace LKvitai.MES.Modules.Sales.Contracts.Orders;

public sealed record OrderNoteHistoryEntry(long Id, string Note, DateTime? At, string? Author);
