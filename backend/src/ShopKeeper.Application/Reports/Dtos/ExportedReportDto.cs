namespace ShopKeeper.Application.Reports.Dtos;

public record ExportedReportDto(byte[] Content, string FileName, string ContentType);
