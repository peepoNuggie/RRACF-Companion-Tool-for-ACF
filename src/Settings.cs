using System;
using System.Collections.Generic;
using System.IO;
using System.Diagnostics;
using System.Text.RegularExpressions;
using Microsoft.Win32;

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

        // The folder the engine build puts the game in. Whoever packaged a copy picks the name of
        // the folder ABOVE this one - Steam calls it MGSDelta, an installer may call it anything at
        // all - but this one comes from the build, so it is what the last-resort search looks for.
        //
        // It has to match exactly rather than by prefix: MGSDelta_Foxhunt and MGSDelta_Nightmare sit
        // right beside it, each with a Content\Paks\global.utoc of its own. A prefix match would
        // quietly hand back the wrong game's archives.
        private const string ProjectFolder = "MGSDelta";

        // Limits for that search. Real installs sit three to five levels below a drive root
        // (D:\SteamLibrary\steamapps\common\MGSDelta\MGSDelta is five), so six covers them with room
        // spare, and the time budget stops a crowded disk from holding up the window.
        private const int MaxDepth = 6;
        private const int BudgetMs = 10000;

        // Folders a game is never installed inside, and which are slow or noisy to walk.
        private static readonly string[] SkipNames =
        {
            "Windows", "ProgramData", "AppData", "node_modules", ".git"
        };

        /// <summary>
        /// A folder is the game's Paks folder only if global.utoc is sitting in it. That the
        /// directory merely exists proves nothing - Content\Paks\mods exists too, and pointing at it
        /// gets all the way to a retoc call before failing with a message about the camo ID.
        /// </summary>
        public static bool IsPaksFolder(string path)
        {
            if (string.IsNullOrEmpty(path)) return false;
            try { return File.Exists(Path.Combine(path, "global.utoc")); }
            catch (Exception) { return false; }
        }

        /// <summary>
        /// Where the game is, or "" if it could not be worked out. Cheapest first, and each step only
        /// runs if the one before it found nothing - an ordinary Steam copy is answered by the first
        /// one in well under a millisecond and never reaches the search.
        /// </summary>
        public static string FindPaksFolder()
        {
            string hit = FindInSteamGuesses();
            if (hit.Length != 0) return hit;

            hit = FindInSteamLibraries();
            if (hit.Length != 0) return hit;

            return FindGameFolder();
        }

        /// <summary>Where a Steam copy nearly always is.</summary>
        private static string FindInSteamGuesses()
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
                try { if (IsPaksFolder(candidate)) return candidate; }
                catch (Exception) { }
            }
            return "";
        }

        /// <summary>
        /// Steam keeps its own list of library folders, so ask it rather than guess again. This
        /// covers a library sitting somewhere the names above do not cover.
        /// </summary>
        private static string FindInSteamLibraries()
        {
            var quoted = new Regex("\"(?:path|[0-9]+)\"\\s*\"([^\"]+)\"", RegexOptions.IgnoreCase);
            foreach (string steam in SteamInstallFolders())
            {
                string[] lines;
                try
                {
                    string vdf = Path.Combine(steam, @"steamapps\libraryfolders.vdf");
                    if (!File.Exists(vdf)) continue;
                    lines = File.ReadAllLines(vdf);
                }
                catch (Exception) { continue; }

                // Handles the current format - "path"  "D:\\SteamLibrary" - and the older one where
                // the library was the value of a numbered key. Anything else the pattern happens to
                // catch simply fails the check below.
                foreach (string line in lines)
                {
                    Match m = quoted.Match(line);
                    if (!m.Success) continue;
                    try
                    {
                        string lib = m.Groups[1].Value.Replace(@"\\", @"\");
                        string candidate = Path.Combine(lib, PaksTail);
                        if (IsPaksFolder(candidate)) return candidate;
                    }
                    catch (Exception) { }
                }
            }
            return "";
        }

        /// <summary>Steam's own record of where it is, with the usual spots as a backup.</summary>
        private static List<string> SteamInstallFolders()
        {
            var folders = new List<string>();
            AddRegistryPath(folders, Registry.CurrentUser, @"Software\Valve\Steam", "SteamPath");
            AddRegistryPath(folders, Registry.LocalMachine, @"SOFTWARE\Valve\Steam", "InstallPath");
            AddRegistryPath(folders, Registry.LocalMachine,
                @"SOFTWARE\Wow6432Node\Valve\Steam", "InstallPath");
            foreach (DriveInfo d in DriveInfo.GetDrives())
            {
                if (!d.IsReady) continue;
                folders.Add(Path.Combine(d.RootDirectory.FullName, "Steam"));
                folders.Add(Path.Combine(d.RootDirectory.FullName, @"Program Files (x86)\Steam"));
            }
            return folders;
        }

        private static void AddRegistryPath(List<string> into, RegistryKey hive,
                                            string subKey, string valueName)
        {
            try
            {
                using (RegistryKey k = hive.OpenSubKey(subKey))
                {
                    if (k == null) return;
                    string v = k.GetValue(valueName) as string;
                    // Steam writes this one with forward slashes.
                    if (!string.IsNullOrEmpty(v)) into.Add(v.Replace('/', '\\'));
                }
            }
            catch (Exception) { }
        }

        /// <summary>
        /// Last resort: go and look for the game's own folder, wherever it was put. This is the only
        /// part that searches, and it only runs on a machine where neither lookup above found
        /// anything - which means the copy was not installed through Steam.
        ///
        /// One pass, breadth-first from each fixed drive, six levels deep, skipping the folders a
        /// game is never in. It gives up after six seconds rather than let a crowded disk stall the
        /// window, and the caller saves whatever it finds, so it does not run twice.
        /// </summary>
        private static string FindGameFolder()
        {
            var drives = new List<string>();
            foreach (DriveInfo d in DriveInfo.GetDrives())
            {
                try
                {
                    if (!d.IsReady || d.DriveType != DriveType.Fixed) continue;
                    drives.Add(d.RootDirectory.FullName);
                }
                catch (Exception) { }
            }
            if (drives.Count == 0) return "";

            // Each drive gets its own share of the budget rather than drawing on a common one. A
            // crowded C: would otherwise use the lot and the game would never be looked for on the
            // drive it is actually installed on.
            int perDrive = BudgetMs / drives.Count;
            foreach (string root in drives)
            {
                string hit = SearchDrive(root, Stopwatch.StartNew(), perDrive);
                if (hit.Length != 0) return hit;
            }
            return "";
        }

        private static string SearchDrive(string root, Stopwatch clock, int budgetMs)
        {
            var thisLevel = new List<string>();
            thisLevel.Add(root);

            for (int depth = 0; depth < MaxDepth && thisLevel.Count > 0; depth++)
            {
                var nextLevel = new List<string>();
                foreach (string dir in thisLevel)
                {
                    if (clock.ElapsedMilliseconds > budgetMs) return "";

                    string[] children;
                    try { children = Directory.GetDirectories(dir); }
                    catch (Exception) { continue; }   // denied, or gone since the parent was listed

                    foreach (string child in children)
                    {
                        string name = Path.GetFileName(child);
                        if (IsSkipped(name)) continue;

                        if (name.Equals(ProjectFolder, StringComparison.OrdinalIgnoreCase))
                        {
                            string paks = Path.Combine(child, @"Content\Paks");
                            if (IsPaksFolder(paks)) return paks;
                        }

                        // On a Steam copy the folder above is called MGSDelta as well and has no
                        // Content of its own, so a miss still has to be descended into.
                        nextLevel.Add(child);
                    }
                }
                thisLevel = nextLevel;
            }
            return "";
        }

        private static bool IsSkipped(string name)
        {
            // $Recycle.Bin, $WinREAgent and friends.
            if (name.Length != 0 && name[0] == '$') return true;
            foreach (string s in SkipNames)
                if (name.Equals(s, StringComparison.OrdinalIgnoreCase)) return true;
            return false;
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
