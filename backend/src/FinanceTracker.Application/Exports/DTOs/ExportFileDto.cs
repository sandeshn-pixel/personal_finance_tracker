namespace FinanceTracker.Application.Exports.DTOs;

public sealed record ExportFileDto(
    string FileName,
    string ContentType,
    byte[] Content);