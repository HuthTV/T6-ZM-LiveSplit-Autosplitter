using System.Collections.Generic;

namespace LiveSplit.T6ZM
{
    internal sealed class BuriedDetector : EeDetector
    {
        const int ChestUsedWatch = 2;
        const int SpawnGhostsWatch = 3;
        const int TimeTravelRoundWatch = 4;
        const int WispSavedWatch = 5;
        const int MaxisSqCompleteWatch = 7;
        const int RichtofenSqCompleteWatch = 8;
        const int MaxisMetagameWatch = 9;
        const int RichtofenMetagameWatch = 10;
        const int BuiltWatch = 11;

        const int OilLampBit = 6;

        public override IEnumerable<EeHit> OnChange(GameEvent e)
        {
            switch (e.Watch)
            {
                case ChestUsedWatch:
                    if (Rose(e)) yield return new EeHit("boxhit", e.Clock);
                    break;
                case SpawnGhostsWatch:
                    if (Rose(e)) yield return new EeHit("ghosts", e.Clock);
                    break;
                case TimeTravelRoundWatch:
                case WispSavedWatch:
                    if (Rose(e)) yield return new EeHit("time_travel", e.Clock);
                    break;
                case MaxisSqCompleteWatch:
                case RichtofenSqCompleteWatch:
                    if (Rose(e)) yield return new EeHit("sharpshooter", e.Clock);
                    break;
                case MaxisMetagameWatch:
                case RichtofenMetagameWatch:
                    if (Rose(e)) yield return new EeHit("mined_games", e.Clock);
                    break;
                case BuiltWatch:
                    if ((e.OldValue & (1 << OilLampBit)) == 0 && (e.NewValue & (1 << OilLampBit)) != 0) yield return new EeHit("cipher", e.Clock);
                    break;
            }
        }
    }
}
