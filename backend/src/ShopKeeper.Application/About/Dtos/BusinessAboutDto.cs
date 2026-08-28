namespace ShopKeeper.Application.About.Dtos;

public record BusinessAboutDto(
    string BusinessName,
    string? LogoUrl,
    string? Description,
    string? OwnerBio,
    IReadOnlyList<YearlySalesDto> SalesByYear);

public record YearlySalesDto(int Year, decimal Revenue, decimal Profit, int SalesCount);
