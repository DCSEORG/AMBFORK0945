using ExpenseManagement.Models;
using Microsoft.Data.SqlClient;

namespace ExpenseManagement.Services;

public interface IExpenseService
{
    Task<(List<Expense> Expenses, string? Error)> GetExpensesAsync(string? status = null, string? category = null, int? userId = null, string? searchTerm = null);
    Task<(Expense? Expense, string? Error)> GetExpenseByIdAsync(int expenseId);
    Task<(int? ExpenseId, string? Error)> CreateExpenseAsync(CreateExpenseRequest request);
    Task<(bool Success, string? Error)> UpdateExpenseAsync(int expenseId, UpdateExpenseRequest request);
    Task<(bool Success, string? Error)> DeleteExpenseAsync(int expenseId);
    Task<(bool Success, string? Error)> SubmitExpenseAsync(int expenseId);
    Task<(bool Success, string? Error)> ApproveExpenseAsync(int expenseId, int reviewerId);
    Task<(bool Success, string? Error)> RejectExpenseAsync(int expenseId, int reviewerId);
    Task<(List<Expense> Expenses, string? Error)> GetPendingExpensesAsync(string? searchTerm = null);
    Task<(List<Category> Categories, string? Error)> GetCategoriesAsync();
    Task<(List<ExpenseStatus> Statuses, string? Error)> GetStatusesAsync();
    Task<(List<User> Users, string? Error)> GetUsersAsync();
    Task<(List<ExpenseSummary> Summary, string? Error)> GetExpenseSummaryByCategoryAsync(int? userId = null);
    Task<(List<StatusSummary> Summary, string? Error)> GetExpenseSummaryByStatusAsync(int? userId = null);
}

public class ExpenseService : IExpenseService
{
    private readonly IConfiguration _configuration;
    private readonly ILogger<ExpenseService> _logger;

    public ExpenseService(IConfiguration configuration, ILogger<ExpenseService> logger)
    {
        _configuration = configuration;
        _logger = logger;
    }

    private string GetConnectionString()
    {
        return _configuration.GetConnectionString("DefaultConnection") 
            ?? throw new InvalidOperationException("Connection string 'DefaultConnection' not found.");
    }

    private string FormatErrorMessage(Exception ex, string operation, string file, int line)
    {
        var errorMessage = $"Database Error during {operation}. ";
        
        if (ex is SqlException sqlEx)
        {
            if (sqlEx.Message.Contains("managed identity", StringComparison.OrdinalIgnoreCase) ||
                sqlEx.Message.Contains("authentication", StringComparison.OrdinalIgnoreCase) ||
                sqlEx.Message.Contains("Login failed", StringComparison.OrdinalIgnoreCase))
            {
                errorMessage += "Managed Identity authentication failed. Please ensure: " +
                    "1) The App Service has a User-Assigned Managed Identity configured. " +
                    "2) The Managed Identity has been granted database access (db_datareader, db_datawriter, EXECUTE). " +
                    "3) The connection string uses 'Authentication=Active Directory Managed Identity' with the correct User Id (Client ID). " +
                    "4) For local development, use 'Authentication=Active Directory Default' and run 'az login' first. " +
                    $"File: {file}, Line: {line}. ";
            }
            else if (sqlEx.Message.Contains("network", StringComparison.OrdinalIgnoreCase) ||
                     sqlEx.Message.Contains("connection", StringComparison.OrdinalIgnoreCase))
            {
                errorMessage += "Network connection to database failed. Please ensure: " +
                    "1) The SQL Server firewall allows Azure services. " +
                    "2) Your IP address is added to the firewall for local development. " +
                    $"File: {file}, Line: {line}. ";
            }
            else
            {
                errorMessage += $"SQL Error: {sqlEx.Message}. File: {file}, Line: {line}. ";
            }
        }
        else
        {
            errorMessage += $"{ex.Message}. File: {file}, Line: {line}. ";
        }
        
        return errorMessage;
    }

    public async Task<(List<Expense> Expenses, string? Error)> GetExpensesAsync(string? status = null, string? category = null, int? userId = null, string? searchTerm = null)
    {
        try
        {
            var expenses = new List<Expense>();
            
            using var connection = new SqlConnection(GetConnectionString());
            await connection.OpenAsync();
            
            using var command = new SqlCommand("usp_GetExpenses", connection);
            command.CommandType = System.Data.CommandType.StoredProcedure;
            command.Parameters.AddWithValue("@StatusName", (object?)status ?? DBNull.Value);
            command.Parameters.AddWithValue("@CategoryName", (object?)category ?? DBNull.Value);
            command.Parameters.AddWithValue("@UserId", (object?)userId ?? DBNull.Value);
            command.Parameters.AddWithValue("@SearchTerm", (object?)searchTerm ?? DBNull.Value);
            
            using var reader = await command.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                expenses.Add(MapExpense(reader));
            }
            
            return (expenses, null);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting expenses");
            var error = FormatErrorMessage(ex, "GetExpenses", "ExpenseService.cs", 95);
            return (DummyData.GetDummyExpenses(), error);
        }
    }

    public async Task<(Expense? Expense, string? Error)> GetExpenseByIdAsync(int expenseId)
    {
        try
        {
            using var connection = new SqlConnection(GetConnectionString());
            await connection.OpenAsync();
            
            using var command = new SqlCommand("usp_GetExpenseById", connection);
            command.CommandType = System.Data.CommandType.StoredProcedure;
            command.Parameters.AddWithValue("@ExpenseId", expenseId);
            
            using var reader = await command.ExecuteReaderAsync();
            if (await reader.ReadAsync())
            {
                return (MapExpense(reader), null);
            }
            
            return (null, null);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting expense {ExpenseId}", expenseId);
            var error = FormatErrorMessage(ex, "GetExpenseById", "ExpenseService.cs", 120);
            return (null, error);
        }
    }

    public async Task<(int? ExpenseId, string? Error)> CreateExpenseAsync(CreateExpenseRequest request)
    {
        try
        {
            using var connection = new SqlConnection(GetConnectionString());
            await connection.OpenAsync();
            
            using var command = new SqlCommand("usp_CreateExpense", connection);
            command.CommandType = System.Data.CommandType.StoredProcedure;
            command.Parameters.AddWithValue("@UserId", request.UserId);
            command.Parameters.AddWithValue("@CategoryId", request.CategoryId);
            command.Parameters.AddWithValue("@Amount", request.Amount);
            command.Parameters.AddWithValue("@ExpenseDate", request.ExpenseDate);
            command.Parameters.AddWithValue("@Description", (object?)request.Description ?? DBNull.Value);
            command.Parameters.AddWithValue("@ReceiptFile", (object?)request.ReceiptFile ?? DBNull.Value);
            
            var outputParam = command.Parameters.Add("@ExpenseId", System.Data.SqlDbType.Int);
            outputParam.Direction = System.Data.ParameterDirection.Output;
            
            await command.ExecuteNonQueryAsync();
            
            return ((int)outputParam.Value, null);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating expense");
            var error = FormatErrorMessage(ex, "CreateExpense", "ExpenseService.cs", 155);
            return (null, error);
        }
    }

    public async Task<(bool Success, string? Error)> UpdateExpenseAsync(int expenseId, UpdateExpenseRequest request)
    {
        try
        {
            using var connection = new SqlConnection(GetConnectionString());
            await connection.OpenAsync();
            
            using var command = new SqlCommand("usp_UpdateExpense", connection);
            command.CommandType = System.Data.CommandType.StoredProcedure;
            command.Parameters.AddWithValue("@ExpenseId", expenseId);
            command.Parameters.AddWithValue("@CategoryId", request.CategoryId);
            command.Parameters.AddWithValue("@Amount", request.Amount);
            command.Parameters.AddWithValue("@ExpenseDate", request.ExpenseDate);
            command.Parameters.AddWithValue("@Description", (object?)request.Description ?? DBNull.Value);
            command.Parameters.AddWithValue("@ReceiptFile", (object?)request.ReceiptFile ?? DBNull.Value);
            
            await command.ExecuteNonQueryAsync();
            
            return (true, null);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating expense {ExpenseId}", expenseId);
            var error = FormatErrorMessage(ex, "UpdateExpense", "ExpenseService.cs", 183);
            return (false, error);
        }
    }

    public async Task<(bool Success, string? Error)> DeleteExpenseAsync(int expenseId)
    {
        try
        {
            using var connection = new SqlConnection(GetConnectionString());
            await connection.OpenAsync();
            
            using var command = new SqlCommand("usp_DeleteExpense", connection);
            command.CommandType = System.Data.CommandType.StoredProcedure;
            command.Parameters.AddWithValue("@ExpenseId", expenseId);
            
            await command.ExecuteNonQueryAsync();
            
            return (true, null);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting expense {ExpenseId}", expenseId);
            var error = FormatErrorMessage(ex, "DeleteExpense", "ExpenseService.cs", 205);
            return (false, error);
        }
    }

    public async Task<(bool Success, string? Error)> SubmitExpenseAsync(int expenseId)
    {
        try
        {
            using var connection = new SqlConnection(GetConnectionString());
            await connection.OpenAsync();
            
            using var command = new SqlCommand("usp_SubmitExpense", connection);
            command.CommandType = System.Data.CommandType.StoredProcedure;
            command.Parameters.AddWithValue("@ExpenseId", expenseId);
            
            await command.ExecuteNonQueryAsync();
            
            return (true, null);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error submitting expense {ExpenseId}", expenseId);
            var error = FormatErrorMessage(ex, "SubmitExpense", "ExpenseService.cs", 227);
            return (false, error);
        }
    }

    public async Task<(bool Success, string? Error)> ApproveExpenseAsync(int expenseId, int reviewerId)
    {
        try
        {
            using var connection = new SqlConnection(GetConnectionString());
            await connection.OpenAsync();
            
            using var command = new SqlCommand("usp_ApproveExpense", connection);
            command.CommandType = System.Data.CommandType.StoredProcedure;
            command.Parameters.AddWithValue("@ExpenseId", expenseId);
            command.Parameters.AddWithValue("@ReviewerId", reviewerId);
            
            await command.ExecuteNonQueryAsync();
            
            return (true, null);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error approving expense {ExpenseId}", expenseId);
            var error = FormatErrorMessage(ex, "ApproveExpense", "ExpenseService.cs", 250);
            return (false, error);
        }
    }

    public async Task<(bool Success, string? Error)> RejectExpenseAsync(int expenseId, int reviewerId)
    {
        try
        {
            using var connection = new SqlConnection(GetConnectionString());
            await connection.OpenAsync();
            
            using var command = new SqlCommand("usp_RejectExpense", connection);
            command.CommandType = System.Data.CommandType.StoredProcedure;
            command.Parameters.AddWithValue("@ExpenseId", expenseId);
            command.Parameters.AddWithValue("@ReviewerId", reviewerId);
            
            await command.ExecuteNonQueryAsync();
            
            return (true, null);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error rejecting expense {ExpenseId}", expenseId);
            var error = FormatErrorMessage(ex, "RejectExpense", "ExpenseService.cs", 273);
            return (false, error);
        }
    }

    public async Task<(List<Expense> Expenses, string? Error)> GetPendingExpensesAsync(string? searchTerm = null)
    {
        try
        {
            var expenses = new List<Expense>();
            
            using var connection = new SqlConnection(GetConnectionString());
            await connection.OpenAsync();
            
            using var command = new SqlCommand("usp_GetPendingExpenses", connection);
            command.CommandType = System.Data.CommandType.StoredProcedure;
            command.Parameters.AddWithValue("@SearchTerm", (object?)searchTerm ?? DBNull.Value);
            
            using var reader = await command.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                expenses.Add(MapExpense(reader));
            }
            
            return (expenses, null);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting pending expenses");
            var error = FormatErrorMessage(ex, "GetPendingExpenses", "ExpenseService.cs", 301);
            var dummyPending = DummyData.GetDummyExpenses().Where(e => e.StatusName == "Submitted").ToList();
            return (dummyPending, error);
        }
    }

    public async Task<(List<Category> Categories, string? Error)> GetCategoriesAsync()
    {
        try
        {
            var categories = new List<Category>();
            
            using var connection = new SqlConnection(GetConnectionString());
            await connection.OpenAsync();
            
            using var command = new SqlCommand("usp_GetCategories", connection);
            command.CommandType = System.Data.CommandType.StoredProcedure;
            
            using var reader = await command.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                categories.Add(new Category
                {
                    CategoryId = reader.GetInt32(reader.GetOrdinal("CategoryId")),
                    CategoryName = reader.GetString(reader.GetOrdinal("CategoryName")),
                    IsActive = reader.GetBoolean(reader.GetOrdinal("IsActive"))
                });
            }
            
            return (categories, null);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting categories");
            var error = FormatErrorMessage(ex, "GetCategories", "ExpenseService.cs", 335);
            return (DummyData.GetDummyCategories(), error);
        }
    }

    public async Task<(List<ExpenseStatus> Statuses, string? Error)> GetStatusesAsync()
    {
        try
        {
            var statuses = new List<ExpenseStatus>();
            
            using var connection = new SqlConnection(GetConnectionString());
            await connection.OpenAsync();
            
            using var command = new SqlCommand("usp_GetStatuses", connection);
            command.CommandType = System.Data.CommandType.StoredProcedure;
            
            using var reader = await command.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                statuses.Add(new ExpenseStatus
                {
                    StatusId = reader.GetInt32(reader.GetOrdinal("StatusId")),
                    StatusName = reader.GetString(reader.GetOrdinal("StatusName"))
                });
            }
            
            return (statuses, null);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting statuses");
            var error = FormatErrorMessage(ex, "GetStatuses", "ExpenseService.cs", 367);
            return (DummyData.GetDummyStatuses(), error);
        }
    }

    public async Task<(List<User> Users, string? Error)> GetUsersAsync()
    {
        try
        {
            var users = new List<User>();
            
            using var connection = new SqlConnection(GetConnectionString());
            await connection.OpenAsync();
            
            using var command = new SqlCommand("usp_GetUsers", connection);
            command.CommandType = System.Data.CommandType.StoredProcedure;
            
            using var reader = await command.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                users.Add(new User
                {
                    UserId = reader.GetInt32(reader.GetOrdinal("UserId")),
                    UserName = reader.GetString(reader.GetOrdinal("UserName")),
                    Email = reader.GetString(reader.GetOrdinal("Email")),
                    RoleId = reader.GetInt32(reader.GetOrdinal("RoleId")),
                    RoleName = reader.GetString(reader.GetOrdinal("RoleName")),
                    ManagerId = reader.IsDBNull(reader.GetOrdinal("ManagerId")) ? null : reader.GetInt32(reader.GetOrdinal("ManagerId")),
                    ManagerName = reader.IsDBNull(reader.GetOrdinal("ManagerName")) ? null : reader.GetString(reader.GetOrdinal("ManagerName")),
                    IsActive = reader.GetBoolean(reader.GetOrdinal("IsActive")),
                    CreatedAt = reader.GetDateTime(reader.GetOrdinal("CreatedAt"))
                });
            }
            
            return (users, null);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting users");
            var error = FormatErrorMessage(ex, "GetUsers", "ExpenseService.cs", 405);
            return (DummyData.GetDummyUsers(), error);
        }
    }

    public async Task<(List<ExpenseSummary> Summary, string? Error)> GetExpenseSummaryByCategoryAsync(int? userId = null)
    {
        try
        {
            var summaries = new List<ExpenseSummary>();
            
            using var connection = new SqlConnection(GetConnectionString());
            await connection.OpenAsync();
            
            using var command = new SqlCommand("usp_GetExpenseSummaryByCategory", connection);
            command.CommandType = System.Data.CommandType.StoredProcedure;
            command.Parameters.AddWithValue("@UserId", (object?)userId ?? DBNull.Value);
            
            using var reader = await command.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                summaries.Add(new ExpenseSummary
                {
                    CategoryName = reader.GetString(reader.GetOrdinal("CategoryName")),
                    ExpenseCount = reader.GetInt32(reader.GetOrdinal("ExpenseCount")),
                    TotalAmount = reader.GetDecimal(reader.GetOrdinal("TotalAmount"))
                });
            }
            
            return (summaries, null);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting expense summary by category");
            var error = FormatErrorMessage(ex, "GetExpenseSummaryByCategory", "ExpenseService.cs", 438);
            return (new List<ExpenseSummary>(), error);
        }
    }

    public async Task<(List<StatusSummary> Summary, string? Error)> GetExpenseSummaryByStatusAsync(int? userId = null)
    {
        try
        {
            var summaries = new List<StatusSummary>();
            
            using var connection = new SqlConnection(GetConnectionString());
            await connection.OpenAsync();
            
            using var command = new SqlCommand("usp_GetExpenseSummaryByStatus", connection);
            command.CommandType = System.Data.CommandType.StoredProcedure;
            command.Parameters.AddWithValue("@UserId", (object?)userId ?? DBNull.Value);
            
            using var reader = await command.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                summaries.Add(new StatusSummary
                {
                    StatusName = reader.GetString(reader.GetOrdinal("StatusName")),
                    ExpenseCount = reader.GetInt32(reader.GetOrdinal("ExpenseCount")),
                    TotalAmount = reader.GetDecimal(reader.GetOrdinal("TotalAmount"))
                });
            }
            
            return (summaries, null);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting expense summary by status");
            var error = FormatErrorMessage(ex, "GetExpenseSummaryByStatus", "ExpenseService.cs", 471);
            return (new List<StatusSummary>(), error);
        }
    }

    private static Expense MapExpense(SqlDataReader reader)
    {
        return new Expense
        {
            ExpenseId = reader.GetInt32(reader.GetOrdinal("ExpenseId")),
            UserId = reader.GetInt32(reader.GetOrdinal("UserId")),
            UserName = reader.GetString(reader.GetOrdinal("UserName")),
            Email = reader.GetString(reader.GetOrdinal("Email")),
            CategoryId = reader.GetInt32(reader.GetOrdinal("CategoryId")),
            CategoryName = reader.GetString(reader.GetOrdinal("CategoryName")),
            StatusId = reader.GetInt32(reader.GetOrdinal("StatusId")),
            StatusName = reader.GetString(reader.GetOrdinal("StatusName")),
            Amount = reader.GetDecimal(reader.GetOrdinal("Amount")),
            Currency = reader.GetString(reader.GetOrdinal("Currency")),
            ExpenseDate = reader.GetDateTime(reader.GetOrdinal("ExpenseDate")),
            Description = reader.IsDBNull(reader.GetOrdinal("Description")) ? null : reader.GetString(reader.GetOrdinal("Description")),
            ReceiptFile = reader.IsDBNull(reader.GetOrdinal("ReceiptFile")) ? null : reader.GetString(reader.GetOrdinal("ReceiptFile")),
            SubmittedAt = reader.IsDBNull(reader.GetOrdinal("SubmittedAt")) ? null : reader.GetDateTime(reader.GetOrdinal("SubmittedAt")),
            ReviewedBy = reader.IsDBNull(reader.GetOrdinal("ReviewedBy")) ? null : reader.GetInt32(reader.GetOrdinal("ReviewedBy")),
            ReviewerName = reader.IsDBNull(reader.GetOrdinal("ReviewerName")) ? null : reader.GetString(reader.GetOrdinal("ReviewerName")),
            ReviewedAt = reader.IsDBNull(reader.GetOrdinal("ReviewedAt")) ? null : reader.GetDateTime(reader.GetOrdinal("ReviewedAt")),
            CreatedAt = reader.GetDateTime(reader.GetOrdinal("CreatedAt"))
        };
    }
}
