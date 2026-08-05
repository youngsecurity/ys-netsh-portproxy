#region + -- IMPORTS -- +

using JSE_Utils;
using PortProxyGooey.Utils;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Windows.Forms;

#endregion

namespace PortProxyGooey.Data {

    public class MigrationUtil {

        public ApplicationDbScope DbScope { get; private set; }

        public MigrationUtil(ApplicationDbScope context) {

            DbScope = context;
            EnsureHistoryTable();
            EnsureUpdateVersion();

        }

        public void EnsureHistoryTable() {

            if (!DbScope.SqlQuery($"SELECT * FROM sqlite_master WHERE type = 'table' AND name = '__history';").Any()) {

                DbScope.UnsafeSql(@"CREATE TABLE __history ( MigrationId text PRIMARY KEY, ProductVersion text);");
                DbScope.UnsafeSql($"INSERT INTO __history (MigrationId, ProductVersion) VALUES ('000000000000', '0.0');");

            }

        }

        public void EnsureUpdateVersion() {

            Migration migration = DbScope.GetLastMigration();
            Version assemblyVersion = Assembly.GetExecutingAssembly().GetName().Version;

            if (new Version(migration.ProductVersion) > assemblyVersion) {

                if (MessageBox.Show(
                    string.Format("The current software version cannot use the configuration.{0}{0}You need to use a newer version of PortProxyGooey.{0}{0}Would you like to download it now?", Environment.NewLine),
                    "Upgrade",
                    MessageBoxButtons.YesNo,
                    MessageBoxIcon.Warning)
                    == DialogResult.Yes)
                {
                    Misc.RunCommand("explorer.exe", $"{PortProxyGooey.strAppURL}/releases");
                }

                Environment.Exit(0);
            }

        }

        public void MigrateToLast() {

            Migration migration = DbScope.GetLastMigration();
            string migrationId = migration.MigrationId;
            IEnumerable<KeyValuePair<MigrationKey, string[]>> pendingMigrations = migrationId != "000000000000"
                ? History.SkipWhile(pair => pair.Key.MigrationId != migrationId).Skip(1)
                : History;

            foreach (KeyValuePair<MigrationKey, string[]> pendingMigration in pendingMigrations) {

                foreach (string sql in pendingMigration.Value) {
                    DbScope.UnsafeSql(sql);
                }

                DbScope.Sql($"INSERT INTO __history (MigrationId, ProductVersion) VALUES ({pendingMigration.Key.MigrationId}, {pendingMigration.Key.ProductVersion});");
            }

        }

        public Dictionary<MigrationKey, string[]> History = new() {

            [new MigrationKey { MigrationId = "202103021542", ProductVersion = "1.1.0" }] = new[]
            {
                @"CREATE TABLE rules
(
    Id text PRIMARY KEY,
    Type text,
    ListenOn text,
    ListenPort integer,
    ConnectTo text,
    ConnectPort integer
);",
                "CREATE UNIQUE INDEX IX_Rules_Type_ListenOn_ListenPort ON Rules(Type, ListenOn, ListenPort);",
            },

            [new MigrationKey { MigrationId = "202201172103", ProductVersion = "1.2.0" }] = new[]
            {
                "ALTER TABLE rules ADD Note text;",
                "ALTER TABLE rules ADD `Group` text;",
                "ALTER TABLE rules ADD `FWHash` text;",
            },

            [new MigrationKey { MigrationId = "202202221635", ProductVersion = "1.3.0" }] = new[]
            {
                "ALTER TABLE rules RENAME TO rulesOld;",
                "DROP INDEX IX_Rules_Type_ListenOn_ListenPort;",

                @"CREATE TABLE rules (
	Id text PRIMARY KEY,
	Type text,
	ListenOn text,
	ListenPort integer,
	ConnectTo text,
	ConnectPort integer,
	Comment text,
	`Group` text 
	`FWHash` text 
);",
                "CREATE UNIQUE INDEX IX_Rules_Type_ListenOn_ListenPort ON Rules ( Type, ListenOn, ListenPort );",

                "INSERT INTO rules SELECT Id, Type, ListenOn, ListenPort, ConnectTo, ConnectPort, Note, `Group`, `FWHash` FROM rulesOld;",
                "DROP TABLE rulesOld;",
            },

            [new MigrationKey { MigrationId = "202303092024", ProductVersion = "1.4.0" }] = new[]
            {
                @"CREATE TABLE configs (
	Item text,
	`Key` text,
	Value text
);",

"CREATE UNIQUE INDEX IX_Configs_Key ON configs ( Item, `Key` );",

"INSERT INTO configs ( Item, `Key`, Value ) VALUES ( 'MainWindow', 'Width', '720' );",
"INSERT INTO configs ( Item, `Key`, Value ) VALUES ( 'MainWindow', 'Height', '500' );",
"INSERT INTO configs ( Item, `Key`, Value ) VALUES ( 'MainWindow', 'LocX', '0' );",
"INSERT INTO configs ( Item, `Key`, Value ) VALUES ( 'MainWindow', 'LocY', '0' );",
"INSERT INTO configs ( Item, `Key`, Value ) VALUES ( 'PortProxy', 'ColumnWidths', '[24, 64, 140, 100, 140, 100, 100]' );",
"INSERT INTO configs ( Item, `Key`, Value ) VALUES ( 'PortProxy', 'Column', '0' );",
"INSERT INTO configs ( Item, `Key`, Value ) VALUES ( 'PortProxy', 'Order', '0' );",
            },
        };
    }
}