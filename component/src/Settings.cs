using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Forms;
using System.Xml;

namespace LiveSplit.T6ZM
{
    internal enum Category { Rounds, EasterEgg, SuperEasterEgg, Songs, Rainbow, Buildables, PackAPunch }

    internal sealed class GroupedList : CheckedListBox
    {
        const int Indent = 16;

        public Func<int, bool> IsHeader { get; set; } = _ => false;
        public bool Grouped { get; set; }
        public bool BoldHeaders { get; set; } = true;

        protected override void OnDrawItem(System.Windows.Forms.DrawItemEventArgs e)
        {
            if (e.Index < 0 || e.Index >= Items.Count) return;
            bool header = IsHeader(e.Index);
            bool selected = !header && (e.State & DrawItemState.Selected) != 0;
            System.Drawing.Color back = selected ? System.Drawing.SystemColors.Highlight : BackColor;
            System.Drawing.Color fore = selected ? System.Drawing.SystemColors.HighlightText : ForeColor;
            using (var brush = new System.Drawing.SolidBrush(back)) e.Graphics.FillRectangle(brush, e.Bounds);
            int x = e.Bounds.X + 2;
            if (header)
            {
                using (var bold = new System.Drawing.Font(Font, BoldHeaders ? System.Drawing.FontStyle.Bold : System.Drawing.FontStyle.Regular))
                    TextRenderer.DrawText(e.Graphics, Items[e.Index].ToString(), bold, new System.Drawing.Point(x, e.Bounds.Y + 1), fore);
                return;
            }
            if (Grouped) x += Indent;
            var state = GetItemChecked(e.Index) ? System.Windows.Forms.VisualStyles.CheckBoxState.CheckedNormal : System.Windows.Forms.VisualStyles.CheckBoxState.UncheckedNormal;
            CheckBoxRenderer.DrawCheckBox(e.Graphics, new System.Drawing.Point(x, e.Bounds.Y + Math.Max(0, (e.Bounds.Height - 13) / 2)), state);
            TextRenderer.DrawText(e.Graphics, Items[e.Index].ToString(), Font, new System.Drawing.Point(x + 18, e.Bounds.Y + 1), fore);
        }
    }

    internal sealed class Settings : UserControl
    {
        const int LastRound = 255;
        const int PageSize = 100;
        static readonly string[] ModeNames = { "Round end", "Text fade", "Round start" };

        readonly ComboBox category = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Width = 220 };
        readonly NumericUpDown maxRound = new NumericUpDown { Minimum = 2, Maximum = LastRound, Value = 100, Width = 60 };
        readonly CheckBox unlimited = new CheckBox { Text = "Unlimited", AutoSize = true };
        readonly ComboBox lastMode = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Width = 95 };
        readonly ComboBox splitMode = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Width = 95 };
        readonly CheckedListBox splitRounds = new CheckedListBox { CheckOnClick = true, MultiColumn = true, ColumnWidth = 38, Width = 386, IntegralHeight = false, Margin = Padding.Empty };
        readonly Button previousPage = new Button { Text = "<", Width = 24, Margin = Padding.Empty };
        readonly Button nextPage = new Button { Text = ">", Width = 24, Margin = Padding.Empty };
        int page;
        readonly CheckBox showInfo = new CheckBox { Text = "Show info display", Checked = true, AutoSize = true };
        readonly Label blocked = new Label { Text = "Blocked: timer running", AutoSize = true, ForeColor = System.Drawing.Color.Firebrick, Anchor = AnchorStyles.Left, Visible = false };
        readonly Label description = new Label { AutoSize = true, MaximumSize = new System.Drawing.Size(424, 0), ForeColor = System.Drawing.SystemColors.ControlText, Font = new System.Drawing.Font(Control.DefaultFont.FontFamily, 9f), Margin = Padding.Empty };
        readonly Panel descriptionBox = new Panel { AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink, BackColor = System.Drawing.SystemColors.ControlLight, BorderStyle = BorderStyle.FixedSingle, Padding = new Padding(6), Margin = new Padding(3, 2, 3, 8), MinimumSize = new System.Drawing.Size(436, 0) };

        static readonly string[] Descriptions =
        {
            "Split on selected rounds.",
            "Split on each selected step. The final split is always selected.",
            "The timer continues through TranZit, Die Rise and Buried. In-game time is always paused between maps.",
            "Splits fire in any order.",
            "Solo only. The final split is when all perk slots are full.",
            "Splits can be done in any order.",
            "Splits once a player grabs an upgraded weapon from Pack-a-Punch machine.",
        };
        readonly ComboBox buildMap = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Width = 220 };
        readonly CheckedListBox buildChecks = new CheckedListBox { CheckOnClick = true, IntegralHeight = false, Width = 386, Margin = Padding.Empty };
        readonly Label buildNote = new Label { AutoSize = true, MaximumSize = new System.Drawing.Size(386, 0), ForeColor = System.Drawing.SystemColors.GrayText, Margin = new Padding(3, 4, 3, 0) };
        readonly TableLayoutPanel buildPanel = new TableLayoutPanel { ColumnCount = 1, AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink };
        readonly Dictionary<string, uint> buildChosen = new Dictionary<string, uint>();
        readonly ComboBox rainbowMap = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Width = 220 };
        readonly CheckedListBox rainbowChecks = new CheckedListBox { CheckOnClick = true, IntegralHeight = false, Width = 386, Margin = Padding.Empty };
        readonly TableLayoutPanel rainbowPanel = new TableLayoutPanel { ColumnCount = 1, AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink };
        readonly Dictionary<string, uint> rainbowChosen = new Dictionary<string, uint>();
        readonly ComboBox songMap = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Width = 300 };
        readonly CheckedListBox songChecks = new CheckedListBox { CheckOnClick = true, IntegralHeight = false, Width = 386, Margin = Padding.Empty };
        readonly Label songNote = new Label { AutoSize = true, MaximumSize = new System.Drawing.Size(386, 0), ForeColor = System.Drawing.SystemColors.GrayText, Margin = new Padding(3, 4, 3, 0) };
        readonly TableLayoutPanel songPanel = new TableLayoutPanel { ColumnCount = 1, AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink };
        readonly Dictionary<string, uint> songChosen = new Dictionary<string, uint>();
        readonly Label eeMapCaption = Caption("Map:");
        readonly ComboBox eeMap = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Width = 150 };
        readonly ComboBox eePlayers = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Width = 75 };
        readonly CheckBox pauseRta = new CheckBox { Text = "Pause the real-time clock between maps", AutoSize = true, Margin = new Padding(3, 6, 3, 0) };
        readonly List<(EeMap map, int step)> eeRows = new List<(EeMap map, int step)>();
        string eeShown;
        readonly GroupedList eeChecks = new GroupedList { CheckOnClick = true, IntegralHeight = false, Width = 386, Margin = Padding.Empty };
        readonly TableLayoutPanel eePanel = new TableLayoutPanel { ColumnCount = 1, AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink };
        readonly Dictionary<string, uint> eeChosen = new Dictionary<string, uint>();
        readonly TableLayoutPanel roundsPanel = new TableLayoutPanel { ColumnCount = 2, AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink };

        readonly HashSet<int> selected = new HashSet<int>(Every(1));
        bool loading, filling, locked, onMapPage;

        public Settings()
        {
            category.Items.AddRange(new object[] { "Round speedrun", "Easter egg", "Super Easter egg", "Songs", "Rainbow Perk (solo)", "Buildables", "Pack-a-Punch" });
            category.Width = category.Items.Cast<object>().Max(item => TextRenderer.MeasureText(item.ToString(), category.Font).Width) + SystemInformation.VerticalScrollBarWidth + 12;
            buildMap.Items.AddRange(BuildableMaps.All.Select(m => (object)m.Name).ToArray());
            buildMap.SelectedIndex = 0;
            buildPanel.Controls.Add(Row(Caption("Map:"), buildMap));
            buildPanel.Controls.Add(buildChecks);
            buildPanel.Controls.Add(buildNote);
            buildChecks.Height = buildChecks.ItemHeight * 11 + (buildChecks.Height - buildChecks.ClientSize.Height);
            rainbowMap.Items.AddRange(RainbowMaps.All.Select(m => (object)m.Name).ToArray());
            rainbowMap.SelectedIndex = 0;
            rainbowPanel.Controls.Add(Row(Caption("Map:"), rainbowMap));
            rainbowPanel.Controls.Add(rainbowChecks);
            rainbowChecks.Height = rainbowChecks.ItemHeight * 8 + (rainbowChecks.Height - rainbowChecks.ClientSize.Height);
            songMap.Items.AddRange(SongMaps.All.Select(m => (object)m.Name).ToArray());
            songMap.SelectedIndex = 0;
            songPanel.Controls.Add(Row(Caption("Song:"), songMap));
            songPanel.Controls.Add(songChecks);
            songPanel.Controls.Add(songNote);
            songChecks.Height = songChecks.ItemHeight * 9 + (songChecks.Height - songChecks.ClientSize.Height);
            eeMap.Items.AddRange(EeMaps.All.Select(m => (object)m.Name).ToArray());
            eeMap.SelectedIndex = 0;
            eeChecks.IsHeader = i => i < eeRows.Count && eeRows[i].step < 0;
            eePlayers.Items.AddRange(new object[] { "Solo", "Co-op" });
            eePlayers.SelectedIndex = 0;
            eePanel.Controls.Add(Row(eeMapCaption, eeMap, Caption("Mode:"), eePlayers));
            eePanel.Controls.Add(eeChecks);
            eePanel.Controls.Add(pauseRta);
            category.SelectedIndex = 0;
            lastMode.Items.AddRange(ModeNames);
            splitMode.Items.AddRange(ModeNames);
            lastMode.SelectedIndex = (int)RoundMode.FadeDone - 1;
            splitMode.SelectedIndex = (int)RoundMode.FadeDone - 1;

            roundsPanel.Controls.Add(Caption("Max round:"));
            roundsPanel.Controls.Add(Row(maxRound, unlimited));
            Wide(Row(Caption("Split timing:"), splitMode, Caption("Last split timing:"), lastMode));
            Wide(new Label { Text = "Split at rounds:", AutoSize = true, Margin = new Padding(3, 8, 3, 0) });
            Wide(Row(
                Preset("Every 5", () => SetRounds(Every(5))),
                Preset("Every 10", () => SetRounds(Every(10))),
                Preset("Every round", () => SetRounds(Every(1))),
                Preset("None", () => SetRounds(new int[0]))));
            splitRounds.Height = splitRounds.ItemHeight * 10 + (splitRounds.Height - splitRounds.ClientSize.Height);
            previousPage.Height = nextPage.Height = splitRounds.Height;
            previousPage.Click += (s, e) => { page--; FillRounds(); };
            nextPage.Click += (s, e) => { page++; FillRounds(); };
            Wide(Row(previousPage, splitRounds, nextPage));

            var layout = new FlowLayoutPanel { FlowDirection = FlowDirection.TopDown, AutoSize = true, WrapContents = false };
            Label categoryCaption = Caption("Category:");
            categoryCaption.Font = new System.Drawing.Font(Control.DefaultFont.FontFamily, 9.5f, System.Drawing.FontStyle.Bold);
            layout.Controls.Add(Row(categoryCaption, category, blocked));
            descriptionBox.Controls.Add(description);
            layout.Controls.Add(descriptionBox);
            layout.Controls.Add(roundsPanel);
            layout.Controls.Add(buildPanel);
            layout.Controls.Add(rainbowPanel);
            layout.Controls.Add(songPanel);
            layout.Controls.Add(eePanel);
            layout.Controls.Add(new Label { BorderStyle = BorderStyle.Fixed3D, Height = 2, Width = 436, Margin = new Padding(3, 8, 3, 8) });
            layout.Controls.Add(showInfo);
            Controls.Add(layout);
            AutoSize = true;

            foreach (Control control in new Control[] { category, maxRound, unlimited, lastMode, splitMode, showInfo, buildMap, rainbowMap, songMap, eeMap, eePlayers, pauseRta })
                Hook(control);
            eeMap.SelectedIndexChanged += (s, e) => FillEe();
            eePlayers.SelectedIndexChanged += (s, e) => FillEe();
            eeChecks.ItemCheck += OnEeCheck;
            songMap.SelectedIndexChanged += (s, e) => FillSongs();
            songChecks.ItemCheck += OnSongCheck;
            buildMap.SelectedIndexChanged += (s, e) => FillBuildables();
            buildChecks.ItemCheck += OnBuildCheck;
            rainbowMap.SelectedIndexChanged += (s, e) => FillRainbow();
            rainbowChecks.ItemCheck += OnRainbowCheck;
            FillBuildables();
            FillRainbow();
            FillSongs();
            FillEe();
            splitRounds.ItemCheck += OnItemCheck;
            FillRounds();
            Changed();
        }

        public int Version { get; private set; }

        public int RoundsVersion { get; private set; }

        public IEnumerable<int> SplitRounds => selected.ToArray();

        public bool Locked
        {
            get => locked;
            set
            {
                if (locked == value) return;
                locked = value;
                UpdateEnabled();
            }
        }

        public bool ShowInfo => showInfo.Checked;
        public Category Category => (Category)Math.Max(0, category.SelectedIndex);

        int Limit => unlimited.Checked ? LastRound : (int)maxRound.Value;

        public RunLogic CreateLogic()
        {
            if (Category == Category.Songs) return new SongsLogic(SelectedSongMap, SongMask);
            if (Category == Category.EasterEgg || Category == Category.SuperEasterEgg)
            {
                bool super = Category == Category.SuperEasterEgg;
                bool pause = pauseRta.Checked;
                return new EasterEggLogic(super, () => eePlayers.SelectedIndex == 1, (part, coop) => EeMask(part, super, coop), () => pause);
            }
            if (Category == Category.PackAPunch) return new PackAPunchLogic();
            if (Category == Category.Buildables) return new BuildablesLogic(BuildableMask);
            if (Category == Category.Rainbow) return new RainbowLogic(RainbowMask);
            return new RoundLogic(selected, (int)maxRound.Value, unlimited.Checked, (RoundMode)(splitMode.SelectedIndex + 1), (RoundMode)(lastMode.SelectedIndex + 1));
        }

        public uint BuildableMask(string script)
        {
            BuildableMap map = BuildableMaps.For(script);
            if (map == null) return 0;
            return buildChosen.TryGetValue(script, out uint mask) ? mask & map.Every : map.Required;
        }

        public uint RainbowMask(string script)
        {
            RainbowMap map = RainbowMaps.For(script);
            if (map == null) return 0;
            return rainbowChosen.TryGetValue(script, out uint mask) ? mask & map.Every : map.Every;
        }

        public uint SongMask(string key)
        {
            SongMap map = SongMaps.ByKey(key);
            if (map == null) return 0;
            return (songChosen.TryGetValue(key, out uint mask) ? mask & map.Every : map.Default) | map.Forced;
        }

        bool SuperPage => Category == Category.SuperEasterEgg;
        bool EeCoop => eePlayers.SelectedIndex == 1;
        EeMap SelectedEeMap => EeMaps.All[Math.Max(0, Math.Min(eeMap.SelectedIndex, EeMaps.All.Length - 1))];

        static string EeKey(bool super, EeMap part, bool coop) => (super ? "super_" : "") + part.Key + (coop ? "_coop" : "_solo");

        public uint EeMask(EeMap part, bool super, bool coop)
        {
            if (part == null) return 0;
            uint forced = part.ForcedMask(coop, super);
            return (eeChosen.TryGetValue(EeKey(super, part, coop), out uint mask) ? mask & part.Every(coop) : part.Every(coop)) | forced;
        }

        void FillEe()
        {
            if (filling) return;
            bool coop = EeCoop;
            string shown = $"{SuperPage}|{coop}|{eeMap.SelectedIndex}";
            if (shown == eeShown && eeChecks.Items.Count > 0) return;
            eeShown = shown;
            eeRows.Clear();
            filling = true;
            eeChecks.BeginUpdate();
            eeChecks.Items.Clear();
            bool columns = !SuperPage && SelectedEeMap.Branches != null;
            eeChecks.Grouped = SuperPage || columns;
            eeChecks.MultiColumn = columns;
            eeChecks.BoldHeaders = SuperPage;
            eeChecks.ColumnWidth = 190;
            foreach (EeMap map in SuperPage ? EeMaps.Super : new[] { SelectedEeMap })
            {
                foreach (EeMap part in map.Parts)
                {
                    if (part.Branch != null || SuperPage)
                    {
                        string title = part.Branch == null ? map.Name : (SuperPage ? map.Name + ": " : "") + part.Branch;
                        eeRows.Add((part, -1));
                        eeChecks.Items.Add(title, CheckState.Indeterminate);
                    }
                    uint mask = EeMask(part, SuperPage, coop);
                    IReadOnlyList<EeStep> steps = part.Steps(coop);
                    for (int i = 0; i < steps.Count; i++)
                    {
                        eeRows.Add((part, i));
                        eeChecks.Items.Add(steps[i].Label, (mask & (1u << i)) != 0);
                    }
                }
            }
            int visibleRows = columns ? 9 : Math.Max(eeRows.Count, SuperPage ? Math.Max(SuperRows(false), SuperRows(true)) : 9);
            eeChecks.Height = eeChecks.ItemHeight * visibleRows + (eeChecks.Height - eeChecks.ClientSize.Height);
            eeChecks.EndUpdate();
            filling = false;
        }

        static int SuperRows(bool coop) =>
            EeMaps.Super.Sum(map => map.Parts.Sum(part => 1 + part.Steps(coop).Count));

        void OnEeCheck(object sender, ItemCheckEventArgs e)
        {
            if (filling || e.Index >= eeRows.Count) return;
            (EeMap map, int step) = eeRows[e.Index];
            if (step < 0)
            {
                e.NewValue = CheckState.Indeterminate;
                return;
            }
            bool coop = EeCoop;
            uint bit = 1u << step;
            if ((map.ForcedMask(coop, SuperPage) & bit) != 0)
            {
                e.NewValue = CheckState.Checked;
                return;
            }
            uint mask = EeMask(map, SuperPage, coop);
            eeChosen[EeKey(SuperPage, map, coop)] = e.NewValue == CheckState.Checked ? mask | bit : mask & ~bit;
            Changed();
        }

        SongMap SelectedSongMap => SongMaps.All[Math.Max(0, songMap.SelectedIndex)];

        void FillSongs()
        {
            SongMap map = SelectedSongMap;
            uint mask = SongMask(map.Key);
            songNote.Text = map.Note ?? "";
            songNote.Visible = !string.IsNullOrEmpty(map.Note);
            filling = true;
            songChecks.BeginUpdate();
            songChecks.Items.Clear();
            for (int i = 0; i < map.Steps.Count; i++) songChecks.Items.Add(map.Steps[i].Label, (mask & (1u << i)) != 0);
            songChecks.EndUpdate();
            filling = false;
        }

        void OnSongCheck(object sender, ItemCheckEventArgs e)
        {
            if (filling) return;
            SongMap map = SelectedSongMap;
            uint bit = 1u << e.Index;
            if ((map.Forced & bit) != 0)
            {
                e.NewValue = CheckState.Checked;
                return;
            }
            uint mask = SongMask(map.Key);
            songChosen[map.Key] = e.NewValue == CheckState.Checked ? mask | bit : mask & ~bit;
            Changed();
        }

        RainbowMap SelectedRainbowMap => RainbowMaps.All[Math.Max(0, rainbowMap.SelectedIndex)];

        void FillRainbow()
        {
            RainbowMap map = SelectedRainbowMap;
            uint mask = RainbowMask(map.Script);
            filling = true;
            rainbowChecks.BeginUpdate();
            rainbowChecks.Items.Clear();
            for (int i = 0; i < map.Steps.Count; i++) rainbowChecks.Items.Add(map.Steps[i].Label, (mask & (1u << i)) != 0);
            rainbowChecks.EndUpdate();
            filling = false;
        }

        void OnRainbowCheck(object sender, ItemCheckEventArgs e)
        {
            if (filling) return;
            RainbowMap map = SelectedRainbowMap;
            uint bit = 1u << e.Index;
            uint mask = RainbowMask(map.Script);
            rainbowChosen[map.Script] = e.NewValue == CheckState.Checked ? mask | bit : mask & ~bit;
            Changed();
        }

        BuildableMap SelectedBuildMap => BuildableMaps.All[Math.Max(0, buildMap.SelectedIndex)];

        void FillBuildables()
        {
            BuildableMap map = SelectedBuildMap;
            uint mask = BuildableMask(map.Script);
            buildNote.Text = map.Note ?? "";
            buildNote.Visible = !string.IsNullOrEmpty(map.Note);
            filling = true;
            buildChecks.BeginUpdate();
            buildChecks.Items.Clear();
            for (int i = 0; i < map.Items.Count; i++) buildChecks.Items.Add(map.Items[i].Label, (mask & (1u << i)) != 0);
            buildChecks.EndUpdate();
            filling = false;
        }

        void OnBuildCheck(object sender, ItemCheckEventArgs e)
        {
            if (filling) return;
            BuildableMap map = SelectedBuildMap;
            uint bit = 1u << e.Index;
            uint mask = BuildableMask(map.Script);
            buildChosen[map.Script] = e.NewValue == CheckState.Checked ? mask | bit : mask & ~bit;
            Changed();
        }

        static int[] Every(int step) => Enumerable.Range(2, LastRound - 1).Where(r => r % step == 0).ToArray();

        void SetRounds(IEnumerable<int> rounds)
        {
            selected.Clear();
            selected.UnionWith(rounds);
            RoundsVersion++;
            FillRounds();
            Changed();
        }

        void FillRounds()
        {
            int last = unlimited.Checked ? LastRound : Limit - 1;
            int pages = Math.Max(1, (last + PageSize - 1) / PageSize);
            page = Math.Max(0, Math.Min(page, pages - 1));
            int first = page * PageSize + 1;
            int count = Math.Max(0, Math.Min(last, first + PageSize - 1) - first + 1);
            previousPage.Enabled = page > 0;
            nextPage.Enabled = page < pages - 1;
            if (splitRounds.Items.Count == count && count > 0 && (int)splitRounds.Items[0] == first
                && Enumerable.Range(0, count).All(i => first + i == 1 || splitRounds.GetItemChecked(i) == selected.Contains(first + i)))
                return;
            filling = true;
            splitRounds.BeginUpdate();
            splitRounds.Items.Clear();
            for (int round = first; round < first + count; round++)
                splitRounds.Items.Add(round, round == 1 ? CheckState.Indeterminate : selected.Contains(round) ? CheckState.Checked : CheckState.Unchecked);
            splitRounds.EndUpdate();
            filling = false;
        }

        void OnItemCheck(object sender, ItemCheckEventArgs e)
        {
            if (filling) return;
            int round = (int)splitRounds.Items[e.Index];
            if (round == 1)
            {
                e.NewValue = CheckState.Indeterminate;
                return;
            }
            if (e.NewValue == CheckState.Checked) selected.Add(round);
            else selected.Remove(round);
            RoundsVersion++;
            Changed();
        }

        void Changed()
        {
            bool mapPage = Category == Category.Buildables || Category == Category.Rainbow || Category == Category.Songs || Category == Category.EasterEgg || Category == Category.SuperEasterEgg;
            bool opened = mapPage && !onMapPage;
            onMapPage = mapPage;
            roundsPanel.Visible = Category == Category.Rounds;
            buildPanel.Visible = Category == Category.Buildables;
            rainbowPanel.Visible = Category == Category.Rainbow;
            songPanel.Visible = Category == Category.Songs;
            eePanel.Visible = Category == Category.EasterEgg || Category == Category.SuperEasterEgg;
            eeMapCaption.Visible = eeMap.Visible = !SuperPage;
            pauseRta.Visible = SuperPage;
            if (eePanel.Visible) FillEe();
            description.Text = Descriptions[(int)Category];
            UpdateEnabled();
            FillRounds();
            if (!loading) Version++;
            if (opened) SelectGameMap();
        }

        public string GameMap { get; set; }

        void SelectGameMap()
        {
            if (loading) return;
            int index = Array.FindIndex(BuildableMaps.All, m => m.Script == GameMap);
            if (index >= 0 && buildMap.SelectedIndex != index) buildMap.SelectedIndex = index;
            index = Array.FindIndex(RainbowMaps.All, m => m.Script == GameMap);
            if (index >= 0 && rainbowMap.SelectedIndex != index) rainbowMap.SelectedIndex = index;
            index = Array.FindIndex(SongMaps.All, m => m.Script == GameMap);
            if (index >= 0 && SelectedSongMap.Script != GameMap) songMap.SelectedIndex = index;
            index = Array.FindIndex(EeMaps.All, m => m.Script == GameMap);
            if (index >= 0 && eeMap.SelectedIndex != index) eeMap.SelectedIndex = index;
        }

        protected override void OnVisibleChanged(EventArgs e)
        {
            base.OnVisibleChanged(e);
            if (Visible && (Category == Category.Buildables || Category == Category.Rainbow || Category == Category.Songs || Category == Category.EasterEgg || Category == Category.SuperEasterEgg)) SelectGameMap();
        }

        void UpdateEnabled()
        {
            category.Enabled = unlimited.Enabled = splitMode.Enabled = !locked;
            blocked.Visible = locked;
            maxRound.Enabled = lastMode.Enabled = !locked && !unlimited.Checked;
        }

        void Hook(Control control)
        {
            switch (control)
            {
                case ComboBox box: box.SelectedIndexChanged += (s, e) => Changed(); break;
                case NumericUpDown number: number.ValueChanged += (s, e) => Changed(); break;
                case CheckBox check: check.CheckedChanged += (s, e) => Changed(); break;
            }
        }

        void Wide(Control control)
        {
            roundsPanel.Controls.Add(control);
            roundsPanel.SetColumnSpan(control, 2);
        }

        static Label Caption(string text) => new Label { Text = text, AutoSize = true, Anchor = AnchorStyles.Left };

        static Button Preset(string text, Action apply)
        {
            var button = new Button { Text = text, AutoSize = true };
            button.Click += (s, e) => apply();
            return button;
        }

        static FlowLayoutPanel Row(params Control[] controls)
        {
            var row = new FlowLayoutPanel { AutoSize = true, WrapContents = false, Margin = Padding.Empty };
            row.Controls.AddRange(controls);
            return row;
        }

        public XmlNode WriteXml(XmlDocument document)
        {
            XmlElement node = document.CreateElement("Settings");
            node.AppendChild(Element(document, "Version", "3"));
            node.AppendChild(Element(document, "Category", Category.ToString()));
            node.AppendChild(Element(document, "MaxRound", maxRound.Value.ToString()));
            node.AppendChild(Element(document, "Unlimited", unlimited.Checked.ToString()));
            node.AppendChild(Element(document, "SplitTiming", (splitMode.SelectedIndex + 1).ToString()));
            node.AppendChild(Element(document, "LastSplitTiming", (lastMode.SelectedIndex + 1).ToString()));
            node.AppendChild(Element(document, "SplitRounds", string.Join(",", selected.OrderBy(r => r))));
            foreach (BuildableMap map in BuildableMaps.All)
            {
                uint mask = BuildableMask(map.Script);
                node.AppendChild(Element(document, "Built_" + map.Script, string.Join(",", map.Items.Where((item, i) => (mask & (1u << i)) != 0).Select(item => item.Key))));
            }
            foreach (RainbowMap map in RainbowMaps.All)
            {
                uint mask = RainbowMask(map.Script);
                node.AppendChild(Element(document, "Rainbow_" + map.Script, string.Join(",", map.Steps.Where((step, i) => (mask & (1u << i)) != 0).Select(step => step.Label))));
            }
            foreach (SongMap map in SongMaps.All)
            {
                uint mask = SongMask(map.Key);
                node.AppendChild(Element(document, "Songs_" + map.Key, string.Join("|", map.Steps.Where((step, i) => (mask & (1u << i)) != 0).Select(step => step.Label))));
            }
            foreach (bool super in new[] { false, true })
                foreach (EeMap map in super ? EeMaps.Super : EeMaps.All)
                    foreach (EeMap part in map.Parts)
                        foreach (bool coop in new[] { false, true })
                        {
                            uint mask = EeMask(part, super, coop);
                            IReadOnlyList<EeStep> steps = part.Steps(coop);
                            node.AppendChild(Element(document, "EE_" + EeKey(super, part, coop), string.Join(",", steps.Where((step, i) => (mask & (1u << i)) != 0).Select(step => step.Id))));
                        }
            node.AppendChild(Element(document, "EE_Players", eePlayers.SelectedIndex.ToString()));
            node.AppendChild(Element(document, "EE_PauseRta", pauseRta.Checked.ToString()));
            node.AppendChild(Element(document, "ShowInfo", showInfo.Checked.ToString()));
            return node;
        }

        public void ReadXml(XmlNode node)
        {
            loading = true;
            try
            {
                if (Enum.TryParse(node?["Category"]?.InnerText, out Category saved)) category.SelectedIndex = (int)saved;
                maxRound.Value = Math.Max(2, Math.Min(LastRound, ReadInt(node, "MaxRound", 100)));
                unlimited.Checked = ReadBool(node, "Unlimited", false);
                splitMode.SelectedIndex = Math.Max(1, Math.Min(3, ReadInt(node, "SplitTiming", 2))) - 1;
                lastMode.SelectedIndex = Math.Max(1, Math.Min(3, ReadInt(node, "LastSplitTiming", 2))) - 1;
                string rounds = node?["SplitRounds"]?.InnerText;
                if (rounds != null)
                {
                    selected.Clear();
                    selected.UnionWith(rounds.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries).Select(s => int.TryParse(s, out int r) ? r : 0).Where(r => r >= 2 && r <= LastRound));
                }
                foreach (BuildableMap map in BuildableMaps.All)
                {
                    string keys = node?["Built_" + map.Script]?.InnerText;
                    if (keys == null) continue;
                    var chosen = new HashSet<string>(keys.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries));
                    uint mask = 0;
                    for (int i = 0; i < map.Items.Count; i++)
                        if (chosen.Contains(map.Items[i].Key)) mask |= 1u << i;
                    buildChosen[map.Script] = mask;
                }
                foreach (RainbowMap map in RainbowMaps.All)
                {
                    string labels = node?["Rainbow_" + map.Script]?.InnerText?.Replace("Red dig ", "Bottle ");
                    if (labels == null) continue;
                    var chosen = new HashSet<string>(labels.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries));
                    uint mask = 0;
                    for (int i = 0; i < map.Steps.Count; i++)
                        if (chosen.Contains(map.Steps[i].Label)) mask |= 1u << i;
                    rainbowChosen[map.Script] = mask;
                }
                foreach (SongMap map in SongMaps.All)
                {
                    string labels = node?["Songs_" + map.Key]?.InnerText;
                    if (labels == null) continue;
                    var chosen = new HashSet<string>(labels.Split(new[] { '|' }, StringSplitOptions.RemoveEmptyEntries));
                    uint mask = 0;
                    for (int i = 0; i < map.Steps.Count; i++)
                        if (chosen.Contains(map.Steps[i].Label)) mask |= 1u << i;
                    songChosen[map.Key] = mask;
                }
                foreach (bool super in new[] { false, true })
                    foreach (EeMap map in super ? EeMaps.Super : EeMaps.All)
                        foreach (EeMap part in map.Parts)
                            foreach (bool coop in new[] { false, true })
                            {
                                string ids = node?["EE_" + EeKey(super, part, coop)]?.InnerText;
                                if (ids == null) continue;
                                var chosen = new HashSet<string>(ids.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries));
                                uint mask = 0;
                                IReadOnlyList<EeStep> steps = part.Steps(coop);
                                for (int i = 0; i < steps.Count; i++)
                                    if (chosen.Contains(steps[i].Id)) mask |= 1u << i;
                                eeChosen[EeKey(super, part, coop)] = mask;
                            }
                eePlayers.SelectedIndex = Math.Max(0, Math.Min(1, ReadInt(node, "EE_Players", 0)));
                pauseRta.Checked = ReadBool(node, "EE_PauseRta", false);
                eeShown = null;
                showInfo.Checked = ReadBool(node, "ShowInfo", true);
            }
            finally
            {
                loading = false;
            }
            FillBuildables();
            FillRainbow();
            FillSongs();
            FillEe();
            Changed();
        }

        static XmlElement Element(XmlDocument document, string name, string value)
        {
            XmlElement element = document.CreateElement(name);
            element.InnerText = value;
            return element;
        }

        static bool ReadBool(XmlNode node, string name, bool fallback) =>
            bool.TryParse(node?[name]?.InnerText, out bool value) ? value : fallback;

        static int ReadInt(XmlNode node, string name, int fallback) =>
            int.TryParse(node?[name]?.InnerText, out int value) ? value : fallback;
    }
}
