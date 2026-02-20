namespace LKvitai.MES.Modules.Warehouse.Application.Models;

public sealed record ImportErrorReport(
    int Row,
    string Column,
    string? Value,
    string Message);
