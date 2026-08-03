using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;

namespace Rracf
{
    internal class ManifestEntry
    {
        public string PackageName = "";
        public string ChunkId = "";
    }

    /// <summary>
    /// Reads the pakstore.json that "retoc manifest" writes. We only need the package names and the
    /// first chunk id of each, so a light regex pass beats pulling in a JSON library.
    /// </summary>
    internal static class Manifest
    {
        private static readonly Regex NamePattern =
            new Regex("\"packagename\"\\s*:\\s*\"([^\"]*)\"", RegexOptions.Compiled);
        private static readonly Regex IdPattern =
            new Regex("\"id\"\\s*:\\s*\"([0-9a-fA-F]+)\"", RegexOptions.Compiled);

        /// <summary>Runs "retoc manifest" against a .utoc and returns its package entries.</summary>
        public static List<ManifestEntry> Read(Tools tools, string utocPath, string scratchFolder, Action<string> log)
        {
            Directory.CreateDirectory(scratchFolder);
            string jsonPath = Path.Combine(scratchFolder, "pakstore.json");
            if (File.Exists(jsonPath)) File.Delete(jsonPath);

            // retoc always writes pakstore.json into the working directory.
            tools.RunRetoc(new[] { "manifest", utocPath }, scratchFolder, log);

            if (!File.Exists(jsonPath))
                throw new InvalidOperationException("retoc did not produce a pakstore.json for " + utocPath + ".");

            return Parse(File.ReadAllText(jsonPath));
        }

        public static List<ManifestEntry> Parse(string json)
        {
            var names = NamePattern.Matches(json);
            var ids = IdPattern.Matches(json);

            var entries = new List<ManifestEntry>();
            foreach (Match nm in names)
            {
                var e = new ManifestEntry();
                e.PackageName = nm.Groups[1].Value;
                entries.Add(e);
            }

            // Each id belongs to the package whose name most recently preceded it.
            foreach (Match im in ids)
            {
                int owner = -1;
                for (int i = 0; i < names.Count; i++)
                {
                    if (names[i].Index < im.Index) owner = i; else break;
                }
                if (owner >= 0 && entries[owner].ChunkId.Length == 0)
                    entries[owner].ChunkId = im.Groups[1].Value;
            }

            return entries;
        }

        /// <summary>
        /// Counts how many packages each camouflage art folder owns, e.g. { "Tuxedo" -> 8, "Tuxedo_White" -> 5 }.
        /// A replacer mod usually touches one folder, but bundles do turn up.
        /// </summary>
        public static Dictionary<string, int> CountCamouflageFolders(List<ManifestEntry> entries)
        {
            var pattern = new Regex(@"/camouflage/([A-Za-z0-9_]+)/", RegexOptions.IgnoreCase);
            var counts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            foreach (ManifestEntry e in entries)
            {
                Match m = pattern.Match(e.PackageName);
                if (!m.Success) continue;
                string folder = m.Groups[1].Value;
                counts[folder] = counts.ContainsKey(folder) ? counts[folder] + 1 : 1;
            }
            return counts;
        }
    }
}
