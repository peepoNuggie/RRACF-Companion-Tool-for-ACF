using System;
using System.Collections.Generic;
using System.IO;

namespace Rracf
{
    /// <summary>A plain key=value file next to the program, so paths only need choosing once.</summary>
    internal class Settings
    {
        private readonly string _path;
        private readonly Dictionary<string, string> _values =
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        private Settings(string path) { _path = path; }

        public static Settings Load(string path)
        {
            var s = new Settings(path);
            if (File.Exists(path))
            {
                foreach (string raw in File.ReadAllLines(path))
                {
                    string line = raw.Trim();
                    if (line.Length == 0 || line.StartsWith("#")) continue;
                    int eq = line.IndexOf('=');
                    if (eq <= 0) continue;
                    s._values[line.Substring(0, eq).Trim()] = line.Substring(eq + 1).Trim();
                }
            }
            return s;
        }

        public string Get(string key, string fallback)
        {
            string v;
            if (_values.TryGetValue(key, out v) && v.Length > 0) return v;
            return fallback == null ? "" : fallback;
        }

        public void Set(string key, string value) { _values[key] = value == null ? "" : value; }

        public void Save()
        {
            try
            {
                var lines = new List<string> { "# RRACF settings" };
                foreach (KeyValuePair<string, string> kv in _values)
                    lines.Add(kv.Key + " = " + kv.Value);
                File.WriteAllLines(_path, lines.ToArray());
            }
            catch (Exception)
            {
                // Settings are a convenience; never fail the build over them.
            }
        }
    }

    /// <summary>Best-effort guesses for where the game and its header dump live.</summary>
    internal static class GameFinder
    {
        private const string PaksTail = @"steamapps\common\MGSDelta\MGSDelta\Content\Paks";

        public static string FindPaksFolder()
        {
            var roots = new List<string>();
            foreach (DriveInfo d in DriveInfo.GetDrives())
            {
                if (!d.IsReady) continue;
                roots.Add(Path.Combine(d.RootDirectory.FullName, "SteamLibrary"));
                roots.Add(Path.Combine(d.RootDirectory.FullName, "Steam"));
                roots.Add(Path.Combine(d.RootDirectory.FullName, @"Program Files (x86)\Steam"));
                roots.Add(Path.Combine(d.RootDirectory.FullName, @"Games\SteamLibrary"));
            }
            foreach (string root in roots)
            {
                string candidate = Path.Combine(root, PaksTail);
                try { if (Directory.Exists(candidate)) return candidate; }
                catch (Exception) { }
            }
            return "";
        }

        /// <summary>
        /// MGS3_enums.hpp lives under the game's UE4SS header dump. Only used to put friendly
        /// GM_CAMOUF_* labels next to each ID - the actual ID mapping comes from the game's assets.
        /// </summary>
        public static string FindEnumHeader(string paksFolder, string overridePath)
        {
            if (!string.IsNullOrEmpty(overridePath) && File.Exists(overridePath)) return overridePath;
            if (string.IsNullOrEmpty(paksFolder)) return "";
            try
            {
                // ...\MGSDelta\Content\Paks -> ...\MGSDelta
                DirectoryInfo content = Directory.GetParent(paksFolder);
                if (content == null) return "";
                DirectoryInfo gameRoot = content.Parent;
                if (gameRoot == null) return "";
                string candidate = Path.Combine(gameRoot.FullName,
                    @"Binaries\Win64\ue4ss\CXXHeaderDump\MGS3_enums.hpp");
                if (File.Exists(candidate)) return candidate;
            }
            catch (Exception) { }
            return "";
        }
    }
}
