namespace ExpenseManagement.Models;

/// <summary>
/// Provides dummy data when database connection fails
/// </summary>
public static class DummyData
{
    public static List<Expense> GetDummyExpenses()
    {
        return new List<Expense>
        {
            new Expense
            {
                ExpenseId = 1,
                UserId = 1,
                UserName = "Demo User",
                Email = "demo@example.co.uk",
                CategoryId = 1,
                CategoryName = "Travel",
                StatusId = 2,
                StatusName = "Submitted",
                Amount = 120.00m,
                Currency = "GBP",
                ExpenseDate = DateTime.Now.AddDays(-10),
                Description = "Taxi to client site (Demo Data)",
                CreatedAt = DateTime.Now.AddDays(-10)
            },
            new Expense
            {
                ExpenseId = 2,
                UserId = 1,
                UserName = "Demo User",
                Email = "demo@example.co.uk",
                CategoryId = 2,
                CategoryName = "Meals",
                StatusId = 3,
                StatusName = "Approved",
                Amount = 45.50m,
                Currency = "GBP",
                ExpenseDate = DateTime.Now.AddDays(-20),
                Description = "Client lunch meeting (Demo Data)",
                CreatedAt = DateTime.Now.AddDays(-20)
            },
            new Expense
            {
                ExpenseId = 3,
                UserId = 1,
                UserName = "Demo User",
                Email = "demo@example.co.uk",
                CategoryId = 3,
                CategoryName = "Supplies",
                StatusId = 1,
                StatusName = "Draft",
                Amount = 25.99m,
                Currency = "GBP",
                ExpenseDate = DateTime.Now.AddDays(-5),
                Description = "Office supplies (Demo Data)",
                CreatedAt = DateTime.Now.AddDays(-5)
            },
            new Expense
            {
                ExpenseId = 4,
                UserId = 1,
                UserName = "Demo User",
                Email = "demo@example.co.uk",
                CategoryId = 4,
                CategoryName = "Accommodation",
                StatusId = 2,
                StatusName = "Submitted",
                Amount = 189.00m,
                Currency = "GBP",
                ExpenseDate = DateTime.Now.AddDays(-15),
                Description = "Hotel during conference (Demo Data)",
                CreatedAt = DateTime.Now.AddDays(-15)
            }
        };
    }

    public static List<Category> GetDummyCategories()
    {
        return new List<Category>
        {
            new Category { CategoryId = 1, CategoryName = "Travel", IsActive = true },
            new Category { CategoryId = 2, CategoryName = "Meals", IsActive = true },
            new Category { CategoryId = 3, CategoryName = "Supplies", IsActive = true },
            new Category { CategoryId = 4, CategoryName = "Accommodation", IsActive = true },
            new Category { CategoryId = 5, CategoryName = "Other", IsActive = true }
        };
    }

    public static List<ExpenseStatus> GetDummyStatuses()
    {
        return new List<ExpenseStatus>
        {
            new ExpenseStatus { StatusId = 1, StatusName = "Draft" },
            new ExpenseStatus { StatusId = 2, StatusName = "Submitted" },
            new ExpenseStatus { StatusId = 3, StatusName = "Approved" },
            new ExpenseStatus { StatusId = 4, StatusName = "Rejected" }
        };
    }

    public static List<User> GetDummyUsers()
    {
        return new List<User>
        {
            new User { UserId = 1, UserName = "Demo User", Email = "demo@example.co.uk", RoleId = 1, RoleName = "Employee", IsActive = true, CreatedAt = DateTime.Now.AddMonths(-6) },
            new User { UserId = 2, UserName = "Demo Manager", Email = "manager@example.co.uk", RoleId = 2, RoleName = "Manager", IsActive = true, CreatedAt = DateTime.Now.AddMonths(-12) }
        };
    }
}
