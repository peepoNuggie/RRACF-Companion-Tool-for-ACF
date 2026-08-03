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
        /// <summary>The BaseCamo= value written into ACF_Slot&lt;slot&gt;.txt. Negative means "use the source
        /// camo"; 0 is a real value (Normal), so it cannot double as the unset marker.</summary>
        public int BaseCamoId = -1;
        /// <summary>True when the template is the mod's own Camouf asset rather than the vanilla one.</summary>
        public bool TemplateFromMod;
        /// <summary>The .utoc holding that asset. It is deliberately not shipped: keeping it would leave
        /// the mod overriding the vanilla camo as well as filling the slot.</summary>
        public string TemplateContainer = "";
        public string ModName = "";
        public string DisplayName = "";
        public string Description = "";
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

    internal static class Pipeline
    {
        /// <summary>ACF's additional-uniform slots.</summary>
        public static readonly int[] ValidSlots = { 61, 62, 63, 64 };

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

                foreach (ManifestEntry e in entries)
                {
                    if (samplePackages.Count < 8) samplePackages.Add(e.PackageName);
                }

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
            tools.Validate();

            // The in-game name drives every generated name, so the folder and the paks always match.
            // Spaces and punctuation are stripped: "Fox Suit" on slot 61 becomes ACF_FoxSuit61.
            string modName = SanitiseName(o.DisplayName);
            if (modName.Length == 0) modName = SanitiseName(o.ModName);
            if (modName.Length == 0)
                throw new InvalidOperationException("Please enter an in-game name - it becomes the output folder and file names.");
            if (Array.IndexOf(ValidSlots, o.Slot) < 0)
                throw new InvalidOperationException("Slot must be one of " + string.Join(", ", Array.ConvertAll(ValidSlots, delegate(int i) { return i.ToString(); })) + ".");

            // A negative value means "not set", so BaseCamo=0 (Normal) stays possible.
            if (o.BaseCamoId < 0) o.BaseCamoId = o.SourceCamoId;

            if (o.SourceCamoId < 0 || o.SourceCamoId > 99)
                throw new InvalidOperationException("Camo ID " + o.SourceCamoId + " is out of range.");

            if (!Directory.Exists(o.PaksFolder))
                throw new InvalidOperationException("Game Paks folder not found: " + o.PaksFolder);

            List<string> containers = FindContainers(o.ModInput);

            string oldName = "Camouf_" + o.SourceCamoId + "_asset";
            string newName = "Camouf_" + o.Slot + "_asset";
            string stagingName = modName + o.Slot;               // e.g. Zero63
            string outputStem = "ACF_" + modName + o.Slot + "_P"; // e.g. ACF_Zero63_P

            // One self-contained folder that can be dropped straight into Content\Paks\mods.
            string modFolder = Path.Combine(o.OutputFolder, "ACF_" + modName + o.Slot);
            Directory.CreateDirectory(modFolder);

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
                CheckArtReferences(File.ReadAllBytes(srcUasset), oldName, log);

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
                File.WriteAllText(result.SlotTxtPath,
                    SlotFile.Generate(o.Slot, o.BaseCamoId, o.DisplayName, o.Description));
                log("  BaseCamo=" + o.BaseCamoId +
                    (o.BaseCamoId == o.SourceCamoId ? "" : "  (template was camo " + o.SourceCamoId + ")"));

                // 8. The replacer supplies the actual art, so it has to travel with the slot files.
                log("Copying the replacer mod's own files...");
                result.CopiedReplacerFiles = CopyReplacerFiles(containers, modFolder,
                    o.TemplateFromMod ? o.TemplateContainer : "", log);

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
        /// Fails the build if the camo asset no longer names any camouflage art.
        ///
        /// retoc writes imports it cannot resolve as /Engine/UnknownPackage. A slot built from such an
        /// asset packs, verifies and installs perfectly happily, then shows up as an empty camo - so
        /// this is checked rather than left to be discovered in game.
        /// </summary>
        private static void CheckArtReferences(byte[] assetBytes, string assetName, Action<string> log)
        {
            string text = System.Text.Encoding.ASCII.GetString(assetBytes);
            int artRefs = Regex.Matches(text, "/Body/Camouflage/[A-Za-z0-9_]+/", RegexOptions.IgnoreCase).Count;
            int unresolved = Regex.Matches(text, "/Engine/UnknownPackage").Count;

            if (artRefs > 0)
            {
                log("  points at " + artRefs + " camouflage art reference" + (artRefs == 1 ? "" : "s"));
                if (unresolved > 0)
                    log("  note: " + unresolved + " import(s) could not be resolved");
                return;
            }

            throw new InvalidOperationException(
                assetName + " does not point at any camouflage art, so the slot would be empty in game.\r\n\r\n" +
                "This usually means the mod ships only a camo asset and gets its art from a separate " +
                "download - often the base mod on the same Nexus page. Put that download in the Input " +
                "folder alongside this one and try again.");
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
                                                      string excludeContainer, Action<string> log)
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

        /// <summary>A chunk ID is a 16-hex-digit package ID followed by an 8-digit index; only the first half identifies the package.</summary>
        private static string PackageIdOf(string chunkId)
        {
            if (chunkId == null) return "";
            return chunkId.Length >= 16 ? chunkId.Substring(0, 16).ToLowerInvariant() : chunkId.ToLowerInvariant();
        }
    }
}
