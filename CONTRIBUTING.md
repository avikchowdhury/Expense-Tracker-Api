# Contributing to Expense Tracker API

Thank you for your interest in contributing to the Expense Tracker API! This document outlines the development workflow, code standards, and best practices we follow.

## Development Workflow

### 1. Setting Up Local Environment

```bash
# Clone the repository
git clone https://github.com/avikchowdhury/Expense-Tracker-Api.git
cd Expense-Tracker-Api

# Restore dependencies
dotnet restore

# Set up user secrets for development
dotnet user-secrets init
dotnet user-secrets set "Jwt:Secret" "your-dev-secret-key-min-32-chars"
dotnet user-secrets set "OpenAI:ApiKey" "gsk_your_groq_api_key"
dotnet user-secrets set "AzureBlob:ConnectionString" "your-connection-string"
dotnet user-secrets set "Email:SenderPassword" "your-app-password"

# Apply database migrations
dotnet ef database update

# Run the application
dotnet run
```

### 2. Branch Naming Convention

All branches must follow this naming pattern:

- **Feature**: `feature/expense-filtering`
- **Bug Fix**: `fix/jwt-token-expiry-issue`
- **Documentation**: `docs/api-endpoint-guide`
- **Refactor**: `refactor/service-layer-optimization`
- **Performance**: `perf/query-optimization`

### 3. Making Changes

1. **Create a new branch** from `master`:
   ```bash
   git checkout -b feature/your-feature-name
   ```

2. **Keep commits focused**:
   - One logical change per commit
   - Write clear, descriptive commit messages
   - Reference related issues in the commit message

3. **Push to your branch** and create a Pull Request

### 4. Code Standards

#### C# Naming Conventions

```csharp
// Classes, Methods, Properties - PascalCase
public class ExpenseService { }
public async Task<ExpenseDto> GetExpenseAsync(int id) { }
public string VendorName { get; set; }

// Local variables, parameters - camelCase
var totalAmount = expense.Amount;
public void ProcessExpense(decimal amount) { }

// Constants - PascalCase
private const string DefaultCurrency = "USD";
public const int MaxPageSize = 100;

// Private fields - _camelCase
private readonly IExpenseRepository _expenseRepository;
private string _cachedResult;

// Interfaces - IPascalCase
public interface IExpenseService { }
```

#### Async/Await Patterns

All I/O operations must be async:

```csharp
// ✅ Good
public async Task<ExpenseDto> GetExpenseAsync(int id)
{
    var expense = await _repository.GetByIdAsync(id);
    return _mapper.Map<ExpenseDto>(expense);
}

// ❌ Avoid
public ExpenseDto GetExpense(int id)
{
    var expense = _repository.GetById(id).Result; // Deadlock risk
    return _mapper.Map<ExpenseDto>(expense);
}
```

#### Null Handling

Use nullable reference types (enabled in project):

```csharp
// ✅ Good
public class User
{
    public required string Email { get; set; } // Non-nullable, must be set
    public string? MiddleName { get; set; }    // Nullable, can be null
    public string LastName { get; set; } = "";  // Non-nullable with default
}

// Null checks
if (user?.Profile?.Avatar != null)
{
    // Safe navigation
}
```

#### Dependency Injection

Always inject dependencies through constructors, never static access:

```csharp
// ✅ Good
public class ExpenseController : ControllerBase
{
    private readonly IExpenseService _expenseService;
    private readonly ILogger<ExpenseController> _logger;

    public ExpenseController(IExpenseService expenseService, ILogger<ExpenseController> logger)
    {
        _expenseService = expenseService;
        _logger = logger;
    }
}

// ❌ Avoid
public class ExpenseController : ControllerBase
{
    public async Task<IActionResult> GetExpense(int id)
    {
        var service = ServiceLocator.GetService<IExpenseService>(); // Anti-pattern
    }
}
```

### 5. Git Commit Messages

Follow the conventional commits format:

```
<type>(<scope>): <subject>

<body>

<footer>
```

**Types**: `feat`, `fix`, `docs`, `style`, `refactor`, `perf`, `test`, `chore`, `ci`, `sec`

**Examples**:

```
feat(auth): add OTP verification with email

- Implement OTP generation using random provider
- Add OTP validation with 10-minute expiry
- Send OTP via SMTP email service
- Update User model with OtpCode and OtpExpiryTime fields

Fixes #42
```

```
fix(budget): calculate monthly budget correctly

Previously the budget calculation didn't account for partial months.
Now we properly distribute budgets across the full calendar month.

Relates to #89
```

```
perf(receipts): optimize category filtering query

Added database index on CategoryId in Receipts table.
Reduced query time from 450ms to 85ms on 100k records.
```

### 6. Pull Request Process

Before submitting a PR:

1. **Update** your branch with latest `master`:
   ```bash
   git fetch origin
   git rebase origin/master
   ```

2. **Run tests locally**:
   ```bash
   dotnet test
   ```

3. **Build and verify** no warnings:
   ```bash
   dotnet build -c Release /p:TreatWarningsAsErrors=true
   ```

4. **Create a PR** with:
   - Clear, descriptive title
   - Summary of changes
   - Reference to related issues
   - Screenshots (if UI-related)

5. **PR Requirements**:
   - ✅ All tests pass
   - ✅ Code builds without warnings
   - ✅ At least 1 approval from maintainer
   - ✅ All conversations resolved
   - ✅ Commits are squashed if needed

### 7. API Design Guidelines

#### Endpoint Naming

- Use **nouns** for resource names (not verbs)
- Use **plural** for collections
- Use **sub-resources** for nested relationships

```csharp
// ✅ Good
GET    /api/expenses           // List all expenses
POST   /api/expenses           // Create expense
GET    /api/expenses/{id}      // Get specific expense
PUT    /api/expenses/{id}      // Update expense
DELETE /api/expenses/{id}      // Delete expense

GET    /api/expenses/{id}/attachments     // Sub-resource
GET    /api/categories/{id}/vendor-rules  // Sub-resource

// ❌ Avoid
GET    /api/getExpenses
POST   /api/createExpense
GET    /api/expense/{id}/getAttachments
```

#### HTTP Status Codes

- **200 OK** - Successful GET, PUT
- **201 Created** - Successful POST (include Location header)
- **204 No Content** - Successful DELETE
- **400 Bad Request** - Invalid input validation
- **401 Unauthorized** - Missing/invalid authentication
- **403 Forbidden** - Authenticated but insufficient permissions
- **404 Not Found** - Resource doesn't exist
- **409 Conflict** - Duplicate resource or state conflict
- **422 Unprocessable Entity** - Semantic errors
- **500 Internal Server Error** - Unexpected server error

#### DTOs for API Responses

Always use DTOs, never expose entity models directly:

```csharp
// In DTOs folder
public class ExpenseDto
{
    public int Id { get; set; }
    public string VendorName { get; set; } = "";
    public decimal Amount { get; set; }
    public DateTime TransactionDate { get; set; }
    public string? Notes { get; set; }
}

// In Controller
[HttpGet("{id}")]
public async Task<ActionResult<ExpenseDto>> GetExpense(int id)
{
    var expense = await _expenseService.GetExpenseAsync(id);
    if (expense == null)
        return NotFound();
    
    return Ok(expense);
}
```

### 8. Service Layer Pattern

All business logic belongs in the service layer:

```csharp
public interface IExpenseService
{
    Task<ExpenseDto?> GetExpenseAsync(int id);
    Task<IEnumerable<ExpenseDto>> GetUserExpensesAsync(int userId, int skip, int take);
    Task<ExpenseDto> CreateExpenseAsync(CreateExpenseRequest request, int userId);
    Task<bool> UpdateExpenseAsync(int id, UpdateExpenseRequest request, int userId);
    Task<bool> DeleteExpenseAsync(int id, int userId);
}

public class ExpenseService : IExpenseService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IMapper _mapper;
    private readonly ILogger<ExpenseService> _logger;

    public ExpenseService(IUnitOfWork unitOfWork, IMapper mapper, ILogger<ExpenseService> logger)
    {
        _unitOfWork = unitOfWork;
        _mapper = mapper;
        _logger = logger;
    }

    public async Task<ExpenseDto> CreateExpenseAsync(CreateExpenseRequest request, int userId)
    {
        // Validation
        if (request.Amount <= 0)
            throw new ValidationException("Amount must be greater than 0");

        // Business logic
        var expense = new Expense
        {
            VendorName = request.VendorName,
            Amount = request.Amount,
            UserId = userId,
            TransactionDate = request.TransactionDate,
            CategoryId = request.CategoryId
        };

        // Persistence through unit of work
        await _unitOfWork.ExpenseRepository.AddAsync(expense);
        await _unitOfWork.SaveAsync();

        _logger.LogInformation("Expense created: {ExpenseId} for user {UserId}", expense.Id, userId);

        return _mapper.Map<ExpenseDto>(expense);
    }
}
```

### 9. Logging Standards

Use structured logging with proper levels:

```csharp
// Debug - diagnostic info
_logger.LogDebug("Querying expenses for user {UserId} with filter {Filter}", userId, filter);

// Information - general flow
_logger.LogInformation("Expense created: {ExpenseId} for user {UserId}", expense.Id, userId);

// Warning - something unexpected but recovered
_logger.LogWarning("Failed to parse AI response, using default. Response: {Response}", response);

// Error - failure but application continues
_logger.LogError(ex, "Failed to upload receipt for user {UserId}", userId);

// Critical - application stopping
_logger.LogCritical(ex, "Database connection failed, application cannot continue");
```

### 10. Security Best Practices

- **Never** commit secrets - use `dotnet user-secrets` or environment variables
- **Always** validate user input on the server side
- **Sanitize** any output going to frontend
- **Use** parameterized queries (EF Core handles this)
- **Implement** proper authorization checks:

```csharp
// ❌ Don't trust client
[HttpDelete("{id}")]
public async Task<IActionResult> DeleteExpense(int id)
{
    await _expenseService.DeleteExpenseAsync(id); // Anyone could delete anything
}

// ✅ Verify ownership
[HttpDelete("{id}")]
[Authorize]
public async Task<IActionResult> DeleteExpense(int id)
{
    var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
    var success = await _expenseService.DeleteExpenseAsync(id, int.Parse(userId));
    if (!success)
        return Unauthorized();
    
    return NoContent();
}
```

### 11. Testing Requirements

- Write tests for critical business logic
- Use the `Tests` folder structure matching source
- Follow AAA pattern (Arrange, Act, Assert)

```csharp
[Fact]
public async Task CreateExpense_WithValidData_ReturnsCreatedExpense()
{
    // Arrange
    var request = new CreateExpenseRequest
    {
        VendorName = "Grocery Store",
        Amount = 45.50m,
        TransactionDate = DateTime.Now
    };
    var userId = 1;

    // Act
    var result = await _expenseService.CreateExpenseAsync(request, userId);

    // Assert
    Assert.NotNull(result);
    Assert.Equal("Grocery Store", result.VendorName);
    Assert.Equal(45.50m, result.Amount);
}
```

### 12. Code Review Checklist

When reviewing PRs, verify:

- [ ] Code follows naming and style conventions
- [ ] No hardcoded secrets or credentials
- [ ] Proper error handling
- [ ] Appropriate logging levels
- [ ] SQL injection prevention (parameterized queries)
- [ ] Authorization checks where needed
- [ ] Tests cover new functionality
- [ ] Documentation updated
- [ ] No performance degradation

### 13. Reporting Issues

When reporting bugs, include:

1. **Reproduction steps** - Exact steps to reproduce
2. **Expected behavior** - What should happen
3. **Actual behavior** - What actually happened
4. **Environment** - OS, .NET version, database
5. **Logs/Error messages** - Full stack trace if available
6. **Screenshots** - If applicable

Example:

```
Title: Budget calculation incorrect for partial months

Reproduction:
1. Create budget of $500 for January
2. Add expenses on Jan 5th ($200)
3. View budget status on Jan 15th
4. Expected: Shows $200/$500 spent
5. Actual: Shows $200/$1500 spent (calculated as 3 months)

Environment: Windows 11, .NET 8.0.1, SQL Server 2019
```

## Questions?

Feel free to:
- Open a GitHub Discussion for general questions
- Open an Issue for bugs or feature requests
- Contact maintainers for sensitive concerns

---

**Happy coding!** 🚀
