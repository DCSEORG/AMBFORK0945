using Azure.AI.OpenAI;
using Azure.Core;
using Azure.Identity;
using ExpenseManagement.Models;
using OpenAI.Chat;
using System.Text.Json;
using OpenAIChatMessage = OpenAI.Chat.ChatMessage;

namespace ExpenseManagement.Services;

public interface IChatService
{
    Task<ChatResponse> ProcessMessageAsync(ChatRequest request);
}

public class ChatService : IChatService
{
    private readonly IConfiguration _configuration;
    private readonly ILogger<ChatService> _logger;
    private readonly IExpenseService _expenseService;

    public ChatService(IConfiguration configuration, ILogger<ChatService> logger, IExpenseService expenseService)
    {
        _configuration = configuration;
        _logger = logger;
        _expenseService = expenseService;
    }

    public async Task<ChatResponse> ProcessMessageAsync(ChatRequest request)
    {
        var endpoint = _configuration["OpenAI:Endpoint"];
        var deploymentName = _configuration["OpenAI:DeploymentName"];

        // Check if GenAI is configured
        if (string.IsNullOrEmpty(endpoint) || string.IsNullOrEmpty(deploymentName))
        {
            _logger.LogWarning("GenAI resources not configured");
            return new ChatResponse
            {
                Success = true,
                Message = GetDummyResponse(request.Message)
            };
        }

        try
        {
            // Get credential for Azure OpenAI
            var managedIdentityClientId = _configuration["ManagedIdentityClientId"];
            TokenCredential credential;

            if (!string.IsNullOrEmpty(managedIdentityClientId))
            {
                _logger.LogInformation("Using ManagedIdentityCredential with client ID: {ClientId}", managedIdentityClientId);
                credential = new ManagedIdentityCredential(managedIdentityClientId);
            }
            else
            {
                _logger.LogInformation("Using DefaultAzureCredential");
                credential = new DefaultAzureCredential();
            }

            var client = new AzureOpenAIClient(new Uri(endpoint), credential);
            var chatClient = client.GetChatClient(deploymentName);

            // Define available functions
            var tools = GetFunctionTools();
            var systemPrompt = GetSystemPrompt();

            var messages = new List<OpenAIChatMessage>
            {
                new SystemChatMessage(systemPrompt)
            };

            // Add conversation history
            if (request.History != null)
            {
                foreach (var msg in request.History)
                {
                    if (msg.Role.Equals("user", StringComparison.OrdinalIgnoreCase))
                        messages.Add(new UserChatMessage(msg.Content));
                    else if (msg.Role.Equals("assistant", StringComparison.OrdinalIgnoreCase))
                        messages.Add(new AssistantChatMessage(msg.Content));
                }
            }

            messages.Add(new UserChatMessage(request.Message));

            var options = new ChatCompletionOptions();
            foreach (var tool in tools)
            {
                options.Tools.Add(tool);
            }

            // Function calling loop
            var maxIterations = 5;
            var iteration = 0;

            while (iteration < maxIterations)
            {
                iteration++;

                var completion = await chatClient.CompleteChatAsync(messages, options);
                var response = completion.Value;

                if (response.FinishReason == ChatFinishReason.ToolCalls)
                {
                    // Process tool calls
                    var assistantMessage = new AssistantChatMessage(response);
                    messages.Add(assistantMessage);

                    foreach (var toolCall in response.ToolCalls)
                    {
                        var functionResult = await ExecuteFunctionAsync(toolCall.FunctionName, toolCall.FunctionArguments.ToString());
                        messages.Add(new ToolChatMessage(toolCall.Id, functionResult));
                    }
                }
                else
                {
                    // Final response
                    return new ChatResponse
                    {
                        Success = true,
                        Message = response.Content[0].Text
                    };
                }
            }

            return new ChatResponse
            {
                Success = false,
                Message = "I apologize, but I encountered an issue processing your request. Please try again.",
                Error = "Max iterations reached"
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing chat message");
            return new ChatResponse
            {
                Success = false,
                Message = "I apologize, but I'm having trouble connecting to the AI service. " + GetDummyResponse(request.Message),
                Error = ex.Message
            };
        }
    }

    private string GetSystemPrompt()
    {
        return @"You are an AI assistant for the Expense Management System. You help users manage their expenses, view expense reports, and understand their spending.

You have access to the following functions to interact with the expense database:
- get_expenses: Retrieve expenses with optional filters (status, category, search term)
- get_pending_expenses: Get expenses pending approval
- get_categories: Get available expense categories
- get_expense_summary_by_category: Get spending summary grouped by category
- get_expense_summary_by_status: Get spending summary grouped by status
- create_expense: Create a new expense (requires userId, categoryId, amount, date, description)
- submit_expense: Submit an expense for approval
- approve_expense: Approve a pending expense (manager function)
- reject_expense: Reject a pending expense (manager function)

When listing expenses or data, format the response nicely with clear formatting:
- Use numbered lists for multiple items
- Show amounts in GBP (£) format
- Include relevant details like date, category, and status
- Summarize totals when appropriate

Always be helpful and provide clear, concise responses. If a user wants to perform an action, confirm what you're about to do before executing it.";
    }

    private List<ChatTool> GetFunctionTools()
    {
        return new List<ChatTool>
        {
            ChatTool.CreateFunctionTool(
                "get_expenses",
                "Retrieves expenses from the database with optional filters",
                BinaryData.FromString(@"{
                    ""type"": ""object"",
                    ""properties"": {
                        ""status"": {
                            ""type"": ""string"",
                            ""description"": ""Filter by status: Draft, Submitted, Approved, or Rejected""
                        },
                        ""category"": {
                            ""type"": ""string"",
                            ""description"": ""Filter by category: Travel, Meals, Supplies, Accommodation, Other""
                        },
                        ""searchTerm"": {
                            ""type"": ""string"",
                            ""description"": ""Search term to filter expenses by description or user""
                        }
                    }
                }")
            ),
            ChatTool.CreateFunctionTool(
                "get_pending_expenses",
                "Retrieves expenses that are pending approval (Submitted status)",
                BinaryData.FromString(@"{
                    ""type"": ""object"",
                    ""properties"": {
                        ""searchTerm"": {
                            ""type"": ""string"",
                            ""description"": ""Optional search term to filter pending expenses""
                        }
                    }
                }")
            ),
            ChatTool.CreateFunctionTool(
                "get_categories",
                "Retrieves all available expense categories",
                BinaryData.FromString(@"{
                    ""type"": ""object"",
                    ""properties"": {}
                }")
            ),
            ChatTool.CreateFunctionTool(
                "get_expense_summary_by_category",
                "Gets a summary of expenses grouped by category, showing count and total amount",
                BinaryData.FromString(@"{
                    ""type"": ""object"",
                    ""properties"": {}
                }")
            ),
            ChatTool.CreateFunctionTool(
                "get_expense_summary_by_status",
                "Gets a summary of expenses grouped by status, showing count and total amount",
                BinaryData.FromString(@"{
                    ""type"": ""object"",
                    ""properties"": {}
                }")
            ),
            ChatTool.CreateFunctionTool(
                "create_expense",
                "Creates a new expense in Draft status",
                BinaryData.FromString(@"{
                    ""type"": ""object"",
                    ""properties"": {
                        ""userId"": {
                            ""type"": ""integer"",
                            ""description"": ""User ID who is creating the expense""
                        },
                        ""categoryId"": {
                            ""type"": ""integer"",
                            ""description"": ""Category ID (1=Travel, 2=Meals, 3=Supplies, 4=Accommodation, 5=Other)""
                        },
                        ""amount"": {
                            ""type"": ""number"",
                            ""description"": ""Expense amount in GBP""
                        },
                        ""expenseDate"": {
                            ""type"": ""string"",
                            ""description"": ""Date of expense in ISO format (YYYY-MM-DD)""
                        },
                        ""description"": {
                            ""type"": ""string"",
                            ""description"": ""Description of the expense""
                        }
                    },
                    ""required"": [""userId"", ""categoryId"", ""amount"", ""expenseDate"", ""description""]
                }")
            ),
            ChatTool.CreateFunctionTool(
                "submit_expense",
                "Submits an expense for approval",
                BinaryData.FromString(@"{
                    ""type"": ""object"",
                    ""properties"": {
                        ""expenseId"": {
                            ""type"": ""integer"",
                            ""description"": ""ID of the expense to submit""
                        }
                    },
                    ""required"": [""expenseId""]
                }")
            ),
            ChatTool.CreateFunctionTool(
                "approve_expense",
                "Approves a pending expense (manager function)",
                BinaryData.FromString(@"{
                    ""type"": ""object"",
                    ""properties"": {
                        ""expenseId"": {
                            ""type"": ""integer"",
                            ""description"": ""ID of the expense to approve""
                        },
                        ""reviewerId"": {
                            ""type"": ""integer"",
                            ""description"": ""ID of the manager approving the expense""
                        }
                    },
                    ""required"": [""expenseId"", ""reviewerId""]
                }")
            ),
            ChatTool.CreateFunctionTool(
                "reject_expense",
                "Rejects a pending expense (manager function)",
                BinaryData.FromString(@"{
                    ""type"": ""object"",
                    ""properties"": {
                        ""expenseId"": {
                            ""type"": ""integer"",
                            ""description"": ""ID of the expense to reject""
                        },
                        ""reviewerId"": {
                            ""type"": ""integer"",
                            ""description"": ""ID of the manager rejecting the expense""
                        }
                    },
                    ""required"": [""expenseId"", ""reviewerId""]
                }")
            )
        };
    }

    private async Task<string> ExecuteFunctionAsync(string functionName, string arguments)
    {
        try
        {
            var args = JsonDocument.Parse(arguments);
            var root = args.RootElement;

            switch (functionName)
            {
                case "get_expenses":
                    var status = root.TryGetProperty("status", out var s) ? s.GetString() : null;
                    var category = root.TryGetProperty("category", out var c) ? c.GetString() : null;
                    var searchTerm = root.TryGetProperty("searchTerm", out var st) ? st.GetString() : null;
                    var (expenses, expError) = await _expenseService.GetExpensesAsync(status, category, null, searchTerm);
                    return JsonSerializer.Serialize(new { success = expError == null, data = expenses, error = expError });

                case "get_pending_expenses":
                    var pendingSearch = root.TryGetProperty("searchTerm", out var ps) ? ps.GetString() : null;
                    var (pending, pendingError) = await _expenseService.GetPendingExpensesAsync(pendingSearch);
                    return JsonSerializer.Serialize(new { success = pendingError == null, data = pending, error = pendingError });

                case "get_categories":
                    var (categories, catError) = await _expenseService.GetCategoriesAsync();
                    return JsonSerializer.Serialize(new { success = catError == null, data = categories, error = catError });

                case "get_expense_summary_by_category":
                    var (catSummary, catSumError) = await _expenseService.GetExpenseSummaryByCategoryAsync();
                    return JsonSerializer.Serialize(new { success = catSumError == null, data = catSummary, error = catSumError });

                case "get_expense_summary_by_status":
                    var (statusSummary, statusSumError) = await _expenseService.GetExpenseSummaryByStatusAsync();
                    return JsonSerializer.Serialize(new { success = statusSumError == null, data = statusSummary, error = statusSumError });

                case "create_expense":
                    var createRequest = new CreateExpenseRequest
                    {
                        UserId = root.GetProperty("userId").GetInt32(),
                        CategoryId = root.GetProperty("categoryId").GetInt32(),
                        Amount = root.GetProperty("amount").GetDecimal(),
                        ExpenseDate = DateTime.ParseExact(root.GetProperty("expenseDate").GetString()!, "yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture),
                        Description = root.GetProperty("description").GetString()
                    };
                    var (expenseId, createError) = await _expenseService.CreateExpenseAsync(createRequest);
                    return JsonSerializer.Serialize(new { success = createError == null, expenseId, error = createError });

                case "submit_expense":
                    var submitId = root.GetProperty("expenseId").GetInt32();
                    var (submitSuccess, submitError) = await _expenseService.SubmitExpenseAsync(submitId);
                    return JsonSerializer.Serialize(new { success = submitSuccess, error = submitError });

                case "approve_expense":
                    var approveId = root.GetProperty("expenseId").GetInt32();
                    var approverId = root.GetProperty("reviewerId").GetInt32();
                    var (approveSuccess, approveError) = await _expenseService.ApproveExpenseAsync(approveId, approverId);
                    return JsonSerializer.Serialize(new { success = approveSuccess, error = approveError });

                case "reject_expense":
                    var rejectId = root.GetProperty("expenseId").GetInt32();
                    var rejecterId = root.GetProperty("reviewerId").GetInt32();
                    var (rejectSuccess, rejectError) = await _expenseService.RejectExpenseAsync(rejectId, rejecterId);
                    return JsonSerializer.Serialize(new { success = rejectSuccess, error = rejectError });

                default:
                    return JsonSerializer.Serialize(new { success = false, error = $"Unknown function: {functionName}" });
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error executing function {FunctionName}", functionName);
            return JsonSerializer.Serialize(new { success = false, error = ex.Message });
        }
    }

    private string GetDummyResponse(string message)
    {
        var lowerMessage = message.ToLower();

        if (lowerMessage.Contains("expense") && (lowerMessage.Contains("list") || lowerMessage.Contains("show") || lowerMessage.Contains("all")))
        {
            return @"Here are your expenses (Demo Data - GenAI not deployed):

1. **Travel** - £120.00 - Taxi to client site - Submitted
2. **Meals** - £45.50 - Client lunch meeting - Approved
3. **Supplies** - £25.99 - Office supplies - Draft
4. **Accommodation** - £189.00 - Hotel during conference - Submitted

**Total: £380.49**

To enable AI-powered conversations, deploy the GenAI resources using `./deploy-with-chat.sh`";
        }

        if (lowerMessage.Contains("pending") || lowerMessage.Contains("approve"))
        {
            return @"Here are the pending expenses (Demo Data - GenAI not deployed):

1. **Travel** - £120.00 - Taxi to client site - Awaiting approval
2. **Accommodation** - £189.00 - Hotel during conference - Awaiting approval

To enable AI-powered approvals, deploy the GenAI resources using `./deploy-with-chat.sh`";
        }

        if (lowerMessage.Contains("categor"))
        {
            return @"Available expense categories (Demo Data):

1. Travel
2. Meals
3. Supplies
4. Accommodation
5. Other

To enable AI-powered conversations, deploy the GenAI resources using `./deploy-with-chat.sh`";
        }

        if (lowerMessage.Contains("help"))
        {
            return @"I can help you with:

- **View expenses** - 'Show all my expenses' or 'List travel expenses'
- **Check pending** - 'What expenses are pending approval?'
- **Create expense** - 'Create a new expense for £50 lunch'
- **Submit expense** - 'Submit expense #1 for approval'
- **Approve/Reject** - 'Approve expense #2' (Manager only)
- **Summaries** - 'Show spending by category'

Note: GenAI resources are not deployed. Deploy using `./deploy-with-chat.sh` for full AI capabilities.";
        }

        return @"I'm the Expense Management Assistant (Demo Mode).

GenAI resources are not currently deployed, so I'm showing pre-configured responses.

To enable full AI-powered conversations with your expense data:
1. Run `./deploy-with-chat.sh` to deploy Azure OpenAI resources
2. This will connect me to GPT-4o for natural language understanding

Try asking: 'Show all my expenses' or 'What expenses are pending?'";
    }
}
