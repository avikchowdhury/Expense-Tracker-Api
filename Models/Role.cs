using System.ComponentModel.DataAnnotations;

namespace ExpenseTracker.Api.Models
{
    public class Role
    {
        public int Id { get; set; }

        [Required]
        [MaxLength(64)]
        public string Name { get; set; } = null!;

        [Required]
        [MaxLength(64)]
        public string NormalizedName { get; set; } = null!;

        public ICollection<UserRoleMapping> UserMappings { get; set; } = new List<UserRoleMapping>();
    }
}
