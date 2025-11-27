using ExpenseManagement.Models;
using ExpenseManagement.Services;
using Microsoft.AspNetCore.Mvc;

namespace ExpenseManagement.Controllers;

[ApiController]
[Route("api/[controller]")]
[Produces("application/json")]
public class ExpensesController : ControllerBase
{
    private readonly IExpenseService _expenseService;
    private readonly ILogger<ExpensesController> _logger;

    public ExpensesController(IExpenseService expenseService, ILogger<ExpensesController> logger)
    {
        _expenseService = expenseService;
        _logger = logger;
    }

    /// <summary>
    /// Get all expenses with optional filtering
    /// </summary>
    /// <param name="status">Filter by status (Draft, Submitted, Approved, Rejected)</param>
    /// <param name="category">Filter by category</param>
    /// <param name="userId">Filter by user ID</param>
    /// <param name="search">Search in description and username</param>
    [HttpGet]
    [ProducesResponseType(typeof(List<Expense>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetExpenses(
        [FromQuery] string? status = null,
        [FromQuery] string? category = null,
        [FromQuery] int? userId = null,
        [FromQuery] string? search = null)
    {
        var (expenses, error) = await _expenseService.GetExpensesAsync(status, category, userId, search);
        
        if (error != null)
        {
            return Ok(new { data = expenses, warning = error });
        }
        
        return Ok(expenses);
    }

    /// <summary>
    /// Get a specific expense by ID
    /// </summary>
    /// <param name="id">Expense ID</param>
    [HttpGet("{id}")]
    [ProducesResponseType(typeof(Expense), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetExpense(int id)
    {
        var (expense, error) = await _expenseService.GetExpenseByIdAsync(id);
        
        if (error != null)
        {
            return StatusCode(500, new { error });
        }
        
        if (expense == null)
        {
            return NotFound(new { message = $"Expense {id} not found" });
        }
        
        return Ok(expense);
    }

    /// <summary>
    /// Get pending expenses awaiting approval
    /// </summary>
    /// <param name="search">Search in description and username</param>
    [HttpGet("pending")]
    [ProducesResponseType(typeof(List<Expense>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetPendingExpenses([FromQuery] string? search = null)
    {
        var (expenses, error) = await _expenseService.GetPendingExpensesAsync(search);
        
        if (error != null)
        {
            return Ok(new { data = expenses, warning = error });
        }
        
        return Ok(expenses);
    }

    /// <summary>
    /// Create a new expense
    /// </summary>
    /// <param name="request">Expense details</param>
    [HttpPost]
    [ProducesResponseType(typeof(object), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> CreateExpense([FromBody] CreateExpenseRequest request)
    {
        if (request.Amount <= 0)
        {
            return BadRequest(new { message = "Amount must be greater than 0" });
        }
        
        var (expenseId, error) = await _expenseService.CreateExpenseAsync(request);
        
        if (error != null || expenseId == null)
        {
            return StatusCode(500, new { error = error ?? "Failed to create expense" });
        }
        
        return CreatedAtAction(nameof(GetExpense), new { id = expenseId }, new { expenseId });
    }

    /// <summary>
    /// Update an existing expense
    /// </summary>
    /// <param name="id">Expense ID</param>
    /// <param name="request">Updated expense details</param>
    [HttpPut("{id}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> UpdateExpense(int id, [FromBody] UpdateExpenseRequest request)
    {
        if (request.Amount <= 0)
        {
            return BadRequest(new { message = "Amount must be greater than 0" });
        }
        
        var (success, error) = await _expenseService.UpdateExpenseAsync(id, request);
        
        if (error != null)
        {
            return StatusCode(500, new { error });
        }
        
        return Ok(new { message = "Expense updated successfully" });
    }

    /// <summary>
    /// Delete an expense
    /// </summary>
    /// <param name="id">Expense ID</param>
    [HttpDelete("{id}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> DeleteExpense(int id)
    {
        var (success, error) = await _expenseService.DeleteExpenseAsync(id);
        
        if (error != null)
        {
            return StatusCode(500, new { error });
        }
        
        return Ok(new { message = "Expense deleted successfully" });
    }

    /// <summary>
    /// Submit an expense for approval
    /// </summary>
    /// <param name="id">Expense ID</param>
    [HttpPost("{id}/submit")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> SubmitExpense(int id)
    {
        var (success, error) = await _expenseService.SubmitExpenseAsync(id);
        
        if (error != null)
        {
            return StatusCode(500, new { error });
        }
        
        return Ok(new { message = "Expense submitted for approval" });
    }

    /// <summary>
    /// Approve an expense (Manager only)
    /// </summary>
    /// <param name="id">Expense ID</param>
    /// <param name="reviewerId">Reviewer's user ID</param>
    [HttpPost("{id}/approve")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> ApproveExpense(int id, [FromQuery] int reviewerId)
    {
        var (success, error) = await _expenseService.ApproveExpenseAsync(id, reviewerId);
        
        if (error != null)
        {
            return StatusCode(500, new { error });
        }
        
        return Ok(new { message = "Expense approved" });
    }

    /// <summary>
    /// Reject an expense (Manager only)
    /// </summary>
    /// <param name="id">Expense ID</param>
    /// <param name="reviewerId">Reviewer's user ID</param>
    [HttpPost("{id}/reject")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> RejectExpense(int id, [FromQuery] int reviewerId)
    {
        var (success, error) = await _expenseService.RejectExpenseAsync(id, reviewerId);
        
        if (error != null)
        {
            return StatusCode(500, new { error });
        }
        
        return Ok(new { message = "Expense rejected" });
    }

    /// <summary>
    /// Get expense summary by category
    /// </summary>
    /// <param name="userId">Optional user ID filter</param>
    [HttpGet("summary/category")]
    [ProducesResponseType(typeof(List<ExpenseSummary>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetSummaryByCategory([FromQuery] int? userId = null)
    {
        var (summary, error) = await _expenseService.GetExpenseSummaryByCategoryAsync(userId);
        
        if (error != null)
        {
            return Ok(new { data = summary, warning = error });
        }
        
        return Ok(summary);
    }

    /// <summary>
    /// Get expense summary by status
    /// </summary>
    /// <param name="userId">Optional user ID filter</param>
    [HttpGet("summary/status")]
    [ProducesResponseType(typeof(List<StatusSummary>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetSummaryByStatus([FromQuery] int? userId = null)
    {
        var (summary, error) = await _expenseService.GetExpenseSummaryByStatusAsync(userId);
        
        if (error != null)
        {
            return Ok(new { data = summary, warning = error });
        }
        
        return Ok(summary);
    }
}
