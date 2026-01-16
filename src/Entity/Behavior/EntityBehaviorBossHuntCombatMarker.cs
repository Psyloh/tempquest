using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;

namespace VsQuest
{
    public class EntityBehaviorBossHuntCombatMarker : EntityBehavior
    {
        public EntityBehaviorBossHuntCombatMarker(Entity entity) : base(entity)
        {
        }

        public override void OnEntityReceiveDamage(DamageSource damageSource, ref float damage)
        {
            base.OnEntityReceiveDamage(damageSource, ref damage);

            if (entity?.Api?.Side != EnumAppSide.Server) return;
            if (damage <= 0) return;

            try
            {
                var calendar = entity.World?.Calendar;
                if (calendar == null) return;

                double nowHours = calendar.TotalHours;

                var wa = entity.WatchedAttributes;
                if (wa == null) return;

                wa.SetDouble(BossHuntSystem.LastBossDamageTotalHoursKey, nowHours);
                wa.MarkPathDirty(BossHuntSystem.LastBossDamageTotalHoursKey);
            }
            catch
            {
            }
        }

        public override string PropertyName() => "bosshuntcombatmarker";
    }
}
