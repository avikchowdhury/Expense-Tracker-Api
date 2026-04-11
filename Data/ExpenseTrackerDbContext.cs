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
        public DbSet<VendorCategoryRule> VendorCategoryRules => Set<VendorCategoryRule>();

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
                receipt.Property(x => x.TotalAmount).HasPrecision(18, 2);
                receipt.HasIndex(x => new { x.UserId, x.UploadedAt });
                receipt.HasOne(x => x.User)
                    .WithMany(x => x.Receipts)
                    .HasForeignKey(x => x.UserId)
                    .OnDelete(DeleteBehavior.Cascade);
            });

            modelBuilder.Entity<Expense>(expense =>
            {
                expense.HasKey(x => x.Id);
                expense.Property(x => x.Amount).HasPrecision(18, 2);
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
                    .OnDelete(DeleteBehavior.NoAction);
            });

            modelBuilder.Entity<Budget>(budget =>
            {
                budget.HasKey(x => x.Id);
                budget.Property(x => x.MonthlyLimit).HasPrecision(18, 2);
                budget.Property(x => x.CurrentSpent).HasPrecision(18, 2);
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

            modelBuilder.Entity<VendorCategoryRule>(rule =>
            {
                rule.HasKey(x => x.Id);
                rule.Property(x => x.VendorPattern).HasMaxLength(255);
                rule.HasIndex(x => new { x.UserId, x.VendorPattern }).IsUnique();
                rule.HasOne(x => x.User)
                    .WithMany()
                    .HasForeignKey(x => x.UserId)
                    .OnDelete(DeleteBehavior.Cascade);
                rule.HasOne(x => x.Category)
                    .WithMany(x => x.VendorRules)
                    .HasForeignKey(x => x.CategoryId)
                    .OnDelete(DeleteBehavior.Cascade);
            });
        }
    }
}
