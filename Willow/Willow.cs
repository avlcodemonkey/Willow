using System;
using System.Collections;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.Data.Common;
using System.Linq;
using System.Reflection;
using Dapper;
using FastMember;

namespace Willow {

    public static class DataExtensions {

        /// <summary>
        /// Get an attribute for a property if it exists.
        /// </summary>
        /// <typeparam name="T">Attribute to check for.</typeparam>
        /// <param name="member">Property to check against.</param>
        /// <returns>Returns attribute if member has the attribute, else false.</returns>
        public static T GetMemberAttribute<T>(this Member member) where T : Attribute {
            return GetPrivateField<MemberInfo>(member, "member").GetCustomAttribute<T>();
        }

        /// <summary>
        /// Get details about a private field in a class.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="obj"></param>
        /// <param name="name"></param>
        /// <returns></returns>
        public static T GetPrivateField<T>(this object obj, string name) {
            BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
            Type type = obj.GetType();
            FieldInfo field = type.GetField(name, flags);
            return (T)field.GetValue(obj);
        }

        /// <summary>
        /// Check if a member has an attribute assigned to it.
        /// </summary>
        /// <typeparam name="T">Attribute to check for.</typeparam>
        /// <param name="member">Property to check against.</param>
        /// <returns>Returns true if member has attribute set, else false.</returns>
        public static bool HasAttribute<T>(this Member member) where T : Attribute {
            return member.GetMemberAttribute<T>() != null;
        }

        /// <summary>
        /// Convert a object to an int. Defaults to zero.
        /// </summary>
        /// <param name="val">Object value to convert.</param>
        /// <returns>Integer value.</returns>
        public static int ToInt(this object val) {
            return (val.ToString() ?? "").ToInt();
        }
    }

    /// <summary>
    /// Attribute for specifying has many relationships.
    /// </summary>
    [AttributeUsage(AttributeTargets.Class, Inherited = true, AllowMultiple = true)]
    public class HasMany : Attribute {

        public HasMany(Type childType) {
            ChildType = childType;
        }

        public Type ChildType { get; set; }
    }

    /// <summary>
    /// Attribute that specifies a property should be ignored when inserting into or updating the db.
    /// </summary>
    [AttributeUsage(AttributeTargets.Property, Inherited = true)]
    public class Ignore : Attribute {

        public Ignore() {
        }
    }

    /// <summary>
    /// Simple stored procedure based ORM. Suports find by ID, save, delete.
    /// </summary>
    public class ORM {
        /// <summary>
        /// Specifies the type of properties that should be including when building sql parameters to save an object.
        /// </summary>
        private static readonly Type[] SavableTypes = { typeof(string), typeof(bool), typeof(int), typeof(long), typeof(DateTime), typeof(DateTimeOffset),
            typeof(decimal), typeof(int?), typeof(long?), typeof(Byte[]), typeof(Enum), typeof(double) };

        [Ignore]
        public DateTimeOffset DateCreated { get; set; }
        [Ignore]
        public DateTimeOffset DateUpdated { get; set; }
        public int Id { get; set; }
        private static int? CurrentUserId { get { return null; /* add your own implementation here for auditing */ } }

        /// <summary>
        /// Delete an object from the database.
        /// </summary>
        /// <param name="id">ID of the object to delete.</param>
        /// <param name="type">Type of object to delete.</typeparam>
        public static void Delete(int id, Type type) {
            using (var conn = GetOpenConnection()) {
                conn.Execute($"{type.Name}Delete", new { RequestUserId = CurrentUserId, Id = id }, commandType: CommandType.StoredProcedure);
            }
        }

        /// <summary>
        /// Run a stored procedure that doesn't return a result.
        /// </summary>
        /// <param name="procName">Name of the stored procedure to run.</param>
        /// <param name="parameters">Parameters to pass to stored procedure.</param>
        public static void Execute(string procName, object parameters) {
            using (var conn = GetOpenConnection()) {
                conn.Execute(procName, parameters, commandType: CommandType.StoredProcedure);
            }
        }

        /// <summary>
        /// Find a record in the database by Id and load into the specified object type.
        /// </summary>
        /// <typeparam name="T">Any object type derived from this class.</typeparam>
        /// <param name="id">Id of the record to load</param>
        /// <returns>Returns a new object of type T or null.</returns>
        public static T Get<T>(int id) where T : ORM {
            if (id == 0) {
                return null;
            }
            using (var conn = GetOpenConnection()) {
                return conn.Query<T>($"{typeof(T).Name}Get", new { Id = id }, commandType: CommandType.StoredProcedure).FirstOrDefault();
            }
        }

        /// <summary>
        /// Find all the records of the requested type and return as objects. Does not load children.
        /// </summary>
        /// <typeparam name="T">Any object type derived from this class.</typeparam>
        /// <param name="parameters">Sql parameters. Each property is the parameter name and the value is the param value.</param>
        /// <returns>Return enumerable of objects.</returns>
        public static IEnumerable<T> GetAll<T>(object parameters = null) where T : ORM {
            using (var conn = GetOpenConnection()) {
                return conn.Query<T>($"{typeof(T).Name}Get", parameters, commandType: CommandType.StoredProcedure).ToArray();
            }
        }

        /// <summary>
        /// Get an open connection.
        /// </summary>
        /// <param name="connectionStringName">Name of the connection string to use.</param>
        /// <returns>Returns an open dbConnection object.</returns>
        public static DbConnection GetOpenConnection(string connectionStringName = "Default") {
            var connString = ConfigurationManager.ConnectionStrings[connectionStringName];
            var conn = DbProviderFactories.GetFactory(connString.ProviderName).CreateConnection();
            conn.ConnectionString = connString.ConnectionString;
            conn.Open();
            return conn;
        }

        /// <summary>
        /// Run a stored procedure.
        /// </summary>
        /// <typeparam name="T">Any object type derived from this class.</typeparam>
        /// <param name="procName">Name of the stored procedure to run.</param>
        /// <param name="parameters">Parameters to pass to stored procedure.</param>
        /// <param name="connectionName">Use the named connection string instead of the default one.</param>
        /// <returns>Returns a new object of type T.</returns>
        public static IEnumerable<T> Query<T>(string procName, object parameters = null) {
            using (var conn = GetOpenConnection()) {
                return conn.Query<T>(procName, parameters, commandType: CommandType.StoredProcedure);
            }
        }

        /// <summary>
        /// Delete an object from the database.
        /// </summary>
        public void Delete() {
            var myType = GetType();
            using (var conn = GetOpenConnection()) {
                // @todo need to handle deleting related objects
                conn.Execute($"{myType.Name}Delete", new { RequestUserId = CurrentUserId, Id = myType.GetProperty("Id").GetValue(this) }, commandType: CommandType.StoredProcedure);
            }
        }

        /// <summary>
        /// Saves the object to the database. Includes any children.
        /// </summary>
        /// <param name="lazySave">Save children objects if true.</param>
        /// <returns>true</returns>
        public void Save(bool lazySave = true, bool forceSaveNulls = false) {
            var myType = GetType();
            using (var conn = GetOpenConnection()) {
                // build the parameters for saving
                var paramList = new DynamicParameters();
                paramList.Add("RequestUserId", CurrentUserId);

                var accessor = TypeAccessor.Create(myType);

                // iterate through all the properties of the object adding to param list
                accessor.GetMembers().Where(x => (SavableTypes.Contains(x.Type) || SavableTypes.Contains(x.Type.BaseType)) && !x.HasAttribute<Ignore>()).ToList().ForEach(x => {
                    var val = accessor[this, x.Name];
                    if (x.Type.BaseType == typeof(Enum)) {
                        val = Enum.GetName(x.Type, val) ?? val.ToString();
                    }
                    paramList.Add(x.Name, val, null, x.Name.ToLower() == "id" ? ParameterDirection.InputOutput : ParameterDirection.Input);
                });

                conn.Execute($"{myType.Name}Save", paramList, commandType: CommandType.StoredProcedure);
                var id = paramList.Get<int>("Id");
                accessor[this, "Id"] = id;

                if (!lazySave) {
                    return;
                }
                // process the hasMany relationships
                Attribute.GetCustomAttributes(myType, typeof(HasMany)).ToList().ForEach(x => {
                    var childType = ((HasMany)x)?.ChildType;
                    if (childType == null) {
                        return;
                    }

                    var children = (IList)accessor[this, childType.Name];
                    if (children == null && !forceSaveNulls) {
                        return;
                    }
                    var existingIds = new List<int>();
                    try {
                        // first lets get the full list from the db so we can figure out who to delete
                        var childParams = new DynamicParameters();
                        childParams.Add($"{myType.Name}Id", id);
                        var res = conn.Query($"{childType.Name}Get", childParams, commandType: CommandType.StoredProcedure);
                        res.Select(y => y.Id).ToList().ForEach(y => existingIds.Add(y));
                    } catch { }

                    if (children != null && children.Count > 0) {
                        var childAccessor = TypeAccessor.Create(childType);
                        var saveMethod = childType.GetMethod("Save");
                        foreach (var child in children) {
                            // remove this id from the list that has to be cleaned up later
                            existingIds.Remove(childAccessor[child, "Id"].ToInt());
                            // make sure the parent Id is set on the child object
                            childAccessor[child, $"{myType.Name}Id"] = id;
                            // now save the child
                            saveMethod.Invoke(child, new object[] { lazySave, forceSaveNulls });
                        }
                    }

                    // delete any child ids are that are leftover
                    existingIds.ForEach(y => Delete(y.ToInt(), childType));
                });
                conn.Close();
            }
        }
    }
}