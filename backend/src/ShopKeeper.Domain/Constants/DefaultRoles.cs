namespace ShopKeeper.Domain.Constants;

/// <summary>
/// Blueprint for the roles seeded into every new Business at onboarding. Each business
/// gets its own copies of these rows (see BusinessSeeder), so editing a business's
/// "Cashier" role never affects any other tenant.
/// </summary>
public static class DefaultRoles
{
    public const string Owner = "Owner";
    public const string Administrator = "Administrator";
    public const string BranchManager = "Branch Manager";
    public const string Cashier = "Cashier";
    public const string InventoryManager = "Inventory Manager";
    public const string Accountant = "Accountant";
    public const string Viewer = "Viewer";

    public static readonly IReadOnlyDictionary<string, string[]> RolePermissionKeys =
        new Dictionary<string, string[]>
        {
            [Owner] = PermissionKeys.All.Select(p => p.Key).ToArray(),

            [Administrator] = new[]
            {
                PermissionKeys.SalesView, PermissionKeys.SalesCreate, PermissionKeys.SalesRefund, PermissionKeys.SalesVoid,
                PermissionKeys.InventoryView, PermissionKeys.InventoryModify,
                PermissionKeys.ProfitView, PermissionKeys.ExpensesView, PermissionKeys.ExpensesManage,
                PermissionKeys.EmployeesManage, PermissionKeys.BranchesManage, PermissionKeys.ProductsManage,
                PermissionKeys.SuppliersManage, PermissionKeys.CustomersManage,
                PermissionKeys.ReportsView, PermissionKeys.SettingsManage, PermissionKeys.UsersManage,
                PermissionKeys.AuditLogsView, PermissionKeys.AiConsultantUse,
            },

            [BranchManager] = new[]
            {
                PermissionKeys.SalesView, PermissionKeys.SalesCreate, PermissionKeys.SalesRefund, PermissionKeys.SalesVoid,
                PermissionKeys.InventoryView, PermissionKeys.InventoryModify,
                PermissionKeys.ProfitView, PermissionKeys.ExpensesView,
                PermissionKeys.EmployeesManage, PermissionKeys.ProductsManage, PermissionKeys.SuppliersManage,
                PermissionKeys.CustomersManage, PermissionKeys.ReportsView, PermissionKeys.AiConsultantUse,
            },

            [Cashier] = new[]
            {
                PermissionKeys.SalesView, PermissionKeys.SalesCreate,
                PermissionKeys.InventoryView, PermissionKeys.CustomersManage,
            },

            [InventoryManager] = new[]
            {
                PermissionKeys.InventoryView, PermissionKeys.InventoryModify,
                PermissionKeys.ProductsManage, PermissionKeys.SuppliersManage, PermissionKeys.ReportsView,
            },

            [Accountant] = new[]
            {
                PermissionKeys.ProfitView, PermissionKeys.ExpensesView, PermissionKeys.ExpensesManage,
                PermissionKeys.ReportsView, PermissionKeys.SalesView, PermissionKeys.AuditLogsView,
            },

            [Viewer] = new[]
            {
                PermissionKeys.SalesView, PermissionKeys.InventoryView, PermissionKeys.ReportsView,
            },
        };
}
