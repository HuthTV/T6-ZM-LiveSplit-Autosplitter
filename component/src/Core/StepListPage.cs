using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Forms;
using System.Xml;
using static LiveSplit.T6ZM.PageParts;

namespace LiveSplit.T6ZM
{
    internal sealed class StepTicks
    {
        readonly Dictionary<string, uint> chosen = new Dictionary<string, uint>();
        readonly bool honourForced;

        public StepTicks(bool honourForced) => this.honourForced = honourForced;

        public uint Mask<TStep>(StepList<TStep> list) where TStep : Step =>
            (chosen.TryGetValue(list.Key, out uint mask) ? mask & list.Every : list.Default) | (honourForced ? list.Forced : 0);

        public void Set<TStep>(StepList<TStep> list, int step, bool ticked) where TStep : Step
        {
            uint mask = Mask(list);
            uint bit = 1u << step;
            chosen[list.Key] = ticked ? mask | bit : mask & ~bit;
        }

        public string Write<TStep>(StepList<TStep> list, char separator) where TStep : Step
        {
            uint mask = Mask(list);
            return string.Join(separator.ToString(), list.Steps.Where((step, i) => (mask & (1u << i)) != 0).Select(step => step.Id));
        }

        public void Read<TStep>(StepList<TStep> list, string ids, char separator) where TStep : Step
        {
            if (ids == null) return;
            var saved = new HashSet<string>(ids.Split(new[] { separator }, StringSplitOptions.RemoveEmptyEntries));
            uint mask = 0;
            for (int i = 0; i < list.Steps.Count; i++)
                if (saved.Contains(list.Steps[i].Id)) mask |= 1u << i;
            chosen[list.Key] = mask;
        }
    }

    internal abstract class StepListPage<TList, TStep> : ModePage where TList : StepList<TStep> where TStep : Step
    {
        readonly TList[] lists;
        readonly ComboBox map;
        readonly CheckedListBox checks = new CheckedListBox { CheckOnClick = true, IntegralHeight = false, Width = 386, Margin = Padding.Empty };
        readonly Label note;
        readonly bool honourForced;
        bool filling;

        protected readonly StepTicks Ticks;

        protected StepListPage(TList[] lists, string caption, int mapWidth, int rows, bool hasNote, bool honourForced)
        {
            this.lists = lists;
            this.honourForced = honourForced;
            Ticks = new StepTicks(honourForced);
            map = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Width = mapWidth };
            map.Items.AddRange(lists.Select(m => (object)m.Name).ToArray());
            map.SelectedIndex = 0;
            Controls.Add(Row(Caption(caption), map));
            Controls.Add(checks);
            if (hasNote)
            {
                note = new Label { AutoSize = true, MaximumSize = new System.Drawing.Size(386, 0), ForeColor = System.Drawing.SystemColors.GrayText, Margin = new Padding(3, 4, 3, 0) };
                Controls.Add(note);
            }
            checks.Height = checks.ItemHeight * rows + (checks.Height - checks.ClientSize.Height);
            Hook(map);
            map.SelectedIndexChanged += (s, e) => Fill();
            checks.ItemCheck += OnCheck;
            Fill();
        }

        public override bool ListsMaps => true;

        protected ComboBox MapBox => map;

        protected TList Selected => lists[Math.Max(0, map.SelectedIndex)];

        protected uint Mask(TList list) => list == null ? 0 : Ticks.Mask(list);

        public override void SelectGameMap(string game, bool locked)
        {
            int index = Array.FindIndex(lists, m => m.Script == game);
            if (index >= 0 && map.SelectedIndex != index) map.SelectedIndex = index;
        }

        public override void AfterLoad() => Fill();

        protected void WriteTicks(XmlDocument document, XmlElement node, string prefix, char separator)
        {
            foreach (TList list in lists)
                node.AppendChild(Element(document, prefix + list.Key, Ticks.Write(list, separator)));
        }

        protected void ReadTicks(XmlNode node, string prefix, char separator, Func<string, string> fix = null)
        {
            foreach (TList list in lists)
            {
                string ids = node?[prefix + list.Key]?.InnerText;
                if (ids != null && fix != null) ids = fix(ids);
                Ticks.Read(list, ids, separator);
            }
        }

        void Fill()
        {
            TList list = Selected;
            uint mask = Mask(list);
            if (note != null)
            {
                note.Text = list.Note ?? "";
                note.Visible = !string.IsNullOrEmpty(list.Note);
            }
            filling = true;
            checks.BeginUpdate();
            checks.Items.Clear();
            for (int i = 0; i < list.Steps.Count; i++) checks.Items.Add(list.Steps[i].Label, (mask & (1u << i)) != 0);
            checks.EndUpdate();
            filling = false;
        }

        void OnCheck(object sender, ItemCheckEventArgs e)
        {
            if (filling) return;
            TList list = Selected;
            if (honourForced && (list.Forced & (1u << e.Index)) != 0)
            {
                e.NewValue = CheckState.Checked;
                return;
            }
            Ticks.Set(list, e.Index, e.NewValue == CheckState.Checked);
            OnSettingChanged();
        }
    }
}
