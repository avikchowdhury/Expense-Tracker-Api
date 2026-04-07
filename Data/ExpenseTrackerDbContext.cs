using ExpenseTracker.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace ExpenseTracker.Api.Data
{
    public class ExpenseTrackerDbContext : DbContext
    {
        public ExpenseTrackerDbContext(DbContextOptions<ExpenseTrackerDbContext> options) : base(options)
        {
        }

        public DbSet<User> Users => Set<User>();
        public DbSet<Receipt> Receipts => Set<Receipt>();
        public DbSet<Expense> Expenses => Set<Expense>();
        public DbSet<Budget> Budgets => Set<Budget>();

        public DbSet<Category> Categories => Set<Category>();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<User>(user =>
            {
                user.HasKey(x => x.Id);
                user.HasIndex(x => x.Email).IsUnique();
            });

            modelBuilder.Entity<Receipt>(receipt =>
            {
                receipt.HasKey(x => x.Id);
                receipt.HasOne(x => x.User)
                    .WithMany(x => x.Receipts)
                    .HasForeignKey(x => x.UserId)
                    .OnDelete(DeleteBehavior.Cascade);
            });

            modelBuilder.Entity<Expense>(expense =>
            {
                expense.HasKey(x => x.Id);
                expense.HasOne(x => x.User)
                    .WithMany(x => x.Expenses)
                    .HasForeignKey(x => x.UserId)
                    .OnDelete(DeleteBehavior.Cascade);

                expense.HasOne(x => x.Category)
                    .WithMany()
                    .HasForeignKey(x => x.CategoryId)
                    .OnDelete(DeleteBehavior.SetNull);

                expense.HasOne(x => x.Receipt)
                    .WithMany(x => x.Expenses)
                    .HasForeignKey(x => x.ReceiptId)
                    .OnDelete(DeleteBehavior.Cascade);
            });

            modelBuilder.Entity<Budget>(budget =>
            {
                budget.HasKey(x => x.Id);
                budget.HasOne(x => x.User)
                    .WithMany(x => x.Budgets)
                    .HasForeignKey(x => x.UserId)
                    .OnDelete(DeleteBehavior.Cascade);
            });

            modelBuilder.Entity<Category>(category =>
            {
                category.HasKey(x => x.Id);
                category.HasIndex(x => new { x.UserId, x.Name }).IsUnique();
            });
        }
    }
}
