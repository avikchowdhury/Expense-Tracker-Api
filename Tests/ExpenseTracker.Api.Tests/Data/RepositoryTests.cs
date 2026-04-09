using ExpenseTracker.Api.Data;
using ExpenseTracker.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace ExpenseTracker.Api.Tests.Data;

public class RepositoryTests
{
    [Fact]
    public async Task Query_SupportsLinqFilteringAndOrdering()
    {
        await using var dbContext = CreateDbContext();
        dbContext.Categories.AddRange(
            new Category { Id = 1, UserId = 7, Name = "Groceries" },
            new Category { Id = 2, UserId = 7, Name = "Fuel" },
            new Category { Id = 3, UserId = 9, Name = "Travel" });
        await dbContext.SaveChangesAsync();

        var repository = new Repository<Category>(dbContext);

        var result = await repository.Query()
            .Where(category => category.UserId == 7)
            .OrderBy(category => category.Name)
            .Select(category => category.Name)
            .ToListAsync();

        Assert.Equal(new[] { "Fuel", "Groceries" }, result);
    }

    [Fact]
    public async Task AddUpdateRemoveAndFind_WorkAgainstTrackedEntities()
    {
        await using var dbContext = CreateDbContext();
        var repository = new Repository<Category>(dbContext);

        var category = new Category { Id = 10, UserId = 4, Name = "Dining" };
        await repository.AddAsync(category);
        await dbContext.SaveChangesAsync();

        var saved = await repository.FindAsync(category.Id);
        Assert.NotNull(saved);
        Assert.Equal("Dining", saved!.Name);

        saved.Name = "Dining Out";
        repository.Update(saved);
        await dbContext.SaveChangesAsync();

        var updatedName = await repository.Query()
            .Where(item => item.Id == category.Id)
            .Select(item => item.Name)
            .SingleAsync();
        Assert.Equal("Dining Out", updatedName);

        repository.Remove(saved);
        await dbContext.SaveChangesAsync();

        var existsAfterDelete = await repository.Query().AnyAsync(item => item.Id == category.Id);
        Assert.False(existsAfterDelete);
    }

    private static ExpenseTrackerDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<ExpenseTrackerDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        return new ExpenseTrackerDbContext(options);
    }
}
