using ExpenseManagement.Models;
using ExpenseManagement.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace ExpenseManagement.Pages;

public class IndexModel : PageModel
{
    private readonly IExpenseService _expenseService;
    private readonly ILogger<IndexModel> _logger;

    public IndexModel(IExpenseService expenseService, ILogger<IndexModel> logger)
    {
        _expenseService = expenseService;
        _logger = logger;
    }

    public List<Expense> Expenses { get; set; } = new();
    public List<Category> Categories { get; set; } = new();
    public List<ExpenseStatus> Statuses { get; set; } = new();

    [BindProperty(SupportsGet = true)]
    public string? SearchTerm { get; set; }

    [BindProperty(SupportsGet = true)]
    public string? StatusFilter { get; set; }

    [BindProperty(SupportsGet = true)]
    public string? CategoryFilter { get; set; }

    public async Task OnGetAsync()
    {
        var (expenses, expenseError) = await _expenseService.GetExpensesAsync(
            StatusFilter, CategoryFilter, null, SearchTerm);
        
        if (expenseError != null)
        {
            ViewData["ErrorMessage"] = expenseError;
        }
        
        Expenses = expenses;

        var (categories, _) = await _expenseService.GetCategoriesAsync();
        Categories = categories;

        var (statuses, _) = await _expenseService.GetStatusesAsync();
        Statuses = statuses;
    }

    public async Task<IActionResult> OnPostSubmitAsync(int id)
    {
        var (success, error) = await _expenseService.SubmitExpenseAsync(id);
        
        if (error != null)
        {
            TempData["Error"] = error;
        }
        else
        {
            TempData["Success"] = "Expense submitted for approval.";
        }
        
        return RedirectToPage();
    }

    public async Task<IActionResult> OnPostDeleteAsync(int id)
    {
        var (success, error) = await _expenseService.DeleteExpenseAsync(id);
        
        if (error != null)
        {
            TempData["Error"] = error;
        }
        else
        {
            TempData["Success"] = "Expense deleted.";
        }
        
        return RedirectToPage();
    }
}
