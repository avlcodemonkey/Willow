using System.ComponentModel.DataAnnotations;
using Willow;

namespace WillowORM
{
    /// <summary>
    ///   RolePermission is the link between a role and a permission.
    /// </summary>
    [BelongsTo(typeof (Role))]
    [BelongsTo(typeof (Permission))]
    public class RolePermission : ORM {
        public int Id { get; set; }

        [Required]
        public int RoleId { get; set; }

        [Required]
        public int PermissionId { get; set; }

        public Permission Permission { get; set; }
    }
}