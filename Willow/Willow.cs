using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Data;
using System.Data.Common;
using System.Data.SqlClient;
using System.Reflection;
using System.Linq;
using System.Security.Principal;
using System.Web;
using System.Configuration;
using Dapper;

namespace Willow
{
    /// <summary>
    /// Simple stored procedure based ORM. Suports find by ID, save, delete.
    /// </summary>
    public class ORM
    {
        /// <summary>
        /// Specifies the type of properties that should be including when building sql parameters to save an object.
        /// </summary>
        private static readonly Type[] SaveDataTypes = { typeof(string), typeof(bool), typeof(int), typeof(long), typeof(DateTime), typeof(decimal), typeof(int?), typeof(long?), typeof(Byte[]), typeof(Enum) };

        private List<string> ErrorList = new List<string>();

        /// <summary>
        /// Get an open connection.
        /// </summary>
        /// <param name="connectionStringName">Name of the connection string to use.</param>
        /// <returns>Returns an open dbConnection object.</returns>
        private static DbConnection GetOpenConnection(string connectionStringName = "Default")
        {
            
            if (connectionStringName == null || connectionStringName.Trim().Length == 0)
            {
                connectionStringName = "Default";
            }

            SqlConnection connection;
            connection = new SqlConnection(ConfigurationManager.ConnectionStrings[connectionStringName].ToString());
            connection.Open();

            return connection;
        }

        /// <summary>
        /// Run a stored procedure.
        /// </summary>
        /// <typeparam name="T">Any object type derived from this class.</typeparam>
        /// <param name="procName">Name of the stored procedure to run.</param>
        /// <param name="d">Dynamic parameters to pass to stored procedure.</param>
        /// <param name="connectionName">Use the named connection string instead of the default one.</param>
        /// <returns>Returns a new object of type T.</returns>
        public static IEnumerable<T> Query<T>(string procName, DynamicParameters d, string connectionName = null)
        {
            using (var conn = GetOpenConnection(connectionName))
            {
                var res = conn.Query<T>(procName, d, commandType: CommandType.StoredProcedure);
                conn.Close();
                return res;
            }
        }

        /// <summary>
        /// Run a stored procedure that doesn't return a result.
        /// </summary>
        /// <param name="procName">Name of the stored procedure to run.</param>
        /// <param name="d">Dynamic parameters to pass to stored procedure.</param>
        /// <param name="connectionName">Use the named connection string instead of the default one.</param>
        public static void Execute(string procName, DynamicParameters d, string connectionName = null)
        {
            using (var conn = GetOpenConnection())
            {
                conn.Execute(procName, d, commandType: CommandType.StoredProcedure);
                conn.Close();
            }
        }

        /// <summary>
        /// Find a recprd in the database by Id and load into the specified object type.
        /// </summary>
        /// <typeparam name="T">Any object type derived from this class.</typeparam>
        /// <param name="id">Id of the record to load</param>
        /// <param name="lazyLoad">Load the children of the object.</param>
        /// <returns>Returns a new object of type T.</returns>
        public static T Find<T>(int id, bool lazyLoad = false)
        {
            var res = (T)Convert.ChangeType(null, typeof(T));
            if (typeof(T).Name.Length > 0 && typeof(T).IsSubclassOf(typeof(ORM)))
            {
                using (var conn = GetOpenConnection())
                {
                    var result = conn.Query<T>(typeof(T).Name + "Get", new { Id = id }, commandType: CommandType.StoredProcedure);
                    conn.Close();

                    if (result.Any())
                    {
                        res = result.First();
                        if (lazyLoad)
                        {
                            FindChildren(res);
                        }
                    }
                }
            }
            return res;
        }

        /// <summary>
        /// Find the children of an object based on the data annotations in the class.
        /// </summary>
        /// <typeparam name="T">Any object type derived from this class.</typeparam>
        /// <param name="myObject">The current object to get children for.</param>
        /// <returns>The object with the children loaded into it.</returns>
        private static T FindChildren<T>(T myObject)
        {
            if (myObject.GetType().Name.Length > 0 && myObject.GetType().IsSubclassOf(typeof(ORM)))
            {
                // process hasMany relationships
                var children = Attribute.GetCustomAttributes(myObject.GetType(), typeof(HasMany));
                if (children.Length > 0)
                {
                    foreach (HasMany attr in children)
                    {
                        // the childtype is set, and there is a property in the object to store the child in, load the data
                        if (attr.ChildType != null && myObject.GetType().GetProperty(attr.ChildType.Name) != null)
                        {
                            var procName = attr.ChildType.Name + "GetFor" + myObject.GetType().Name;
                            using (var conn = GetOpenConnection())
                            {
                                var d = new DynamicParameters();
                                d.Add(myObject.GetType().Name + "Id", myObject.GetType().GetProperty("Id").GetValue(myObject));

                                var results = conn.Query(procName, d, commandType: CommandType.StoredProcedure);
                                conn.Close();

                                if (results.Any())
                                {
                                    // turn the dynamic data results into an array of objects
                                    var res = Array.CreateInstance(attr.ChildType, results.Count());
                                    var i = 0;
                                    foreach (var r in results)
                                    {
                                        res.SetValue(LoadFromData(attr.ChildType, r), i);
                                        i++;
                                    }

                                    // save the array of children back to the parent object
                                    var prop = myObject.GetType().GetProperty(attr.ChildType.Name);
                                    if (prop != null)
                                    {
                                        prop.SetValue(myObject, res);
                                    }
                                }
                            }
                        }
                    }
                }

                // process belongsTo relationships
                children = Attribute.GetCustomAttributes(myObject.GetType(), typeof(BelongsTo));
                if (children.Length > 0)
                {
                    foreach (BelongsTo attr in children)
                    {
                        // the childtype is set, and there is a property in the object to store the child in, load the data
                        if (attr.ParentType != null && myObject.GetType().GetProperty(attr.ParentType.Name) != null)
                        {
                            var procName = attr.ParentType.Name + "Get";
                            using (var conn = GetOpenConnection())
                            {
                                var d = new DynamicParameters();
                                d.Add("Id", myObject.GetType().GetProperty(attr.ParentType.Name + "Id").GetValue(myObject));

                                var results = conn.Query(procName, d, commandType: CommandType.StoredProcedure);
                                conn.Close();

                                if (results.Any())
                                {
                                    // turn the dynamic data results into an object
                                    var res = LoadFromData(attr.ParentType, results.First());

                                    // save the array of children back to the parent object
                                    var prop = myObject.GetType().GetProperty(attr.ParentType.Name);
                                    if (prop != null)
                                    {
                                        prop.SetValue(myObject, res);
                                    }
                                }
                            }
                        }
                    }
                }
            }
            return myObject;
        }

        /// <summary>
        /// Create an object of the requested type setting the properties from the dynamic data object.
        /// </summary>
        /// <param name="newType">Object type to create.</param>
        /// <param name="data">Dynamic data object to get values for properties from.</param>
        /// <returns>Returns a new object.</returns>
        private static dynamic LoadFromData(Type newType, dynamic data)
        {
            var obj = Convert.ChangeType(Activator.CreateInstance(newType), newType);

            // enumerating over it exposes the Properties and Values as a KeyValuePair
            foreach (KeyValuePair<string, object> kvp in data)
            {
                var p = obj.GetType().GetProperty(kvp.Key);
                if (p != null && kvp.Value != DBNull.Value)
                {
                    p.SetValue(obj, kvp.Value);
                }
            }

            obj = FindChildren(obj);

            return obj;
        }

        /// <summary>
        /// Find all the records of the requested type and return as objects. Does not load children.
        /// </summary>
        /// <typeparam name="T">Any object type derived from this class.</typeparam>
        /// <param name="parameters">Dynamic object of sql parameters. Each property is the parameter name and the value is the param value.</param>
        /// <returns>Return enumerable of objects.</returns>
        public static IEnumerable<T> FindAll<T>(DynamicParameters parameters = null)
        {
            var results = new T[0];
            if (typeof(T).Name.Length > 0 && typeof(T).IsSubclassOf(typeof(ORM)))
            {
                using (var conn = GetOpenConnection())
                {
                    results = conn.Query<T>(typeof(T).Name + "GetAll", parameters, commandType: CommandType.StoredProcedure).ToArray();
                    conn.Close();
                }
            }
            return results;
        }

        /// <summary>
        /// Validates the object using data annotations.
        /// </summary>
        /// <returns>Returns true if object and any children are valid, else false. Use GetErrorList to see errors.</returns>
        public bool IsValid() {
            ErrorList.Clear();
            var context = new ValidationContext(this, null, null);
            var errList = new List<ValidationResult>{};
            var result = Validator.TryValidateObject(this, context, errList, true);

            if (errList.Count > 0) {
                errList.ForEach(e => ErrorList.Add(e.ErrorMessage));
            }

            if (result) {
                // process the hasMany relationships
                var children = Attribute.GetCustomAttributes(this.GetType(), typeof (HasMany));
                if (children.Length > 0) {
                    foreach (HasMany attr in children) {
                        if (attr.ChildType != null && this.GetType().GetProperty(attr.ChildType.Name) != null) {
                            var propChildren = (Array) this.GetType().GetProperty(attr.ChildType.Name).GetValue(this);
                            if (propChildren != null && propChildren.Length > 0) {
                                foreach (var propChild in propChildren) {
                                    result = (bool) propChild.GetType().GetMethod("IsValid").Invoke(propChild, new object[] {});
                                    if (!result) {
                                        ErrorList.AddRange((List<string>)propChild.GetType().GetMethod("GetErrorList").Invoke(propChild, new object[] {}));
                                    }
                                }
                            }
                        }
                    }
                }
            }

                // @todo process belongsTo relationships

            return result;
        }

        /// <summary>
        /// Get the list of errors from validating an object.
        /// </summary>
        /// <returns>Returns a list of error messages.</returns>
        public List<string> GetErrorList() {
            return ErrorList;
        } 

        /// <summary>
        /// Saves the object to the database. Includes any children.
        /// </summary>
        /// <returns>true</returns>
        public bool Save()
        {
            var res = false;

            if (!(this.GetType().Name.Length > 0 && this.GetType().IsSubclassOf(typeof (ORM)))) {
                return false;
            }

            if (!IsValid()) {
                return false;
            }

            var procName = this.GetType().Name + "Save";
            using (var conn = GetOpenConnection())
            {

                // build the parameters for saving
                var paramList = new DynamicParameters();
                var username = HttpContext.Current != null && HttpContext.Current.User.Identity.Name.Length > 0
                    ? HttpContext.Current.User.Identity.Name
                    : (WindowsIdentity.GetCurrent() != null ? WindowsIdentity.GetCurrent().Name : "");

                paramList.Add("UserName", username);
                paramList.Add("IP", GetIp());

                Type type = this.GetType();
                PropertyInfo[] properties = type.GetProperties();

                // get the objects primary key
                var id = properties.Single(s => s.Name.ToLower() == "id").GetValue(this).ToString();
                // iterate through all the properties of the object adding to param list
                foreach (PropertyInfo property in properties)
                {
                    // only save allowed data types to db
                    if (SaveDataTypes.Contains(property.PropertyType) || SaveDataTypes.Contains(property.PropertyType.BaseType))
                    {
                        // check if this property should be ignore based on the data annotations
                        var attr = ((IgnoreOn)Attribute.GetCustomAttribute(property, typeof(IgnoreOn)));
                        bool ignore = false;
                        if (attr != null)
                        {
                            ignore = String.IsNullOrEmpty(id) || id == "0" ? attr.Insert : attr.Update;
                        }

                        // add this param
                        if (!ignore)
                        {
                            if (property.PropertyType.BaseType == typeof(Enum))
                            {
                                paramList.Add(property.Name, Enum.GetName((Type)property.PropertyType, property.GetValue(this)) ?? property.GetValue(this).ToString(), null,
                                    property.Name.ToLower() == "id" ? ParameterDirection.InputOutput : ParameterDirection.Input);

                            }
                            else
                            {
                                paramList.Add(property.Name, property.GetValue(this), null,
                                    property.Name.ToLower() == "id" ? ParameterDirection.InputOutput : ParameterDirection.Input);
                            }
                        }
                    }
                }

                var results = conn.Execute(procName, paramList, commandType: CommandType.StoredProcedure);

                this.GetType().GetProperty("Id").SetValue(this, paramList.Get<int>("Id"));

                // process the hasMany relationships
                var children = Attribute.GetCustomAttributes(this.GetType(), typeof(HasMany));
                if (children.Length > 0)
                {
                    foreach (HasMany attr in children)
                    {
                        if (attr.ChildType != null && this.GetType().GetProperty(attr.ChildType.Name) != null)
                        {
                            var propChildren = (Array)this.GetType().GetProperty(attr.ChildType.Name).GetValue(this);
                            if (propChildren != null)
                            {
                                var badIdList = new ArrayList() { };
                                try
                                {
                                    // first lets get the full list from the db so we can figure out who to delete
                                    var childParams = new DynamicParameters();
                                    childParams.Add(this.GetType().Name + "Id", this.GetType().GetProperty("Id").GetValue(this));
                                    var childRecords = conn.Query(attr.ChildType.Name + "GetFor" + this.GetType().Name, childParams,
                                        commandType: CommandType.StoredProcedure);
                                    // find requested data
                                    if (childRecords.Any())
                                    {
                                        foreach (var c in childRecords)
                                        {
                                            badIdList.Add(c.Id.ToString());
                                        }
                                    }
                                }
                                catch (Exception ex)
                                {
                                    //HttpContext.Current.Response.Write(ex.Message + " " + ex.StackTrace);
                                }

                                if (propChildren.Length > 0)
                                {
                                    foreach (var propChild in propChildren)
                                    {
                                        // remove this id from the list that has to be cleaned up later
                                        var idProp = propChild.GetType().GetProperty("Id");
                                        if (idProp != null)
                                        {
                                            string myId = idProp.GetValue(propChild).ToString();
                                            if (badIdList.Contains(myId))
                                            {
                                                badIdList.Remove(myId);
                                            }
                                        }

                                        // make sure the parent Id is set on the child object
                                        var parentIdProp = propChild.GetType().GetProperty(this.GetType().Name + "Id");
                                        if (parentIdProp != null)
                                        {
                                            parentIdProp.SetValue(propChild, this.GetType().GetProperty("Id").GetValue(this));
                                        }

                                        propChild.GetType().GetMethod("Save").Invoke(propChild, new object[] { });
                                    }
                                }

                                // delete any child ids are that are leftover in the db
                                if (badIdList.Count > 0)
                                {
                                    foreach (string myId in badIdList)
                                    {
                                        var dChild = Activator.CreateInstance(attr.ChildType);
                                        dChild.GetType().GetProperty("Id").SetValue(dChild, myId.ToInt());
                                        var meth = attr.ChildType.GetMethod("Delete");
                                        if (meth != null)
                                        {
                                            meth.Invoke(dChild, null);
                                        }
                                    }
                                }
                            }
                        }
                    }
                }

                // @todo process belongsTo relationships


                conn.Close();
            }
            res = true;
            return res;
        }

        /// <summary>
        /// Delete an object from the database.
        /// </summary>
        /// <returns>True</returns>
        public bool Delete()
        {
            bool res = false;
            if (this.GetType().Name.Length > 0 && this.GetType().IsSubclassOf(typeof(ORM)))
            {
                using (var conn = GetOpenConnection())
                {
                    var parameters = new DynamicParameters();
                    var username = HttpContext.Current!=null && HttpContext.Current.User.Identity.Name.Length > 0
                        ? HttpContext.Current.User.Identity.Name
                        : (WindowsIdentity.GetCurrent() != null ? WindowsIdentity.GetCurrent().Name : "");

                    parameters.Add("UserName", username);
                    parameters.Add("IP", GetIp());
                    parameters.Add("Id", this.GetType().GetProperty("Id").GetValue(this));
                    conn.Execute(this.GetType().Name + "Delete", parameters, commandType: CommandType.StoredProcedure);
                    conn.Close();
                    res = true;
                }
            }
            return res;
        }

        /// <summary>
        ///     Gets the current IP address.
        /// </summary>
        /// <returns>Host IP address.</returns>
        private static string GetIp()
        {
            // if the header exists return the contents
            // else return remote_addr (no proxy headers set)
            var ip = "";
            if (HttpContext.Current != null)
            {
                ip = (!string.IsNullOrEmpty(HttpContext.Current.Request.ServerVariables["HTTP_X_FORWARDED_FOR"]))
                    ? HttpContext.Current.Request.ServerVariables["HTTP_X_FORWARDED_FOR"]
                    : HttpContext.Current.Request.ServerVariables["REMOTE_ADDR"];
            }

            // grab the first object in the string array if comma exists
            // comma means we have an x-forwarded-for header present
            return ip.Contains(",") ? ip.Split(',').First().Trim() : ip;
        }


    }

    /// <summary>
    /// Data annotation that specifies a property should be ignore when inserting into or updating the db.
    /// </summary>
    [AttributeUsage(AttributeTargets.Property, Inherited = true)]
    public class IgnoreOn : Attribute
    {
        public bool Insert { get; set; }
        public bool Update { get; set; }

        public IgnoreOn(bool insert = false, bool update = false)
        {
            this.Insert = insert;
            this.Update = update;
        }
    }

    /// <summary>
    /// Attribute for specifying has many relationships.
    /// </summary>
    [AttributeUsage(AttributeTargets.Class, Inherited = true, AllowMultiple = true)]
    public class HasMany : Attribute
    {
        public Type ChildType { get; set; }

        public HasMany(Type childType)
        {
            this.ChildType = childType;
        }
    }

    /// <summary>
    /// Attribute for specifying belongs to relationships.
    /// </summary>
    [AttributeUsage(AttributeTargets.Class, Inherited = true, AllowMultiple = true)]
    public class BelongsTo : Attribute
    {
        public Type ParentType { get; set; }

        public BelongsTo(Type parentType)
        {
            this.ParentType = parentType;
        }
    }

    internal static class Helper
    {
        /// <summary>
        ///   Convert a string to an int. Defaults to zero.
        /// </summary>
        /// <param name="val">String value to convert.</param>
        /// <returns>Integer value.</returns>
        public static int ToInt(this string val)
        {
            int res = 0;
            try
            {
                res = Convert.ToInt32(val);
            }
            catch { }
            return res;
        }
    }
}