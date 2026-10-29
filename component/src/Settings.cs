using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Forms;
using System.Xml;

namespace LiveSplit.T6ZM
{
    internal enum Category { Rounds, PackAPunch, TestBox }

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
        readonly CheckBox autoReset = new CheckBox { Text = "Reset when a map starts (map_restart, fast_restart, map load)", Checked = true, AutoSize = true };
        readonly Label blocked = new Label { Text = "Blocked: timer running", AutoSize = true, ForeColor = System.Drawing.Color.Firebrick, Anchor = AnchorStyles.Left, Visible = false };
        readonly Label description = new Label { AutoSize = true, MaximumSize = new System.Drawing.Size(436, 0), ForeColor = System.Drawing.SystemColors.GrayText, Margin = new Padding(3, 0, 3, 6) };

        static readonly string[] Descriptions =
        {
            "Starts when the black screen ends. Splits when each chosen round is reached; the split timings below decide the exact moment.",
            "Starts when the black screen ends. Splits once, when any player takes a Pack-a-Punched weapon from the machine.",
            "Starts when the black screen ends. Splits on the first box use.",
        };
        readonly TableLayoutPanel roundsPanel = new TableLayoutPanel { ColumnCount = 2, AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink };

        readonly HashSet<int> selected = new HashSet<int>(Every(1));
        bool loading, filling, locked;

        public Settings()
        {
            category.Items.AddRange(new object[] { "Round speedrun", "Pack-a-Punch", "Test: first box hit" });
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
            layout.Controls.Add(new Label { BorderStyle = BorderStyle.Fixed3D, Height = 2, Width = 436, Margin = new Padding(3, 8, 3, 8) });
            layout.Controls.Add(showInfo);
            layout.Controls.Add(autoReset);
            Controls.Add(layout);
            AutoSize = true;

            foreach (Control control in new Control[] { category, maxRound, unlimited, lastMode, splitMode, showInfo, autoReset })
                Hook(control);
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
        public bool AutoReset => autoReset.Checked;
        public Category Category => (Category)Math.Max(0, category.SelectedIndex);

        int Limit => unlimited.Checked ? LastRound : (int)maxRound.Value;

        public RunLogic CreateLogic()
        {
            if (Category == Category.TestBox) return new TestBoxLogic();
            if (Category == Category.PackAPunch) return new PackAPunchLogic();
            return new RoundLogic(selected, (int)maxRound.Value, unlimited.Checked, (RoundMode)(splitMode.SelectedIndex + 1), (RoundMode)(lastMode.SelectedIndex + 1));
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
            roundsPanel.Visible = Category == Category.Rounds;
            description.Text = Descriptions[(int)Category];
            UpdateEnabled();
            FillRounds();
            if (!loading) Version++;
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
            node.AppendChild(Element(document, "ShowInfo", showInfo.Checked.ToString()));
            node.AppendChild(Element(document, "AutoReset", autoReset.Checked.ToString()));
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
                showInfo.Checked = ReadBool(node, "ShowInfo", true);
                autoReset.Checked = ReadBool(node, "AutoReset", true);
            }
            finally
            {
                loading = false;
            }
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
