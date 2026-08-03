using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Threading;
using System.Windows.Forms;

namespace Rracf
{
    internal class MainForm : Form
    {
        private TextBox _modBox, _paksBox, _outBox, _displayBox, _descBox, _logBox, _baseBox;
        private ComboBox _camoCombo, _slotCombo;
        private Label _baseHint;
        private CheckBox _openWhenDone;
        private Button _analyseButton, _buildButton, _rebuildMapButton;
        private Label _previewLabel, _statusLabel;

        private Tools _tools;
        private List<CamoEntry> _camoMap;
        private Analysis _analysis;
        private readonly Settings _settings;
        private readonly string _appFolder;
        private readonly string _camoMapPath;
        private bool _busy;

        public MainForm()
        {
            _appFolder = Path.GetDirectoryName(Application.ExecutablePath);
            // These live in Resources in the shipped layout, but are picked up from any folder under
            // the program - and rewritten wherever they were found, rather than dumped at the root.
            _camoMapPath = AppFiles.ResolveDataFile(_appFolder, "rracf-camomap.txt");
            _settings = Settings.Load(AppFiles.ResolveDataFile(_appFolder, "rracf-settings.txt"));
            _tools = Tools.Discover(_appFolder);

            Text = "RRACF " + "Version 0.1.0" + " - Replacer to ACF Slot Converter";
            Font = new Font("Segoe UI", 9f);
            ClientSize = new Size(760, 650);
            MinimumSize = new Size(700, 590);
            StartPosition = FormStartPosition.CenterScreen;

            BuildLayout();
            LoadSettingsIntoUi();
        }

        private const int LabelX = 12, FieldX = 150, FieldW = 480, BrowseX = 640, RowH = 30;

        private Label AddLabel(string text, int y)
        {
            var l = new Label();
            l.Text = text;
            l.Location = new Point(LabelX, y + 3);
            l.Size = new Size(135, 20);
            Controls.Add(l);
            return l;
        }

        private TextBox AddField(int y)
        {
            var t = new TextBox();
            t.Location = new Point(FieldX, y);
            t.Size = new Size(FieldW, 23);
            t.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
            Controls.Add(t);
            return t;
        }

        private Button AddBrowse(int y, EventHandler handler)
        {
            var b = new Button();
            b.Text = "Browse...";
            b.Location = new Point(BrowseX, y - 1);
            b.Size = new Size(100, 25);
            b.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            b.Click += handler;
            Controls.Add(b);
            return b;
        }

        private void BuildLayout()
        {
            int y = 14;

            AddLabel("Replacer mod folder", y);
            _modBox = AddField(y);
            AddBrowse(y, delegate { BrowseFolder(_modBox, "Select the folder containing the replacer mod"); });
            y += RowH;

            AddLabel("Game Paks folder", y);
            _paksBox = AddField(y);
            AddBrowse(y, delegate { BrowseFolder(_paksBox, "Select the game's Content\\Paks folder"); });
            y += RowH;

            AddLabel("Output folder", y);
            _outBox = AddField(y);
            AddBrowse(y, delegate { BrowseFolder(_outBox, "Where should the finished mod go?"); });
            y += RowH + 8;

            _analyseButton = new Button();
            _analyseButton.Text = "1.  Analyse mod";
            _analyseButton.Location = new Point(FieldX, y);
            _analyseButton.Size = new Size(160, 30);
            _analyseButton.Click += OnAnalyse;
            Controls.Add(_analyseButton);

            _rebuildMapButton = new Button();
            _rebuildMapButton.Text = "Rebuild camo list";
            _rebuildMapButton.Location = new Point(FieldX + 172, y);
            _rebuildMapButton.Size = new Size(140, 30);
            _rebuildMapButton.Click += OnRebuildMap;
            Controls.Add(_rebuildMapButton);

            _statusLabel = new Label();
            _statusLabel.Location = new Point(FieldX + 322, y + 7);
            _statusLabel.Size = new Size(300, 20);
            _statusLabel.ForeColor = Color.DimGray;
            _statusLabel.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
            Controls.Add(_statusLabel);
            y += 44;

            AddLabel("Replaces", y);
            _camoCombo = new ComboBox();
            _camoCombo.Location = new Point(FieldX, y);
            _camoCombo.Size = new Size(FieldW + 100, 23);
            _camoCombo.DropDownStyle = ComboBoxStyle.DropDownList;
            _camoCombo.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
            // Base camo is deliberately never auto-filled: it is a concealment value, not a camo ID,
            // so nothing about the detected camo is a sensible guess for it.
            Controls.Add(_camoCombo);
            y += RowH;

            AddLabel("Base camo", y);
            _baseBox = new TextBox();
            _baseBox.Location = new Point(FieldX, y);
            _baseBox.Size = new Size(60, 23);
            _baseBox.Anchor = AnchorStyles.Top | AnchorStyles.Left;
            Controls.Add(_baseBox);
            _baseHint = new Label();
            _baseHint.Text = "written as BaseCamo= in the .txt - safe to experiment with";
            _baseHint.Location = new Point(FieldX + 70, y + 3);
            _baseHint.Size = new Size(570, 20);
            _baseHint.ForeColor = Color.DimGray;
            _baseHint.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
            Controls.Add(_baseHint);
            y += RowH;

            AddLabel("ACF slot", y);
            _slotCombo = new ComboBox();
            _slotCombo.Location = new Point(FieldX, y);
            _slotCombo.Size = new Size(160, 23);
            _slotCombo.DropDownStyle = ComboBoxStyle.DropDownList;
            foreach (int s in Pipeline.ValidSlots)
                _slotCombo.Items.Add("Slot " + (s - 60) + "  (camo ID " + s + ")");
            _slotCombo.SelectedIndex = 0;
            _slotCombo.SelectedIndexChanged += delegate { UpdatePreview(); };
            Controls.Add(_slotCombo);
            y += RowH;

            AddLabel("In-game name", y);
            _displayBox = AddField(y);
            _displayBox.Size = new Size(200, 23);
            _displayBox.Anchor = AnchorStyles.Top | AnchorStyles.Left;
            _displayBox.TextChanged += delegate { UpdatePreview(); };
            _previewLabel = new Label();
            _previewLabel.Location = new Point(FieldX + 210, y + 3);
            _previewLabel.Size = new Size(430, 20);
            _previewLabel.ForeColor = Color.DimGray;
            _previewLabel.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
            Controls.Add(_previewLabel);
            y += RowH;

            AddLabel("Description", y);
            _descBox = AddField(y);
            y += RowH + 8;

            _buildButton = new Button();
            _buildButton.Text = "2.  Build ACF slot mod";
            _buildButton.Location = new Point(FieldX, y);
            _buildButton.Size = new Size(200, 34);
            Controls.Add(_buildButton);
            _buildButton.Click += OnBuild;

            _openWhenDone = new CheckBox();
            _openWhenDone.Text = "Open the output folder when finished";
            _openWhenDone.Location = new Point(FieldX + 212, y + 8);
            _openWhenDone.Size = new Size(300, 22);
            _openWhenDone.Anchor = AnchorStyles.Top | AnchorStyles.Left;
            Controls.Add(_openWhenDone);
            y += 46;

            var logLabel = new Label();
            logLabel.Text = "Log";
            logLabel.Location = new Point(LabelX, y);
            logLabel.Size = new Size(100, 18);
            Controls.Add(logLabel);
            y += 20;

            _logBox = new TextBox();
            _logBox.Multiline = true;
            _logBox.ReadOnly = true;
            _logBox.ScrollBars = ScrollBars.Vertical;
            _logBox.Font = new Font("Consolas", 8.5f);
            _logBox.BackColor = Color.White;
            _logBox.Location = new Point(LabelX, y);
            _logBox.Size = new Size(ClientSize.Width - 24, ClientSize.Height - y - 12);
            _logBox.Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;
            Controls.Add(_logBox);
        }

        private void LoadSettingsIntoUi()
        {
            // A remembered folder is only used if it is really there - otherwise a settings file from
            // another machine would have us create D:\Mod Hub\RRACF\Input on someone else's drive.
            _modBox.Text = ExistingOr(_settings.Get("input", ""), Path.Combine(_appFolder, "Input"));
            _outBox.Text = ExistingOr(_settings.Get("output", ""), Path.Combine(_appFolder, "Output"));
            _paksBox.Text = _settings.Get("paks", GameFinder.FindPaksFolder());

            _openWhenDone.Checked = _settings.Get("openwhendone", "1") != "0";

            // Empty folders do not survive being zipped up, so make sure they exist on first run.
            EnsureFolder(_modBox.Text);
            EnsureFolder(_outBox.Text);
            // A remembered tool path only wins if it still exists. Otherwise the settings file that
            // shipped with the download - or one carried over from another machine - would override
            // a perfectly good auto-detect with a path that is not there.
            string savedRetoc = _settings.Get("retoc", "");
            string savedRepak = _settings.Get("repak", "");
            _tools = new Tools(
                File.Exists(savedRetoc) ? savedRetoc : _tools.RetocPath,
                File.Exists(savedRepak) ? savedRepak : _tools.RepakPath);
            _tools.BaseFolder = _appFolder;

            // Same for the game folder: a path from someone else's machine is worse than detecting.
            if (!Directory.Exists(_paksBox.Text)) _paksBox.Text = GameFinder.FindPaksFolder();

            Log("RRACF " + AppInfo.Version + " - turns a replacer camo mod into an ACF slot mod.");
            Log("retoc: " + (File.Exists(_tools.RetocPath) ? _tools.RetocPath : "NOT FOUND"));
            Log("repak: " + (File.Exists(_tools.RepakPath) ? _tools.RepakPath : "NOT FOUND"));
            if (!_tools.IsReady)
            {
                Log("");
                Log("*** RRACF cannot run without these. Copy the retoc and repak folders");
                Log("*** next to RRACF.exe (" + _appFolder + "), then restart.");
            }
            Log("");
            // Reuse the camo list built on a previous run so the first Analyse is instant.
            List<CamoEntry> cached = CamoMap.Load(_camoMapPath);
            if (cached != null)
            {
                CamoMap.ApplyEnumNames(cached, GameFinder.FindEnumHeader(_paksBox.Text, _settings.Get("enums", "")),
                    delegate(string s) { });
                _camoMap = cached;
            }

            Log("Step 1: put the replacer mod in");
            Log("  " + _modBox.Text);
            Log("then press Analyse.");
            UpdatePreview();
            UpdateStatus();
        }

        private static string ExistingOr(string preferred, string fallback)
        {
            if (!string.IsNullOrEmpty(preferred) && Directory.Exists(preferred)) return preferred;
            return fallback;
        }

        private static void EnsureFolder(string path)
        {
            try { if (!string.IsNullOrEmpty(path)) Directory.CreateDirectory(path); }
            catch (Exception) { /* a bad saved path must not stop the window opening */ }
        }

        private void UpdateStatus()
        {
            string cache = _camoMapPath;
            _statusLabel.Text = File.Exists(cache)
                ? "Camo list ready."
                : "Camo list will be built on first analyse.";
        }

        private void UpdatePreview()
        {
            string name = Pipeline.SanitiseName(_displayBox == null ? "" : _displayBox.Text);
            int slot = SelectedSlot();
            string stem = "ACF_" + name + slot;
            _previewLabel.Text = name.Length == 0
                ? "-> enter a name to see the output"
                : "-> " + stem + "\\" + stem + "_P.pak / .ucas / .utoc";
        }

        private int SelectedSlot()
        {
            int i = _slotCombo == null ? 0 : _slotCombo.SelectedIndex;
            if (i < 0) i = 0;
            return Pipeline.ValidSlots[i];
        }

        private void BrowseFolder(TextBox target, string description)
        {
            using (var d = new FolderBrowserDialog())
            {
                d.Description = description;
                if (Directory.Exists(target.Text)) d.SelectedPath = target.Text;
                if (d.ShowDialog(this) == DialogResult.OK) target.Text = d.SelectedPath;
            }
        }

        private void Log(string message)
        {
            if (InvokeRequired) { BeginInvoke(new Action<string>(Log), message); return; }
            _logBox.AppendText(message + Environment.NewLine);
        }

        private void SetBusy(bool busy)
        {
            if (InvokeRequired) { BeginInvoke(new Action<bool>(SetBusy), busy); return; }
            _busy = busy;
            _analyseButton.Enabled = !busy;
            _buildButton.Enabled = !busy;
            _rebuildMapButton.Enabled = !busy;
            Cursor = busy ? Cursors.WaitCursor : Cursors.Default;
        }

        private void RunInBackground(ThreadStart work)
        {
            if (_busy) return;
            SetBusy(true);
            var t = new Thread(delegate()
            {
                try
                {
                    work();
                }
                catch (Exception ex)
                {
                    Log("");
                    Log("ERROR: " + ex.Message);
                    ShowError(ex.Message);
                }
                finally
                {
                    SetBusy(false);
                }
            });
            t.IsBackground = true;
            t.Start();
        }

        private void ShowError(string message)
        {
            if (InvokeRequired) { BeginInvoke(new Action<string>(ShowError), message); return; }
            MessageBox.Show(this, message, "RRACF", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }

        /// <summary>
        /// Asks a yes/no question from the build thread. Invoke rather than BeginInvoke, because the
        /// build is waiting on the answer.
        /// </summary>
        private bool Confirm(string title, string message)
        {
            if (InvokeRequired)
                return (bool)Invoke(new ConfirmCallback(Confirm), new object[] { title, message });
            return MessageBox.Show(this, message, "RRACF - " + title,
                       MessageBoxButtons.YesNo, MessageBoxIcon.Warning, MessageBoxDefaultButton.Button2) == DialogResult.Yes;
        }

        private List<CamoEntry> EnsureCamoMap(bool forceRebuild)
        {
            string cachePath = _camoMapPath;
            if (!forceRebuild && _camoMap != null) return _camoMap;

            List<CamoEntry> map = forceRebuild ? null : CamoMap.Load(cachePath);
            if (map == null)
            {
                string scratch = Path.Combine(Path.GetTempPath(), "RRACF_map_" + Guid.NewGuid().ToString("N"));
                Directory.CreateDirectory(scratch);
                try
                {
                    map = CamoMap.Build(_tools, _paksBox.Text, scratch, Log);
                    CamoMap.Save(map, cachePath);
                }
                finally { try { Io.DeleteDirectory(scratch); } catch (Exception) { } }
            }
            else
            {
                Log("Loaded the camo list from " + Path.GetFileName(cachePath) + " (" + map.Count + " camouflages).");
            }

            CamoMap.ApplyEnumNames(map, GameFinder.FindEnumHeader(_paksBox.Text, _settings.Get("enums", "")), Log);
            _camoMap = map;
            BeginInvoke(new Action(UpdateStatus));
            return _camoMap;
        }

        private void OnRebuildMap(object sender, EventArgs e)
        {
            RunInBackground(delegate
            {
                _tools.Validate();
                Log("");
                Log("Rebuilding the camo list from the game...");
                EnsureCamoMap(true);
                Log("Camo list rebuilt.");
            });
        }

        private void OnAnalyse(object sender, EventArgs e)
        {
            string modPath = _modBox.Text.Trim();
            SaveSettings();
            RunInBackground(delegate
            {
                _tools.Validate();
                Log("");
                Log("=== Analysing " + modPath + " ===");
                var map = EnsureCamoMap(false);
                string scratch = Path.Combine(Path.GetTempPath(), "RRACF_an_" + Guid.NewGuid().ToString("N"));
                Directory.CreateDirectory(scratch);
                Analysis analysis;
                try { analysis = Pipeline.Analyze(_tools, modPath, map, scratch, Log); }
                finally { try { Io.DeleteDirectory(scratch); } catch (Exception) { } }

                _analysis = analysis;

                foreach (string f in analysis.UnmatchedFolders)
                    Log("Note: folder \"" + f + "\" does not match any vanilla camouflage - ignoring it.");

                string suggested = Pipeline.SuggestName(analysis.ChosenUtoc);
                BeginInvoke(new Action(delegate
                {
                    _camoCombo.Items.Clear();
                    foreach (CamoChoice c in analysis.Choices) _camoCombo.Items.Add(c);
                    if (_camoCombo.Items.Count > 0) _camoCombo.SelectedIndex = 0;
                    if (_displayBox.Text.Length == 0) _displayBox.Text = suggested;
                    UpdatePreview();
                }));

                if (analysis.AddOnDetected)
                {
                    Log("");
                    foreach (string line in analysis.AddOnMessage.Replace("\r\n", "\n").Split('\n'))
                        Log(analysis.BaseModMissing ? "*** " + line : line);
                    Log("");
                    if (analysis.BaseModMissing)
                        ShowError(analysis.AddOnMessage);
                }

                Log("This mod replaces:");
                foreach (CamoChoice c in analysis.Choices) Log("  " + c);
                if (analysis.Choices.Count > 1)
                    Log("More than one match - the best guess is selected, change it in the \"Replaces\" box if needed.");
                Log("Step 2: choose a slot and press Build.");
            });
        }

        private void OnBuild(object sender, EventArgs e)
        {
            var choice = _camoCombo.SelectedItem as CamoChoice;
            if (choice == null)
            {
                ShowError("Press \"Analyse mod\" first so the tool knows which camouflage this mod replaces.");
                return;
            }

            var options = new BuildOptions();
            options.ModInput = _modBox.Text.Trim();
            options.PaksFolder = _paksBox.Text.Trim();
            options.OutputFolder = _outBox.Text.Trim();
            options.Slot = SelectedSlot();
            options.SourceCamoId = choice.CamoId;
            options.TemplateFromMod = choice.TemplateFromMod;
            options.TemplateContainer = choice.TemplateContainer;
            if (_analysis != null) options.DefinitionOnlyContainers = _analysis.DefinitionOnlyContainers;
            // BaseCamo is a camouflage index, not a camo ID: a signed byte where Naked is 0, Olive Drab
            // 10, Tiger Stripe 30 and Gold -100. Negatives are legal, so blank simply means zero.
            int baseCamo = 0;
            string baseText = _baseBox.Text.Trim();
            if (baseText.Length > 0 && !int.TryParse(baseText, out baseCamo))
            {
                ShowError("Base camo must be a whole number between -128 and 127 (or left blank for 0).");
                return;
            }
            if (baseCamo < -128 || baseCamo > 127)
            {
                ShowError("Base camo is stored as a single signed byte, so it must be between -128 and 127.\r\n\r\n" +
                          "For scale: Naked is 0, Olive Drab 10, Tiger Stripe 30, Gold -100.");
                return;
            }
            options.BaseCamoId = baseCamo;
            options.DisplayName = _displayBox.Text.Trim();
            options.Description = _descBox.Text.Trim();
            SaveSettings();

            RunInBackground(delegate
            {
                Log("");
                Log("=== Building slot " + options.Slot + " from camo " + options.SourceCamoId + " ===");
                BuildResult r = Pipeline.Build(_tools, options, Log, Confirm);
                Log("");
                Log("Created " + r.ModFolder + " containing:");
                foreach (string p in new[] { r.PakPath, r.UcasPath, r.UtocPath, r.SlotTxtPath })
                    Log("  " + Path.GetFileName(p));
                foreach (string p in r.CopiedReplacerFiles)
                    Log("  " + Path.GetFileName(p) + "   (from the replacer mod)");
                Log("");
                Log("Drop that whole folder into the game's Content\\Paks\\mods folder.");
                OpenOutputIfWanted(r.ModFolder);
            });
        }

        private void OpenOutputIfWanted(string outputFolder)
        {
            if (InvokeRequired) { BeginInvoke(new Action<string>(OpenOutputIfWanted), outputFolder); return; }
            if (!_openWhenDone.Checked) return;
            try { System.Diagnostics.Process.Start("explorer.exe", Io.Quote(outputFolder)); }
            catch (Exception) { }
        }

        private void SaveSettings()
        {
            _settings.Set("openwhendone", _openWhenDone.Checked ? "1" : "0");
            _settings.Set("input", _modBox.Text.Trim());
            _settings.Set("paks", _paksBox.Text.Trim());
            _settings.Set("output", _outBox.Text.Trim());
            _settings.Set("retoc", _tools.RetocPath);
            _settings.Set("repak", _tools.RepakPath);
            _settings.Save();
        }
    }
}
