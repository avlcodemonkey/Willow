using System.ComponentModel.DataAnnotations;
using Willow;

namespace WillowORM {
    /// <summary>
    ///   Permission represents a single permission. Normally a combination of action and controller.
    /// </summary>
    public class Permission : ORM {
        [Required]
        [MaxLength(100)]
        public string Action { get; set; }
        [Required]
        [MaxLength(100)]
        public string Controller { get; set; }
    }
}