using System;
using System.Collections.Generic;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.GameContent;

namespace VsQuest
{
    public class EntityBehaviorBossGrowthRitual : EntityBehavior
    {
        private const string GrowthStageKey = "alegacyvsquest:bossgrowthstage";
        private const string GrowthScaleKey = "alegacyvsquest:bossgrowthritual:growthScale";
        private const string LastGrowthStartMsKey = "alegacyvsquest:bossgrowthritual:lastStartMs";

        private const string GrowthAnimSeqKey = "alegacyvsquest:bossgrowthritual:animseq";
        private const string GrowthAnimKey = "alegacyvsquest:bossgrowthritual:anim";
        private const string GrowthAnimMsKey = "alegacyvsquest:bossgrowthritual:animms";
        private const string GrowthDamageMultKey = "alegacyvsquest:bossgrowthritual:damagemult";

        private class GrowthStage
        {
            public float whenHealthRelBelow;
            public float sizeMultiplier;
            public float speedMultiplier;
            public float damageMultiplier;
            public string animation;
            public int animationMs;
            public string sound;
            public float soundRange;
            public int soundStartMs;
            public bool lightningFlash;
            public float cooldownSeconds;
        }

        private readonly List<GrowthStage> stages = new List<GrowthStage>();

        private bool baseSizesCaptured;
        private float baseClientSize = 1f;
        private Vec2f baseCollisionBoxSize;
        private Vec2f baseDeadCollisionBoxSize;
        private Vec2f baseSelectionBoxSize;
        private Vec2f baseDeadSelectionBoxSize;
        private double baseEyeHeight;
        private float baseWalkSpeed;

        private float lastAppliedClientScale = 1f;
        private float lastAppliedServerScale = 1f;

        private int lastClientAnimSeq;

        private WeatherSystemBase weatherSystem;

        public EntityBehaviorBossGrowthRitual(Entity entity) : base(entity)
        {
        }

        public override string PropertyName() => "bossgrowthritual";

        public override void Initialize(EntityProperties properties, JsonObject attributes)
        {
            base.Initialize(properties, attributes);

            if (entity?.Api is Vintagestory.API.Server.ICoreServerAPI sapi)
            {
                try
                {
                    weatherSystem = sapi.ModLoader?.GetModSystem<WeatherSystemBase>();
                }
                catch
                {
                    weatherSystem = null;
                }
            }

            CaptureBaseSizes();

            stages.Clear();
            try
            {
                foreach (var stageObj in attributes["stages"].AsArray())
                {
                    if (stageObj == null || !stageObj.Exists) continue;

                    var stage = new GrowthStage
                    {
                        whenHealthRelBelow = stageObj["whenHealthRelBelow"].AsFloat(1f),
                        sizeMultiplier = stageObj["sizeMultiplier"].AsFloat(1f),
                        speedMultiplier = stageObj["speedMultiplier"].AsFloat(1f),
                        damageMultiplier = stageObj["damageMultiplier"].AsFloat(0f),
                        animation = stageObj["animation"].AsString(null),
                        animationMs = stageObj["animationMs"].AsInt(0),
                        sound = stageObj["sound"].AsString(null),
                        soundRange = stageObj["soundRange"].AsFloat(24f),
                        soundStartMs = stageObj["soundStartMs"].AsInt(0),
                        lightningFlash = stageObj["lightningFlash"].AsBool(false),
                        cooldownSeconds = stageObj["cooldownSeconds"].AsFloat(0f)
                    };

                    if (stage.sizeMultiplier > 1.01f || stage.speedMultiplier > 1.01f)
                    {
                        stages.Add(stage);
                    }
                }
            }
            catch
            {
            }
        }

        private void TryRestoreFullHealth()
        {
            try
            {
                if (!BossBehaviorUtils.TryGetHealth(entity, out var healthTree, out float currentHealth, out float maxHealth)) return;
                if (healthTree == null || maxHealth <= 0f) return;

                if (currentHealth < maxHealth)
                {
                    healthTree.SetFloat("currenthealth", maxHealth);
                    entity.WatchedAttributes?.MarkPathDirty("health");
                }
            }
            catch
            {
            }
        }

        private void TryPlayClientGrowthAnimationFromWatchedAttributes()
        {
            if (entity?.Api is not ICoreClientAPI capi) return;

            var wa = entity?.WatchedAttributes;
            if (wa == null) return;

            int seq = 0;
            try
            {
                seq = wa.GetInt(GrowthAnimSeqKey, 0);
            }
            catch
            {
                seq = 0;
            }

            if (seq == 0 || seq == lastClientAnimSeq) return;
            lastClientAnimSeq = seq;

            string anim = null;
            int ms = 0;
            try
            {
                anim = wa.GetString(GrowthAnimKey, null);
                ms = wa.GetInt(GrowthAnimMsKey, 0);
            }
            catch
            {
                anim = null;
                ms = 0;
            }

            if (string.IsNullOrWhiteSpace(anim)) return;

            try
            {
                entity?.AnimManager?.StartAnimation(anim);
            }
            catch
            {
            }

            if (ms <= 0) return;

            capi.Event.RegisterCallback(_ =>
            {
                try
                {
                    entity?.AnimManager?.StopAnimation(anim);
                }
                catch
                {
                }
            }, ms);
        }

        private void TryTriggerClientAnimation(GrowthStage stage)
        {
            if (stage == null) return;
            if (string.IsNullOrWhiteSpace(stage.animation)) return;

            var wa = entity?.WatchedAttributes;
            if (wa == null) return;

            try
            {
                wa.SetString(GrowthAnimKey, stage.animation);
                wa.SetInt(GrowthAnimMsKey, Math.Max(0, stage.animationMs));
                wa.SetInt(GrowthAnimSeqKey, wa.GetInt(GrowthAnimSeqKey, 0) + 1);
                wa.MarkPathDirty(GrowthAnimKey);
                wa.MarkPathDirty(GrowthAnimMsKey);
                wa.MarkPathDirty(GrowthAnimSeqKey);
            }
            catch
            {
            }
        }

        public override void OnGameTick(float dt)
        {
            base.OnGameTick(dt);

            ApplyClientGrowthFromWatchedAttributes();

            if (!(entity?.Api is Vintagestory.API.Server.ICoreServerAPI)) return;
            if (entity == null || stages.Count == 0) return;
            if (!entity.Alive) return;

            try
            {
                float currentScale = entity?.WatchedAttributes?.GetFloat(GrowthScaleKey, 1f) ?? 1f;
                if (currentScale < 0.01f) currentScale = 1f;

                if (Math.Abs(currentScale - 1f) > 0.001f && Math.Abs(currentScale - lastAppliedServerScale) > 0.0005f)
                {
                    lastAppliedServerScale = currentScale;
                }

                if (Math.Abs(currentScale - 1f) > 0.001f)
                {
                    var cb = entity.Properties?.CollisionBoxSize;
                    if (cb != null)
                    {
                        entity.SetCollisionBox(cb.X, cb.Y);
                        var sb = entity.Properties.SelectionBoxSize ?? cb;
                        entity.SetSelectionBox(sb.X, sb.Y);
                    }

                    double td = (entity.touchDistance = entity.GetTouchDistance());
                    entity.touchDistanceSq = td * td;
                }
            }
            catch
            {
            }

            if (!TryGetHealthFraction(out float frac)) return;

            int stageProgress = entity.WatchedAttributes?.GetInt(GrowthStageKey, 0) ?? 0;
            for (int i = stageProgress; i < stages.Count; i++)
            {
                var stage = stages[i];
                if (frac <= stage.whenHealthRelBelow)
                {
                    var serverApi = entity.Api as Vintagestory.API.Server.ICoreServerAPI;
                    if (!BossBehaviorUtils.IsCooldownReady(serverApi, entity, LastGrowthStartMsKey, stage.cooldownSeconds)) return;

                    entity.WatchedAttributes.SetInt(GrowthStageKey, i + 1);
                    entity.WatchedAttributes.MarkPathDirty(GrowthStageKey);

                    BossBehaviorUtils.MarkCooldownStart(serverApi, entity, LastGrowthStartMsKey);
                    ApplyGrowth(stage);
                    break;
                }
            }
        }

        private void ApplyGrowth(GrowthStage stage)
        {
            bool applySize = stage.sizeMultiplier > 1.01f;
            bool applySpeed = stage.speedMultiplier > 1.01f;
            if (!applySize && !applySpeed) return;

            if (stage.damageMultiplier <= 0f)
            {
                stage.damageMultiplier = applySize ? stage.sizeMultiplier : 1f;
            }

            try
            {
                entity?.WatchedAttributes?.SetFloat(GrowthScaleKey, stage.sizeMultiplier);
                entity?.WatchedAttributes?.MarkPathDirty(GrowthScaleKey);
            }
            catch
            {
            }

            if (applySize && entity?.Properties != null)
            {
                if (entity.Properties.Client != null)
                {
                    entity.Properties.Client.Size = baseClientSize * stage.sizeMultiplier;
                }

                if (entity.Properties.CollisionBoxSize != null)
                {
                    entity.Properties.CollisionBoxSize = new Vec2f(baseCollisionBoxSize.X * stage.sizeMultiplier, baseCollisionBoxSize.Y * stage.sizeMultiplier);
                }

                if (entity.Properties.SelectionBoxSize != null)
                {
                    entity.Properties.SelectionBoxSize = new Vec2f(baseSelectionBoxSize.X * stage.sizeMultiplier, baseSelectionBoxSize.Y * stage.sizeMultiplier);
                }

                if (entity.Properties.DeadCollisionBoxSize != null)
                {
                    entity.Properties.DeadCollisionBoxSize = new Vec2f(baseDeadCollisionBoxSize.X * stage.sizeMultiplier, baseDeadCollisionBoxSize.Y * stage.sizeMultiplier);
                }

                if (entity.Properties.DeadSelectionBoxSize != null)
                {
                    entity.Properties.DeadSelectionBoxSize = new Vec2f(baseDeadSelectionBoxSize.X * stage.sizeMultiplier, baseDeadSelectionBoxSize.Y * stage.sizeMultiplier);
                }
            }

            entity.Properties.EyeHeight = baseEyeHeight * stage.sizeMultiplier;

            try
            {
                var cb = entity.Properties.CollisionBoxSize;
                if (cb != null)
                {
                    entity.SetCollisionBox(cb.X, cb.Y);
                    var sb = entity.Properties.SelectionBoxSize ?? cb;
                    entity.SetSelectionBox(sb.X, sb.Y);
                }

                double td = (entity.touchDistance = entity.GetTouchDistance());
                entity.touchDistanceSq = td * td;
            }
            catch
            {
            }

            if (applySpeed && entity?.Stats != null)
            {
                if (baseWalkSpeed <= 0f)
                {
                    baseWalkSpeed = entity.Stats.GetBlended("walkspeed");
                }

                if (baseWalkSpeed > 0f)
                {
                    entity.Stats.Set("walkspeed", "alegacyvsquest", baseWalkSpeed * stage.speedMultiplier, true);
                }
            }

            if (stage.damageMultiplier > 0f && entity?.WatchedAttributes != null)
            {
                try
                {
                    entity.WatchedAttributes.SetFloat(GrowthDamageMultKey, stage.damageMultiplier);
                    entity.WatchedAttributes.MarkPathDirty(GrowthDamageMultKey);
                }
                catch
                {
                }
            }

            TryRestoreFullHealth();

            TryPlayStageAnimation(stage);
            TryPlayStageSound(stage);
            TrySpawnLightningFlash(stage);
        }

        private void TrySpawnLightningFlash(GrowthStage stage)
        {
            if (stage == null) return;
            if (!stage.lightningFlash) return;

            try
            {
                weatherSystem?.SpawnLightningFlash(entity?.Pos?.XYZ);
            }
            catch
            {
            }
        }

        private void TryPlayStageAnimation(GrowthStage stage)
        {
            if (stage == null) return;
            if (string.IsNullOrWhiteSpace(stage.animation)) return;

            try
            {
                entity?.AnimManager?.StartAnimation(stage.animation);
            }
            catch
            {
            }

            int ms = stage.animationMs;
            if (ms <= 0) return;

            if (entity?.Api is not Vintagestory.API.Server.ICoreServerAPI sapi) return;

            sapi.Event.RegisterCallback(_ =>
            {
                try
                {
                    entity?.AnimManager?.StopAnimation(stage.animation);
                }
                catch
                {
                }
            }, ms);

            TryTriggerClientAnimation(stage);
        }

        private void TryPlayStageSound(GrowthStage stage)
        {
            if (stage == null) return;
            if (string.IsNullOrWhiteSpace(stage.sound)) return;
            if (entity?.Api is not Vintagestory.API.Server.ICoreServerAPI sapi) return;

            AssetLocation soundLoc = AssetLocation.Create(stage.sound, "game").WithPathPrefixOnce("sounds/");
            if (soundLoc == null) return;

            float range = stage.soundRange;
            if (range <= 0f) range = 24f;

            int startMs = stage.soundStartMs;
            if (startMs > 0)
            {
                sapi.Event.RegisterCallback(_ =>
                {
                    try
                    {
                        sapi.World.PlaySoundAt(soundLoc, entity, null, randomizePitch: true, range);
                    }
                    catch
                    {
                    }
                }, startMs);
                return;
            }

            try
            {
                sapi.World.PlaySoundAt(soundLoc, entity, null, randomizePitch: true, range);
            }
            catch
            {
            }
        }

        private void CaptureBaseSizes()
        {
            if (baseSizesCaptured || entity?.Properties == null) return;

            baseSizesCaptured = true;

            baseEyeHeight = entity.Properties.EyeHeight;
            if (entity.Properties.Client != null)
            {
                baseClientSize = entity.Properties.Client.Size;
            }

            if (entity.Properties.CollisionBoxSize != null)
            {
                baseCollisionBoxSize = entity.Properties.CollisionBoxSize;
            }

            if (entity.Properties.SelectionBoxSize != null)
            {
                baseSelectionBoxSize = entity.Properties.SelectionBoxSize;
            }
            else
            {
                baseSelectionBoxSize = baseCollisionBoxSize;
            }

            if (entity.Properties.DeadCollisionBoxSize != null)
            {
                baseDeadCollisionBoxSize = entity.Properties.DeadCollisionBoxSize;
            }

            if (entity.Properties.DeadSelectionBoxSize != null)
            {
                baseDeadSelectionBoxSize = entity.Properties.DeadSelectionBoxSize;
            }
            else
            {
                baseDeadSelectionBoxSize = baseDeadCollisionBoxSize;
            }
        }

        private void ApplyClientGrowthFromWatchedAttributes()
        {
            if (!(entity?.Api is ICoreClientAPI)) return;

            TryPlayClientGrowthAnimationFromWatchedAttributes();

            CaptureBaseSizes();

            float scale = 1f;
            try
            {
                scale = entity?.WatchedAttributes?.GetFloat(GrowthScaleKey, 1f) ?? 1f;
            }
            catch
            {
                scale = 1f;
            }

            if (scale < 0.01f) scale = 1f;
            if (Math.Abs(scale - lastAppliedClientScale) < 0.001f) return;
            lastAppliedClientScale = scale;

            try
            {
                if (entity?.Properties?.Client != null)
                {
                    entity.Properties.Client.Size = baseClientSize * scale;
                }

                if (entity?.Properties?.CollisionBoxSize != null)
                {
                    entity.Properties.CollisionBoxSize = new Vec2f(baseCollisionBoxSize.X * scale, baseCollisionBoxSize.Y * scale);
                }

                if (entity?.Properties?.SelectionBoxSize != null)
                {
                    entity.Properties.SelectionBoxSize = new Vec2f(baseSelectionBoxSize.X * scale, baseSelectionBoxSize.Y * scale);
                }

                if (entity?.Properties?.DeadCollisionBoxSize != null)
                {
                    entity.Properties.DeadCollisionBoxSize = new Vec2f(baseDeadCollisionBoxSize.X * scale, baseDeadCollisionBoxSize.Y * scale);
                }

                if (entity?.Properties?.DeadSelectionBoxSize != null)
                {
                    entity.Properties.DeadSelectionBoxSize = new Vec2f(baseDeadSelectionBoxSize.X * scale, baseDeadSelectionBoxSize.Y * scale);
                }

				if (entity?.Properties != null)
				{
					entity.Properties.EyeHeight = baseEyeHeight * scale;
				}

				var cb = entity?.Properties?.CollisionBoxSize;
				if (cb != null)
				{
					entity.SetCollisionBox(cb.X, cb.Y);
					var sb = entity.Properties.SelectionBoxSize ?? cb;
					entity.SetSelectionBox(sb.X, sb.Y);
				}

				double td = (entity.touchDistance = entity.GetTouchDistance());
				entity.touchDistanceSq = td * td;
            }
            catch
            {
            }
        }

        private bool TryGetHealthFraction(out float fraction)
        {
            fraction = 1f;
            var wa = entity?.WatchedAttributes;
            if (wa == null) return false;

            var healthTree = wa.GetTreeAttribute("health");
            if (healthTree == null) return false;

            float maxHealth = healthTree.GetFloat("maxhealth", 0f);
            if (maxHealth <= 0f)
            {
                maxHealth = healthTree.GetFloat("basemaxhealth", 0f);
            }

            float curHealth = healthTree.GetFloat("currenthealth", 0f);
            if (maxHealth <= 0f || curHealth <= 0f) return false;

            fraction = curHealth / maxHealth;
            return true;
        }
    }
}
