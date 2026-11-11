using System;
using System.Windows.Forms;
using System.Xml;

namespace LiveSplit.T6ZM
{
    internal abstract class ModePage : TableLayoutPanel
    {
        protected ModePage(int columns = 1)
        {
            ColumnCount = columns;
            AutoSize = true;
            AutoSizeMode = AutoSizeMode.GrowAndShrink;
        }

        public event Action SettingChanged;

        public event Action LiveSettingChanged;

        public virtual LiveSettings LiveValues => null;

        protected void OnSettingChanged() => SettingChanged?.Invoke();
        protected void OnLiveSettingChanged() => LiveSettingChanged?.Invoke();

        public virtual bool ListsMaps => false;

        public virtual void SelectGameMap(string map, bool locked) { }

        public virtual void UpdateView() { }

        public virtual void UpdateEnabled(bool locked, bool timingsHeld) { }

        public abstract void WriteXml(XmlDocument document, XmlElement node);

        public abstract void ReadXml(XmlNode node);

        public virtual void AfterLoad() { }

        protected void Hook(Control control)
        {
            switch (control)
            {
                case ComboBox box: box.SelectedIndexChanged += (s, e) => OnSettingChanged(); break;
                case NumericUpDown number: number.ValueChanged += (s, e) => OnSettingChanged(); break;
                case CheckBox check: check.CheckedChanged += (s, e) => OnSettingChanged(); break;
            }
        }
    }

    internal static class PageParts
    {
        public static Label Caption(string text) => new Label { Text = text, AutoSize = true, Anchor = AnchorStyles.Left };

        public static FlowLayoutPanel Row(params Control[] controls)
        {
            var row = new FlowLayoutPanel { AutoSize = true, WrapContents = false, Margin = Padding.Empty };
            row.Controls.AddRange(controls);
            return row;
        }

        public static XmlElement Element(XmlDocument document, string name, string value)
        {
            XmlElement element = document.CreateElement(name);
            element.InnerText = value;
            return element;
        }

        public static bool ReadBool(XmlNode node, string name, bool fallback) =>
            bool.TryParse(node?[name]?.InnerText, out bool value) ? value : fallback;

        public static int ReadInt(XmlNode node, string name, int fallback) =>
            int.TryParse(node?[name]?.InnerText, out int value) ? value : fallback;
    }
}
