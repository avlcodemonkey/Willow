using System;
using System.Linq;
using Willow;

namespace WillowORM {
    class Program {
        static void Main(string[] args) {
            var user = new User() { UID = "UserName", FirstName = "John", LastName = "Doe", Email = "email@domain.com", LanguageCode = "en" };
            user.Save();
            Console.WriteLine("User saved with id=" + user.Id);

            var role = new Role() { Name = "test" };
            role.Save();
            Console.WriteLine("Role saved with id=" + role.Id);

            var users = ORM.GetAll<User>();
            if (users.Any()) {
                foreach (var u in users) {
                    // do something with users
                    Console.WriteLine("UID=" + u.UID);
                }

                var myUser = users.First();
                var myUserRole = new UserRole() { RoleId = role.Id, UserId = myUser.Id };
                myUser.UserRole = new[] { myUserRole };
                myUser.Save();
                Console.WriteLine("UserID=" + myUser.UID);

                var newUser = ORM.Get<User>(myUser.Id);
                foreach (var userRole in newUser.UserRole) {
                    Console.WriteLine("Deleting role id=" + userRole.Id);
                    userRole.Delete();
                }
            }

            Console.ReadKey();
        }
    }
}
