using ExpenseTracker.Api.Data;
using ExpenseTracker.Api.Models;
using ExpenseTracker.Api.Services;
using Microsoft.EntityFrameworkCore;

namespace ExpenseTracker.Api.Tests.Services;

public class BudgetAdvisorServiceTests
{
    [Fact]
    public async Task GetBudgetAdvisorAsync_ComputesProjectedSpendAndCategoryRisk()
    {
        await using var dbContext = CreateDbContext();
        SeedBudgetScenario(dbContext);
        var unitOfWork = new UnitOfWork(dbContext);
        var service = new BudgetAdvisorService(unitOfWork);

        var snapshot = await service.GetBudgetAdvisorAsync(
            userId: 12,
            referenceUtc: new DateTime(2026, 4, 15, 10, 0, 0, DateTimeKind.Utc));

        Assert.Equal(700m, snapshot.TotalBudget);
        Assert.Equal(350m, snapshot.CurrentSpend);
        Assert.Equal(700m, snapshot.ProjectedSpend);
        Assert.Equal("critical", snapshot.PaceStatus);
        Assert.Equal("Food", snapshot.Categories.First().Category);
        Assert.Equal("critical", snapshot.Categories.First().RiskLevel);
        Assert.Equal(600m, snapshot.Categories.First().ProjectedSpend);
        Assert.Contains(snapshot.Recommendations, recommendation => recommendation.Contains("Food", StringComparison.OrdinalIgnoreCase));
    }

    private static void SeedBudgetScenario(ExpenseTrackerDbContext dbContext)
    {
        dbContext.Users.Add(new User
        {
            Id = 12,
            Email = "tests@example.com",
            PasswordHash = "hash"
        });

        dbContext.Categories.AddRange(
            new Category { Id = 101, UserId = 12, Name = "Food" },
            new Category { Id = 102, UserId = 12, Name = "Travel" });

        dbContext.Budgets.AddRange(
            new Budget { Id = 201, UserId = 12, Category = "Food", MonthlyLimit = 500m },
            new Budget { Id = 202, UserId = 12, Category = "Travel", MonthlyLimit = 200m });

        dbContext.Expenses.AddRange(
            new Expense { Id = 301, UserId = 12, CategoryId = 101, Date = new DateTime(2026, 4, 2), Amount = 200m },
            new Expense { Id = 302, UserId = 12, CategoryId = 101, Date = new DateTime(2026, 4, 10), Amount = 100m },
            new Expense { Id = 303, UserId = 12, CategoryId = 102, Date = new DateTime(2026, 4, 6), Amount = 50m },
            new Expense { Id = 304, UserId = 12, CategoryId = 101, Date = new DateTime(2026, 3, 10), Amount = 120m },
            new Expense { Id = 305, UserId = 12, CategoryId = 101, Date = new DateTime(2026, 2, 10), Amount = 90m },
            new Expense { Id = 306, UserId = 12, CategoryId = 101, Date = new DateTime(2026, 1, 10), Amount = 60m });

        dbContext.SaveChanges();
    }

    private static ExpenseTrackerDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<ExpenseTrackerDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        return new ExpenseTrackerDbContext(options);
    }
}
