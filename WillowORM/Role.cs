using System.ComponentModel.DataAnnotations;
using Willow;

namespace WillowORM
{
    /// <summary>
    ///   Role represents a single role.
    /// </summary>
    [HasMany(typeof (RolePermission))]
    public class Role : ORM {
        public int Id { get; set; }

        [Required]
        [MaxLength(100)]
        public string Name { get; set; }

        public RolePermission[] RolePermission { get; set; }

    }
}