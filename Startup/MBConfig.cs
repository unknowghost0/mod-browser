using System;
using System.Globalization;
using System.IO;

namespace ModBrowser
{
    internal static class MBConfig
    {
        public static string CommunityCatalogUrl = "https://raw.githubusercontent.com/RussDev7/CastleForge-CommunityMods/main/Index/mods.json";
        public static string OfficialCatalogUrl = "https://raw.githubusercontent.com/RussDev7/CastleForge/main/Index/mods.json";
        public static string CatalogUrl = CommunityCatalogUrl; // legacy alias
        public static string OfficialReleasesUrl = "https://github.com/RussDev7/CastleForge/releases"; // fallback only
        public static int HttpTimeoutSeconds = 15;
        public static string VisualStudioPath = "";
        public static bool VisualStudioWarningShown = false;

        private static string _configPath;

        public static string ConfigPath
        {
            get
            {
                if (string.IsNullOrEmpty(_configPath))
                {
                    string modsRoot = Path.GetDirectoryName(typeof(MBConfig).Assembly.Location) ?? ".";
                    _configPath = Path.Combine(modsRoot, "ModBrowser", "ModBrowser.Config.ini");
                }
                return _configPath;
            }
        }

        public static void LoadApply()
        {
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(ConfigPath) ?? ".");
                if (!File.Exists(ConfigPath))
                {
                    Save();
                    return;
                }

                foreach (var raw in File.ReadAllLines(ConfigPath))
                {
                    string line = (raw ?? "").Trim();
                    if (line.Length == 0 || line.StartsWith(";") || line.StartsWith("#") || line.StartsWith("["))
                        continue;

                    int eq = line.IndexOf('=');
                    if (eq <= 0)
                        continue;

                    string key = line.Substring(0, eq).Trim();
                    string val = (eq + 1 < line.Length ? line.Substring(eq + 1) : "").Trim();

                    if (key.Equals("CommunityCatalogUrl", StringComparison.OrdinalIgnoreCase))
                    {
                        CommunityCatalogUrl = val;
                        CatalogUrl = val;
                    }
                    else if (key.Equals("CatalogUrl", StringComparison.OrdinalIgnoreCase))
                    {
                        CatalogUrl = val;
                        CommunityCatalogUrl = val;
                    }
                    else if (key.Equals("OfficialCatalogUrl", StringComparison.OrdinalIgnoreCase))
                        OfficialCatalogUrl = val;
                    else if (key.Equals("OfficialReleasesUrl", StringComparison.OrdinalIgnoreCase))
                        OfficialReleasesUrl = val;
                    else if (key.Equals("HttpTimeoutSeconds", StringComparison.OrdinalIgnoreCase))
                    {
                        int t;
                        if (int.TryParse(val, NumberStyles.Integer, CultureInfo.InvariantCulture, out t))
                            HttpTimeoutSeconds = Math.Max(5, Math.Min(120, t));
                    }
                    else if (key.Equals("VisualStudioPath", StringComparison.OrdinalIgnoreCase))
                        VisualStudioPath = val;
                    else if (key.Equals("VisualStudioWarningShown", StringComparison.OrdinalIgnoreCase))
                    {
                        bool b;
                        if (bool.TryParse(val, out b))
                            VisualStudioWarningShown = b;
                    }
                }
            }
            catch
            {
            }
        }

        public static void Save()
        {
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(ConfigPath) ?? ".");
                File.WriteAllText(
                    ConfigPath,
                    "; ModBrowser config\r\n" +
                    "[ModBrowser]\r\n" +
                    "CommunityCatalogUrl=" + CommunityCatalogUrl + "\r\n" +
                    "OfficialCatalogUrl=" + OfficialCatalogUrl + "\r\n" +
                    "CatalogUrl=" + CommunityCatalogUrl + "\r\n" +
                    "OfficialReleasesUrl=" + OfficialReleasesUrl + "\r\n" +
                    "HttpTimeoutSeconds=" + HttpTimeoutSeconds.ToString(CultureInfo.InvariantCulture) + "\r\n" +
                    "VisualStudioPath=" + VisualStudioPath + "\r\n" +
                    "VisualStudioWarningShown=" + VisualStudioWarningShown.ToString(CultureInfo.InvariantCulture) + "\r\n");
            }
            catch
            {
            }
        }
    }
}
