namespace ShopKeeper.Domain.Constants;

/// <summary>
/// The full platform permission catalog. Keys are stable strings persisted in the
/// Permission table and referenced by RolePermission - never rename a key, only add.
/// </summary>
public static class PermissionKeys
{
    public const string SalesView = "sales:view";
    public const string SalesCreate = "sales:create";
    public const string SalesRefund = "sales:refund";
    public const string SalesVoid = "sales:void";

    public const string InventoryView = "inventory:view";
    public const string InventoryModify = "inventory:modify";

    public const string ProfitView = "profit:view";
    public const string ExpensesView = "expenses:view";
    public const string ExpensesManage = "expenses:manage";

    public const string EmployeesManage = "employees:manage";
    public const string BranchesManage = "branches:manage";
    public const string ProductsManage = "products:manage";
    public const string SuppliersManage = "suppliers:manage";
    public const string CustomersManage = "customers:manage";

    public const string ReportsView = "reports:view";
    public const string SettingsManage = "settings:manage";
    public const string UsersManage = "users:manage";
    public const string AuditLogsView = "audit_logs:view";
    public const string AiConsultantUse = "ai_consultant:use";

    public static readonly IReadOnlyList<(string Key, string Name, string Category)> All = new[]
    {
        (SalesView, "View Sales", "Sales"),
        (SalesCreate, "Create Sales", "Sales"),
        (SalesRefund, "Refund Sales", "Sales"),
        (SalesVoid, "Void Transactions", "Sales"),

        (InventoryView, "View Inventory", "Inventory"),
        (InventoryModify, "Modify Inventory", "Inventory"),

        (ProfitView, "View Profit", "Finance"),
        (ExpensesView, "View Expenses", "Finance"),
        (ExpensesManage, "Manage Expenses", "Finance"),

        (EmployeesManage, "Manage Employees", "Team"),
        (BranchesManage, "Manage Branches", "Operations"),
        (ProductsManage, "Manage Products", "Inventory"),
        (SuppliersManage, "Manage Suppliers", "Operations"),
        (CustomersManage, "Manage Customers", "Operations"),

        (ReportsView, "View Reports", "Reporting"),
        (SettingsManage, "Manage Settings", "Administration"),
        (UsersManage, "Manage Users", "Administration"),
        (AuditLogsView, "View Audit Logs", "Administration"),
        (AiConsultantUse, "Use AI Consultant", "AI"),
    };
}
