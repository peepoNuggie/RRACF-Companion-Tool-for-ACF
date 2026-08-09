using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace Rracf
{
    internal static class Program
    {
        [DllImport("kernel32.dll")]
        private static extern bool AttachConsole(int processId);
        private const int AttachParentProcess = -1;

        [STAThread]
        private static int Main(string[] args)
        {
            if (args.Length == 0)
            {
                Application.EnableVisualStyles();
                Application.SetCompatibleTextRenderingDefault(false);
                Application.Run(new MainForm());
                return 0;
            }

            // Started from a terminal: borrow the parent console so output is visible.
            AttachConsole(AttachParentProcess);
            var stdout = new StreamWriter(Console.OpenStandardOutput());
            stdout.AutoFlush = true;
            Console.SetOut(stdout);

            try
            {
                return RunCli(args);
            }
            catch (Exception ex)
            {
                Console.WriteLine();
                Console.WriteLine("ERROR: " + ex.Message);
                return 1;
            }
        }

        private static int RunCli(string[] args)
        {
            var opts = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            for (int i = 0; i < args.Length; i++)
            {
                string a = args[i];
                if (a == "-h" || a == "--help" || a == "/?") { PrintUsage(); return 0; }
                if (a == "-v" || a == "--version") { Console.WriteLine("RRACF " + AppInfo.Version); return 0; }
                if (!a.StartsWith("--")) throw new ArgumentException("Unexpected argument: " + a);
                string key = a.Substring(2);
                if (i + 1 >= args.Length) throw new ArgumentException("Missing value for --" + key);
                opts[key] = args[++i];
            }

            string appFolder = Path.GetDirectoryName(Application.ExecutablePath);
            var settings = Settings.Load(AppFiles.ResolveDataFile(appFolder, "rracf-settings.txt"));
            Tools tools = Tools.Discover(appFolder);
            if (opts.ContainsKey("retoc") || opts.ContainsKey("repak"))
                tools = new Tools(Value(opts, "retoc", tools.RetocPath), Value(opts, "repak", tools.RepakPath));

            Action<string> log = Console.WriteLine;

            // Remembered folders are only honoured if they exist, so a settings file from another
            // machine cannot send output somewhere meaningless.
            string modInput = Value(opts, "mod", ExistingOr(settings.Get("input", ""), Path.Combine(appFolder, "Input")));
            string output = Value(opts, "out", ExistingOr(settings.Get("output", ""), Path.Combine(appFolder, "Output")));
            string paks = Value(opts, "paks", settings.Get("paks", ""));
            if (!Directory.Exists(paks)) paks = GameFinder.FindPaksFolder();

            // Empty folders do not survive being zipped up, so make sure they exist on first run.
            try { Directory.CreateDirectory(modInput); Directory.CreateDirectory(output); }
            catch (Exception) { }

            string cachePath = AppFiles.ResolveDataFile(appFolder, "rracf-camomap.txt");
            List<CamoEntry> map = opts.ContainsKey("rebuild-map") ? null : CamoMap.Load(cachePath);
            if (map == null)
            {
                string mapScratch = Path.Combine(Path.GetTempPath(), "RRACF_map_" + Guid.NewGuid().ToString("N"));
                Directory.CreateDirectory(mapScratch);
                try
                {
                    map = CamoMap.Build(tools, paks, mapScratch, log);
                    CamoMap.Save(map, cachePath);
                }
                finally { try { Io.DeleteDirectory(mapScratch); } catch (Exception) { } }
            }
            CamoMap.ApplyEnumNames(map, GameFinder.FindEnumHeader(paks, settings.Get("enums", "")), log);

            string scratch = Path.Combine(Path.GetTempPath(), "RRACF_cli_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(scratch);
            Analysis analysis;
            try { analysis = Pipeline.Analyze(tools, modInput, map, scratch, log); }
            finally { try { Io.DeleteDirectory(scratch); } catch (Exception) { } }

            if (analysis.AddOnDetected)
            {
                Console.WriteLine();
                Console.WriteLine(analysis.AddOnMessage);
                Console.WriteLine();
            }

            Console.WriteLine("This mod replaces:");
            foreach (CamoChoice c in analysis.Choices) Console.WriteLine("  " + c);

            if (!opts.ContainsKey("slot"))
            {
                Console.WriteLine();
                Console.WriteLine("Add --slot 61|62|63|64 to build. Add --source <id> to override the camo above.");
                return 0;
            }

            var o = new BuildOptions();
            o.ModInput = modInput;
            o.PaksFolder = paks;
            o.OutputFolder = output;
            o.Slot = int.Parse(Require(opts, "slot"));
            CamoChoice chosen = analysis.Choices[0];
            if (opts.ContainsKey("source"))
            {
                int wanted = int.Parse(opts["source"]);
                foreach (CamoChoice c in analysis.Choices)
                {
                    if (c.CamoId == wanted) { chosen = c; break; }
                }
            }
            o.SourceCamoId = chosen.CamoId;
            o.TemplateFromMod = chosen.TemplateFromMod;
            o.TemplateContainer = chosen.TemplateContainer;
            o.DefinitionOnlyContainers = analysis.DefinitionOnlyContainers;
            o.BaseCamoId = opts.ContainsKey("base") ? int.Parse(opts["base"]) : 0;
            o.ModName = Value(opts, "name", Pipeline.SuggestName(analysis.ChosenUtoc));
            o.DisplayName = Value(opts, "display", "");
            // ACF 2.0 replaced the single Description with four coloured lines. --desc maps to the
            // plain one; the legacy key is deliberately never written.
            o.PlainDesc = Value(opts, "desc", "");
            o.AbilityDescOrange = Value(opts, "ability-desc", "");
            o.WarningDesc = Value(opts, "warning-desc", "");
            o.SpecialDesc = Value(opts, "special-desc", "");
            o.Abilities = new SlotAbilities();
            o.Abilities.InfAmmoAll = Flag(opts, "inf-ammo");
            o.Abilities.SteadyAim = Flag(opts, "steady-aim");
            o.Abilities.InfSuppressor = Flag(opts, "inf-suppressor");
            o.Abilities.SilentSteps = Flag(opts, "silent-steps");
            o.Abilities.InfAmmoWeapons = Value(opts, "ammo-weapons", "");

            BuildResult r = Pipeline.Build(tools, o, log);
            Console.WriteLine();
            Console.WriteLine("Created " + r.ModFolder + " containing:");
            foreach (string p in new[] { r.PakPath, r.UcasPath, r.UtocPath, r.SlotTxtPath })
                Console.WriteLine("  " + Path.GetFileName(p));
            foreach (string p in r.CopiedReplacerFiles)
                Console.WriteLine("  " + Path.GetFileName(p) + "   (from the replacer mod)");
            return 0;
        }

        private static string ExistingOr(string preferred, string fallback)
        {
            if (!string.IsNullOrEmpty(preferred) && Directory.Exists(preferred)) return preferred;
            return fallback;
        }

        private static string Require(Dictionary<string, string> o, string key)
        {
            if (!o.ContainsKey(key)) throw new ArgumentException("Missing required option --" + key);
            return o[key];
        }

        /// <summary>Reads a switch the way ACF reads its flags - any digit 1-9 means on.</summary>
        private static bool Flag(Dictionary<string, string> o, string key)
        {
            if (!o.ContainsKey(key)) return false;
            foreach (char c in o[key]) { if (c >= '1' && c <= '9') return true; }
            return false;
        }

        private static string Value(Dictionary<string, string> o, string key, string fallback)
        {
            return o.ContainsKey(key) && o[key].Length > 0 ? o[key] : fallback;
        }

        private static void PrintUsage()
        {
            Console.WriteLine("RRACF " + AppInfo.Version + " - turns a replacer camo mod into an ACF slot mod.");
            Console.WriteLine();
            Console.WriteLine("  Run with no arguments to open the window.");
            Console.WriteLine();
            Console.WriteLine("  RRACF.exe [--mod <folder>] [--slot 61|62|63|64] [options]");
            Console.WriteLine();
            Console.WriteLine("    --mod <path>      folder holding the replacer mod (or a .utoc);");
            Console.WriteLine("                      defaults to the Input folder");
            Console.WriteLine("    --slot <61-64>    ACF slot to fill; omit to just inspect the mod");
            Console.WriteLine("    --source <id>     override the detected camo used as the template");
            Console.WriteLine("                      (must be the camo the mod actually replaces)");
            Console.WriteLine("    --base <n>        BaseCamo= concealment value, -128..127 (default 0).");
            Console.WriteLine("                      Naked 0, Olive Drab 10, Tiger Stripe 30, Gold -100");
            Console.WriteLine("    --name <text>     mod name used in ACF_<name><slot>_P");
            Console.WriteLine("    --display <text>  in-game name written into ACF_Slot<slot>.txt");
            Console.WriteLine("    --desc <text>         PlainDesc line");
            Console.WriteLine("    --ability-desc <t>    AbilityDescOrange line (orange)");
            Console.WriteLine("    --warning-desc <t>    WarningDesc line (red)");
            Console.WriteLine("    --special-desc <t>    SpecialDesc line (yellow)");
            Console.WriteLine("    --inf-ammo 1          infinite ammo on every weapon");
            Console.WriteLine("    --ammo-weapons <list> comma-separated weapons/categories, e.g. Handguns,Grenades");
            Console.WriteLine("    --steady-aim 1        no shake while aiming");
            Console.WriteLine("    --inf-suppressor 1    suppressor never wears out");
            Console.WriteLine("    --silent-steps 1      footsteps make no noise");
            Console.WriteLine("    --paks <dir>      the game's Content\\Paks folder");
            Console.WriteLine("    --out <dir>       output folder");
            Console.WriteLine("    --rebuild-map 1   re-derive the camo list from the game");
        }
    }
}
