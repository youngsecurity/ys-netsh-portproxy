#region + -- IMPORTS -- +

using Microsoft.Data.Sqlite;
using NStandard;
    using SQLib.Sqlite;
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;

#endregion

namespace PortProxyGooey.Data {

    public class ApplicationDbScope : SqliteScope<ApplicationDbScope> {

        #region + -- VAR DECLARATIONS -- +

            public static readonly string AppDB = Path.Combine(Environment.ExpandEnvironmentVariables("%ALLUSERSPROFILE%"), "ScottElblein", "PortProxyGooey", "config.db");

        #endregion
  
        public override void Initialize() {}      
        
        public ApplicationDbScope(string connectionString) : base(connectionString) {}

        /// <summary>
        /// Check if path and db already exist; if not, create.
        /// </summary>
        /// <param name="file"></param>
        /// <returns></returns>
        public static ApplicationDbScope FromFile(string file) {

            string dir = Path.GetDirectoryName(file);

            if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);

            if (!File.Exists(file)) {

#if NETCOREAPP3_0_OR_GREATER
#else
                System.Data.SQLite.SQLiteConnection.CreateFile(file);
#endif
            }

            ApplicationDbScope scope = new($"Data Source=\"{file}\"");
            scope.Migrate();
            return scope;
        
        }

        #region + -- APP: MIGRATION -- +

        public void Migrate() => new MigrationUtil(this).MigrateToLast();

        public Migration GetLastMigration() {
            return SqlQuery<Migration>($"SELECT * FROM __history ORDER BY MigrationId DESC LIMIT 1;").First();
        }

        #endregion

        #region + -- RULES -- +

        public IEnumerable<Rule> Rules => SqlQuery<Rule>($"SELECT * FROM Rules;");

        /// <summary>
        /// RULE: Get a Rule from the DB
        /// </summary>
        /// <param name="type"></param>
        /// <param name="listenOn"></param>
        /// <param name="listenPort"></param>
        /// <returns></returns>
        public Rule GetRule(string type, string listenOn, int listenPort) {
            return SqlQuery<Rule>($"SELECT * FROM Rules WHERE Type={type} AND ListenOn={listenOn} AND ListenPort={listenPort} LIMIT 1;").FirstOrDefault();
        }

        #region + -- RULE: ADD -- +

        /// <summary>
        /// Add a new proxy rule into the db
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="obj"></param>
        /// <exception cref="NotSupportedException"></exception>
        public void Add<T>(T obj) where T : class {

            string newid = Guid.NewGuid().ToString();

            if (obj is Rule rule) {
                Sql($"INSERT INTO Rules (Id, Type, ListenOn, ListenPort, ConnectTo, ConnectPort, Comment, `Group`) VALUES ({newid}, {rule.Type}, {rule.ListenOn}, {rule.ListenPort}, {rule.ConnectTo}, {rule.ConnectPort}, {rule.Comment ?? ""}, {rule.Group ?? ""});");
                rule.Id = newid;
            }
            else throw new NotSupportedException($"Adding {obj.GetType().FullName} is not supported.");
        
        }
        
        public void AddRange<T>(IEnumerable<T> objs) where T : class  {
            foreach (T obj in objs) Add(obj);
        }

        #endregion

        #region + -- RULE: MODIFY -- +

        /// <summary>
        /// Modify proxy rule already in the db
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="obj"></param>
        /// <exception cref="NotSupportedException"></exception>
        public void Update<T>(T obj) where T : class {

            if (obj is Rule rule) {
                Sql($"UPDATE Rules SET Type={rule.Type}, ListenOn={rule.ListenOn}, ListenPort={rule.ListenPort}, ConnectTo={rule.ConnectTo}, ConnectPort={rule.ConnectPort} WHERE Id={rule.Id};");
            }
            else throw new NotSupportedException($"Updating {obj.GetType().FullName} is not supported.");
        
        }

        public void UpdateRange<T>(IEnumerable<T> objs) where T : class {
            foreach (T obj in objs) Update(obj);
        }

        /// <summary>
        /// Renames a group by updating the Group table in all matching proxies
        /// </summary>
        /// <param name="obj">a Proxy Rule containing the old group to rename</param>
        /// <param name="strNewGroupName">The new group name</param>
        public void RenameGroup<T>(T obj, string strNewGroupName) where T : class {

            if (obj is Rule rule) {
                Sql($"UPDATE Rules SET `Group`={strNewGroupName} WHERE `Group`={rule.Group};");
            }
            else throw new NotSupportedException($"Renaming Group {obj.GetType().FullName} is not supported.");
        
        }

        public void MoveToGroup<T>(T obj, string strNewGroupName) where T : class {

            if (obj is Rule rule) {
                Sql($"UPDATE Rules SET `Group`={strNewGroupName} WHERE `Group`={rule.Group};");
            } else
                throw new NotSupportedException($"Renaming Group {obj.GetType().FullName} is not supported.");

        }

        #endregion

        #region + -- RULE: REMOVE -- +

        /// <summary>
        /// Delete a proxy rule from the db
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="obj"></param>
        /// <exception cref="NotSupportedException"></exception>
        public void Remove<T>(T obj) where T : class {

            if (obj is Rule rule) {
                Sql($"DELETE FROM Rules WHERE Id={rule.Id};");
            }
            else throw new NotSupportedException($"Removing {obj.GetType().FullName} is not supported.");
        
        }

        public void RemoveRange<T>(IEnumerable<T> objs) where T : class {
            foreach (T obj in objs) Remove(obj);
        }

        #endregion

        #endregion

        #region + -- APP: CONFIG -- +

        /// <summary>
        /// CONFIG: Read from DB
        /// </summary>
        /// <returns></returns>
        public AppConfig GetAppConfig() {

            Config[] configRows = SqlQuery<Config>($"SELECT * FROM Configs;");
            AppConfig appConfig = new(configRows);
            return appConfig;

        }

        /// <summary>
        ///  CONFIG: Save current settings to the DB
        /// </summary>
        /// <param name="appConfig"></param>
        public void SaveAppConfig(AppConfig appConfig) {

            // Store Main Window Dimensions
            Sql($"UPDATE Configs SET Value = {appConfig.MainWindowSize.Width} WHERE Item = 'MainWindow' AND `Key` = 'Width';");
            Sql($"UPDATE Configs SET Value = {appConfig.MainWindowSize.Height} WHERE Item = 'MainWindow' AND `Key` = 'Height';");

            // Store Main Window Location
            Sql($"UPDATE Configs SET Value = {appConfig.MainWindowLocationX} WHERE Item = 'MainWindow' AND `Key` = 'LocX';");
            Sql($"UPDATE Configs SET Value = {appConfig.MainWindowLocationY} WHERE Item = 'MainWindow' AND `Key` = 'LocY';");

            // Store column widths
            string s_portProxyColumnWidths = $"[{appConfig.PortProxyColumnWidths.Select(x => x.ToString()).Join(", ")}]";
            Sql($"UPDATE Configs SET Value = {s_portProxyColumnWidths} WHERE Item = 'PortProxy' AND `Key` = 'ColumnWidths';");
            
            // Store column sorting
            Sql($"UPDATE Configs SET Value = {appConfig.SortColumn} WHERE Item = 'PortProxy' AND `Key` = 'Column';");
            Sql($"UPDATE Configs SET Value = {appConfig.SortOrder} WHERE Item = 'PortProxy' AND `Key` = 'Order';");
        
        }

        #endregion

    }
}
