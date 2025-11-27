using ExpenseManagement.Models;
using ExpenseManagement.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace ExpenseManagement.Pages;

public class ApproveModel : PageModel
{
    private readonly IExpenseService _expenseService;
    private readonly ILogger<ApproveModel> _logger;

    public ApproveModel(IExpenseService expenseService, ILogger<ApproveModel> logger)
    {
        _expenseService = expenseService;
        _logger = logger;
    }

    public List<Expense> PendingExpenses { get; set; } = new();

    [BindProperty(SupportsGet = true)]
    public string? SearchTerm { get; set; }

    // Default to manager user for demo
    private const int ManagerUserId = 2;

    public async Task OnGetAsync()
    {
        var (expenses, error) = await _expenseService.GetPendingExpensesAsync(SearchTerm);
        
        if (error != null)
        {
            ViewData["ErrorMessage"] = error;
        }
        
        PendingExpenses = expenses;
    }

    public async Task<IActionResult> OnPostApproveAsync(int id)
    {
        var (success, error) = await _expenseService.ApproveExpenseAsync(id, ManagerUserId);
        
        if (error != null)
        {
            TempData["Error"] = error;
        }
        else
        {
            TempData["Success"] = "Expense approved.";
        }
        
        return RedirectToPage();
    }

    public async Task<IActionResult> OnPostRejectAsync(int id)
    {
        var (success, error) = await _expenseService.RejectExpenseAsync(id, ManagerUserId);
        
        if (error != null)
        {
            TempData["Error"] = error;
        }
        else
        {
            TempData["Success"] = "Expense rejected.";
        }
        
        return RedirectToPage();
    }
}
