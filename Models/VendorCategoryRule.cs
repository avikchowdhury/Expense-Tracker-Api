using System.ComponentModel.DataAnnotations;

namespace ExpenseTracker.Api.Models
{
    public class VendorCategoryRule
    {
        public int Id { get; set; }

        [Required]
        public int UserId { get; set; }

        public User? User { get; set; }

        [Required]
        public int CategoryId { get; set; }

        public Category? Category { get; set; }

        [Required]
        [MaxLength(255)]
        public string VendorPattern { get; set; } = string.Empty;

        public bool IsActive { get; set; } = true;

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}
