using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Forms;
using System.Xml;

namespace LiveSplit.T6ZM
{
    internal enum Category { Rounds, PackAPunch, Buildables, Rainbow, TestBox }

    internal sealed class Settings : UserControl
    {
        const int LastRound = 255;
        const int PageSize = 100;
        static readonly int[] SrcMilestones = { 5, 15, 30, 50, 70, 100 };
        static readonly string[] ModeNames = { "Round end", "Text fade", "Round start" };

        readonly ComboBox category = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Width = 220 };
        readonly NumericUpDown maxRound = new NumericUpDown { Minimum = 2, Maximum = LastRound, Value = 30, Width = 60 };
        readonly CheckBox unlimited = new CheckBox { Text = "Unlimited", AutoSize = true };
        readonly ComboBox lastMode = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Width = 140 };
        readonly ComboBox splitMode = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Width = 140 };
        readonly CheckedListBox splitRounds = new CheckedListBox { CheckOnClick = true, MultiColumn = true, ColumnWidth = 38, Width = 386, IntegralHeight = false, Margin = Padding.Empty };
        readonly Button previousPage = new Button { Text = "<", Width = 24, Margin = Padding.Empty };
        readonly Button nextPage = new Button { Text = ">", Width = 24, Margin = Padding.Empty };
        int page;
        readonly CheckBox showInfo = new CheckBox { Text = "Show info display", Checked = true, AutoSize = true };
        readonly Label blocked = new Label { Text = "Blocked: timer running", AutoSize = true, ForeColor = System.Drawing.Color.Firebrick, Anchor = AnchorStyles.Left, Visible = false };
        readonly Label description = new Label { AutoSize = true, MaximumSize = new System.Drawing.Size(436, 0), ForeColor = System.Drawing.SystemColors.GrayText, Margin = new Padding(3, 0, 3, 6) };

        static readonly string[] Descriptions =
        {
            "Starts on player control. Splits when each chosen round is reached; the split timings below decide the exact moment.",
            "Starts on player control. Splits once, when any player takes a Pack-a-Punched weapon from the machine.",
            "Starts on player control. Splits when each ticked buildable is built, in any order, so the splits file can be ordered as you like. The run ends when everything required on the map's list is built (Buried's sidequest items are optional), and that last one always splits.",
            "Solo only. Starts on player control. Splits the first time each ticked step is reached, in any order; the last step always splits and ends the run. Buried: every perk up to 7. Origins: the golden shovel, each perk bottle from a red dig (an extra perk slot), then 8 perks.",
            "Starts on player control. Splits on the first box use.",
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
        readonly TableLayoutPanel roundsPanel = new TableLayoutPanel { ColumnCount = 2, AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink };

        readonly HashSet<int> selected = new HashSet<int>(Every(1));
        bool loading, filling, locked, onMapPage;

        public Settings()
        {
            category.Items.AddRange(new object[] { "Round speedrun", "Pack-a-Punch", "Buildables", "Rainbow Perk (solo)", "Test: first box hit" });
            buildMap.Items.AddRange(BuildableMaps.All.Select(m => (object)m.Name).ToArray());
            buildMap.SelectedIndex = 0;
            buildPanel.Controls.Add(Row(Caption("Map:"), buildMap));
            buildPanel.Controls.Add(new Label { Text = "Split when built:", AutoSize = true, Margin = new Padding(3, 8, 3, 0) });
            buildPanel.Controls.Add(buildChecks);
            buildPanel.Controls.Add(buildNote);
            buildChecks.Height = buildChecks.ItemHeight * 11 + (buildChecks.Height - buildChecks.ClientSize.Height);
            rainbowMap.Items.AddRange(RainbowMaps.All.Select(m => (object)m.Name).ToArray());
            rainbowMap.SelectedIndex = 0;
            rainbowPanel.Controls.Add(Row(Caption("Map:"), rainbowMap));
            rainbowPanel.Controls.Add(new Label { Text = "Split when reached:", AutoSize = true, Margin = new Padding(3, 8, 3, 0) });
            rainbowPanel.Controls.Add(rainbowChecks);
            rainbowChecks.Height = rainbowChecks.ItemHeight * 8 + (rainbowChecks.Height - rainbowChecks.ClientSize.Height);
            category.SelectedIndex = 0;
            lastMode.Items.AddRange(ModeNames);
            splitMode.Items.AddRange(ModeNames);
            lastMode.SelectedIndex = (int)RoundMode.FadeDone - 1;
            splitMode.SelectedIndex = (int)RoundMode.FadeDone - 1;

            roundsPanel.Controls.Add(Caption("Max round:"));
            roundsPanel.Controls.Add(Row(maxRound, unlimited));
            roundsPanel.Controls.Add(Caption("Split timing:"));
            roundsPanel.Controls.Add(splitMode);
            roundsPanel.Controls.Add(Caption("Last split timing:"));
            roundsPanel.Controls.Add(lastMode);
            Wide(new Label { Text = "Split at rounds:", AutoSize = true, Margin = new Padding(3, 8, 3, 0) });
            Wide(Row(
                Preset("Speedrun.com", () => SetRounds(SrcMilestones)),
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
            layout.Controls.Add(Row(Caption("Category:"), category, blocked));
            layout.Controls.Add(description);
            layout.Controls.Add(roundsPanel);
            layout.Controls.Add(buildPanel);
            layout.Controls.Add(rainbowPanel);
            layout.Controls.Add(new Label { BorderStyle = BorderStyle.Fixed3D, Height = 2, Width = 436, Margin = new Padding(3, 8, 3, 8) });
            layout.Controls.Add(showInfo);
            Controls.Add(layout);
            AutoSize = true;

            foreach (Control control in new Control[] { category, maxRound, unlimited, lastMode, splitMode, showInfo, buildMap, rainbowMap })
                Hook(control);
            buildMap.SelectedIndexChanged += (s, e) => FillBuildables();
            buildChecks.ItemCheck += OnBuildCheck;
            rainbowMap.SelectedIndexChanged += (s, e) => FillRainbow();
            rainbowChecks.ItemCheck += OnRainbowCheck;
            FillBuildables();
            FillRainbow();
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
            if (Category == Category.TestBox) return new TestBoxLogic();
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
            bool mapPage = Category == Category.Buildables || Category == Category.Rainbow;
            bool opened = mapPage && !onMapPage;
            onMapPage = mapPage;
            roundsPanel.Visible = Category == Category.Rounds;
            buildPanel.Visible = Category == Category.Buildables;
            rainbowPanel.Visible = Category == Category.Rainbow;
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
        }

        protected override void OnVisibleChanged(EventArgs e)
        {
            base.OnVisibleChanged(e);
            if (Visible && (Category == Category.Buildables || Category == Category.Rainbow)) SelectGameMap();
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
            node.AppendChild(Element(document, "ShowInfo", showInfo.Checked.ToString()));
            return node;
        }

        public void ReadXml(XmlNode node)
        {
            loading = true;
            try
            {
                if (Enum.TryParse(node?["Category"]?.InnerText, out Category saved)) category.SelectedIndex = (int)saved;
                maxRound.Value = Math.Max(2, Math.Min(LastRound, ReadInt(node, "MaxRound", 30)));
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
                showInfo.Checked = ReadBool(node, "ShowInfo", true);
            }
            finally
            {
                loading = false;
            }
            FillBuildables();
            FillRainbow();
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
