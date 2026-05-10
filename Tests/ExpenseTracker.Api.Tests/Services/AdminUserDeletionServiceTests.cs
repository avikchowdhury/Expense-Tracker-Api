using ExpenseTracker.Api.Data;
using ExpenseTracker.Api.Dtos;
using ExpenseTracker.Api.Models;
using ExpenseTracker.Api.Security;
using ExpenseTracker.Api.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace ExpenseTracker.Api.Tests.Services;

public sealed class AdminUserDeletionServiceTests
{
    [Fact]
    public async Task DeleteUsersAsync_RemovesUserDataAndStoredFiles()
    {
        var tempRoot = Path.Combine(Path.GetTempPath(), "expense-tracker-admin-delete-" + Guid.NewGuid().ToString("N"));
        var avatarsPath = Path.Combine(tempRoot, "avatars");
        var receiptsPath = Path.Combine(tempRoot, "receipts");
        Directory.CreateDirectory(avatarsPath);
        Directory.CreateDirectory(receiptsPath);

        try
        {
            await using var dbContext = CreateDbContext();
            SeedDeleteScenario(dbContext, avatarsPath, receiptsPath);
            var unitOfWork = new UnitOfWork(dbContext);
            var userRoleRepository = new UserRoleRepository(dbContext);
            var userRoleService = new UserRoleService(userRoleRepository, unitOfWork);
            var adminUserDeletionRepository = new AdminUserDeletionRepository(dbContext);

            var service = new AdminUserDeletionService(
                adminUserDeletionRepository,
                unitOfWork,
                userRoleService,
                new FileStoragePaths
                {
                    RootPath = tempRoot,
                    AvatarsPath = avatarsPath,
                    ReceiptsPath = receiptsPath
                },
                NullLogger<AdminUserDeletionService>.Instance);

            var result = await service.DeleteUsersAsync(actingUserId: 1, userIds: new[] { 2 });

            Assert.Equal(1, result.DeletedCount);
            Assert.Equal(1, result.DeletedReceiptCount);
            Assert.Equal(1, result.DeletedExpenseCount);
            Assert.Equal(1, result.DeletedBudgetCount);
            Assert.Equal(1, result.DeletedCategoryCount);
            Assert.Equal(1, result.DeletedVendorRuleCount);
            Assert.Equal(new[] { 2 }, result.DeletedUserIds);

            Assert.False(await dbContext.Users.AnyAsync(user => user.Id == 2));
            Assert.False(await dbContext.Receipts.AnyAsync(receipt => receipt.UserId == 2));
            Assert.False(await dbContext.Expenses.AnyAsync(expense => expense.UserId == 2));
            Assert.False(await dbContext.Budgets.AnyAsync(budget => budget.UserId == 2));
            Assert.False(await dbContext.Categories.AnyAsync(category => category.UserId == 2));
            Assert.False(await dbContext.VendorCategoryRules.AnyAsync(rule => rule.UserId == 2));
            Assert.False(await dbContext.UserRoleMappings.AnyAsync(mapping => mapping.UserId == 2));
            Assert.True(await dbContext.Users.AnyAsync(user => user.Id == 1));
            Assert.False(File.Exists(Path.Combine(receiptsPath, "receipt-2.pdf")));
            Assert.False(File.Exists(Path.Combine(avatarsPath, "2.png")));
        }
        finally
        {
            if (Directory.Exists(tempRoot))
            {
                Directory.Delete(tempRoot, recursive: true);
            }
        }
    }

    [Fact]
    public async Task DeleteUsersAsync_RejectsDeletingCurrentAdmin()
    {
        await using var dbContext = CreateDbContext();
        SeedAdminOnlyScenario(dbContext);
        var unitOfWork = new UnitOfWork(dbContext);
        var userRoleRepository = new UserRoleRepository(dbContext);
        var userRoleService = new UserRoleService(userRoleRepository, unitOfWork);
        var adminUserDeletionRepository = new AdminUserDeletionRepository(dbContext);

        var service = new AdminUserDeletionService(
            adminUserDeletionRepository,
            unitOfWork,
            userRoleService,
            new FileStoragePaths
            {
                RootPath = "storage",
                AvatarsPath = Path.Combine("storage", "avatars"),
                ReceiptsPath = Path.Combine("storage", "receipts")
            },
            NullLogger<AdminUserDeletionService>.Instance);

        var exception = await Assert.ThrowsAsync<ArgumentException>(() =>
            service.DeleteUsersAsync(actingUserId: 1, userIds: new[] { 1 }));

        Assert.Contains("delete your own account", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    private static ExpenseTrackerDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<ExpenseTrackerDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        return new ExpenseTrackerDbContext(options);
    }

    private static void SeedDeleteScenario(
        ExpenseTrackerDbContext dbContext,
        string avatarsPath,
        string receiptsPath)
    {
        File.WriteAllText(Path.Combine(receiptsPath, "receipt-2.pdf"), "receipt");
        File.WriteAllText(Path.Combine(avatarsPath, "2.png"), "avatar");

        dbContext.Roles.AddRange(
            new Role { Id = 10, Name = AppRoles.Admin, NormalizedName = "ADMIN" },
            new Role { Id = 11, Name = AppRoles.User, NormalizedName = "USER" });

        dbContext.Users.AddRange(
            new User
            {
                Id = 1,
                Email = "admin@example.com",
                PasswordHash = "hash",
                Role = AppRoles.Admin
            },
            new User
            {
                Id = 2,
                Email = "user@example.com",
                PasswordHash = "hash",
                Role = AppRoles.User,
                AvatarUrl = "/avatars/2.png"
            });

        dbContext.UserRoleMappings.AddRange(
            new UserRoleMapping { UserId = 1, RoleId = 10 },
            new UserRoleMapping { UserId = 2, RoleId = 11 });

        dbContext.Categories.Add(new Category
        {
            Id = 20,
            UserId = 2,
            Name = "Travel"
        });

        dbContext.VendorCategoryRules.Add(new VendorCategoryRule
        {
            Id = 21,
            UserId = 2,
            CategoryId = 20,
            VendorPattern = "airline",
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        });

        dbContext.Budgets.Add(new Budget
        {
            Id = 30,
            UserId = 2,
            Category = "Travel",
            MonthlyLimit = 500m,
            CurrentSpent = 120m,
            LastReset = DateTime.UtcNow
        });

        dbContext.Receipts.Add(new Receipt
        {
            Id = 40,
            UserId = 2,
            FileName = "receipt-2.pdf",
            BlobUrl = Path.Combine(receiptsPath, "receipt-2.pdf"),
            UploadedAt = DateTime.UtcNow,
            TotalAmount = 120m,
            Category = "Travel",
            ParsedContentJson = "{}"
        });

        dbContext.Expenses.Add(new Expense
        {
            Id = 41,
            UserId = 2,
            ReceiptId = 40,
            CategoryId = 20,
            Date = DateTime.UtcNow,
            Amount = 120m,
            Currency = "USD"
        });

        dbContext.SaveChanges();
    }

    private static void SeedAdminOnlyScenario(ExpenseTrackerDbContext dbContext)
    {
        dbContext.Roles.Add(new Role { Id = 10, Name = AppRoles.Admin, NormalizedName = "ADMIN" });
        dbContext.Users.Add(new User
        {
            Id = 1,
            Email = "admin@example.com",
            PasswordHash = "hash",
            Role = AppRoles.Admin
        });
        dbContext.UserRoleMappings.Add(new UserRoleMapping { UserId = 1, RoleId = 10 });
        dbContext.SaveChanges();
    }
}
