using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;

namespace Rracf
{
    /// <summary>
    /// Everything the user typed, saved so a slot can be picked up again later.
    ///
    /// Input and output folders are deliberately left out: those describe the machine, not the mod,
    /// and a save passed to someone else should not point at a folder they do not have.
    /// </summary>
    internal class ProjectState
    {
        public int Slot = 61;
        public string Name = "";
        public string PlainDesc = "";
        public string AbilityDescOrange = "";
        public string WarningDesc = "";
        public string SpecialDesc = "";
        public int BaseCamo;
        public TerrainGrid Grid = new TerrainGrid();
        public SlotAbilities Abilities = new SlotAbilities();

        private const string Header = "# RRACF save file";

        public void Save(string path)
        {
            string dir = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);

            var sb = new StringBuilder();
            sb.AppendLine(Header);
            sb.AppendLine("# Written by RRACF " + AppInfo.Version + ". Safe to edit by hand.");
            sb.AppendLine();
            sb.AppendLine("Slot=" + Slot);
            sb.AppendLine("Name=" + Name);
            sb.AppendLine("PlainDesc=" + PlainDesc);
            sb.AppendLine("AbilityDescOrange=" + AbilityDescOrange);
            sb.AppendLine("WarningDesc=" + WarningDesc);
            sb.AppendLine("SpecialDesc=" + SpecialDesc);
            sb.AppendLine("BaseCamo=" + BaseCamo);
            sb.AppendLine("INFAmmoFlag=" + (Abilities.InfAmmoAll ? "1" : "0"));
            sb.AppendLine("AnimalsSA=" + (Abilities.SteadyAim ? "1" : "0"));
            sb.AppendLine("INFSuppressor=" + (Abilities.InfSuppressor ? "1" : "0"));
            sb.AppendLine("SilentSteps=" + (Abilities.SilentSteps ? "1" : "0"));
            sb.AppendLine("INFAmmoWeapon=" + Abilities.InfAmmoWeapons);
            sb.AppendLine();
            foreach (string s in Terrain.AllSurfaces())
            {
                int[] r = Grid.Row(s);
                sb.AppendLine(s + "=" + r[0] + "," + r[1] + "," + r[2] + "," + r[3] + "," + r[4]);
            }
            File.WriteAllText(path, sb.ToString());
        }

        public static ProjectState Load(string path)
        {
            var state = new ProjectState();
            var values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

            foreach (string raw in File.ReadAllLines(path))
            {
                string line = raw.Trim();
                if (line.Length == 0 || line.StartsWith("#") || line.StartsWith(";")) continue;
                int eq = line.IndexOf('=');
                if (eq <= 0) continue;
                values[line.Substring(0, eq).Trim()] = line.Substring(eq + 1).Trim();
            }

            state.Slot = ReadInt(values, "Slot", 61);
            state.Name = Read(values, "Name");
            state.PlainDesc = Read(values, "PlainDesc");
            state.AbilityDescOrange = Read(values, "AbilityDescOrange");
            state.WarningDesc = Read(values, "WarningDesc");
            state.SpecialDesc = Read(values, "SpecialDesc");
            state.BaseCamo = ReadInt(values, "BaseCamo", 0);
            state.Abilities.InfAmmoAll = ReadFlag(values, "INFAmmoFlag");
            state.Abilities.SteadyAim = ReadFlag(values, "AnimalsSA");
            state.Abilities.InfSuppressor = ReadFlag(values, "INFSuppressor");
            state.Abilities.SilentSteps = ReadFlag(values, "SilentSteps");
            state.Abilities.InfAmmoWeapons = Read(values, "INFAmmoWeapon");

            foreach (string s in Terrain.AllSurfaces())
            {
                string line = Read(values, s);
                if (line.Length == 0) continue;
                string[] parts = line.Split(',');
                int[] target = state.Grid.Row(s);
                for (int i = 0; i < target.Length && i < parts.Length; i++)
                {
                    int v;
                    if (int.TryParse(parts[i].Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out v))
                        target[i] = v;
                }
            }
            return state;
        }

        private static string Read(Dictionary<string, string> v, string key)
        {
            string s;
            return v.TryGetValue(key, out s) ? s : "";
        }

        private static int ReadInt(Dictionary<string, string> v, string key, int fallback)
        {
            int n;
            return int.TryParse(Read(v, key), out n) ? n : fallback;
        }

        /// <summary>ACF treats any digit 1-9 as on, so saves are read the same way.</summary>
        private static bool ReadFlag(Dictionary<string, string> v, string key)
        {
            foreach (char c in Read(v, key)) { if (c >= '1' && c <= '9') return true; }
            return false;
        }
    }
}
