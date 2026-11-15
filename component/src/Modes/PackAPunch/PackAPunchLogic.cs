using System.Collections.Generic;

namespace LiveSplit.T6ZM
{
    internal sealed class PackAPunchLogic : RunLogic
    {
        const int FirstSlotWatch = 1;
        const int Slots = 4;

        bool split;

        public override ReaderProfile Profile => ReaderProfile.PackAPunch;

        public override IEnumerable<TimerAction> OnChange(GameEvent e, bool running)
        {
            if (!running)
            {
                if (BlackscreenEnded(e)) yield return StartRun(e.Clock);
            }
            else if (!split && e.Watch >= FirstSlotWatch && e.Watch < FirstSlotWatch + Slots && Became(e, e.Watch))
            {
                split = true;
                yield return new TimerAction(false, e.Clock, $"player {e.Watch - FirstSlotWatch + 1} pack-a-punch");
            }
        }

        protected override void Reset() => split = false;

        public override string Next => split ? "done" : "waiting for PaP grab";
    }
}
