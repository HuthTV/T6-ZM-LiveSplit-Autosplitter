using System.Collections.Generic;

namespace LiveSplit.T6ZM
{
    internal sealed class PrisonDetector : EeDetector
    {
        const int DryerWatch = 1;
        const int GondolaReadyWatch = 2;
        const int GondolaMovingWatch = 3;
        const int PlaneBoardedWatch = 4;
        const int NixieFlashingWatch = 5;
        const int CodesWatch = 7;
        const int HeadphonesWatch = 8;
        const int WinnerWatch = 9;

        const uint ShowdownTicks = 40;

        bool gondolaReady, codesStage;
        int gondolas, planes, finals;

        public override IEnumerable<EeHit> OnChange(GameEvent e)
        {
            switch (e.Watch)
            {
                case DryerWatch:
                    if (Rose(e)) yield return new EeHit("dryer", e.Clock);
                    break;
                case GondolaReadyWatch:
                    if (e.NewValue != 0) gondolaReady = true;
                    break;
                case GondolaMovingWatch:
                    if (Rose(e) && gondolaReady && ++gondolas <= 3) yield return new EeHit("gondola_" + gondolas, e.Clock);
                    break;
                case PlaneBoardedWatch:
                    if (Rose(e) && ++planes <= 3) yield return new EeHit("plane_" + planes, e.Clock);
                    break;
                case CodesWatch:
                    if (e.NewValue - e.OldValue >= 4) codesStage = true;
                    break;
                case NixieFlashingWatch:
                    if (Rose(e) && codesStage && ++finals == 4) yield return new EeHit("codes", e.Clock);
                    break;
                case HeadphonesWatch:
                    if (e.NewValue == 2) yield return new EeHit("headphones", e.Clock);
                    break;
                case WinnerWatch:
                    if (Rose(e)) yield return new EeHit("fight", e.Clock + ShowdownTicks);
                    break;
            }
        }
    }
}
