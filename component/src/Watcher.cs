using System;
using System.Collections.Concurrent;
using System.Threading;

namespace LiveSplit.T6ZM
{
    internal enum GameEventKind { NewLevel, Change }

    internal readonly struct GameEvent
    {
        public GameEvent(GameEventKind kind, uint generation, uint clock, int watch, int oldValue, int newValue, uint window)
        {
            Kind = kind;
            Generation = generation;
            Clock = clock;
            Watch = watch;
            OldValue = oldValue;
            NewValue = newValue;
            Window = window;
        }

        public GameEventKind Kind { get; }
        public uint Generation { get; }
        public uint Clock { get; }
        public int Watch { get; }
        public int OldValue { get; }
        public int NewValue { get; }
        public uint Window { get; }
    }

    internal sealed class Watcher : IDisposable
    {
        const int MaxWatches = 8;

        readonly Thread thread;
        readonly object gate = new object();
        volatile bool stopping;
        volatile ReaderProfile profile;
        ReaderSample latest;
        string error;

        public Watcher(ReaderProfile profile)
        {
            this.profile = profile;
            thread = new Thread(Run) { IsBackground = true, Name = "T6ZM reader", Priority = ThreadPriority.AboveNormal };
            thread.Start();
        }

        public ConcurrentQueue<GameEvent> Events { get; } = new ConcurrentQueue<GameEvent>();

        public ReaderProfile Profile { get => profile; set => profile = value; }

        public ReaderSample Latest { get { lock (gate) return latest; } }

        public string Error { get { lock (gate) return error; } }

        void Run()
        {
            try
            {
                uint generation = 0;
                ReaderProfile current = profile;
                var lastValue = new int?[MaxWatches];
                var lastClock = new uint[MaxWatches];
                while (!stopping)
                {
                    ReaderProfile wanted = profile;
                    ReaderSample sample = Reader.Poll(wanted);
                    if (wanted != current)
                    {
                        current = wanted;
                        Array.Clear(lastValue, 0, MaxWatches);
                    }
                    if (sample.Status >= ReaderStatus.Resolving)
                    {
                        if (sample.Generation != generation)
                        {
                            if (generation != 0) Events.Enqueue(new GameEvent(GameEventKind.NewLevel, sample.Generation, sample.Clock, -1, 0, 0, 0));
                            generation = sample.Generation;
                            Array.Clear(lastValue, 0, MaxWatches);
                        }
                        for (int i = 0; i < MaxWatches; i++)
                        {
                            if (!sample.Known(i)) continue;
                            int value = sample.Values[i];
                            if (lastValue[i] is int old && old != value)
                                Events.Enqueue(new GameEvent(GameEventKind.Change, generation, sample.Clock, i, old, value, sample.Clock - lastClock[i]));
                            lastValue[i] = value;
                            lastClock[i] = sample.Clock;
                        }
                    }
                    lock (gate) latest = sample;
                    Thread.Sleep(1);
                }
            }
            catch (Exception e)
            {
                lock (gate) error = e.Message;
            }
            finally
            {
                try { Reader.Close(); } catch { }
            }
        }

        public void Dispose()
        {
            stopping = true;
            thread.Join(1000);
        }
    }
}
