using System.ComponentModel.DataAnnotations;

namespace ExpenseTracker.Api.Models
{
    public class Category
    {
        [Key]
        public int Id { get; set; }
        [Required]
        public int UserId { get; set; }
        [Required]
        public string Name { get; set; } = string.Empty;

        public ICollection<VendorCategoryRule> VendorRules { get; set; } = new List<VendorCategoryRule>();
    }
}
