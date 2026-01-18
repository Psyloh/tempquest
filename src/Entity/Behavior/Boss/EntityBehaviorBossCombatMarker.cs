using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;

namespace VsQuest
{
    public class EntityBehaviorBossCombatMarker : EntityBehavior
    {
        public const string BossCombatAttackersKey = "alegacyvsquest:bosscombat:attackers";
        public const string BossCombatDamageByPlayerKey = "alegacyvsquest:bosscombat:damageByPlayer";
        public const string BossCombatLastDamageMsKey = "alegacyvsquest:bosscombat:lastDamageMs";

        private bool trackBossHuntDamageLock;

        public EntityBehaviorBossCombatMarker(Entity entity) : base(entity)
        {
        }

        public override void Initialize(EntityProperties properties, Vintagestory.API.Datastructures.JsonObject attributes)
        {
            base.Initialize(properties, attributes);

            trackBossHuntDamageLock = attributes["trackBossHuntDamageLock"].AsBool(false);
        }

        public override void OnEntityReceiveDamage(DamageSource damageSource, ref float damage)
        {
            base.OnEntityReceiveDamage(damageSource, ref damage);

            if (entity?.Api?.Side != EnumAppSide.Server) return;
            if (damage <= 0) return;

            try
            {
                var wa = entity.WatchedAttributes;
                if (wa != null)
                {
                    wa.SetLong(BossCombatLastDamageMsKey, entity.World.ElapsedMilliseconds);
                    wa.MarkPathDirty(BossCombatLastDamageMsKey);
                }
            }
            catch
            {
            }

            if (damageSource?.SourceEntity is EntityPlayer byPlayer && !string.IsNullOrWhiteSpace(byPlayer.PlayerUID))
            {
                try
                {
                    var wa = entity.WatchedAttributes;
                    if (wa != null)
                    {
                        var existing = wa.GetStringArray(BossCombatAttackersKey, new string[0]) ?? new string[0];
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
                            wa.SetStringArray(BossCombatAttackersKey, merged);
                            wa.MarkPathDirty(BossCombatAttackersKey);
                        }

                        try
                        {
                            var tree = wa.GetTreeAttribute(BossCombatDamageByPlayerKey);
                            if (tree == null)
                            {
                                tree = new Vintagestory.API.Datastructures.TreeAttribute();
                                wa.SetAttribute(BossCombatDamageByPlayerKey, tree);
                            }

                            double prev = tree.GetDouble(byPlayer.PlayerUID, 0);
                            tree.SetDouble(byPlayer.PlayerUID, prev + damage);
                            wa.MarkPathDirty(BossCombatDamageByPlayerKey);
                        }
                        catch
                        {
                        }
                    }
                }
                catch
                {
                }
            }

            if (trackBossHuntDamageLock)
            {
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
        }

        public override string PropertyName() => "bosscombatmarker";
    }
}
