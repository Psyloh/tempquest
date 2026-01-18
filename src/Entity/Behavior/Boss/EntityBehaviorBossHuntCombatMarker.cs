using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;

namespace VsQuest
{
    public class EntityBehaviorBossHuntCombatMarker : EntityBehavior
    {
        public const string BossHuntAttackersKey = "alegacyvsquest:bosshunt:attackers";

        public EntityBehaviorBossHuntCombatMarker(Entity entity) : base(entity)
        {
        }

        public override void OnEntityReceiveDamage(DamageSource damageSource, ref float damage)
        {
            base.OnEntityReceiveDamage(damageSource, ref damage);

            if (entity?.Api?.Side != EnumAppSide.Server) return;
            if (damage <= 0) return;

            if (damageSource?.SourceEntity is EntityPlayer byPlayer && !string.IsNullOrWhiteSpace(byPlayer.PlayerUID))
            {
                try
                {
                    var wa = entity.WatchedAttributes;
                    if (wa != null)
                    {
                        var existing = wa.GetStringArray(BossHuntAttackersKey, new string[0]) ?? new string[0];
                        bool found = false;
                        for (int i = 0; i < existing.Length; i++)
                        {
                            if (string.Equals(existing[i], byPlayer.PlayerUID, System.StringComparison.OrdinalIgnoreCase))
                            {
                                found = true;
                                break;
                            }
                        }

                        if (!found)
                        {
                            var merged = new string[existing.Length + 1];
                            for (int i = 0; i < existing.Length; i++) merged[i] = existing[i];
                            merged[existing.Length] = byPlayer.PlayerUID;
                            wa.SetStringArray(BossHuntAttackersKey, merged);
                            wa.MarkPathDirty(BossHuntAttackersKey);
                        }
                    }
                }
                catch
                {
                }
            }

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
