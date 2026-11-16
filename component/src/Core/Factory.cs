using System;
using LiveSplit.Model;
using LiveSplit.UI.Components;

[assembly: ComponentFactory(typeof(LiveSplit.T6ZM.Factory))]

namespace LiveSplit.T6ZM
{
    public sealed class Factory : IComponentFactory
    {
        public string ComponentName => "T6ZM Autosplitter";
        public string Description => "Times and splits Black Ops II Zombies by reading game memory (Plutonium, Steam, Windows Store).";
        public ComponentCategory Category => ComponentCategory.Control;
        public IComponent Create(LiveSplitState state) => new T6Component(state);

        public string UpdateName => ComponentName;
        public string XMLURL => "https://raw.githubusercontent.com/HuthTV/T6-ZM-LiveSplit-Autosplitter/main/update.xml";
        public string UpdateURL => "https://github.com/HuthTV/T6-ZM-LiveSplit-Autosplitter/releases/download/";
        public Version Version => typeof(Factory).Assembly.GetName().Version;
    }
}
