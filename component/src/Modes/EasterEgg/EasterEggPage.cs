using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Forms;
using System.Xml;
using static LiveSplit.T6ZM.PageParts;

namespace LiveSplit.T6ZM
{
    internal sealed class StepGrid : Panel
    {
        const int ColumnWidth = 190;
        const int Indent = 16;
        const int DividerGap = 9;
        const int Pad = 3;
        int top;

        public StepGrid()
        {
            BackColor = System.Drawing.SystemColors.Window;
            BorderStyle = BorderStyle.Fixed3D;
            AutoScroll = true;
            Width = 386;
            Margin = Padding.Empty;
        }

        public int RowHeight => Math.Max(17, Font.Height + 4);

        public void Reset()
        {
            Control[] old = Controls.Cast<Control>().ToArray();
            Controls.Clear();
            foreach (Control control in old) control.Dispose();
            top = Pad;
        }

        public void Divider()
        {
            Controls.Add(new Label { BackColor = System.Drawing.SystemColors.ControlLight, Location = new System.Drawing.Point(Pad, top + DividerGap / 2), Size = new System.Drawing.Size(ClientSize.Width - 2 * Pad, 1) });
            top += DividerGap;
        }

        public void Place(Control control, int column, int row, bool indent)
        {
            control.Location = new System.Drawing.Point(Pad + column * ColumnWidth + (indent ? Indent : 0), top + row * RowHeight);
            Controls.Add(control);
        }

        public void EndSection(int rows) => top += rows * RowHeight;

        public int FitHeight(int rows, int dividers) => rows * RowHeight + dividers * DividerGap + 2 * Pad + (Height - ClientSize.Height);
    }

    internal sealed class EasterEggPage : ModePage
    {
        readonly Func<bool> super;
        readonly Label eeMapCaption = Caption("Map:");
        readonly ComboBox eeMap = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Width = 150 };
        readonly ComboBox eePlayers = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Width = 75 };
        readonly CheckBox pauseRta = new CheckBox { Text = "Pause the real-time clock between maps", AutoSize = true, Margin = new Padding(3, 6, 3, 0) };
        string eeShown;
        readonly StepGrid eeChecks = new StepGrid();
        readonly StepTicks ticks = new StepTicks(honourForced: true);
        bool filling;

        public EasterEggPage(Func<bool> super)
        {
            this.super = super;
            eeMap.Items.AddRange(EeMaps.All.Select(m => (object)m.Name).ToArray());
            eeMap.SelectedIndex = 0;
            eePlayers.Items.AddRange(new object[] { "Solo", "Co-op" });
            eePlayers.SelectedIndex = 0;
            Controls.Add(Row(eeMapCaption, eeMap, Caption("Mode:"), eePlayers));
            Controls.Add(eeChecks);
            Controls.Add(pauseRta);
            foreach (Control control in new Control[] { eeMap, eePlayers, pauseRta })
                Hook(control);
            eeMap.SelectedIndexChanged += (s, e) => FillEe();
            eePlayers.SelectedIndexChanged += (s, e) => FillEe();
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
            if (shown == eeShown && eeChecks.Controls.Count > 0) return;
            eeShown = shown;
            filling = true;
            eeChecks.SuspendLayout();
            eeChecks.Reset();
            (int rows, int dividers) = Lay(Shown(SuperPage), SuperPage, coop, eeChecks);
            if (SuperPage)
            {
                (int solo, int soloDividers) = Lay(EeMaps.Super, true, false, null);
                (int duo, int duoDividers) = Lay(EeMaps.Super, true, true, null);
                eeChecks.Height = Math.Max(eeChecks.FitHeight(solo, soloDividers), eeChecks.FitHeight(duo, duoDividers));
            }
            else
                eeChecks.Height = EeMaps.All.SelectMany(m => new[] { false, true }.Select(c => Lay(new[] { m }, false, c, null)))
                    .Max(size => eeChecks.FitHeight(size.rows, size.dividers));
            eeChecks.ResumeLayout();
            filling = false;
        }

        IEnumerable<EeMap> Shown(bool superPage) => superPage ? EeMaps.Super : new[] { SelectedEeMap };

        (int rows, int dividers) Lay(IEnumerable<EeMap> maps, bool superPage, bool coop, StepGrid grid)
        {
            int rows = 0, dividers = 0;
            foreach (EeMap map in maps)
            {
                if (rows > 0)
                {
                    grid?.Divider();
                    dividers++;
                }
                IReadOnlyList<EeMap> parts = map.Parts;
                bool headed = superPage || parts.Count > 1;
                int height = 0;
                for (int column = 0; column < parts.Count; column++)
                {
                    EeMap part = parts[column];
                    StepList<EeStep> list = part.List(coop, superPage);
                    height = Math.Max(height, list.Steps.Count + (headed ? 1 : 0));
                    if (grid == null) continue;
                    if (headed)
                    {
                        string title = part.Branch == null ? map.Name : (superPage ? map.Name + ": " : "") + part.Branch;
                        grid.Place(new Label { Text = title, AutoSize = true, Margin = Padding.Empty, Padding = new Padding(0, 2, 0, 0) }, column, 0, false);
                    }
                    uint mask = EeMask(part, superPage, coop);
                    for (int i = 0; i < list.Steps.Count; i++)
                        grid.Place(StepBox(list, i, (mask & (1u << i)) != 0), column, i + (headed ? 1 : 0), headed);
                }
                grid?.EndSection(height);
                rows += height;
            }
            return (rows, dividers);
        }

        CheckBox StepBox(StepList<EeStep> list, int step, bool ticked)
        {
            bool forced = (list.Forced & (1u << step)) != 0;
            var box = new CheckBox { Text = list.Steps[step].Label, AutoSize = true, Checked = ticked || forced, AutoCheck = !forced, Margin = Padding.Empty };
            box.CheckedChanged += (s, e) =>
            {
                if (filling) return;
                ticks.Set(list, step, box.Checked);
                OnSettingChanged();
            };
            return box;
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
