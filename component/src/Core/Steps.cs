using System.Collections.Generic;

namespace LiveSplit.T6ZM
{
    internal class Step
    {
        public Step(string id, string label)
        {
            Id = id;
            Label = label;
        }

        public string Id { get; }
        public string Label { get; }
    }

    internal class StepList<TStep> where TStep : Step
    {
        public StepList(string key, string script, IReadOnlyList<TStep> steps, string name = null, uint forced = 0)
        {
            Key = key;
            Script = script;
            Steps = steps;
            Name = name ?? GameMaps.Name(script);
            Forced = forced;
        }

        public string Key { get; }
        public string Script { get; }
        public string Name { get; }
        public IReadOnlyList<TStep> Steps { get; }
        public uint Every => (uint)((1UL << Steps.Count) - 1);

        public uint Forced { get; }

        public virtual uint Default => Every;

        public int IndexOf(string id)
        {
            for (int i = 0; i < Steps.Count; i++)
                if (Steps[i].Id == id) return i;
            return -1;
        }
    }
}
