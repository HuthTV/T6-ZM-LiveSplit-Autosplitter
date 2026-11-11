using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Forms;
using System.Xml;
using static LiveSplit.T6ZM.PageParts;

namespace LiveSplit.T6ZM
{
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

    internal sealed class EasterEggPage : ModePage
    {
        readonly Func<bool> super;
        readonly Label eeMapCaption = Caption("Map:");
        readonly ComboBox eeMap = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Width = 150 };
        readonly ComboBox eePlayers = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Width = 75 };
        readonly CheckBox pauseRta = new CheckBox { Text = "Pause the real-time clock between maps", AutoSize = true, Margin = new Padding(3, 6, 3, 0) };
        readonly List<(EeMap map, int step)> eeRows = new List<(EeMap map, int step)>();
        string eeShown;
        readonly GroupedList eeChecks = new GroupedList { CheckOnClick = true, IntegralHeight = false, Width = 386, Margin = Padding.Empty };
        readonly StepTicks ticks = new StepTicks(honourForced: true);
        bool filling;

        public EasterEggPage(Func<bool> super)
        {
            this.super = super;
            eeMap.Items.AddRange(EeMaps.All.Select(m => (object)m.Name).ToArray());
            eeMap.SelectedIndex = 0;
            eeChecks.IsHeader = i => i < eeRows.Count && eeRows[i].step < 0;
            eePlayers.Items.AddRange(new object[] { "Solo", "Co-op" });
            eePlayers.SelectedIndex = 0;
            Controls.Add(Row(eeMapCaption, eeMap, Caption("Mode:"), eePlayers));
            Controls.Add(eeChecks);
            Controls.Add(pauseRta);
            foreach (Control control in new Control[] { eeMap, eePlayers, pauseRta })
                Hook(control);
            eeMap.SelectedIndexChanged += (s, e) => FillEe();
            eePlayers.SelectedIndexChanged += (s, e) => FillEe();
            eeChecks.ItemCheck += OnEeCheck;
            FillEe();
        }

        public override bool ListsMaps => true;

        bool SuperPage => super();
        bool EeCoop => eePlayers.SelectedIndex == 1;
        EeMap SelectedEeMap => EeMaps.All[Math.Max(0, Math.Min(eeMap.SelectedIndex, EeMaps.All.Length - 1))];

        public RunLogic CreateLogic(bool super)
        {
            bool pause = pauseRta.Checked;
            return new EasterEggLogic(super, () => eePlayers.SelectedIndex == 1, (part, coop) => EeMask(part, super, coop), () => pause);
        }

        public uint EeMask(EeMap part, bool super, bool coop) => part == null ? 0 : ticks.Mask(part.List(coop, super));

        public override void UpdateView()
        {
            eeMapCaption.Visible = eeMap.Visible = !SuperPage;
            pauseRta.Visible = SuperPage;
            if (Visible) FillEe();
        }

        public override void UpdateEnabled(bool locked, bool timingsHeld) => eePlayers.Enabled = !locked;

        public override void SelectGameMap(string game, bool locked)
        {
            int index = Array.FindIndex(EeMaps.All, m => m.Script == game);
            if (index >= 0 && eeMap.SelectedIndex != index) eeMap.SelectedIndex = index;
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
                    IReadOnlyList<EeStep> steps = part.List(coop, SuperPage).Steps;
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
            EeMaps.Super.Sum(map => map.Parts.Sum(part => 1 + part.List(coop, true).Steps.Count));

        void OnEeCheck(object sender, ItemCheckEventArgs e)
        {
            if (filling || e.Index >= eeRows.Count) return;
            (EeMap map, int step) = eeRows[e.Index];
            if (step < 0)
            {
                e.NewValue = CheckState.Indeterminate;
                return;
            }
            StepList<EeStep> list = map.List(EeCoop, SuperPage);
            if ((list.Forced & (1u << step)) != 0)
            {
                e.NewValue = CheckState.Checked;
                return;
            }
            ticks.Set(list, step, e.NewValue == CheckState.Checked);
            OnSettingChanged();
        }

        static IEnumerable<StepList<EeStep>> SavedLists()
        {
            foreach (bool super in new[] { false, true })
                foreach (EeMap map in super ? EeMaps.Super : EeMaps.All)
                    foreach (EeMap part in map.Parts)
                        foreach (bool coop in new[] { false, true })
                            yield return part.List(coop, super);
        }

        public override void WriteXml(XmlDocument document, XmlElement node)
        {
            foreach (StepList<EeStep> list in SavedLists())
                node.AppendChild(Element(document, "EE_" + list.Key, ticks.Write(list, ',')));
            node.AppendChild(Element(document, "EE_Players", eePlayers.SelectedIndex.ToString()));
            node.AppendChild(Element(document, "EE_PauseRta", pauseRta.Checked.ToString()));
        }

        public override void ReadXml(XmlNode node)
        {
            foreach (StepList<EeStep> list in SavedLists())
                ticks.Read(list, node?["EE_" + list.Key]?.InnerText, ',');
            eePlayers.SelectedIndex = Math.Max(0, Math.Min(1, ReadInt(node, "EE_Players", 0)));
            pauseRta.Checked = ReadBool(node, "EE_PauseRta", false);
            eeShown = null;
        }

        public override void AfterLoad() => FillEe();
    }
}
