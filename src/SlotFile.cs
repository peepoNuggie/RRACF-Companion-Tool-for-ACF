using System;
using System.Text;

namespace Rracf
{
    /// <summary>Generates the ACF_Slot&lt;slot&gt;.txt that ships beside the mod's .pak.</summary>
    internal static class SlotFile
    {
        public static string Generate(int slot, int baseCamoId, string displayName, string description)
        {
            int slotNumber = slot - 60; // slot 61 is "ACF Mod 1"

            if (string.IsNullOrEmpty(displayName))
                displayName = "Rename me ACF Mod " + slotNumber;
            if (string.IsNullOrEmpty(description))
                description = "Redescribe me ACF Mod " + slotNumber;

            var sb = new StringBuilder();
            sb.AppendLine("; ACF slot metadata - slot " + slotNumber + " (camo ID " + slot + ")");
            sb.AppendLine("; Placed next to this mod's .pak by the mod author. File name must remain ACF_Slot" + slot + ".txt ");
            sb.AppendLine(";Experiment with base camo as you can see its extremely easy to edit it and test out in game!");
            sb.AppendLine("Name=" + displayName);
            sb.AppendLine("Description=" + description);
            sb.AppendLine("BaseCamo=" + baseCamoId);
            sb.AppendLine();
            sb.AppendLine(";This config file was built for v1.1");
            sb.AppendLine(";Below isn't currently supported but may come in a future update.");
            sb.AppendLine("SpecialEffectFlag=0");
            sb.AppendLine("SpecialEffectDescription=Silent Steps");
            sb.AppendLine("StandingMoveSpeedMultiplier=1");
            sb.AppendLine("CrouchMoveSpeedMultiplier=1");
            sb.AppendLine("CrawlMoveSpeedMultiplier=1");
            sb.AppendLine("HealthMultiplier=1");
            sb.AppendLine("LifeRecoveryMultiplier=1");
            sb.AppendLine("INFAmmoFlag=0");
            sb.AppendLine("INFAmmoEquipment=Grenade");
            sb.AppendLine();
            string[] camoStats =
            {
                "CamoWater", "CamoMoss", "CamoBlack", "CamoGray", "CamoSoilBrown", "CamoWood",
                "CamoGrass", "CamoLeaf", "CamoSoilBeige", "CamoWoodGreen", "CamoWhite"
            };
            foreach (string s in camoStats) sb.AppendLine(s + "=0");
            sb.AppendLine();
            foreach (string s in new[] { "CamoObjBrown", "CamoObjRed", "CamoObjBeige" }) sb.AppendLine(s + "=0");
            sb.AppendLine();
            string[] roomStats =
            {
                "CamoRoomGray", "CamoRoomWood", "CamoRoomBlack", "CamoRoomBrown", "CamoRoomRed",
                "CamoRoomOrange", "CamoRoomOlive", "CamoRoomBeige", "CamoRoomWhite", "CamoRoomBlue"
            };
            foreach (string s in roomStats) sb.AppendLine(s + "=0");
            return sb.ToString();
        }
    }
}
