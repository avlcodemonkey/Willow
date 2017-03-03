using System.ComponentModel.DataAnnotations;
using Willow;

namespace WillowORM {

    /// <summary>
    ///   RolePermission is the link between a role and a permission.
    /// </summary>
    public class RolePermission : ORM {
        public Permission Permission { get; set; }
        [Required]
        public int PermissionId { get; set; }
        [Required]
        public int RoleId { get; set; }
    }
}