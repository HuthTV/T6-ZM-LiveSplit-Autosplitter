using System.Collections.Generic;

namespace LiveSplit.T6ZM
{
    internal sealed class HighriseDetector : EeDetector
    {
        const int SymbolsWatch = 1;
        const int TowerActiveWatch = 2;
        const int PerksWatch = 3;
        const int PurchaseWatch = 4;
        const int LaststandWatch = 5;
        const int ReviveTriggerWatch = 7;
        const int RewardOrbWatch = 8;

        const uint PurchaseTicks = 65;
        const uint RecentTicks = 2;
        const uint RepeatTicks = 5;

        bool towerActive, marker, laststand, revive, orbSeen;
        uint? markerSetAt, markerClearedAt, standingClearedAt, lastRise;

        public override IEnumerable<EeHit> OnChange(GameEvent e)
        {
            switch (e.Watch)
            {
                case SymbolsWatch:
                    if (Rose(e)) yield return new EeHit("highrise_symbols", e.Clock);
                    break;
                case TowerActiveWatch:
                    towerActive = e.NewValue != 0;
                    break;
                case PurchaseWatch:
                    marker = e.NewValue != 0;
                    if (marker) markerSetAt = e.Clock;
                    else markerClearedAt = e.Clock;
                    break;
                case LaststandWatch:
                case ReviveTriggerWatch:
                    bool was = laststand || revive;
                    if (e.Watch == LaststandWatch) laststand = e.NewValue != 0;
                    else revive = e.NewValue != 0;
                    if (was && !(laststand || revive)) standingClearedAt = e.Clock;
                    break;
                case PerksWatch:
                    if (e.NewValue <= e.OldValue) break;
                    bool purchase = (marker && markerSetAt.HasValue && e.Clock - markerSetAt.Value <= PurchaseTicks)
                                    || (markerClearedAt.HasValue && e.Clock - markerClearedAt.Value <= RecentTicks);
                    bool returned = laststand || revive || (standingClearedAt.HasValue && e.Clock - standingClearedAt.Value <= RecentTicks);
                    bool repeat = lastRise.HasValue && e.Clock - lastRise.Value <= RepeatTicks;
                    lastRise = e.Clock;
                    if (towerActive && !orbSeen && e.NewValue - e.OldValue == 1 && !purchase && !returned && !repeat)
                        yield return new EeHit("highrise_perks", e.Clock);
                    break;
                case RewardOrbWatch:
                    if (e.NewValue >= 2) orbSeen = true;
                    if (e.NewValue == 3) yield return new EeHit("highrise_perks", e.Clock);
                    break;
            }
        }
    }
}
