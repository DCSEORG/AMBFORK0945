using ExpenseManagement.Models;
using ExpenseManagement.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace ExpenseManagement.Pages;

public class AddExpenseModel : PageModel
{
    private readonly IExpenseService _expenseService;
    private readonly ILogger<AddExpenseModel> _logger;

    public AddExpenseModel(IExpenseService expenseService, ILogger<AddExpenseModel> logger)
    {
        _expenseService = expenseService;
        _logger = logger;
    }

    public List<Category> Categories { get; set; } = new();
    public string? ErrorMessage { get; set; }

    [BindProperty]
    public int UserId { get; set; } = 1; // Default to first user for demo

    [BindProperty]
    public decimal Amount { get; set; }

    [BindProperty]
    public DateTime ExpenseDate { get; set; } = DateTime.Today;

    [BindProperty]
    public int CategoryId { get; set; }

    [BindProperty]
    public string? Description { get; set; }

    public async Task OnGetAsync()
    {
        var (categories, error) = await _expenseService.GetCategoriesAsync();
        
        if (error != null)
        {
            ViewData["ErrorMessage"] = error;
        }
        
        Categories = categories;
    }

    public async Task<IActionResult> OnPostAsync()
    {
        if (Amount <= 0)
        {
            ErrorMessage = "Amount must be greater than 0";
            await LoadCategories();
            return Page();
        }

        var request = new CreateExpenseRequest
        {
            UserId = UserId,
            CategoryId = CategoryId,
            Amount = Amount,
            ExpenseDate = ExpenseDate,
            Description = Description
        };

        var (expenseId, error) = await _expenseService.CreateExpenseAsync(request);

        if (error != null)
        {
            ViewData["ErrorMessage"] = error;
            ErrorMessage = "Failed to create expense. See error details above.";
            await LoadCategories();
            return Page();
        }

        TempData["Success"] = "Expense created successfully!";
        return RedirectToPage("Index");
    }

    private async Task LoadCategories()
    {
        var (categories, _) = await _expenseService.GetCategoriesAsync();
        Categories = categories;
    }
}
