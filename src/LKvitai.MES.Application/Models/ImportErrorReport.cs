namespace LKvitai.MES.Application.Models;

public sealed record ImportErrorReport(
    int Row,
    string Column,
    string? Value,
    string Message);
