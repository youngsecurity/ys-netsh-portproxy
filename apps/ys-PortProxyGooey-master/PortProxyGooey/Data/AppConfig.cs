#region + -- IMPORTS -- +

    using System;
    using System.Collections.Generic;
    using System.Drawing;
    using System.Linq;
    using System.Text.RegularExpressions;

#endregion

namespace PortProxyGooey.Data {

    public class AppConfig {

        #region + -- VAR DECLARATIONS -- +

            // Window Size & Column Widths
            public Size MainWindowSize = new(720, 500);
            public int[] PortProxyColumnWidths = new int[] { 24, 64, 140, 100, 140, 100, 100 };

            // Main Window Startup Location
            public int MainWindowLocationX = 0;
            public int MainWindowLocationY = 0;

            // Column Sorting 
            public int SortColumn = 0;
            public int SortOrder = 0;

            private readonly Regex _intArrayRegex = new(@"^\[\s*(\d+)(?:\s*,\s*(\d+))*\s*\]$");

        #endregion

        public AppConfig() {}

        /// <summary>
        /// Fetches settings from .db
        /// </summary>
        /// <param name="rows"></param>
        public AppConfig(Config[] rows) {

            IEnumerable<Config> item = null;

            // Main Window Dimensions
            item = rows.Where(x => x.Item == "MainWindow");
            if (int.TryParse(item.FirstOrDefault(x => x.Key == "Width")?.Value, out int width) && int.TryParse(item.FirstOrDefault(x => x.Key == "Height")?.Value, out int height)) {
                MainWindowSize = new Size(width, height);
            } else {
                MainWindowSize = new Size(720, 500);
            }

            // Main Window Location
            if (int.TryParse(item.FirstOrDefault(x => x.Key == "LocX")?.Value, out int LocX) && int.TryParse(item.FirstOrDefault(x => x.Key == "LocY")?.Value, out int LocY)) {

                MainWindowLocationX = LocX;
                MainWindowLocationY = LocY;

            }

            // Columns
            item = rows.Where(x => x.Item == "PortProxy");
            string s_ColumnWidths = item.FirstOrDefault(x => x.Key == "ColumnWidths").Value;
            Match match = _intArrayRegex.Match(s_ColumnWidths);

            if (match.Success) {

                PortProxyColumnWidths = match.Groups
                    .OfType<Group>().Skip(1)
                    .SelectMany(x => x.Captures.OfType<Capture>())
                    .Select(x => int.Parse(x.Value))
                    .ToArray();

            } else {

#if NETCOREAPP3_0_OR_GREATER 
                PortProxyColumnWidths = Array.Empty<int>();
#else
                PortProxyColumnWidths = new int[0];
#endif
            
            }

            // Column Sorting
            if (int.TryParse(item.FirstOrDefault(x => x.Key == "Column")?.Value, out int Column) && int.TryParse(item.FirstOrDefault(x => x.Key == "Order")?.Value, out int Order)) {

                SortColumn = Column;
                SortOrder = Order;

            } else {

                SortColumn = 0;
                SortOrder = 0;

            }
        }
    }
}
