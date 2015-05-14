using System.ComponentModel.DataAnnotations;
using Willow;

namespace WillowORM
{
    /// <summary>
    ///   UserRole is the link between a role and a user.
    /// </summary>
    [BelongsTo(typeof (Role))]
    public class UserRole : ORM {
        public int Id { get; set; }

        [Required]
        public int UserId { get; set; }

        [Required]
        public int RoleId { get; set; }
    }
}