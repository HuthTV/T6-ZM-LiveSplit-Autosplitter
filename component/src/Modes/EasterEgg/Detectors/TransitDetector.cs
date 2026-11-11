using System.Collections.Generic;

namespace LiveSplit.T6ZM
{
    internal enum TransitSide { Unknown, Richtofen, Maxis }

    internal sealed class TransitDetector : EeDetector
    {
        const int PowerOnWatch = 2;
        const int PowerEventWatch = 3;
        const int JetgunBuiltWatch = 4;
        const int JetgunTowerWatch = 5;
        const int RichtofenFinishedWatch = 7;
        const int MaxisFinishedWatch = 8;

        bool powerSeen, jetgunBuilt;

        public TransitSide Side { get; private set; }

        public override int Branch => Side == TransitSide.Maxis ? 1 : 0;

        public override IEnumerable<EeHit> OnChange(GameEvent e)
        {
            switch (e.Watch)
            {
                case PowerOnWatch:
                    if (Rose(e)) powerSeen = true;
                    break;
                case PowerEventWatch:
                    if (Rose(e) && powerSeen && Side == TransitSide.Unknown)
                    {
                        Side = TransitSide.Maxis;
                        yield return new EeHit("jetgun_power_off", e.Clock);
                    }
                    break;
                case JetgunBuiltWatch:
                    if (Rose(e)) jetgunBuilt = true;
                    break;
                case JetgunTowerWatch:
                    if (Rose(e) && Side == TransitSide.Richtofen) yield return new EeHit("tower", e.Clock);
                    break;
                case RichtofenFinishedWatch:
                    if (Rose(e) && Side == TransitSide.Richtofen) yield return new EeHit("EMP", e.Clock);
                    break;
                case MaxisFinishedWatch:
                    if (Rose(e) && Side == TransitSide.Maxis) yield return new EeHit("tower", e.Clock);
                    break;
            }
            if (Side == TransitSide.Unknown && powerSeen && jetgunBuilt)
            {
                Side = TransitSide.Richtofen;
                yield return new EeHit("jetgun_power_off", e.Clock);
            }
        }
    }
}
