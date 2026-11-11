using System;
using LiveSplit.Model;
using LiveSplit.UI.Components;

[assembly: ComponentFactory(typeof(LiveSplit.T6ZM.Factory))]

namespace LiveSplit.T6ZM
{
    public sealed class Factory : IComponentFactory
    {
        public string ComponentName => "T6 Zombies Autosplitter";
        public string Description => "Times and splits Black Ops II Zombies (Plutonium) by reading game memory.";
        public ComponentCategory Category => ComponentCategory.Control;
        public IComponent Create(LiveSplitState state) => new T6Component(state);

        public string UpdateName => ComponentName;
        public string XMLURL => "";
        public string UpdateURL => "";
        public Version Version => typeof(Factory).Assembly.GetName().Version;
    }
}
