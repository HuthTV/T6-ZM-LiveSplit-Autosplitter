using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Forms;
using System.Xml;
using static LiveSplit.T6ZM.PageParts;

namespace LiveSplit.T6ZM
{
    internal sealed class RoundsPage : ModePage
    {
        const int LastRound = 255;
        const int PageSize = 100;
        static readonly string[] ModeNames = { "Round end", "Text fade", "Round start" };

        readonly NumericUpDown maxRound = new NumericUpDown { Minimum = 2, Maximum = LastRound, Value = 100, Width = 60 };
        readonly CheckBox unlimited = new CheckBox { Text = "Unlimited", AutoSize = true };
        readonly ComboBox lastMode = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Width = 95 };
        readonly ComboBox splitMode = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Width = 95 };
        readonly CheckedListBox splitRounds = new CheckedListBox { CheckOnClick = true, MultiColumn = true, ColumnWidth = 38, Width = 386, IntegralHeight = false, Margin = Padding.Empty };
        readonly Button previousPage = new Button { Text = "<", Width = 24, Margin = Padding.Empty };
        readonly Button nextPage = new Button { Text = ">", Width = 24, Margin = Padding.Empty };
        int page;
        readonly Label timingsHeldNote = new Label { Text = "Timings locked between round end and round start", AutoSize = true, ForeColor = System.Drawing.Color.Firebrick, Anchor = AnchorStyles.Left, Visible = false };

        readonly HashSet<int> selected = new HashSet<int>();
        bool filling, reading;

        public RoundsPage() : base(columns: 2)
        {
            lastMode.Items.AddRange(ModeNames);
            splitMode.Items.AddRange(ModeNames);
            lastMode.SelectedIndex = (int)RoundMode.FadeDone - 1;
            splitMode.SelectedIndex = (int)RoundMode.FadeDone - 1;

            Controls.Add(Caption("Max round:"));
            Controls.Add(Row(maxRound, unlimited));
            Wide(Row(Caption("Split timing:"), splitMode, Caption("Last split timing:"), lastMode));
            Wide(timingsHeldNote);
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

            foreach (Control control in new Control[] { maxRound, unlimited, lastMode, splitMode })
                Hook(control);
            splitRounds.ItemCheck += OnItemCheck;
            maxRound.ValueChanged += (s, e) => { if (!reading) OnLiveSettingChanged(); };
            unlimited.CheckedChanged += (s, e) => { if (!reading) OnLiveSettingChanged(); };
            splitMode.SelectedIndexChanged += (s, e) => { if (!reading) OnLiveSettingChanged(); };
            lastMode.SelectedIndexChanged += (s, e) => { if (!reading) OnLiveSettingChanged(); };
            FillRounds();
        }

        public override LiveSettings LiveValues => new RoundSettings(SplitRounds, MaxRound, Unlimited, SplitTiming, LastTiming);

        public IEnumerable<int> SplitRounds => selected.ToArray();

        public int MaxRound => (int)maxRound.Value;
        public bool Unlimited => unlimited.Checked;
        public RoundMode SplitTiming => (RoundMode)(splitMode.SelectedIndex + 1);
        public RoundMode LastTiming => (RoundMode)(lastMode.SelectedIndex + 1);

        int Limit => unlimited.Checked ? LastRound : (int)maxRound.Value;

        public RunLogic CreateLogic() =>
            new RoundLogic(selected, (int)maxRound.Value, unlimited.Checked, (RoundMode)(splitMode.SelectedIndex + 1), (RoundMode)(lastMode.SelectedIndex + 1));

        public override void UpdateView() => FillRounds();

        public override void UpdateEnabled(bool locked, bool timingsHeld)
        {
            maxRound.Enabled = !unlimited.Checked;
            lastMode.Enabled = !unlimited.Checked && !timingsHeld;
            splitMode.Enabled = !timingsHeld;
            timingsHeldNote.Visible = timingsHeld;
        }

        int[] Every(int step) => Enumerable.Range(2, LastRound - 1).Where(r => r % step == 0 || (!unlimited.Checked && r == MaxRound)).ToArray();

        void SetRounds(IEnumerable<int> rounds)
        {
            selected.Clear();
            selected.UnionWith(rounds);
            OnLiveSettingChanged();
            FillRounds();
            OnSettingChanged();
        }

        void FillRounds()
        {
            int last = Limit;
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
            OnLiveSettingChanged();
            OnSettingChanged();
        }

        void Wide(Control control)
        {
            Controls.Add(control);
            SetColumnSpan(control, 2);
        }

        static Button Preset(string text, Action apply)
        {
            var button = new Button { Text = text, AutoSize = true };
            button.Click += (s, e) => apply();
            return button;
        }

        public override void WriteXml(XmlDocument document, XmlElement node)
        {
            node.AppendChild(Element(document, "MaxRound", maxRound.Value.ToString()));
            node.AppendChild(Element(document, "Unlimited", unlimited.Checked.ToString()));
            node.AppendChild(Element(document, "SplitTiming", (splitMode.SelectedIndex + 1).ToString()));
            node.AppendChild(Element(document, "LastSplitTiming", (lastMode.SelectedIndex + 1).ToString()));
            node.AppendChild(Element(document, "SplitRounds", string.Join(",", selected.OrderBy(r => r))));
        }

        public override void ReadXml(XmlNode node)
        {
            reading = true;
            try
            {
                maxRound.Value = Math.Max(2, Math.Min(LastRound, ReadInt(node, "MaxRound", 100)));
                unlimited.Checked = ReadBool(node, "Unlimited", false);
                splitMode.SelectedIndex = Math.Max(1, Math.Min(3, ReadInt(node, "SplitTiming", 2))) - 1;
                lastMode.SelectedIndex = Math.Max(1, Math.Min(3, ReadInt(node, "LastSplitTiming", 2))) - 1;
            }
            finally
            {
                reading = false;
            }
            string rounds = node?["SplitRounds"]?.InnerText;
            if (rounds != null)
            {
                selected.Clear();
                selected.UnionWith(rounds.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries).Select(s => int.TryParse(s, out int r) ? r : 0).Where(r => r >= 2 && r <= LastRound));
            }
        }
    }
}
