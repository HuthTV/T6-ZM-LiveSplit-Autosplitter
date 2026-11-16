using System;
using System.Linq;
using System.Windows.Forms;
using System.Xml;
using static LiveSplit.T6ZM.PageParts;

namespace LiveSplit.T6ZM
{
    internal sealed class Settings : UserControl
    {
        readonly ComboBox category = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Width = 220 };
        readonly CheckBox showInfo = new CheckBox { Text = "Split debug", Checked = true, AutoSize = true };
        readonly CheckBox autoStart = new CheckBox { Text = "Start", Checked = true, AutoSize = true };
        readonly CheckBox autoSplit = new CheckBox { Text = "Split", Checked = true, AutoSize = true };
        readonly CheckBox autoReset = new CheckBox { Text = "Reset", Checked = true, AutoSize = true };
        readonly CheckBox showVelocity = new CheckBox { Text = "Velocity", AutoSize = true };
        readonly CheckBox velocityDecimals = new CheckBox { Text = "Velocity Decimals", AutoSize = true, Enabled = false };
        readonly CheckBox showFps = new CheckBox { Text = "FPS", AutoSize = true };
        const string LockedText = "Some settings locked while timer is running";
        const string NextRunText = "Applies to the next run";
        readonly Label blocked = new Label { Text = LockedText, AutoSize = true, ForeColor = System.Drawing.Color.Firebrick, Anchor = AnchorStyles.Left, Visible = false };
        readonly Label description = new Label { AutoSize = true, MaximumSize = new System.Drawing.Size(424, 0), ForeColor = System.Drawing.SystemColors.ControlText, Font = new System.Drawing.Font(Control.DefaultFont.FontFamily, 9f), Margin = Padding.Empty };
        readonly Panel descriptionBox = new Panel { AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink, BackColor = System.Drawing.SystemColors.ControlLight, BorderStyle = BorderStyle.FixedSingle, Padding = new Padding(6), Margin = new Padding(3, 2, 3, 8), MinimumSize = new System.Drawing.Size(436, 0) };

        readonly ModePage[] pages;
        bool loading, locked, appliesNextRun, timingsHeld, onMapPage;

        public Settings()
        {
            category.Items.AddRange(Modes.All.Select(mode => (object)mode.Name).ToArray());
            category.Width = category.Items.Cast<object>().Max(item => TextRenderer.MeasureText(item.ToString(), category.Font).Width) + SystemInformation.VerticalScrollBarWidth + 12;
            category.SelectedIndex = 0;
            pages = Modes.CreatePages(() => Category == Category.SuperEasterEgg);

            var layout = new FlowLayoutPanel { FlowDirection = FlowDirection.TopDown, AutoSize = true, WrapContents = false };
            layout.Controls.Add(Row(Caption("Autosplitter:"), autoStart, autoSplit, autoReset));
            layout.Controls.Add(new Label { BorderStyle = BorderStyle.Fixed3D, Height = 2, Width = 436, Margin = new Padding(3, 8, 3, 8) });
            layout.Controls.Add(Row(Caption("Category:"), category, blocked));
            descriptionBox.Controls.Add(description);
            layout.Controls.Add(descriptionBox);
            foreach (ModePage page in pages)
                layout.Controls.Add(page);
            layout.Controls.Add(new Label { BorderStyle = BorderStyle.Fixed3D, Height = 2, Width = 436, Margin = new Padding(3, 8, 3, 8) });
            layout.Controls.Add(Row(showVelocity, velocityDecimals));
            layout.Controls.Add(showFps);
            layout.Controls.Add(showInfo);
            Controls.Add(layout);
            AutoSize = true;

            category.SelectedIndexChanged += (s, e) => Changed();
            showVelocity.CheckedChanged += (s, e) => velocityDecimals.Enabled = showVelocity.Checked;
            foreach (ModePage page in pages)
            {
                page.SettingChanged += Changed;
                page.LiveSettingChanged += () => LiveVersion++;
            }
            Changed();
        }

        public int Version { get; private set; }

        public int LiveVersion { get; private set; }

        public LiveSettings LiveValues => pages.Select(page => page.LiveValues).FirstOrDefault(values => values != null);

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

        public bool AppliesNextRun
        {
            get => appliesNextRun;
            set
            {
                if (appliesNextRun == value) return;
                appliesNextRun = value;
                UpdateEnabled();
            }
        }

        public bool TimingsHeld
        {
            get => timingsHeld;
            set
            {
                if (timingsHeld == value) return;
                timingsHeld = value;
                UpdateEnabled();
            }
        }

        public bool ShowInfo => showInfo.Checked;
        public bool AutoStart => autoStart.Checked;
        public bool AutoSplit => autoSplit.Checked;
        public bool AutoReset => autoReset.Checked;
        public bool ShowVelocity => showVelocity.Checked;
        public bool VelocityDecimals => velocityDecimals.Checked;
        public bool ShowFps => showFps.Checked;
        public Category Category => (Category)Math.Max(0, category.SelectedIndex);

        Mode Mode => Modes.Of(Category);

        ModePage Page => pages.First(page => page.GetType() == Mode.PageType);

        public RunLogic CreateLogic() => Mode.CreateLogic(Page);

        void Changed()
        {
            ModePage shown = Page;
            bool mapPage = shown.ListsMaps;
            bool opened = mapPage && !onMapPage;
            onMapPage = mapPage;
            foreach (ModePage page in pages)
                page.Visible = page == shown;
            foreach (ModePage page in pages)
                page.UpdateView();
            description.Text = Mode.Description;
            UpdateEnabled();
            if (!loading) Version++;
            if (opened) SelectGameMap();
        }

        public string GameMap { get; set; }

        void SelectGameMap()
        {
            if (loading) return;
            foreach (ModePage page in pages)
                page.SelectGameMap(GameMap, locked);
        }

        protected override void OnVisibleChanged(EventArgs e)
        {
            base.OnVisibleChanged(e);
            if (Visible && Page.ListsMaps) SelectGameMap();
        }

        void UpdateEnabled()
        {
            category.Enabled = !locked;
            blocked.Text = locked ? LockedText : NextRunText;
            blocked.Visible = locked || appliesNextRun;
            foreach (ModePage page in pages)
                page.UpdateEnabled(locked, timingsHeld);
        }

        public XmlNode WriteXml(XmlDocument document)
        {
            XmlElement node = document.CreateElement("Settings");
            node.AppendChild(Element(document, "Version", "3"));
            node.AppendChild(Element(document, "Category", Category.ToString()));
            foreach (ModePage page in pages)
                page.WriteXml(document, node);
            node.AppendChild(Element(document, "AutoStart", autoStart.Checked.ToString()));
            node.AppendChild(Element(document, "AutoSplit", autoSplit.Checked.ToString()));
            node.AppendChild(Element(document, "AutoReset", autoReset.Checked.ToString()));
            node.AppendChild(Element(document, "ShowInfo", showInfo.Checked.ToString()));
            node.AppendChild(Element(document, "ShowVelocity", showVelocity.Checked.ToString()));
            node.AppendChild(Element(document, "VelocityDecimals", velocityDecimals.Checked.ToString()));
            node.AppendChild(Element(document, "ShowFps", showFps.Checked.ToString()));
            return node;
        }

        public void ReadXml(XmlNode node)
        {
            loading = true;
            try
            {
                if (Enum.TryParse(node?["Category"]?.InnerText, out Category saved)) category.SelectedIndex = (int)saved;
                foreach (ModePage page in pages)
                    page.ReadXml(node);
                autoStart.Checked = ReadBool(node, "AutoStart", true);
                autoSplit.Checked = ReadBool(node, "AutoSplit", true);
                autoReset.Checked = ReadBool(node, "AutoReset", true);
                showInfo.Checked = ReadBool(node, "ShowInfo", true);
                showVelocity.Checked = ReadBool(node, "ShowVelocity", false);
                velocityDecimals.Checked = ReadBool(node, "VelocityDecimals", false);
                showFps.Checked = ReadBool(node, "ShowFps", false);
            }
            finally
            {
                loading = false;
            }
            foreach (ModePage page in pages)
                page.AfterLoad();
            Changed();
        }
    }
}
