using System.ComponentModel.DataAnnotations;
using Willow;

namespace WillowORM {

    /// <summary>
    ///   User represents a single site user.
    /// </summary>
    [HasMany(typeof(UserRole))]
    public class User : ORM {
        public string CurrentIp { get; set; }
        [Required, EmailAddress, MaxLength(100)]
        public string Email { get; set; }
        [Required]
        [MaxLength(100)]
        public string FirstName { get; set; }
        public bool IsActive { get; set; }
        [Required]
        public string LanguageCode { get; set; }
        [Required]
        [MaxLength(100)]
        public string LastName { get; set; }
        [MaxLength(100)]
        public string SMS { get; set; }
        [Required]
        [MaxLength(250)]
        public string UID { get; set; }
        public UserRole[] UserRole { get; set; }
    }
}