using System.ComponentModel.DataAnnotations;
using Willow;

namespace WillowORM {

    /// <summary>
    ///   UserRole is the link between a role and a user.
    /// </summary>
    public class UserRole : ORM {
        [Required]
        public int RoleId { get; set; }
        [Required]
        public int UserId { get; set; }
    }
}