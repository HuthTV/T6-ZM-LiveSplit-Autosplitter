using System;
using System.Collections.Generic;

namespace LiveSplit.T6ZM
{
    internal sealed class TombDetector : EeDetector
    {
        const int NmlWatch = 1;
        const int SoulBoxesWatch = 2;
        const int StaffsCraftedWatch = 3;
        const int StaffsPlacedWatch = 4;
        const int MechHoleWatch = 5;
        const int SamanthaReleasedWatch = 7;
        const int IntermissionWatch = 8;

        const uint BoxesTicks = 86;

        bool released;

        public override IEnumerable<EeHit> OnChange(GameEvent e)
        {
            switch (e.Watch)
            {
                case NmlWatch:
                    if (Rose(e)) yield return new EeHit("NML", e.Clock);
                    break;
                case SoulBoxesWatch:
                    if (e.OldValue < 4 && e.NewValue >= 4) yield return new EeHit("boxes", e.Clock + BoxesTicks);
                    break;
                case StaffsCraftedWatch:
                    for (int staff = Math.Max(e.OldValue + 1, 1); staff <= Math.Min(e.NewValue, 4); staff++)
                        yield return new EeHit("staff_" + staff, e.Clock);
                    break;
                case StaffsPlacedWatch:
                    if (Rose(e)) yield return new EeHit("AFD", e.Clock);
                    break;
                case MechHoleWatch:
                    if (Rose(e)) yield return new EeHit("rain_fire", e.Clock);
                    break;
                case SamanthaReleasedWatch:
                    if (e.NewValue != 0) released = true;
                    break;
                case IntermissionWatch:
                    if (Rose(e) && released) yield return new EeHit("freedom", e.Clock);
                    break;
            }
        }
    }
}
