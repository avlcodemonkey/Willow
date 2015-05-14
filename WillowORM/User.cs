using System.ComponentModel.DataAnnotations;
using Willow;

namespace WillowORM
{
    /// <summary>
    ///   User represents a single site user.
    /// </summary>
    [HasMany(typeof (UserRole))]
    public class User : ORM {
        public int Id { get; set; }

        [Required]
        [MaxLength(250)]
        public string UID { get; set; }

        [Required]
        [MaxLength(100)]
        public string FirstName { get; set; }

        [Required]
        [MaxLength(100)]
        public string LastName { get; set; }

        [Required,EmailAddress,MaxLength(100)]
        public string Email { get; set; }

        [MaxLength(100)]
        public string SMS { get; set; }

        [Required]
        public string LanguageCode { get; set; }

        public bool IsActive { get; set; }
        public string CurrentIp { get; set; }
        public UserRole[] UserRole { get; set; }

    }
}