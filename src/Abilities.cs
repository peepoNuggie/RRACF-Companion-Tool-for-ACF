using System;
using System.Collections.Generic;
using System.Text;

namespace Rracf
{
    /// <summary>
    /// The ability flags an ACF 2.0 slot can set. All default off.
    /// </summary>
    internal class SlotAbilities
    {
        public bool InfAmmoAll;      // INFAmmoFlag  - every weapon
        public bool SteadyAim;       // AnimalsSA    - no shake while aiming
        public bool InfSuppressor;   // INFSuppressor
        public bool SilentSteps;     // SilentSteps

        /// <summary>Names for INFAmmoWeapon, already comma-separated. Empty means the key is written blank.</summary>
        public string InfAmmoWeapons = "";
    }

    /// <summary>
    /// One tickable entry in the infinite-ammo picker. Name and Note are kept apart from Token so the
    /// list can draw categories and weapons differently without the display text ever reaching the
    /// generated file.
    /// </summary>
    internal class AmmoEntry
    {
        public string Name;       // "Handguns", "MK22"
        public string Note;       // optional aside, e.g. "frag only"
        public string Token;      // exactly what gets written to INFAmmoWeapon
        public bool IsCategory;

        public string Display
        {
            get { return string.IsNullOrEmpty(Note) ? Name : Name + "   (" + Note + ")"; }
        }

        public override string ToString() { return Display; }
    }

    /// <summary>
    /// The weapons and categories INFAmmoWeapon accepts.
    ///
    /// Every token here was checked against ACF's own kEquipNames and kEquipGroups tables in
    /// dllmain.cpp. ACF strips spaces, hyphens, underscores and dots and lowercases before
    /// matching, so "AK-47" reaches it as "ak47".
    ///
    /// EZ Gun and the Patriot are deliberately absent - the game already gives those infinite ammo.
    /// Melee weapons and tools have no ammo to spend.
    /// </summary>
    /// <summary>A category and the individual weapons that sit under it.</summary>
    internal class AmmoGroup
    {
        public AmmoEntry Category;
        public List<AmmoEntry> Weapons = new List<AmmoEntry>();
        public int Rows { get { return 1 + Weapons.Count; } }
    }

    internal static class AmmoCatalogue
    {
        /// <summary>The same entries as All(), kept in their category groupings for laying out.</summary>
        public static List<AmmoGroup> Groups()
        {
            var groups = new List<AmmoGroup>();
            AmmoGroup current = null;
            foreach (AmmoEntry e in All())
            {
                if (e.IsCategory)
                {
                    current = new AmmoGroup();
                    current.Category = e;
                    groups.Add(current);
                }
                else if (current != null)
                {
                    current.Weapons.Add(e);
                }
            }
            return groups;
        }

        public static List<AmmoEntry> All()
        {
            var list = new List<AmmoEntry>();

            AddCategory(list, "Handguns");
            AddWeapon(list, "MK22");
            AddWeapon(list, "M1911A1");
            AddWeapon(list, "Single Action Army");

            AddCategory(list, "Shotguns");
            AddWeapon(list, "M37");

            AddCategory(list, "Snipers");
            AddWeapon(list, "SVD");
            AddWeapon(list, "Mosin Nagant");

            AddCategory(list, "SMGs");
            AddWeapon(list, "Scorpion");

            AddCategory(list, "Rifles");
            AddWeapon(list, "XM16E1");
            AddWeapon(list, "AK-47");

            AddCategory(list, "LMGs");
            AddWeapon(list, "M63");

            // The one-letter trap: "Grenades" is all five throwables, "Grenade" is only the frag.
            AddCategory(list, "Grenades", "all five throwables");
            AddWeapon(list, "Grenade", "frag only");
            AddWeapon(list, "WP Grenade");
            AddWeapon(list, "Stun Grenade");
            AddWeapon(list, "Smoke Grenade");
            AddWeapon(list, "Chaff Grenade");

            AddCategory(list, "Misc");
            AddWeapon(list, "Cigarette Gas Spray");
            AddWeapon(list, "RPG-7");
            AddWeapon(list, "TNT");
            AddWeapon(list, "Claymore");
            AddWeapon(list, "Mousetraps");

            // Kept short: this sits in the last column and a long aside runs off the edge.
            AddCategory(list, "Nonlethal", "6 weapons");

            return list;
        }

        private static void AddCategory(List<AmmoEntry> list, string name, string note)
        {
            var e = new AmmoEntry();
            e.Token = name;
            e.IsCategory = true;
            e.Name = name;
            e.Note = note;
            list.Add(e);
        }

        private static void AddCategory(List<AmmoEntry> list, string name) { AddCategory(list, name, null); }

        private static void AddWeapon(List<AmmoEntry> list, string name, string note)
        {
            var e = new AmmoEntry();
            e.Token = name;
            e.IsCategory = false;
            e.Name = name;
            e.Note = note;
            list.Add(e);
        }

        private static void AddWeapon(List<AmmoEntry> list, string name) { AddWeapon(list, name, null); }

        /// <summary>Turns a comma-separated INFAmmoWeapon value back into the tokens it names.</summary>
        public static List<string> Split(string value)
        {
            var tokens = new List<string>();
            if (string.IsNullOrEmpty(value)) return tokens;
            foreach (string part in value.Split(',', ';'))
            {
                string t = part.Trim();
                if (t.Length > 0) tokens.Add(t);
            }
            return tokens;
        }

        /// <summary>ACF's own normalisation, so a loaded file's spelling matches our tokens.</summary>
        public static string Normalise(string s)
        {
            if (s == null) return "";
            var sb = new StringBuilder();
            foreach (char c in s)
            {
                if (c == ' ' || c == '\t' || c == '-' || c == '_' || c == '.') continue;
                sb.Append(char.ToLowerInvariant(c));
            }
            return sb.ToString();
        }
    }
}
