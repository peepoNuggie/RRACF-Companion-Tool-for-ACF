using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;

namespace Rracf
{
    /// <summary>One vanilla camouflage slot: its numeric ID and the art folders its asset points at.</summary>
    internal class CamoEntry
    {
        public int Id;
        public List<string> Folders = new List<string>();
        public string EnumName = "";

        public string FoldersJoined { get { return string.Join(", ", Folders.ToArray()); } }
    }

    /// <summary>
    /// Maps a camouflage art folder name (as it appears in a replacer mod) to the vanilla camo ID.
    ///
    /// The map is derived from the game's own Camouf_&lt;id&gt;_asset files rather than from the
    /// GM_CAMOUF_* enum, because the enum names frequently do not match the folder names:
    /// ID 6 is folder "Rain_Drop" but enum RAIN_STROKE, 23 is "Snake" but HEBI, 24 is "Ga_Ko" but
    /// GARCO, 35 is "ST_V" but VALEN, 54 is "Tuxedo_White" but WHITE_TUXEDO. The enum is only used
    /// to put a friendly label next to each ID.
    /// </summary>
    internal static class CamoMap
    {
        private static readonly Regex FolderPattern =
            new Regex(@"/camouflage/([A-Za-z0-9_]+)/", RegexOptions.IgnoreCase | RegexOptions.Compiled);

        /// <summary>Rebuilds the map by extracting every vanilla Camouf_*_asset and reading its material paths.</summary>
        public static List<CamoEntry> Build(Tools tools, string paksFolder, string scratchFolder, Action<string> log)
        {
            string extractDir = Path.Combine(scratchFolder, "camomap");
            Io.DeleteDirectory(extractDir);
            Directory.CreateDirectory(extractDir);

            log("Extracting vanilla camouflage assets from the game...");
            tools.RunRetoc(new[]
            {
                "to-legacy", paksFolder, extractDir, "--version", Tools.EngineVersion, "-f", "Camouf_"
            }, scratchFolder, log);

            string assetDir = Path.Combine(extractDir, @"MGSDelta\Content\Maps\AssetCamouflage");
            if (!Directory.Exists(assetDir))
                throw new InvalidOperationException(
                    "No camouflage assets were extracted from " + paksFolder +
                    ". Check that this really is the game's Content\\Paks folder.");

            var entries = new List<CamoEntry>();
            var idPattern = new Regex(@"^Camouf_(\d+)_asset$", RegexOptions.IgnoreCase);

            foreach (string file in Directory.GetFiles(assetDir, "*.uasset"))
            {
                Match m = idPattern.Match(Path.GetFileNameWithoutExtension(file));
                if (!m.Success) continue;

                var entry = new CamoEntry();
                entry.Id = int.Parse(m.Groups[1].Value);

                string text = Encoding.ASCII.GetString(File.ReadAllBytes(file));
                var seen = new List<string>();
                foreach (Match fm in FolderPattern.Matches(text))
                {
                    string folder = fm.Groups[1].Value;
                    if (!seen.Contains(folder, StringComparer.OrdinalIgnoreCase))
                        seen.Add(folder);
                }
                seen.Sort(StringComparer.OrdinalIgnoreCase);
                entry.Folders = seen;
                if (entry.Folders.Count > 0)
                    entries.Add(entry);
            }

            entries.Sort(delegate(CamoEntry a, CamoEntry b) { return a.Id.CompareTo(b.Id); });
            Io.DeleteDirectory(extractDir);
            log("Mapped " + entries.Count + " vanilla camouflages.");
            return entries;
        }

        /// <summary>Adds the GM_CAMOUF_* label for each ID, if the header dump can be found.</summary>
        public static void ApplyEnumNames(List<CamoEntry> entries, string enumHeaderPath, Action<string> log)
        {
            if (string.IsNullOrEmpty(enumHeaderPath) || !File.Exists(enumHeaderPath))
            {
                log("Note: MGS3_enums.hpp not found, so camouflages will be listed without their enum names.");
                return;
            }

            var byId = new Dictionary<int, string>();
            var pattern = new Regex(@"GM_CAMOUF_([A-Z0-9_]+)\s*=\s*(\d+)");
            foreach (string line in File.ReadAllLines(enumHeaderPath))
            {
                Match m = pattern.Match(line);
                if (!m.Success) continue;
                int id = int.Parse(m.Groups[2].Value);
                if (!byId.ContainsKey(id))
                    byId[id] = m.Groups[1].Value;
            }

            foreach (CamoEntry e in entries)
            {
                string name;
                if (byId.TryGetValue(e.Id, out name))
                    e.EnumName = name;
            }
        }

        private static readonly string[] CacheHeader =
        {
            "# RRACF camouflage map - derived from the game's own Camouf_<id>_asset files.",
            "# Delete this file (or press Rebuild) to regenerate it.",
            "# format: id = folder1;folder2"
        };

        public static void Save(List<CamoEntry> entries, string path)
        {
            var lines = new List<string>(CacheHeader);
            foreach (CamoEntry e in entries)
                lines.Add(e.Id + " = " + string.Join(";", e.Folders.ToArray()));
            File.WriteAllLines(path, lines.ToArray());
        }

        public static List<CamoEntry> Load(string path)
        {
            if (!File.Exists(path)) return null;
            var entries = new List<CamoEntry>();
            foreach (string raw in File.ReadAllLines(path))
            {
                string line = raw.Trim();
                if (line.Length == 0 || line.StartsWith("#")) continue;
                int eq = line.IndexOf('=');
                if (eq < 0) continue;
                int id;
                if (!int.TryParse(line.Substring(0, eq).Trim(), out id)) continue;
                var entry = new CamoEntry();
                entry.Id = id;
                foreach (string f in line.Substring(eq + 1).Split(';'))
                {
                    string folder = f.Trim();
                    if (folder.Length > 0) entry.Folders.Add(folder);
                }
                if (entry.Folders.Count > 0) entries.Add(entry);
            }
            return entries.Count > 0 ? entries : null;
        }

        /// <summary>
        /// Every vanilla camo whose asset references <paramref name="folder"/>, best match first.
        ///
        /// Several camos share a folder - IDs 11, 57, 58 and 59 all reference "Naked" - so the camo
        /// that references the fewest folders wins: for "Naked" that is ID 11, the plain Naked camo,
        /// while "Naked_Woodland" only ever matches ID 57.
        /// </summary>
        public static List<CamoEntry> Candidates(List<CamoEntry> entries, string folder)
        {
            var hits = new List<CamoEntry>();
            foreach (CamoEntry e in entries)
            {
                if (e.Folders.Contains(folder, StringComparer.OrdinalIgnoreCase))
                    hits.Add(e);
            }
            hits.Sort(delegate(CamoEntry a, CamoEntry b)
            {
                int c = a.Folders.Count.CompareTo(b.Folders.Count);
                return c != 0 ? c : a.Id.CompareTo(b.Id);
            });
            return hits;
        }
    }

    internal static class ListExtensions
    {
        public static bool Contains(this List<string> list, string value, StringComparer comparer)
        {
            foreach (string s in list)
                if (comparer.Equals(s, value)) return true;
            return false;
        }
    }
}
