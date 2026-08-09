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
        private TextBox _modBox, _paksBox, _outBox, _displayBox, _logBox, _baseBox;
        private TextBox _plainDesc, _abilityDesc, _warningDesc, _specialDesc;
        private ComboBox _slotCombo;
        private Panel _camoPanel;
        private Label _baseHint, _gridHint, _nameWarnLabel, _slot65CamoNote;
        private CheckBox _openWhenDone;
        private CheckBox _cbInfAmmoAll, _cbSteadyAim, _cbInfSuppressor, _cbSilentSteps;
        private CheckedListBox _ammoList;
        private Label _ammoPreview;
        private TabControl _tabs;
        private DataGridView _grid;
        private ComboBox _templateCombo;
        private Button _analyseButton, _buildButton, _rebuildMapButton;
        private Label _previewLabel, _statusLabel;
        private string _lastSavePath = "";

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

            Text = "RRACF " + AppInfo.Version + " - Replacer to ACF Slot Converter";
            Font = new Font("Segoe UI", 9f);
            ClientSize = new Size(1010, 920);
            MinimumSize = new Size(900, 740);
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

        /// <summary>Saves live beside the program so they can be found without being told where.</summary>
        private string SavesFolder
        {
            get
            {
                string resources = Path.Combine(_appFolder, "Resources");
                string root = Directory.Exists(resources) ? resources : _appFolder;
                return Path.Combine(root, "Saves");
            }
        }

        private void BuildMenu()
        {
            var menu = new MenuStrip();
            var file = new ToolStripMenuItem("&File");

            var save = new ToolStripMenuItem("&Save slot settings...");
            save.ShortcutKeys = Keys.Control | Keys.S;
            save.Click += OnSaveProject;
            file.DropDownItems.Add(save);

            var load = new ToolStripMenuItem("&Load slot settings...");
            load.ShortcutKeys = Keys.Control | Keys.O;
            load.Click += OnLoadProject;
            file.DropDownItems.Add(load);

            file.DropDownItems.Add(new ToolStripSeparator());

            var open = new ToolStripMenuItem("Open sa&ves folder");
            open.Click += delegate
            {
                try
                {
                    Directory.CreateDirectory(SavesFolder);
                    System.Diagnostics.Process.Start("explorer.exe", Io.Quote(SavesFolder));
                }
                catch (Exception ex) { ShowError("Could not open " + SavesFolder + "\r\n\r\n" + ex.Message); }
            };
            file.DropDownItems.Add(open);

            file.DropDownItems.Add(new ToolStripSeparator());
            var quit = new ToolStripMenuItem("E&xit");
            quit.Click += delegate { Close(); };
            file.DropDownItems.Add(quit);

            menu.Items.Add(file);
            MainMenuStrip = menu;
            Controls.Add(menu);
        }

        private void BuildLayout()
        {
            BuildMenu();
            int y = 34;

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

            // A list of radio buttons rather than a dropdown, so every camo the mod touches is visible
            // at once. Fixed height with a scrollbar keeps everything below it in place; most mods
            // offer one or two.
            AddLabel("Replaces", y);
            _camoPanel = new Panel();
            _camoPanel.Location = new Point(FieldX, y);
            _camoPanel.Size = new Size(FieldW + 100, 68);
            _camoPanel.AutoScroll = true;
            _camoPanel.BorderStyle = BorderStyle.FixedSingle;
            _camoPanel.BackColor = Color.White;
            _camoPanel.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
            Controls.Add(_camoPanel);
            ShowCamoPlaceholder("press \"1. Analyse mod\" and this fills in by itself");
            y += 76;

            AddLabel("ACF slot", y);
            _slotCombo = new ComboBox();
            _slotCombo.Location = new Point(FieldX, y);
            _slotCombo.Size = new Size(160, 23);
            _slotCombo.DropDownStyle = ComboBoxStyle.DropDownList;
            foreach (int s in Pipeline.ValidSlots)
                _slotCombo.Items.Add("Slot " + (s - 60) + "  (camo ID " + s + ")");
            _slotCombo.SelectedIndex = 0;
            _slotCombo.SelectedIndexChanged += delegate { UpdatePreview(); UpdateSlotWarnings(); };
            Controls.Add(_slotCombo);
            y += RowH;

            AddLabel("In-game name", y);
            _displayBox = AddField(y);
            _displayBox.Size = new Size(200, 23);
            _displayBox.Anchor = AnchorStyles.Top | AnchorStyles.Left;
            _displayBox.TextChanged += delegate { UpdatePreview(); UpdateSlotWarnings(); };
            _previewLabel = new Label();
            _previewLabel.Location = new Point(FieldX + 210, y + 3);
            _previewLabel.AutoSize = true;
            _previewLabel.ForeColor = Color.DimGray;
            _previewLabel.Anchor = AnchorStyles.Top | AnchorStyles.Left;
            Controls.Add(_previewLabel);
            y += RowH - 4;

            // Slot 5's name buffer holds 15 characters and a longer one is ignored outright, so the
            // warning has to be visible while typing rather than at build time.
            _nameWarnLabel = new Label();
            _nameWarnLabel.Location = new Point(FieldX, y);
            _nameWarnLabel.AutoSize = true;
            _nameWarnLabel.ForeColor = Color.Firebrick;
            _nameWarnLabel.Anchor = AnchorStyles.Top | AnchorStyles.Left;
            Controls.Add(_nameWarnLabel);
            y += 22;

            BuildTabs(y);
            y += TabsHeight + 8;

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

        private const int TabsHeight = 330;

        /// <summary>
        /// Description, camouflage and abilities each get a tab. ACF 2.0 added enough fields that a
        /// single column would push the log off the screen; the identity rows and the Build button
        /// stay outside the tabs so the flow is still analyse, name, build.
        /// </summary>
        private void BuildTabs(int y)
        {
            _tabs = new TabControl();
            _tabs.Location = new Point(LabelX, y);
            _tabs.Size = new Size(ClientSize.Width - 24, TabsHeight);
            _tabs.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
            Controls.Add(_tabs);

            _tabs.TabPages.Add(BuildDescriptionTab());
            _tabs.TabPages.Add(BuildCamouflageTab());
            _tabs.TabPages.Add(BuildAbilitiesTab());
        }

        private static Label PageLabel(TabPage page, string text, int x, int yy, Color color)
        {
            var l = new Label();
            l.Text = text;
            l.Location = new Point(x, yy + 3);
            l.AutoSize = true;
            l.ForeColor = color;
            page.Controls.Add(l);
            return l;
        }

        private TabPage BuildDescriptionTab()
        {
            var page = new TabPage("Description");
            page.BackColor = SystemColors.Control;
            int yy = 14;

            PageLabel(page, "Each line is optional. Blank ones are left out of the file entirely.",
                12, yy, Color.DimGray);
            yy += 26;

            _plainDesc = AddPageField(page, "Plain", yy, Color.Gray, "PlainDesc");
            yy += RowH;
            _abilityDesc = AddPageField(page, "Ability", yy, Color.DarkOrange, "AbilityDescOrange");
            yy += RowH;
            _warningDesc = AddPageField(page, "Warning", yy, Color.Firebrick, "WarningDesc");
            yy += RowH;
            _specialDesc = AddPageField(page, "Special", yy, Color.Goldenrod, "SpecialDesc");
            yy += RowH + 10;

            PageLabel(page, "The colors above are how each line appears in game. ACF joins whatever is " +
                            "present, in this order.", 12, yy, Color.DimGray);
            yy += 22;
            PageLabel(page, "Description is cosmetic - writing \"Never runs out of ammo\" here does not grant " +
                            "it. Use the Abilities tab.", 12, yy, Color.DimGray);
            return page;
        }

        /// <summary>A description row: colored label showing the in-game color, then the field.</summary>
        private TextBox AddPageField(TabPage page, string label, int yy, Color color, string key)
        {
            var l = PageLabel(page, label, 12, yy, color);
            l.Font = new Font(Font, FontStyle.Bold);

            var swatch = new Panel();
            swatch.BackColor = color;
            swatch.Location = new Point(80, yy + 4);
            swatch.Size = new Size(14, 14);
            swatch.BorderStyle = BorderStyle.FixedSingle;
            page.Controls.Add(swatch);

            var box = new TextBox();
            box.Location = new Point(104, yy);
            box.Size = new Size(848, 23);
            box.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
            box.Tag = key;
            page.Controls.Add(box);
            return box;
        }

        private TabPage BuildCamouflageTab()
        {
            var page = new TabPage("Camouflage");
            page.BackColor = SystemColors.Control;
            int yy = 12;

            // Base camo is deliberately never auto-filled: it is a concealment value, not a camo ID,
            // so nothing about the detected camo is a sensible guess for it.
            PageLabel(page, "Base camo", 12, yy, SystemColors.ControlText);
            _baseBox = new TextBox();
            _baseBox.Location = new Point(90, yy);
            _baseBox.Size = new Size(60, 23);
            page.Controls.Add(_baseBox);
            _baseHint = PageLabel(page,
                "Written as BaseCamo= in the .txt - not recommended to use for vanilla-like camo's, " +
                "instead use per-terrain values.", 160, yy, Color.DimGray);
            yy += RowH;

            PageLabel(page, "Camo values", 12, yy, SystemColors.ControlText);
            _templateCombo = new ComboBox();
            _templateCombo.Location = new Point(90, yy);
            _templateCombo.Size = new Size(240, 23);
            _templateCombo.DropDownStyle = ComboBoxStyle.DropDownList;
            foreach (GridTemplate t in GridTemplates.All()) _templateCombo.Items.Add(t);
            _templateCombo.SelectedIndex = 0;
            _templateCombo.SelectedIndexChanged += delegate { ApplyTemplate(); };
            page.Controls.Add(_templateCombo);
            _gridHint = PageLabel(page,
                "If you're using per-terrain values, recommend BaseCamo=0 for a more vanilla-like " +
                "experience", 340, yy, Color.DimGray);
            yy += RowH;

            // Only shown when slot 5 is selected - see Pipeline.HasNameLimit's neighbours.
            _slot65CamoNote = PageLabel(page, "", 12, yy, Color.Firebrick);
            _slot65CamoNote.Visible = false;
            yy += 22;

            _grid = new DataGridView();
            _grid.Location = new Point(12, yy);
            _grid.Size = new Size(940, TabsHeight - yy - 34);
            _grid.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right | AnchorStyles.Bottom;
            _grid.AllowUserToAddRows = false;
            _grid.AllowUserToDeleteRows = false;
            _grid.AllowUserToResizeRows = false;
            _grid.RowHeadersVisible = false;
            _grid.BackgroundColor = Color.White;
            _grid.EditMode = DataGridViewEditMode.EditOnEnter;
            _grid.SelectionMode = DataGridViewSelectionMode.CellSelect;
            _grid.ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.DisableResizing;
            BuildGridColumns();
            FillGridRows();
            page.Controls.Add(_grid);
            return page;
        }

        private TabPage BuildAbilitiesTab()
        {
            var page = new TabPage("Abilities");
            page.BackColor = SystemColors.Control;
            int yy = 12;

            PageLabel(page, "All off by default. Each mirrors something a vanilla camo already does.",
                12, yy, Color.DimGray);
            yy += 26;

            _cbSteadyAim = AddPageCheck(page, "Steady aim  -  no shake while aiming (first person)", yy);
            yy += 24;
            _cbSilentSteps = AddPageCheck(page, "Silent steps  -  footsteps make no noise", yy);
            yy += 24;
            _cbInfSuppressor = AddPageCheck(page, "Infinite suppressor  -  durability never drops", yy);
            yy += 24;
            _cbInfAmmoAll = AddPageCheck(page, "Infinite ammo  -  EVERY weapon costs no ammo", yy);
            _cbInfAmmoAll.CheckedChanged += delegate { UpdateAmmoPreview(); };
            yy += 30;

            PageLabel(page, "Infinite ammo for specific weapons only  -  independent of the box above; " +
                            "either alone turns it on.", 12, yy, Color.DimGray);
            yy += 20;

            // Above the list rather than below it: anchored to the bottom it ends up clipped by the
            // tab edge, and the exact line being written is worth seeing.
            _ammoPreview = new Label();
            _ammoPreview.Location = new Point(12, yy);
            _ammoPreview.AutoSize = false;
            _ammoPreview.Size = new Size(940, 18);
            _ammoPreview.ForeColor = Color.DarkSlateBlue;
            _ammoPreview.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
            page.Controls.Add(_ammoPreview);
            yy += 22;

            // Single column with a vertical scrollbar, so the mouse wheel works. Multi-column
            // CheckedListBox scrolls sideways and the wheel does nothing.
            _ammoList = new CheckedListBox();
            _ammoList.Location = new Point(12, yy);
            _ammoList.Size = new Size(940, TabsHeight - yy - 34);
            _ammoList.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right | AnchorStyles.Bottom;
            _ammoList.CheckOnClick = true;
            _ammoList.IntegralHeight = false;
            _ammoList.MultiColumn = false;
            _ammoList.BorderStyle = BorderStyle.FixedSingle;
            _ammoList.DrawMode = DrawMode.OwnerDrawFixed;
            _ammoList.ItemHeight = 20;
            _ammoList.DrawItem += AmmoListDrawItem;
            foreach (AmmoEntry e in AmmoCatalogue.All()) _ammoList.Items.Add(e);
            _ammoList.ItemCheck += delegate { BeginInvoke(new Action(UpdateAmmoPreview)); };
            page.Controls.Add(_ammoList);

            UpdateAmmoPreview();
            return page;
        }

        private CheckBox AddPageCheck(TabPage page, string text, int yy)
        {
            var cb = new CheckBox();
            cb.Text = text;
            cb.Location = new Point(14, yy);
            cb.AutoSize = true;
            page.Controls.Add(cb);
            return cb;
        }

        /// <summary>
        /// Draws the picker so a category reads as a heading and its weapons sit indented under it.
        /// The default list renders every row identically, which made 30 entries hard to scan.
        /// </summary>
        private void AmmoListDrawItem(object sender, DrawItemEventArgs e)
        {
            if (e.Index < 0) return;
            var entry = _ammoList.Items[e.Index] as AmmoEntry;
            if (entry == null) return;

            bool selected = (e.State & DrawItemState.Selected) == DrawItemState.Selected;
            Color back = selected ? SystemColors.Highlight
                       : entry.IsCategory ? Color.FromArgb(238, 240, 245) : SystemColors.Window;
            using (var brush = new SolidBrush(back)) e.Graphics.FillRectangle(brush, e.Bounds);

            int boxLeft = entry.IsCategory ? 6 : 26;
            var glyph = new Rectangle(boxLeft, e.Bounds.Top + (e.Bounds.Height - 14) / 2, 14, 14);
            System.Windows.Forms.VisualStyles.CheckBoxState state =
                _ammoList.GetItemChecked(e.Index)
                    ? System.Windows.Forms.VisualStyles.CheckBoxState.CheckedNormal
                    : System.Windows.Forms.VisualStyles.CheckBoxState.UncheckedNormal;
            CheckBoxRenderer.DrawCheckBox(e.Graphics, glyph.Location, state);

            using (var font = new Font(Font, entry.IsCategory ? FontStyle.Bold : FontStyle.Regular))
            {
                Color fore = selected ? SystemColors.HighlightText
                           : entry.IsCategory ? Color.FromArgb(30, 40, 70) : SystemColors.WindowText;
                var text = new Rectangle(boxLeft + 20, e.Bounds.Top, e.Bounds.Width - boxLeft - 24, e.Bounds.Height);
                TextRenderer.DrawText(e.Graphics, entry.Display, font, text, fore,
                    TextFormatFlags.VerticalCenter | TextFormatFlags.Left | TextFormatFlags.EndEllipsis);
            }

            if (selected) e.DrawFocusRectangle();
        }

        /// <summary>Shows the exact INFAmmoWeapon line the ticks will produce.</summary>
        private void UpdateAmmoPreview()
        {
            string line = SelectedAmmoTokens();
            if (line.Length == 0)
            {
                _ammoPreview.Text = _cbInfAmmoAll.Checked
                    ? "INFAmmoWeapon=          (empty - the box above already covers every weapon)"
                    : "INFAmmoWeapon=          (nothing ticked)";
            }
            else
            {
                _ammoPreview.Text = "INFAmmoWeapon=" + line;
            }
        }

        private string SelectedAmmoTokens()
        {
            var tokens = new List<string>();
            foreach (object o in _ammoList.CheckedItems)
            {
                var e = o as AmmoEntry;
                if (e != null) tokens.Add(e.Token);
            }
            return string.Join(",", tokens.ToArray());
        }

        private void BuildGridColumns()
        {
            var where = new DataGridViewTextBoxColumn();
            where.HeaderText = "Where";
            where.ReadOnly = true;
            where.Width = 70;
            where.SortMode = DataGridViewColumnSortMode.NotSortable;
            where.DefaultCellStyle.BackColor = SystemColors.Control;
            where.DefaultCellStyle.ForeColor = Color.DimGray;
            _grid.Columns.Add(where);

            var surface = new DataGridViewTextBoxColumn();
            surface.HeaderText = "Surface";
            surface.ReadOnly = true;
            surface.Width = 130;
            surface.SortMode = DataGridViewColumnSortMode.NotSortable;
            surface.DefaultCellStyle.BackColor = SystemColors.Control;
            _grid.Columns.Add(surface);

            for (int i = 0; i < Terrain.StanceNames.Length; i++)
            {
                var col = new DataGridViewTextBoxColumn();
                col.HeaderText = Terrain.StanceNames[i];
                // The last header is the longest, so give it the room rather than truncating it.
                col.Width = i == Terrain.StanceNames.Length - 1 ? 88 : 65;
                col.SortMode = DataGridViewColumnSortMode.NotSortable;
                col.DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleRight;
                _grid.Columns.Add(col);
            }
        }

        private void FillGridRows()
        {
            _grid.Rows.Clear();
            foreach (string s in Terrain.AllSurfaces())
            {
                int i = _grid.Rows.Add();
                var row = _grid.Rows[i];
                row.Tag = s;
                row.Cells[0].Value = Terrain.GroupOf(s);
                row.Cells[1].Value = Terrain.FriendlyName(s);
                for (int c = 0; c < Terrain.Stances; c++) row.Cells[2 + c].Value = "0";
            }
        }

        private void ApplyTemplate()
        {
            var t = _templateCombo.SelectedItem as GridTemplate;
            if (t == null) return;
            var g = new TerrainGrid();
            t.Apply(g);
            WriteGridToUi(g);
            UpdatePreview();
        }

        private void WriteGridToUi(TerrainGrid g)
        {
            foreach (DataGridViewRow row in _grid.Rows)
            {
                string s = row.Tag as string;
                if (s == null) continue;
                int[] v = g.Row(s);
                for (int c = 0; c < Terrain.Stances; c++) row.Cells[2 + c].Value = v[c].ToString();
            }
        }

        /// <summary>Reads the grid back, reporting the first cell that is not a usable number.</summary>
        private bool TryReadGrid(out TerrainGrid grid, out string error)
        {
            grid = new TerrainGrid();
            error = null;
            foreach (DataGridViewRow row in _grid.Rows)
            {
                string s = row.Tag as string;
                if (s == null) continue;
                int[] target = grid.Row(s);
                for (int c = 0; c < Terrain.Stances; c++)
                {
                    object raw = row.Cells[2 + c].Value;
                    string text = raw == null ? "" : raw.ToString().Trim();
                    if (text.Length == 0) { target[c] = 0; continue; }

                    int v;
                    if (!int.TryParse(text, out v))
                    {
                        error = Terrain.FriendlyName(s) + " / " + Terrain.StanceNames[c] +
                                " is \"" + text + "\", which is not a whole number.";
                        return false;
                    }
                    if (!TerrainGrid.InRange(v))
                    {
                        error = Terrain.FriendlyName(s) + " / " + Terrain.StanceNames[c] +
                                " is " + v + ". Values are stored as a single signed byte, so they must be " +
                                "between -128 and 127. Stay within -100 to 100 for sensible results.";
                        return false;
                    }
                    target[c] = v;
                }
            }
            return true;
        }

        // ---- save / load -------------------------------------------------------------

        private ProjectState CurrentState()
        {
            var s = new ProjectState();
            s.Slot = SelectedSlot();
            s.Name = _displayBox.Text.Trim();
            s.PlainDesc = _plainDesc.Text.Trim();
            s.AbilityDescOrange = _abilityDesc.Text.Trim();
            s.WarningDesc = _warningDesc.Text.Trim();
            s.SpecialDesc = _specialDesc.Text.Trim();

            int baseCamo;
            int.TryParse(_baseBox.Text.Trim(), out baseCamo);
            s.BaseCamo = baseCamo;

            TerrainGrid grid; string err;
            if (TryReadGrid(out grid, out err)) s.Grid = grid;

            s.Abilities.InfAmmoAll = _cbInfAmmoAll.Checked;
            s.Abilities.SteadyAim = _cbSteadyAim.Checked;
            s.Abilities.InfSuppressor = _cbInfSuppressor.Checked;
            s.Abilities.SilentSteps = _cbSilentSteps.Checked;
            s.Abilities.InfAmmoWeapons = SelectedAmmoTokens();
            return s;
        }

        private void ApplyState(ProjectState s)
        {
            int idx = Array.IndexOf(Pipeline.ValidSlots, s.Slot);
            if (idx >= 0) _slotCombo.SelectedIndex = idx;

            _displayBox.Text = s.Name;
            _plainDesc.Text = s.PlainDesc;
            _abilityDesc.Text = s.AbilityDescOrange;
            _warningDesc.Text = s.WarningDesc;
            _specialDesc.Text = s.SpecialDesc;
            _baseBox.Text = s.BaseCamo.ToString();
            WriteGridToUi(s.Grid);

            _cbInfAmmoAll.Checked = s.Abilities.InfAmmoAll;
            _cbSteadyAim.Checked = s.Abilities.SteadyAim;
            _cbInfSuppressor.Checked = s.Abilities.InfSuppressor;
            _cbSilentSteps.Checked = s.Abilities.SilentSteps;

            // Match saved names against the catalogue the way ACF would, so a hand-edited save with
            // "ak 47" still ticks the AK-47 box.
            var wanted = new List<string>();
            foreach (string t in AmmoCatalogue.Split(s.Abilities.InfAmmoWeapons))
                wanted.Add(AmmoCatalogue.Normalise(t));
            for (int i = 0; i < _ammoList.Items.Count; i++)
            {
                var e = _ammoList.Items[i] as AmmoEntry;
                bool on = e != null && wanted.Contains(AmmoCatalogue.Normalise(e.Token));
                _ammoList.SetItemChecked(i, on);
            }

            UpdatePreview();
            UpdateSlotWarnings();
            UpdateAmmoPreview();
        }

        private void OnSaveProject(object sender, EventArgs e)
        {
            try
            {
                Directory.CreateDirectory(SavesFolder);
                using (var d = new SaveFileDialog())
                {
                    d.Title = "Save slot settings";
                    d.Filter = "RRACF save (*.rracf)|*.rracf|All files (*.*)|*.*";
                    d.InitialDirectory = SavesFolder;
                    string name = Pipeline.SanitiseName(_displayBox.Text);
                    d.FileName = (name.Length > 0 ? name : "slot") + SelectedSlot() + ".rracf";
                    if (d.ShowDialog(this) != DialogResult.OK) return;
                    CurrentState().Save(d.FileName);
                    _lastSavePath = d.FileName;
                    Log("Saved slot settings to " + d.FileName);
                }
            }
            catch (Exception ex) { ShowError("Could not save.\r\n\r\n" + ex.Message); }
        }

        private void OnLoadProject(object sender, EventArgs e)
        {
            try
            {
                Directory.CreateDirectory(SavesFolder);
                using (var d = new OpenFileDialog())
                {
                    d.Title = "Load slot settings";
                    d.Filter = "RRACF save (*.rracf)|*.rracf|All files (*.*)|*.*";
                    d.InitialDirectory = Directory.Exists(SavesFolder) ? SavesFolder : _appFolder;
                    if (d.ShowDialog(this) != DialogResult.OK) return;
                    ApplyState(ProjectState.Load(d.FileName));
                    _lastSavePath = d.FileName;
                    Log("Loaded slot settings from " + d.FileName);
                }
            }
            catch (Exception ex) { ShowError("Could not load that file.\r\n\r\n" + ex.Message); }
        }

        private void ShowCamoPlaceholder(string text)
        {
            _camoPanel.Controls.Clear();
            var l = new Label();
            l.Text = text;
            l.ForeColor = Color.DimGray;
            l.Location = new Point(6, 6);
            l.AutoSize = true;
            _camoPanel.Controls.Add(l);
        }

        /// <summary>One radio button per camo the mod touches; the best guess starts selected.</summary>
        private void SetCamoChoices(List<CamoChoice> choices)
        {
            _camoPanel.Controls.Clear();
            int top = 5;
            foreach (CamoChoice c in choices)
            {
                var rb = new RadioButton();
                rb.Text = c.ToString();
                rb.Tag = c;
                rb.Location = new Point(6, top);
                rb.AutoSize = true;
                rb.Checked = _camoPanel.Controls.Count == 0;   // the first is the best guess
                _camoPanel.Controls.Add(rb);
                top += 22;
            }
            if (choices.Count == 0) ShowCamoPlaceholder("nothing detected");
        }

        private CamoChoice SelectedCamo()
        {
            foreach (Control ctl in _camoPanel.Controls)
            {
                var rb = ctl as RadioButton;
                if (rb != null && rb.Checked) return rb.Tag as CamoChoice;
            }
            return null;
        }

        /// <summary>
        /// Slot 5 has two traps worth showing before the build, not after: a 15-character name limit
        /// that discards a longer name outright, and concealment values the game ignores.
        /// </summary>
        private void UpdateSlotWarnings()
        {
            if (_nameWarnLabel == null || _slot65CamoNote == null) return;
            int slot = SelectedSlot();
            string name = _displayBox == null ? "" : _displayBox.Text.Trim();

            if (Pipeline.HasNameLimit(slot))
            {
                int over = name.Length - Pipeline.Slot65NameLimit;
                _nameWarnLabel.Text = over > 0
                    ? "Slot 5 name limit: " + name.Length + "/" + Pipeline.Slot65NameLimit +
                      " characters. A longer name is IGNORED, and the row falls back to \"ACF Mod 5\"."
                    : "Slot 5 caps the in-game name at " + Pipeline.Slot65NameLimit + " characters (" +
                      name.Length + " used).";
                _nameWarnLabel.ForeColor = over > 0 ? Color.Firebrick : Color.DimGray;
                _nameWarnLabel.Visible = true;

                _slot65CamoNote.Text = "Slot 5 IGNORES concealment - ACF reads these correctly and the game " +
                                       "overrides them with Tiger Stripe. Use slots 1-4 if concealment matters.";
                _slot65CamoNote.Visible = true;
            }
            else
            {
                _nameWarnLabel.Visible = false;
                _slot65CamoNote.Visible = false;
            }
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
                    SetCamoChoices(analysis.Choices);
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
            CamoChoice choice = SelectedCamo();
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

            TerrainGrid grid;
            string gridError;
            if (!TryReadGrid(out grid, out gridError))
            {
                ShowError(gridError);
                return;
            }
            // ACF adds the two together, so a slot with both set conceals better than either number
            // suggests. Worth a word rather than a silent surprise in game.
            if (baseCamo != 0 && !grid.IsAllZero)
            {
                if (!Confirm("Both are set",
                        "Base camo is " + baseCamo + " and the per-terrain grid has values in it.\r\n\r\n" +
                        "ACF ADDS them together, so a grass value of 35 with Base camo " + baseCamo +
                        " gives " + (35 + baseCamo) + " in grass, not 35.\r\n\r\n" +
                        "Normally you want one or the other: Base camo on its own for something simple, " +
                        "or Base camo 0 and the grid filled in for a camo that behaves like a real one.\r\n\r\n" +
                        "Build it anyway?"))
                    return;
            }
            options.Grid = grid;
            options.DisplayName = _displayBox.Text.Trim();
            options.PlainDesc = _plainDesc.Text.Trim();
            options.AbilityDescOrange = _abilityDesc.Text.Trim();
            options.WarningDesc = _warningDesc.Text.Trim();
            options.SpecialDesc = _specialDesc.Text.Trim();
            options.Abilities = new SlotAbilities();
            options.Abilities.InfAmmoAll = _cbInfAmmoAll.Checked;
            options.Abilities.SteadyAim = _cbSteadyAim.Checked;
            options.Abilities.InfSuppressor = _cbInfSuppressor.Checked;
            options.Abilities.SilentSteps = _cbSilentSteps.Checked;
            options.Abilities.InfAmmoWeapons = SelectedAmmoTokens();

            // Slot 5 discards a name longer than 15 characters rather than truncating it, so the row
            // would silently read "ACF Mod 5" in game.
            if (Pipeline.HasNameLimit(options.Slot) &&
                options.DisplayName.Length > Pipeline.Slot65NameLimit)
            {
                if (!Confirm("Name too long for slot 5",
                        "\"" + options.DisplayName + "\" is " + options.DisplayName.Length +
                        " characters. Slot 5 holds " + Pipeline.Slot65NameLimit + ".\r\n\r\n" +
                        "ACF will IGNORE the name rather than shorten it, and the row will read " +
                        "\"ACF Mod 5\" in game.\r\n\r\nBuild it anyway?"))
                    return;
            }
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
