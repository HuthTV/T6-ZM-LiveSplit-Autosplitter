using System.Collections.Generic;

namespace LiveSplit.T6ZM
{
    internal abstract class EeDetector
    {
        protected static bool Rose(GameEvent e) => e.OldValue == 0 && e.NewValue != 0;

        public abstract IEnumerable<EeHit> OnChange(GameEvent e);

        public virtual int Branch => 0;

        public static EeDetector Create(string script)
        {
            switch (script)
            {
                case "zm_transit": return new TransitDetector();
                case "zm_highrise": return new HighriseDetector();
                case "zm_prison": return new PrisonDetector();
                case "zm_buried": return new BuriedDetector();
                case "zm_tomb": return new TombDetector();
                default: return null;
            }
        }
    }
}
