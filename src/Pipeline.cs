using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;

namespace Rracf
{
    internal class CamoChoice
    {
        public string Folder = "";
        public int PackageCount;
        public CamoEntry Camo;
        public int CamoId;

        /// <summary>
        /// True when the mod ships its own Camouf_&lt;id&gt;_asset. Those mods put their art in a new
        /// folder and replace a vanilla camo's asset to point at it, so that asset - not the vanilla
        /// one - is what we rename onto the ACF slot.
        /// </summary>
        public bool TemplateFromMod;
        /// <summary>The .utoc holding the mod's own Camouf asset, when TemplateFromMod is set.</summary>
        public string TemplateContainer = "";

        public string CamoLabel
        {
            get
            {
                string s = "camo ID " + CamoId;
                if (Camo != null && Camo.EnumName.Length > 0) s += " (" + Camo.EnumName + ")";
                else if (Camo != null) s += " (" + Camo.FoldersJoined + ")";
                return s;
            }
        }

        public override string ToString()
        {
            if (TemplateFromMod)
                return "the mod's own Camouf_" + CamoId + "_asset  ->  currently overrides " + CamoLabel;
            return Folder + "  ->  " + CamoLabel +
                   "  [" + PackageCount + " file" + (PackageCount == 1 ? "" : "s") + " in mod]";
        }
    }

    internal class Analysis
    {
        public List<CamoChoice> Choices = new List<CamoChoice>();
        public List<string> UnmatchedFolders = new List<string>();
        /// <summary>Every .utoc found in the input folder - all of them get copied to the output.</summary>
        public List<string> Containers = new List<string>();
        /// <summary>The container that actually carries the camouflage art.</summary>
        public string ChosenUtoc = "";

        /// <summary>A container holding a camo definition but no art of its own - an add-on.</summary>
        public bool AddOnDetected;
        /// <summary>True when an add-on was found and nothing in the Input folder supplies its art.</summary>
        public bool BaseModMissing;
        /// <summary>Plain-English explanation, shown to the user when an add-on is detected.</summary>
        public string AddOnMessage = "";

        /// <summary>
        /// Containers holding a camo definition and no art. None of these are shipped: the ACF slot
        /// takes over that job, so copying them would hijack their vanilla camo as well.
        /// </summary>
        public List<string> DefinitionOnlyContainers = new List<string>();
    }

    internal class BuildOptions
    {
        public string ModInput = "";
        public string PaksFolder = "";
        public string OutputFolder = "";
        public int Slot;
        /// <summary>The vanilla camo whose Camouf_&lt;id&gt;_asset is used as the template. Must be the camo
        /// the replacer mod actually replaces, or the slot points at art nothing supplies.</summary>
        public int SourceCamoId;
        /// <summary>
        /// The BaseCamo= value written into ACF_Slot&lt;slot&gt;.txt. This is a camouflage INDEX, not a
        /// camo ID: ACF adds it to the concealment the game calculates. Naked is 0, Olive Drab 10,
        /// Tiger Stripe 30, Gold -100. Stored by ACF as a signed byte, so -128..127.
        /// </summary>
        public int BaseCamoId;
        /// <summary>Per-terrain values. All zero unless the author filled the grid in.</summary>
        public TerrainGrid Grid = new TerrainGrid();
        /// <summary>The four colored description lines, and the ability flags.</summary>
        public string PlainDesc = "";
        public string AbilityDescOrange = "";
        public string WarningDesc = "";
        public string SpecialDesc = "";
        public SlotAbilities Abilities = new SlotAbilities();
        /// <summary>True when the template is the mod's own Camouf asset rather than the vanilla one.</summary>
        public bool TemplateFromMod;
        /// <summary>The .utoc holding that asset. It is deliberately not shipped: keeping it would leave
        /// the mod overriding the vanilla camo as well as filling the slot.</summary>
        public string TemplateContainer = "";
        /// <summary>
        /// Containers that carry only a camo definition. They are never shipped: each one replaces a
        /// vanilla camo, and the ACF slot now does that job. Without this, dropping both a base mod
        /// and its add-on into Input fills the slot AND leaves the base mod's camo hijacked.
        /// </summary>
        public List<string> DefinitionOnlyContainers = new List<string>();
        public string ModName = "";
        public string DisplayName = "";
    }

    internal class BuildResult
    {
        public string ModFolder = "";
        public string UtocPath = "";
        public string UcasPath = "";
        public string PakPath = "";
        public string SlotTxtPath = "";
        public string ChunkId = "";
        public string VanillaChunkId = "";
        public string PackageName = "";
        public List<string> CopiedReplacerFiles = new List<string>();
    }

    /// <summary>Asks the user a yes/no question mid-build. Null (the CLI) means "carry on".</summary>
    internal delegate bool ConfirmCallback(string title, string message);

    internal static class Pipeline
    {
        /// <summary>ACF's additional-uniform slots. 65 arrived in ACF 2.0.</summary>
        public static readonly int[] ValidSlots = { 61, 62, 63, 64, 65 };

        /// <summary>
        /// Slot 65 borrows a menu row the game only ever labelled "UNLOCKED", and the buffer behind
        /// that word holds 15 characters. A longer name is ignored outright, not truncated.
        /// </summary>
        public const int Slot65NameLimit = 15;

        public static bool HasNameLimit(int slot) { return slot == 65; }

        private const string AssetSubPath = @"MGSDelta\Content\Maps\AssetCamouflage";

        private static readonly Regex CamoAssetPattern =
            new Regex(@"^/Game/Maps/AssetCamouflage/Camouf_(\d+)_asset$", RegexOptions.IgnoreCase);

        private static CamoEntry FindCamo(List<CamoEntry> map, int id)
        {
            foreach (CamoEntry c in map)
            {
                if (c.Id == id) return c;
            }
            return null;
        }

        /// <summary>Every .utoc under the given folder, or the file itself if a .utoc was passed directly.</summary>
        public static List<string> FindContainers(string inputPath)
        {
            var found = new List<string>();
            if (string.IsNullOrEmpty(inputPath))
                throw new InvalidOperationException("Choose the folder containing the replacer mod first.");

            if (File.Exists(inputPath) &&
                string.Equals(Path.GetExtension(inputPath), ".utoc", StringComparison.OrdinalIgnoreCase))
            {
                found.Add(Path.GetFullPath(inputPath));
                return found;
            }

            if (!Directory.Exists(inputPath))
                throw new InvalidOperationException("Folder not found: " + inputPath);

            foreach (string f in Directory.GetFiles(inputPath, "*.utoc", SearchOption.AllDirectories))
                found.Add(Path.GetFullPath(f));

            if (found.Count == 0)
                throw new InvalidOperationException(
                    "No .utoc file found in " + inputPath + ".\r\n\r\n" +
                    "Put the replacer mod's files (.pak, .ucas and .utoc) in that folder and try again.");

            found.Sort(StringComparer.OrdinalIgnoreCase);
            return found;
        }

        /// <summary>Reads the replacer mod and works out which vanilla camo it replaces.</summary>
        public static Analysis Analyze(Tools tools, string inputPath, List<CamoEntry> camoMap,
                                       string scratchFolder, Action<string> log)
        {
            tools.Validate();

            var analysis = new Analysis();
            analysis.Containers = FindContainers(inputPath);
            log("Found " + analysis.Containers.Count + " mod container" +
                (analysis.Containers.Count == 1 ? "" : "s") + ":");
            foreach (string c in analysis.Containers) log("  " + Path.GetFileName(c));
            WarnIfSeveralMods(analysis.Containers, log);

            var samplePackages = new List<string>();
            int bestCamoPackages = 0;
            var addOnContainers = new List<string>();
            var artContainers = new List<string>();

            foreach (string container in analysis.Containers)
            {
                var entries = Manifest.Read(tools, container,
                    Path.Combine(scratchFolder, "m" + analysis.Containers.IndexOf(container)), log);
                var counts = Manifest.CountCamouflageFolders(entries);

                int camoPackages = 0;
                foreach (KeyValuePair<string, int> kv in counts) camoPackages += kv.Value;
                if (camoPackages > bestCamoPackages)
                {
                    bestCamoPackages = camoPackages;
                    analysis.ChosenUtoc = container;
                }

                // A container carrying a camo definition but shipping no art of its own is an add-on:
                // it borrows meshes from a base mod, and is usually a tiny "Optional file" on Nexus.
                int camoDefs = 0, artPackages = 0;
                foreach (ManifestEntry e in entries)
                {
                    if (samplePackages.Count < 8) samplePackages.Add(e.PackageName);
                    if (CamoAssetPattern.IsMatch(e.PackageName)) camoDefs++;
                    else if (e.PackageName.StartsWith("/Game/Art/", StringComparison.OrdinalIgnoreCase)) artPackages++;
                }
                if (artPackages > 0) artContainers.Add(container);
                if (camoDefs > 0 && artPackages == 0) addOnContainers.Add(container);

                // Does this container ship its own Camouf_<id>_asset? If so the mod already points a
                // camo at its art, and that asset is exactly what we want to rename onto the slot.
                foreach (ManifestEntry e in entries)
                {
                    Match cm = CamoAssetPattern.Match(e.PackageName);
                    if (!cm.Success) continue;
                    var choice = new CamoChoice();
                    choice.CamoId = int.Parse(cm.Groups[1].Value);
                    choice.Camo = FindCamo(camoMap, choice.CamoId);
                    choice.TemplateFromMod = true;
                    choice.TemplateContainer = container;
                    choice.PackageCount = 1;
                    analysis.Choices.Add(choice);
                }

                foreach (KeyValuePair<string, int> kv in counts)
                {
                    var candidates = CamoMap.Candidates(camoMap, kv.Key);
                    if (candidates.Count == 0)
                    {
                        if (!analysis.UnmatchedFolders.Contains(kv.Key)) analysis.UnmatchedFolders.Add(kv.Key);
                        continue;
                    }
                    foreach (CamoEntry c in candidates)
                    {
                        var choice = new CamoChoice();
                        choice.Folder = kv.Key;
                        choice.PackageCount = kv.Value;
                        choice.Camo = c;
                        choice.CamoId = c.Id;
                        analysis.Choices.Add(choice);
                    }
                }
            }

            // A Camouf asset shipped by the mod is definitive - it names the camo outright - so those
            // rank first. Otherwise prefer the folder the mod ships the most art for, then the most
            // specific camo.
            analysis.Choices.Sort(delegate(CamoChoice a, CamoChoice b)
            {
                if (a.TemplateFromMod != b.TemplateFromMod) return a.TemplateFromMod ? -1 : 1;
                int c = b.PackageCount.CompareTo(a.PackageCount);
                if (c != 0) return c;
                int aFolders = a.Camo == null ? 99 : a.Camo.Folders.Count;
                int bFolders = b.Camo == null ? 99 : b.Camo.Folders.Count;
                c = aFolders.CompareTo(bFolders);
                if (c != 0) return c;
                return a.CamoId.CompareTo(b.CamoId);
            });

            analysis.DefinitionOnlyContainers = addOnContainers;
            DescribeAddOn(analysis, addOnContainers, artContainers);

            if (analysis.Choices.Count == 0)
                throw new InvalidOperationException(BuildNoCamoMessage(analysis, samplePackages));

            if (analysis.ChosenUtoc.Length == 0) analysis.ChosenUtoc = analysis.Containers[0];
            return analysis;
        }

        /// <summary>
        /// A single mod may ship several paks, but they live together in one folder. Containers spread
        /// across separate folders are almost always two different mods left in Input by mistake - they
        /// would all be bundled into one slot and fight over the same art in game.
        /// </summary>
        private static void WarnIfSeveralMods(List<string> containers, Action<string> log)
        {
            var folders = new List<string>();
            foreach (string c in containers)
            {
                string dir = Path.GetDirectoryName(c);
                if (!folders.Contains(dir, StringComparer.OrdinalIgnoreCase)) folders.Add(dir);
            }
            if (folders.Count < 2) return;

            log("");
            log("*** WARNING: these came from " + folders.Count + " different folders, so this looks like");
            log("*** more than one mod. They will ALL be packed into this one slot and will");
            log("*** conflict in game. Leave only one mod in the Input folder.");
            foreach (string f in folders) log("***   " + f);
            log("");
        }

        /// <summary>
        /// Warns when the Input folder holds a camo definition and no art to go with it.
        ///
        /// Only the missing case is reported. Plenty of complete downloads split their definition and
        /// their art across two paks - Ocelot is one - so "this container has no art" on its own means
        /// nothing. What matters is whether any art is present anywhere in the Input folder.
        /// </summary>
        private static void DescribeAddOn(Analysis analysis, List<string> addOns, List<string> artContainers)
        {
            if (addOns.Count == 0 || artContainers.Count > 0) return;

            analysis.AddOnDetected = true;
            analysis.BaseModMissing = true;

            var names = new List<string>();
            foreach (string a in addOns) names.Add(Path.GetFileName(a));

            var sb = new System.Text.StringBuilder();
            sb.AppendLine("This looks like an ADD-ON rather than a complete mod.");
            sb.AppendLine();
            sb.AppendLine(string.Join(", ", names.ToArray()) +
                          (names.Count == 1 ? " contains" : " contain") +
                          " only a camouflage definition and no art at all.");
            sb.AppendLine();
            sb.AppendLine("Mods like this reuse the meshes from a base mod. On Nexus they are usually");
            sb.AppendLine("small files listed under \"Optional files\" on the base mod's page.");
            sb.AppendLine();
            sb.AppendLine("Put the base mod in the Input folder as well and press Analyse again.");
            sb.Append("Without it the slot would have no clothing on it in game.");
            analysis.AddOnMessage = sb.ToString();
        }

        private static string BuildNoCamoMessage(Analysis analysis, List<string> samplePackages)
        {
            var sb = new System.Text.StringBuilder();
            sb.AppendLine("This mod does not replace any camouflage art, so there is nothing to turn into an ACF slot.");
            sb.AppendLine();
            sb.AppendLine("An ACF slot works by pointing at a vanilla camo's own art paths, which live under");
            sb.AppendLine("  .../Snake_HD/Body/Camouflage/<CamoName>/");
            sb.AppendLine("This mod replaces something else instead:");
            foreach (string p in samplePackages) sb.AppendLine("  " + p);
            if (analysis.UnmatchedFolders.Count > 0)
            {
                sb.AppendLine();
                sb.AppendLine("Unrecognised camouflage folders: " +
                              string.Join(", ", analysis.UnmatchedFolders.ToArray()));
            }
            sb.AppendLine();
            sb.Append("Mods that replace Snake's base body or head meshes apply to every camo at once, " +
                      "so they cannot be confined to a single slot.");
            return sb.ToString();
        }

        public static string SanitiseName(string name)
        {
            if (name == null) return "";
            var sb = new System.Text.StringBuilder();
            foreach (char c in name)
            {
                if (char.IsLetterOrDigit(c)) sb.Append(c);
            }
            return sb.ToString();
        }

        /// <summary>Suggests a mod name from the .utoc filename, e.g. "pakchunk193-Ocelot_P.utoc" -> "Ocelot".</summary>
        public static string SuggestName(string modUtoc)
        {
            if (string.IsNullOrEmpty(modUtoc)) return "";
            string stem = Path.GetFileNameWithoutExtension(modUtoc);
            stem = Regex.Replace(stem, @"^pakchunk\d+[-_]", "", RegexOptions.IgnoreCase);
            stem = Regex.Replace(stem, @"_P$", "", RegexOptions.IgnoreCase);
            return SanitiseName(stem);
        }

        public static BuildResult Build(Tools tools, BuildOptions o, Action<string> log)
        {
            return Build(tools, o, log, null);
        }

        public static BuildResult Build(Tools tools, BuildOptions o, Action<string> log, ConfirmCallback confirm)
        {
            tools.Validate();

            // The in-game name drives every generated name, so the folder and the paks always match.
            // Spaces and punctuation are stripped: "Fox Suit" on slot 61 becomes ACF_FoxSuit61.
            string modName = SanitiseName(o.DisplayName);
            if (modName.Length == 0) modName = SanitiseName(o.ModName);
            if (modName.Length == 0)
                throw new InvalidOperationException("Please enter an in-game name - it becomes the output folder and file names.");
            if (Array.IndexOf(ValidSlots, o.Slot) < 0)
                throw new InvalidOperationException("Slot must be one of " + string.Join(", ", Array.ConvertAll(ValidSlots, delegate(int i) { return i.ToString(); })) + ".");

            // ACF stores BaseCamo as a signed byte. Checked here so the CLI is guarded too.
            if (o.BaseCamoId < -128 || o.BaseCamoId > 127)
                throw new InvalidOperationException(
                    "BaseCamo must be between -128 and 127 - ACF stores it as a single signed byte.\r\n\r\n" +
                    "It is a concealment value, not a camo ID: Naked is 0, Olive Drab 10, " +
                    "Tiger Stripe 30, Gold -100.");

            if (o.SourceCamoId < 0 || o.SourceCamoId > 99)
                throw new InvalidOperationException("Camo ID " + o.SourceCamoId + " is out of range.");

            if (!Directory.Exists(o.PaksFolder))
                throw new InvalidOperationException("Game Paks folder not found: " + o.PaksFolder);

            List<string> containers = FindContainers(o.ModInput);

            string oldName = "Camouf_" + o.SourceCamoId + "_asset";
            string newName = "Camouf_" + o.Slot + "_asset";
            string stagingName = modName + o.Slot;               // e.g. Zero63
            string outputStem = "ACF_" + modName + o.Slot + "_P"; // e.g. ACF_Zero63_P

            // One self-contained folder that can be dropped straight into Content\Paks\mods. It is
            // created only once the checks have passed, so a refused build leaves nothing behind.
            string modFolder = Path.Combine(o.OutputFolder, "ACF_" + modName + o.Slot);

            string scratch = Path.Combine(Path.GetTempPath(), "RRACF_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(scratch);

            try
            {
                var result = new BuildResult();
                result.ModFolder = modFolder;

                // 1. Pull out the camo asset we are going to rename.
                string staging = Path.Combine(scratch, stagingName);
                string extractFrom = o.PaksFolder;
                if (o.TemplateFromMod)
                {
                    log("Taking " + oldName + " from the mod itself (it already points at the mod's art)...");
                    extractFrom = BuildExtractionFolder(o.PaksFolder, containers, scratch, log);
                }
                else
                {
                    log("Extracting the vanilla " + oldName + " from the game...");
                }
                tools.RunRetoc(new[]
                {
                    "to-legacy", extractFrom, staging, "--version", Tools.EngineVersion, "-f", oldName
                }, scratch, log);

                string assetDir = Path.Combine(staging, AssetSubPath);
                string srcUasset = Path.Combine(assetDir, oldName + ".uasset");
                string srcUexp = Path.Combine(assetDir, oldName + ".uexp");
                if (!File.Exists(srcUasset) || !File.Exists(srcUexp))
                    throw new InvalidOperationException(
                        "The game does not contain " + oldName + ". Camo ID " + o.SourceCamoId + " may not exist.");

                // 2. Keep an untouched copy so we can prove the finished chunk ID really moved.
                string controlStaging = Path.Combine(scratch, "control");
                string controlAssetDir = Path.Combine(controlStaging, AssetSubPath);
                Directory.CreateDirectory(controlAssetDir);
                File.Copy(srcUasset, Path.Combine(controlAssetDir, oldName + ".uasset"));
                File.Copy(srcUexp, Path.Combine(controlAssetDir, oldName + ".uexp"));

                // 3. The staging folder must hold the two asset files and nothing else - repak packs
                //    whatever it finds, and retoc leaves a 3 MB scriptobjects.bin behind.
                log("Cleaning the staging folder...");
                foreach (string f in Directory.GetFiles(staging, "*", SearchOption.AllDirectories))
                {
                    string full = Path.GetFullPath(f);
                    if (full.Equals(Path.GetFullPath(srcUasset), StringComparison.OrdinalIgnoreCase)) continue;
                    if (full.Equals(Path.GetFullPath(srcUexp), StringComparison.OrdinalIgnoreCase)) continue;
                    log("  removing " + Path.GetFileName(f));
                    File.Delete(f);
                }

                // 3b. The asset is only useful if it still points at some art. A mod that ships a camo
                //     asset but no art of its own depends on a companion download; without that in the
                //     Input folder its imports resolve to nothing and the slot comes out empty in game.
                CheckArtReferences(File.ReadAllBytes(srcUasset), oldName, log, confirm);

                // 4. Rename the asset onto the ACF slot.
                log("Renaming " + oldName + " to " + newName + "...");
                var asset = new UAsset(File.ReadAllBytes(srcUasset));
                log("  name table: " + asset.NameCount + " names at offset " + asset.NameOffset);
                PatchReport report = asset.Replace(oldName, newName);
                foreach (string n in report.RenamedNames)
                    log("  renamed name-table entry: " + n);
                if (report.PackageNameUpdated) log("  renamed the package name in the header");
                if (report.SizeDelta != 0)
                    log("  header grew by " + report.SizeDelta + " bytes - fixed up the offsets that moved");
                log("  name table: " + report.NamesRehashed + " entr" + (report.NamesRehashed == 1 ? "y" : "ies") +
                    " rehashed, " + report.NamesReferencedAfter + " names referenced from export data");

                File.WriteAllBytes(Path.Combine(assetDir, newName + ".uasset"), asset.Data);
                File.Move(srcUexp, Path.Combine(assetDir, newName + ".uexp"));
                File.Delete(srcUasset);

                // 5. Pack, then convert back to the game's Zen format.
                log("Packing...");
                tools.RunRepak(new[] { "pack", staging, "-q" }, scratch, log);
                string stagedPak = Path.Combine(scratch, stagingName + ".pak");
                if (!File.Exists(stagedPak))
                    throw new InvalidOperationException("repak did not produce " + stagedPak + ".");

                log("Converting to Zen...");
                Directory.CreateDirectory(modFolder);   // only now that every check has passed
                string outUtoc = Path.Combine(modFolder, outputStem + ".utoc");
                tools.RunRetoc(new[] { "to-zen", stagedPak, outUtoc, "--version", Tools.EngineVersion }, scratch, log);

                result.UtocPath = outUtoc;
                result.UcasPath = Path.Combine(modFolder, outputStem + ".ucas");
                result.PakPath = Path.Combine(modFolder, outputStem + ".pak");
                foreach (string expected in new[] { result.UtocPath, result.UcasPath, result.PakPath })
                {
                    if (!File.Exists(expected))
                        throw new InvalidOperationException("Expected output file was not created: " + expected);
                }

                // 6. Verify against the vanilla asset. If the package name had not really changed, this pak
                //    would quietly override the vanilla camo instead of filling an ACF slot, with no error.
                log("Verifying...");
                var produced = Manifest.Read(tools, outUtoc, Path.Combine(scratch, "verify"), log);
                if (produced.Count != 1)
                    throw new InvalidOperationException("Expected exactly one package in the output, found " + produced.Count + ".");

                result.PackageName = produced[0].PackageName;
                result.ChunkId = produced[0].ChunkId;
                string expectedPackage = "/Game/Maps/AssetCamouflage/" + newName;
                if (!string.Equals(result.PackageName, expectedPackage, StringComparison.OrdinalIgnoreCase))
                    throw new InvalidOperationException(
                        "The finished mod declares package \"" + result.PackageName + "\" but should declare \"" +
                        expectedPackage + "\".");
                log("  package name: " + result.PackageName);

                result.VanillaChunkId = BuildControlChunkId(tools, controlStaging, scratch, oldName, log);
                log("  vanilla chunk ID: " + result.VanillaChunkId);
                log("  new chunk ID:     " + result.ChunkId);

                if (PackageIdOf(result.ChunkId) == PackageIdOf(result.VanillaChunkId))
                    throw new InvalidOperationException(
                        "The new chunk ID is identical to the vanilla camo's. This mod would override camo " +
                        o.SourceCamoId + " instead of filling slot " + o.Slot + ". Do not ship it.");
                log("  chunk IDs differ - this is a new slot, not an override.");

                // 7. The metadata file ACF reads.
                result.SlotTxtPath = Path.Combine(modFolder, "ACF_Slot" + o.Slot + ".txt");
                var meta = new SlotMeta();
                meta.Name = o.DisplayName;
                meta.PlainDesc = o.PlainDesc;
                meta.AbilityDescOrange = o.AbilityDescOrange;
                meta.WarningDesc = o.WarningDesc;
                meta.SpecialDesc = o.SpecialDesc;
                meta.BaseCamo = o.BaseCamoId;
                meta.Grid = o.Grid;
                meta.Abilities = o.Abilities;
                File.WriteAllText(result.SlotTxtPath, SlotFile.Generate(o.Slot, meta));

                log("  BaseCamo=" + o.BaseCamoId +
                    (o.Grid != null && !o.Grid.IsAllZero ? " plus per-terrain values" : ""));
                if (o.Slot == 65 && (o.BaseCamoId != 0 || (o.Grid != null && !o.Grid.IsAllZero)))
                    log("  NOTE: slot 5 ignores concealment values - the game overrides them with Tiger Stripe");

                // 8. The replacer supplies the actual art, so it has to travel with the slot files.
                log("Copying the replacer mod's own files...");
                result.CopiedReplacerFiles = CopyReplacerFiles(containers, modFolder,
                    o.TemplateFromMod ? o.TemplateContainer : "", o.DefinitionOnlyContainers, log);

                log("Done.");
                return result;
            }
            finally
            {
                try { Io.DeleteDirectory(scratch); }
                catch (Exception e) { log("Note: could not clean up " + scratch + " (" + e.Message + ")"); }
            }
        }

        /// <summary>
        /// Checks whether the camo asset still names the art it needs.
        ///
        /// retoc writes imports it cannot resolve as /Engine/UnknownPackage, discarding the original
        /// path. A slot built from such an asset packs, verifies and installs perfectly happily, then
        /// shows up wrong in game - so it is caught here instead.
        ///
        /// Nothing resolved at all is a hard stop. A partial miss asks the user, because some mods
        /// legitimately carry one unresolved import and still match their hand-built equivalents.
        /// </summary>
        private static void CheckArtReferences(byte[] assetBytes, string assetName,
                                               Action<string> log, ConfirmCallback confirm)
        {
            string text = System.Text.Encoding.ASCII.GetString(assetBytes);

            var parts = new List<string>();
            foreach (Match m in Regex.Matches(text, "MODEL_PART_TYPE::([A-Za-z]+)"))
            {
                if (!parts.Contains(m.Groups[1].Value)) parts.Add(m.Groups[1].Value);
            }

            var artPaths = new List<string>();
            foreach (Match m in Regex.Matches(text, "/Game/Art[\\x20-\\x7E]{10,}"))
            {
                if (!artPaths.Contains(m.Value)) artPaths.Add(m.Value);
            }

            int unresolved = Regex.Matches(text, "/Engine/UnknownPackage").Count;

            log("  points at " + artPaths.Count + " art reference" + (artPaths.Count == 1 ? "" : "s") +
                (unresolved > 0 ? ", " + unresolved + " could not be found" : ""));

            if (unresolved == 0) return;

            var sb = new System.Text.StringBuilder();
            sb.AppendLine(assetName + " points at files that are not in the Input folder.");
            sb.AppendLine();
            if (parts.Count > 0)
            {
                sb.AppendLine("It describes these body parts:");
                sb.AppendLine("    " + string.Join(", ", parts.ToArray()));
                sb.AppendLine();
            }
            if (artPaths.Count > 0)
            {
                sb.AppendLine("Art it DID find:");
                foreach (string p in artPaths) sb.AppendLine("    " + p);
                sb.AppendLine();
            }
            sb.AppendLine(unresolved + " reference" + (unresolved == 1 ? "" : "s") +
                          " could not be found. Their paths are not recoverable - the game stores them");
            sb.AppendLine("as hashes, so anything missing simply reads back as /Engine/UnknownPackage.");
            sb.AppendLine();
            sb.AppendLine("Missing art almost always lives in another download, usually the base mod on");
            sb.AppendLine("the same Nexus page. Put that download in the Input folder as well and convert");
            sb.AppendLine("again.");

            if (artPaths.Count == 0)
            {
                sb.AppendLine();
                sb.Append("Nothing at all resolved, so this slot would show no clothing in game.");
                throw new InvalidOperationException(sb.ToString());
            }

            sb.AppendLine();
            sb.Append("Build it anyway?");
            log("  WARNING: " + unresolved + " reference(s) point outside the Input folder");
            if (confirm != null && !confirm("Missing files", sb.ToString()))
                throw new InvalidOperationException("Cancelled - add the missing mod to the Input folder and try again.");
        }

        /// <summary>
        /// retoc cannot read a mod container on its own - it needs the engine's script objects, which
        /// live in the game's global.utoc. So we stage a folder holding both.
        /// </summary>
        private static string BuildExtractionFolder(string paksFolder, List<string> containers,
                                                    string scratch, Action<string> log)
        {
            string combo = Path.Combine(scratch, "modsource");
            Directory.CreateDirectory(combo);

            string[] globals = Directory.GetFiles(paksFolder, "global.*");
            if (globals.Length == 0)
                throw new InvalidOperationException(
                    "Could not find global.utoc in " + paksFolder +
                    ". It is needed to read the mod's own camouflage asset.");
            foreach (string g in globals)
                File.Copy(g, Path.Combine(combo, Path.GetFileName(g)), true);

            // Every container has to be here, not just the one holding the camo asset: that asset's
            // imports point into the mod's art paks, and anything retoc cannot resolve is written out
            // as /Engine/UnknownPackage - which silently strips the slot's link to the art.
            foreach (string container in containers)
            {
                string dir = Path.GetDirectoryName(container);
                string stem = Path.GetFileNameWithoutExtension(container);
                foreach (string f in Directory.GetFiles(dir, stem + ".*"))
                {
                    string ext = Path.GetExtension(f).ToLowerInvariant();
                    if (ext != ".utoc" && ext != ".ucas" && ext != ".pak") continue;
                    File.Copy(f, Path.Combine(combo, Path.GetFileName(f)), true);
                }
            }
            return combo;
        }

        /// <summary>Copies each container's .utoc/.ucas/.pak/.sig set next to the generated slot files.</summary>
        private static List<string> CopyReplacerFiles(List<string> containers, string destFolder,
                                                      string excludeContainer, List<string> definitionOnly,
                                                      Action<string> log)
        {
            var copied = new List<string>();
            foreach (string utoc in containers)
            {
                if (excludeContainer.Length > 0 &&
                    string.Equals(Path.GetFullPath(utoc), Path.GetFullPath(excludeContainer), StringComparison.OrdinalIgnoreCase))
                {
                    log("  skipping " + Path.GetFileName(utoc) +
                        " - that is the mod's camo override, replaced by the ACF slot");
                    continue;
                }

                // Any other definition-only pak belongs to a different mod - usually the base mod an
                // add-on needs. Shipping it would hijack that mod's vanilla camo on top of filling
                // the slot, which is not what anyone asked for.
                if (IsDefinitionOnly(utoc, definitionOnly))
                {
                    log("  skipping " + Path.GetFileName(utoc) +
                        " - camo override from another mod, not needed for this slot");
                    continue;
                }
                string dir = Path.GetDirectoryName(utoc);
                string stem = Path.GetFileNameWithoutExtension(utoc);
                foreach (string file in Directory.GetFiles(dir, stem + ".*"))
                {
                    string ext = Path.GetExtension(file).ToLowerInvariant();
                    if (ext != ".utoc" && ext != ".ucas" && ext != ".pak" && ext != ".sig") continue;

                    string dest = Path.Combine(destFolder, Path.GetFileName(file));
                    if (string.Equals(Path.GetFullPath(file), Path.GetFullPath(dest), StringComparison.OrdinalIgnoreCase))
                        continue;
                    File.Copy(file, dest, true);
                    copied.Add(dest);
                    log("  " + Path.GetFileName(file));
                }
            }
            if (copied.Count == 0)
                log("  WARNING: no replacer files were copied - the slot will have no art to show.");
            return copied;
        }

        /// <summary>Packs the untouched vanilla asset the same way, purely to read back its chunk ID.</summary>
        private static string BuildControlChunkId(Tools tools, string controlStaging, string scratch,
                                                  string oldName, Action<string> log)
        {
            tools.RunRepak(new[] { "pack", controlStaging, "-q" }, scratch, log);
            string controlPak = Path.Combine(scratch, "control.pak");
            string controlUtoc = Path.Combine(scratch, "control_zen", "Control_P.utoc");
            Directory.CreateDirectory(Path.GetDirectoryName(controlUtoc));
            tools.RunRetoc(new[] { "to-zen", controlPak, controlUtoc, "--version", Tools.EngineVersion }, scratch, log);

            var control = Manifest.Read(tools, controlUtoc, Path.Combine(scratch, "control_verify"), log);
            foreach (ManifestEntry e in control)
            {
                if (e.PackageName.EndsWith(oldName, StringComparison.OrdinalIgnoreCase))
                    return e.ChunkId;
            }
            throw new InvalidOperationException("Could not read the vanilla chunk ID for " + oldName + ".");
        }

        private static bool IsDefinitionOnly(string container, List<string> definitionOnly)
        {
            if (definitionOnly == null) return false;
            foreach (string d in definitionOnly)
            {
                if (string.Equals(Path.GetFullPath(d), Path.GetFullPath(container), StringComparison.OrdinalIgnoreCase))
                    return true;
            }
            return false;
        }

        /// <summary>A chunk ID is a 16-hex-digit package ID followed by an 8-digit index; only the first half identifies the package.</summary>
        private static string PackageIdOf(string chunkId)
        {
            if (chunkId == null) return "";
            return chunkId.Length >= 16 ? chunkId.Substring(0, 16).ToLowerInvariant() : chunkId.ToLowerInvariant();
        }
    }
}
