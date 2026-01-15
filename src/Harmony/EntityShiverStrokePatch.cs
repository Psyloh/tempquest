using HarmonyLib;
using System;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.GameContent;

namespace VsQuest.Harmony
{
    public static class EntityShiverStrokePatch
    {
        private const double TestStrokeChancePerTick = 0.05;

        [HarmonyPatch(typeof(EntityShiver), "OnGameTick")]
        public static class EntityShiver_OnGameTick_StrokeFreqPatch
        {
            public static bool Prefix(EntityShiver __instance, float dt)
            {
                try
                {
                    if (__instance?.Api == null) return true;
                    if (__instance.Api.Side != EnumAppSide.Server) return true;

                    // Only apply to our boss
                    if (__instance.Code == null || !string.Equals(__instance.Code.ToShortString(), "alstory:bloodhand-clawchief", StringComparison.OrdinalIgnoreCase))
                    {
                        return true;
                    }

                    if (!__instance.Alive) return true;

                    // Don't interfere if stroke animations already active
                    if (__instance.AnimManager == null) return true;
                    if (__instance.AnimManager.IsAnimationActive("stroke-start", "stroke-idle", "stroke-end", "despair")) return true;

                    // Use private fields via reflection
                    var strokeActiveField = AccessTools.Field(typeof(EntityShiver), "strokeActive");
                    var aiTaskManagerField = AccessTools.Field(typeof(EntityShiver), "aiTaskManager");

                    if (strokeActiveField == null || aiTaskManagerField == null) return true;

                    bool strokeActive = (bool)strokeActiveField.GetValue(__instance);
                    if (strokeActive) return true;

                    if (!(__instance.Api.World.Rand.NextDouble() < TestStrokeChancePerTick))
                    {
                        return true;
                    }

                    strokeActiveField.SetValue(__instance, true);

                    var aiTaskManager = aiTaskManagerField.GetValue(__instance) as AiTaskManager;
                    aiTaskManager?.StopTasks();

                    __instance.AnimManager.StartAnimation("stroke-start");
                    __instance.World.PlaySoundAt(new Vintagestory.API.Common.AssetLocation("sounds/creature/shiver/shock"), __instance, null, randomizePitch: true, 16f);

                    __instance.Api.Event.RegisterCallback(_ =>
                    {
                        try
                        {
                            __instance.AnimManager.StartAnimation("stroke-idle");
                        }
                        catch { }
                    }, 666);

                    // Vanilla duration: (rand*3 + 3) * 1000 ms. Make it 2x longer.
                    int baseSeconds = (int)(__instance.Api.World.Rand.NextDouble() * 3.0 + 3.0);
                    int durationMs = baseSeconds * 1000 * 2;

                    __instance.Api.Event.RegisterCallback(_ =>
                    {
                        try
                        {
                            __instance.AnimManager.StopAnimation("stroke-idle");
                            __instance.AnimManager.StartAnimation("stroke-end");
                        }
                        catch { }

                        __instance.Api.Event.RegisterCallback(__ =>
                        {
                            try
                            {
                                strokeActiveField.SetValue(__instance, false);
                            }
                            catch { }
                        }, 1200);

                    }, durationMs);

                    // We handled the special case; skip vanilla OnGameTick to avoid double-trigger.
                    return false;
                }
                catch
                {
                    return true;
                }
            }
        }
    }
}
