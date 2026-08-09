using System;
using System.Collections.Generic;

namespace Rracf
{
    /// <summary>
    /// The per-terrain camouflage grid an ACF slot can carry: 25 surfaces by 5 stances.
    ///
    /// The game stores 27 surfaces, but the first two are its "nothing matches here" defaults and
    /// ACF deliberately leaves them unauthorable, so only 25 appear here. Key names and their order
    /// come from ACF's own table (EGsrMgs3CamoufType order); the grouping below is presentation
    /// only, since ACF parses by key name rather than position.
    /// </summary>
    internal static class Terrain
    {
        public const int Stances = 5;
        public static readonly string[] StanceNames = { "Stand", "Crouch", "Prone", "Wall", "Wall crouch" };

        public static readonly string[] Outdoors =
        {
            "CamoWater", "CamoMoss", "CamoBlack", "CamoGray", "CamoSoilBrown", "CamoSoilBeige",
            "CamoWood", "CamoWoodGreen", "CamoGrass", "CamoLeaf", "CamoWhite"
        };

        public static readonly string[] Objects =
        {
            "CamoObjBrown", "CamoObjRed", "CamoObjOliveGreen", "CamoObjBeige"
        };

        public static readonly string[] Rooms =
        {
            "CamoRoomGray", "CamoRoomWood", "CamoRoomBlack", "CamoRoomBrown", "CamoRoomRed",
            "CamoRoomOrange", "CamoRoomOlive", "CamoRoomBeige", "CamoRoomWhite", "CamoRoomBlue"
        };

        /// <summary>All 25 keys, in the order they are written to the file.</summary>
        public static string[] AllSurfaces()
        {
            var all = new List<string>();
            all.AddRange(Outdoors);
            all.AddRange(Objects);
            all.AddRange(Rooms);
            return all.ToArray();
        }

        public static string GroupOf(string surface)
        {
            if (Array.IndexOf(Outdoors, surface) >= 0) return "Outdoors";
            if (Array.IndexOf(Objects, surface) >= 0) return "Objects";
            return "Indoors";
        }

        /// <summary>Drops the "Camo" prefix so the grid reads as Water / Room gray rather than CamoRoomGray.</summary>
        public static string FriendlyName(string surface)
        {
            string s = surface.StartsWith("Camo", StringComparison.Ordinal) ? surface.Substring(4) : surface;
            var sb = new System.Text.StringBuilder();
            for (int i = 0; i < s.Length; i++)
            {
                if (i > 0 && char.IsUpper(s[i]) && !char.IsUpper(s[i - 1])) sb.Append(' ');
                sb.Append(s[i]);
            }
            return sb.ToString();
        }
    }

    /// <summary>A grid of values, all zero until something sets them.</summary>
    internal class TerrainGrid
    {
        private readonly Dictionary<string, int[]> _rows =
            new Dictionary<string, int[]>(StringComparer.OrdinalIgnoreCase);

        public TerrainGrid()
        {
            foreach (string s in Terrain.AllSurfaces()) _rows[s] = new int[Terrain.Stances];
        }

        public int[] Row(string surface)
        {
            int[] row;
            if (_rows.TryGetValue(surface, out row)) return row;
            row = new int[Terrain.Stances];
            _rows[surface] = row;
            return row;
        }

        public void Set(string surface, int stand, int crouch, int prone, int wall, int wallCrouch)
        {
            int[] r = Row(surface);
            r[0] = stand; r[1] = crouch; r[2] = prone; r[3] = wall; r[4] = wallCrouch;
        }

        public bool IsAllZero
        {
            get
            {
                foreach (string s in Terrain.AllSurfaces())
                {
                    foreach (int v in Row(s)) { if (v != 0) return false; }
                }
                return true;
            }
        }

        public void Clear()
        {
            foreach (string s in Terrain.AllSurfaces())
            {
                int[] r = Row(s);
                for (int i = 0; i < r.Length; i++) r[i] = 0;
            }
        }

        /// <summary>The value range ACF accepts - it stores each cell as a signed byte.</summary>
        public static bool InRange(int v) { return v >= -128 && v <= 127; }
    }

    internal class GridTemplate
    {
        public string Name;
        public Action<TerrainGrid> Apply;

        public override string ToString() { return Name; }
    }

    /// <summary>
    /// Starting points for the grid.
    ///
    /// These are the game's OWN values, read out of the running game with ACF's camotable command -
    /// not approximations. Copying one gives that camo's behaviour exactly.
    ///
    /// The game's table has 27 rows; the first two (NO_CAMOUFLAGE and ROOM_NO_CAMOUFLAGE) are its
    /// "nothing matches here" defaults, which ACF does not let an author set, so they are left out.
    /// </summary>
    internal static class GridTemplates
    {
        public static List<GridTemplate> All()
        {
            var list = new List<GridTemplate>();

            list.Add(Make("All Zero (no per terrain values)",
                delegate(TerrainGrid g) { g.Clear(); }));

            list.Add(Make("Tiger Stripe  CamoID 1",
                delegate(TerrainGrid g)
                {
                    g.Clear();
                    g.Set("CamoWater", 10, 45, 65, 50, 55);
                    g.Set("CamoMoss", 5, 35, 55, 45, 50);
                    g.Set("CamoBlack", 10, 40, 60, 50, 55);
                    g.Set("CamoGray", 35, 65, 80, 70, 75);
                    g.Set("CamoSoilBrown", 30, 60, 75, 65, 70);
                    g.Set("CamoWood", 0, 45, 65, 55, 60);
                    g.Set("CamoObjBrown", 10, 40, 60, 50, 55);
                    g.Set("CamoObjRed", 0, 30, 50, 40, 45);
                    g.Set("CamoObjOliveGreen", 5, 35, 55, 45, 50);
                    g.Set("CamoGrass", 35, 50, 80, 55, 60);
                    g.Set("CamoLeaf", 0, 30, 50, 40, 45);
                    g.Set("CamoSoilBeige", 25, 55, 75, 65, 70);
                    g.Set("CamoObjBeige", 15, 45, 65, 55, 60);
                    g.Set("CamoWoodGreen", 30, 60, 80, 70, 75);
                    g.Set("CamoWhite", -10, 20, 40, 30, 35);
                    g.Set("CamoRoomGray", 15, 30, 45, 35, 40);
                    g.Set("CamoRoomWood", -5, 10, 25, 15, 20);
                    g.Set("CamoRoomBlack", -10, 5, 20, 10, 15);
                    g.Set("CamoRoomBrown", 15, 30, 45, 35, 40);
                    g.Set("CamoRoomRed", -5, 10, 25, 15, 20);
                    g.Set("CamoRoomOrange", -5, 10, 25, 15, 20);
                    g.Set("CamoRoomOlive", 15, 30, 45, 35, 40);
                    g.Set("CamoRoomBeige", -10, 5, 20, 10, 15);
                    g.Set("CamoRoomWhite", -5, 10, 25, 15, 20);
                    g.Set("CamoRoomBlue", 5, 20, 35, 25, 30);
                }));

            list.Add(Make("Squares  CamoID 7",
                delegate(TerrainGrid g)
                {
                    g.Clear();
                    g.Set("CamoWater", -5, 25, 45, 25, 30);
                    g.Set("CamoMoss", -15, 15, 35, 25, 30);
                    g.Set("CamoBlack", 15, 45, 65, 55, 60);
                    g.Set("CamoGray", -20, 10, 30, 20, 25);
                    g.Set("CamoSoilBrown", -10, 20, 40, 30, 35);
                    g.Set("CamoWood", 20, 60, 80, 70, 75);
                    g.Set("CamoObjBrown", -5, 25, 45, 35, 40);
                    g.Set("CamoObjRed", 30, 60, 80, 70, 75);
                    g.Set("CamoObjOliveGreen", -5, 25, 45, 35, 40);
                    g.Set("CamoGrass", -20, 40, 65, 55, 60);
                    g.Set("CamoLeaf", 5, 35, 55, 45, 50);
                    g.Set("CamoSoilBeige", 0, 30, 50, 40, 45);
                    g.Set("CamoObjBeige", -5, 25, 45, 35, 40);
                    g.Set("CamoWoodGreen", -20, 10, 30, 20, 25);
                    g.Set("CamoWhite", -30, 0, 20, 10, 15);
                    g.Set("CamoRoomGray", -20, -5, 10, 0, 5);
                    g.Set("CamoRoomWood", 15, 30, 45, 35, 40);
                    g.Set("CamoRoomBlack", -5, 10, 25, 15, 20);
                    g.Set("CamoRoomBrown", 20, 35, 50, 40, 45);
                    g.Set("CamoRoomRed", 25, 40, 55, 45, 50);
                    g.Set("CamoRoomOrange", 30, 45, 60, 50, 55);
                    g.Set("CamoRoomOlive", -15, 0, 15, 5, 10);
                    g.Set("CamoRoomBeige", -20, -5, 10, 0, 5);
                    g.Set("CamoRoomWhite", -20, -5, 10, 0, 5);
                    g.Set("CamoRoomBlue", -15, 0, 15, 5, 10);
                }));

            list.Add(Make("Sneaking Suit  CamoID 12",
                delegate(TerrainGrid g)
                {
                    g.Clear();
                    // Outdoors is flat 15/45/65/55/60, except Water, which is weaker against a wall.
                    g.Set("CamoWater", 15, 45, 65, 40, 45);
                    string[] outdoors =
                    {
                        "CamoMoss", "CamoBlack", "CamoGray", "CamoSoilBrown", "CamoWood",
                        "CamoObjBrown", "CamoObjRed", "CamoObjOliveGreen", "CamoGrass", "CamoLeaf",
                        "CamoSoilBeige", "CamoObjBeige", "CamoWoodGreen", "CamoWhite"
                    };
                    foreach (string s in outdoors) g.Set(s, 15, 45, 65, 55, 60);

                    foreach (string s in Terrain.Rooms) g.Set(s, 15, 30, 45, 35, 40);
                }));

            return list;
        }

        private static GridTemplate Make(string name, Action<TerrainGrid> apply)
        {
            var t = new GridTemplate();
            t.Name = name;
            t.Apply = apply;
            return t;
        }
    }
}
