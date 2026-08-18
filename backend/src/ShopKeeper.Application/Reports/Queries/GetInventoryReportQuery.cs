namespace ShopKeeper.Application.Reports.Queries;

using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using ShopKeeper.Application.Common.Behaviors;
using ShopKeeper.Application.Common.Extensions;
using ShopKeeper.Application.Common.Interfaces;
using ShopKeeper.Application.Reports.Dtos;
using ShopKeeper.Domain.Constants;
using ShopKeeper.Domain.Enums;

public record GetInventoryReportQuery(DateOnly From, DateOnly To, Guid? BranchId)
    : IRequest<InventoryReportDto>, IRequirePlanFeature
{
    public bool RequiresReports => true;
    public bool RequiresAi => false;
    public bool RequiresCustomRoles => false;
}

public class GetInventoryReportQueryValidator : AbstractValidator<GetInventoryReportQuery>
{
    public GetInventoryReportQueryValidator()
    {
        RuleFor(x => x.To).GreaterThanOrEqualTo(x => x.From).WithMessage("'To' must not be before 'From'.");
    }
}

public class GetInventoryReportQueryHandler(IAppDbContext db, ICurrentUserService currentUser)
    : IRequestHandler<GetInventoryReportQuery, InventoryReportDto>
{
    public async Task<InventoryReportDto> Handle(GetInventoryReportQuery request, CancellationToken cancellationToken)
    {
        currentUser.RequirePermission(PermissionKeys.ReportsView);
        if (request.BranchId.HasValue)
        {
            currentUser.RequireBranchAccess(request.BranchId.Value);
        }

        var branchId = request.BranchId ?? currentUser.BranchId;

        var stockQuery = db.ProductStocks.Include(ps => ps.Product).Where(ps => ps.Product.IsActive);
        if (branchId.HasValue)
        {
            stockQuery = stockQuery.Where(ps => ps.BranchId == branchId);
        }
        var stocks = await stockQuery.ToListAsync(cancellationToken);

        var lowStock = stocks.Where(s => s.QuantityOnHand > 0 && s.QuantityOnHand <= s.Product.ReorderLevel).ToList();
        var outOfStock = stocks.Where(s => s.QuantityOnHand == 0).ToList();

        var valuation = new InventoryValuationDto(
            stocks.Count, lowStock.Count, outOfStock.Count, stocks.Sum(s => s.QuantityOnHand * s.Product.CostPrice));

        var lowStockProducts = lowStock
            .Select(s => new StockAlertProductDto(s.ProductId, s.Product.Name, s.QuantityOnHand, s.Product.ReorderLevel))
            .OrderBy(p => p.QuantityOnHand)
            .ToList();

        var outOfStockProducts = outOfStock
            .Select(s => new StockAlertProductDto(s.ProductId, s.Product.Name, s.QuantityOnHand, s.Product.ReorderLevel))
            .ToList();

        var rangeStart = new DateTimeOffset(request.From.ToDateTime(TimeOnly.MinValue), TimeSpan.Zero);
        var rangeEndExclusive = new DateTimeOffset(request.To.ToDateTime(TimeOnly.MinValue), TimeSpan.Zero).AddDays(1);

        var salesQuery = db.Sales.Include(s => s.Items).AsQueryable();
        if (branchId.HasValue)
        {
            salesQuery = salesQuery.Where(s => s.BranchId == branchId);
        }
        var allSales = await salesQuery.ToListAsync(cancellationToken);

        var unitsSoldByProduct = allSales
            .Where(s => s.Status != SaleStatus.Voided && s.CreatedAt >= rangeStart && s.CreatedAt < rangeEndExclusive)
            .SelectMany(s => s.Items)
            .GroupBy(i => i.ProductId)
            .ToDictionary(g => g.Key, g => g.Sum(i => i.Quantity));

        var turnover = stocks
            .Select(s =>
            {
                var unitsSold = unitsSoldByProduct.GetValueOrDefault(s.ProductId, 0);
                decimal? ratio = s.QuantityOnHand > 0 ? Math.Round((decimal)unitsSold / s.QuantityOnHand, 2) : null;
                return new ProductTurnoverDto(s.ProductId, s.Product.Name, unitsSold, s.QuantityOnHand, ratio);
            })
            .OrderByDescending(t => t.TurnoverRatio ?? 0)
            .ToList();

        return new InventoryReportDto(valuation, lowStockProducts, outOfStockProducts, turnover);
    }
}
