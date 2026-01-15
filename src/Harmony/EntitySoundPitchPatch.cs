using HarmonyLib;
using System;
using System.Reflection;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;

namespace VsQuest.Harmony
{
    [HarmonyPatch]
    public static class EntitySoundPitchPatch
    {
        private const string WatchedAttrKey = "vsquest:soundPitchMul";
        private const string JsonAttrKey = "vsquestSoundPitchMul";

        private static float GetPitchMul(Entity entity)
        {
            if (entity == null) return 1f;

            try
            {
                float mul = entity.WatchedAttributes?.GetFloat(WatchedAttrKey, float.NaN) ?? float.NaN;
                if (!float.IsNaN(mul) && mul > 0f) return mul;

                if (entity.Properties?.Attributes != null)
                {
                    mul = entity.Properties.Attributes[JsonAttrKey].AsFloat(float.NaN);
                    if (!float.IsNaN(mul) && mul > 0f) return mul;
                }
            }
            catch
            {
            }

            return 1f;
        }

        private static float GetRandomPitch(object instance)
        {
            try
            {
                var m = AccessTools.Method(instance.GetType(), "RandomPitch");
                if (m == null) return 1f;
                object val = m.Invoke(instance, null);
                if (val is float f) return f;
            }
            catch
            {
            }

            return 1f;
        }

        private const string ClientMainTypeName = "Vintagestory.Client.NoObf.ClientMain";
        private const string ServerMainTypeName = "Vintagestory.Server.ServerMain";

        // Entity.PlayEntitySound uses coordinate-based World.PlaySoundAt(), which has no entity context.
        // Reroute to the entity-based overload so pitch multiplier can be applied.
        [HarmonyPatch(typeof(Entity), "PlayEntitySound")]
        public static class Entity_PlayEntitySound_RerouteToEntityOverload
        {
            public static bool Prefix(Entity __instance, string type, IPlayer dualCallByPlayer, bool randomizePitch, float range)
            {
                try
                {
                    if (__instance?.Properties?.ResolvedSounds == null) return true;

                    if (!__instance.Properties.ResolvedSounds.TryGetValue(type, out var locations) || locations == null || locations.Length == 0)
                    {
                        return false;
                    }

                    var loc = locations[__instance.World.Rand.Next(locations.Length)];
                    __instance.World.PlaySoundAt(loc, __instance, dualCallByPlayer, randomizePitch, range);
                    return false;
                }
                catch
                {
                    return true;
                }
            }
        }

        // Client side: has direct pitch parameter
        [HarmonyPatch]
        public static class ClientMain_PlaySoundAt_Entity_PitchMul
        {
            public static MethodBase TargetMethod()
            {
                var t = AccessTools.TypeByName(ClientMainTypeName);
                if (t == null) return null;

                return AccessTools.Method(t, "PlaySoundAt", new[]
                {
                    typeof(AssetLocation),
                    typeof(Entity),
                    typeof(IPlayer),
                    typeof(float),
                    typeof(float),
                    typeof(float)
                });
            }

            public static void Prefix(Entity atEntity, ref float pitch)
            {
                float mul = GetPitchMul(atEntity);
                if (mul != 1f)
                {
                    pitch *= mul;
                }
            }
        }

        // Client side: randomizePitch overload loses pitch, so we intercept and reroute to pitch overload.
        [HarmonyPatch]
        public static class ClientMain_PlaySoundAt_Entity_RandomizePitchMul
        {
            public static MethodBase TargetMethod()
            {
                var t = AccessTools.TypeByName(ClientMainTypeName);
                if (t == null) return null;

                return AccessTools.Method(t, "PlaySoundAt", new[]
                {
                    typeof(AssetLocation),
                    typeof(Entity),
                    typeof(IPlayer),
                    typeof(bool),
                    typeof(float),
                    typeof(float)
                });
            }

            public static bool Prefix(object __instance, AssetLocation location, Entity atEntity, IPlayer ignorePlayerUid, bool randomizePitch, float range, float volume)
            {
                if (atEntity == null) return true;

                float mul = GetPitchMul(atEntity);
                if (mul == 1f) return true;

                // Mirror original y-offset logic
                float yoff = 0f;
                if (atEntity.SelectionBox != null)
                {
                    yoff = atEntity.SelectionBox.Y2 / 2f;
                }
                else if (atEntity.Properties?.CollisionBoxSize != null)
                {
                    yoff = atEntity.Properties.CollisionBoxSize.Y / 2f;
                }

                float basePitch = randomizePitch ? GetRandomPitch(__instance) : 1f;

                // Call the pitch overload via reflection: PlaySoundAt(AssetLocation, Entity, IPlayer, float pitch, float range, float volume)
                var pitchOverload = AccessTools.Method(__instance.GetType(), "PlaySoundAt", new[]
                {
                    typeof(AssetLocation),
                    typeof(Entity),
                    typeof(IPlayer),
                    typeof(float),
                    typeof(float),
                    typeof(float)
                });

                pitchOverload?.Invoke(__instance, new object[] { location, atEntity, ignorePlayerUid, basePitch * mul, range, volume });
                return false;
            }
        }

        // Server side: randomizePitch overload loses pitch, so we intercept and reroute to pitch overload.
        [HarmonyPatch]
        public static class ServerMain_PlaySoundAt_Entity_RandomizePitchMul
        {
            public static MethodBase TargetMethod()
            {
                var t = AccessTools.TypeByName(ServerMainTypeName);
                if (t == null) return null;

                return AccessTools.Method(t, "PlaySoundAt", new[]
                {
                    typeof(AssetLocation),
                    typeof(Entity),
                    typeof(IPlayer),
                    typeof(bool),
                    typeof(float),
                    typeof(float)
                });
            }

            public static bool Prefix(object __instance, AssetLocation location, Entity entity, IPlayer dualCallByPlayer, bool randomizePitch, float range, float volume)
            {
                if (entity == null) return true;

                float mul = GetPitchMul(entity);
                if (mul == 1f) return true;

                // Mirror original y-offset logic
                float yoff = 0f;
                if (entity.SelectionBox != null)
                {
                    yoff = entity.SelectionBox.Y2 / 2f;
                }
                else if (entity.Properties?.CollisionBoxSize != null)
                {
                    yoff = entity.Properties.CollisionBoxSize.Y / 2f;
                }

                float basePitch = randomizePitch ? GetRandomPitch(__instance) : 1f;

                // Call overload: PlaySoundAt(AssetLocation, double x, double y, double z, IPlayer, float pitch, float range, float volume)
                var pitchOverload = AccessTools.Method(__instance.GetType(), "PlaySoundAt", new[]
                {
                    typeof(AssetLocation),
                    typeof(double),
                    typeof(double),
                    typeof(double),
                    typeof(IPlayer),
                    typeof(float),
                    typeof(float),
                    typeof(float)
                });

                pitchOverload?.Invoke(__instance, new object[] { location, entity.ServerPos.X, entity.ServerPos.InternalY + (double)yoff, entity.ServerPos.Z, dualCallByPlayer, basePitch * mul, range, volume });
                return false;
            }
        }

        // Server side: direct pitch overload
        [HarmonyPatch]
        public static class ServerMain_PlaySoundAt_Entity_PitchMul
        {
            public static MethodBase TargetMethod()
            {
                var t = AccessTools.TypeByName(ServerMainTypeName);
                if (t == null) return null;

                return AccessTools.Method(t, "PlaySoundAt", new[]
                {
                    typeof(AssetLocation),
                    typeof(Entity),
                    typeof(IPlayer),
                    typeof(float),
                    typeof(float),
                    typeof(float)
                });
            }

            public static void Prefix(Entity entity, ref float pitch)
            {
                float mul = GetPitchMul(entity);
                if (mul != 1f)
                {
                    pitch *= mul;
                }
            }
        }
    }
}
