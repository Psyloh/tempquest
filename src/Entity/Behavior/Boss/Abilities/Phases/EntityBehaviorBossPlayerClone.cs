using System;
using System.Collections.Generic;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;
using Vintagestory.GameContent;

namespace VsQuest
{
    public class EntityBehaviorBossPlayerClone : EntityBehavior
    {
        private const string CloneFlagKey = "alegacyvsquest:bossplayerclone";
        private const string CloneOwnerIdKey = "alegacyvsquest:bossplayerclone:ownerid";
        private const string ClonePlayerUidKey = "alegacyvsquest:bossplayerclone:playeruid";
        private const string ClonePlayerNameKey = "alegacyvsquest:bossplayerclone:playername";

        private const int HandSlotRight = 15;
        private const int HandSlotLeft = 16;

        private ICoreServerAPI sapi;
        private string cloneEntityCode;
        private float cloneRange;
        private int checkIntervalMs;

        private long lastCheckMs;
        private readonly Dictionary<string, long> cloneByPlayerUid = new Dictionary<string, long>(StringComparer.OrdinalIgnoreCase);

        public EntityBehaviorBossPlayerClone(Entity entity) : base(entity)
        {
        }

        public override string PropertyName() => "bossplayerclone";

        public override void Initialize(EntityProperties properties, JsonObject attributes)
        {
            base.Initialize(properties, attributes);
            sapi = entity?.Api as ICoreServerAPI;

            cloneEntityCode = attributes["cloneEntityCode"].AsString(null);
            cloneRange = attributes["cloneRange"].AsFloat(50f);
            checkIntervalMs = attributes["checkIntervalMs"].AsInt(500);

            if (checkIntervalMs < 100) checkIntervalMs = 100;
        }

        public override void OnGameTick(float dt)
        {
            base.OnGameTick(dt);

            if (sapi == null || entity == null) return;

            if (IsCloneEntity())
            {
                SyncClone();
                return;
            }

            if (!entity.Alive)
            {
                CleanupClones();
                return;
            }

            long nowMs = sapi.World.ElapsedMilliseconds;
            if (nowMs - lastCheckMs < checkIntervalMs) return;
            lastCheckMs = nowMs;

            UpdateClones();
        }

        public override void OnEntityReceiveDamage(DamageSource damageSource, ref float damage)
        {
            base.OnEntityReceiveDamage(damageSource, ref damage);

            if (!IsCloneEntity()) return;
            if (damage <= 0f) return;

            var owner = GetCloneOwner();
            if (owner == null || !owner.Alive) return;

            try
            {
                owner.ReceiveDamage(damageSource, damage);
                damage = 0f;
            }
            catch
            {
            }
        }

        public override void OnEntityDeath(DamageSource damageSourceForDeath)
        {
            if (!IsCloneEntity())
            {
                CleanupClones();
            }

            base.OnEntityDeath(damageSourceForDeath);
        }

        public override void OnEntityDespawn(EntityDespawnData despawn)
        {
            if (!IsCloneEntity())
            {
                CleanupClones();
            }

            base.OnEntityDespawn(despawn);
        }

        private void UpdateClones()
        {
            if (string.IsNullOrWhiteSpace(cloneEntityCode)) return;

            var players = sapi.World.AllOnlinePlayers;
            if (players == null) return;

            var aliveUids = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var player in players)
            {
                var plrEntity = player?.Entity;
                if (plrEntity == null || !plrEntity.Alive) continue;
                if (plrEntity.ServerPos.Dimension != entity.ServerPos.Dimension) continue;

                double distSq = plrEntity.ServerPos.DistanceTo(entity.ServerPos);
                if (distSq > cloneRange) continue;

                string uid = player.PlayerUID;
                if (string.IsNullOrWhiteSpace(uid)) continue;

                aliveUids.Add(uid);

                if (!cloneByPlayerUid.TryGetValue(uid, out var cloneId) || cloneId <= 0)
                {
                    SpawnCloneFor(player);
                    continue;
                }

                var cloneEntity = sapi.World.GetEntityById(cloneId);
                if (cloneEntity == null || !cloneEntity.Alive)
                {
                    cloneByPlayerUid.Remove(uid);
                    SpawnCloneFor(player);
                }
            }

            if (cloneByPlayerUid.Count == 0) return;

            var toRemove = new List<string>();
            foreach (var pair in cloneByPlayerUid)
            {
                if (aliveUids.Contains(pair.Key)) continue;

                var clone = sapi.World.GetEntityById(pair.Value);
                if (clone != null)
                {
                    try
                    {
                        sapi.World.DespawnEntity(clone, new EntityDespawnData { Reason = EnumDespawnReason.Removed });
                    }
                    catch
                    {
                    }
                }

                toRemove.Add(pair.Key);
            }

            foreach (var key in toRemove)
            {
                cloneByPlayerUid.Remove(key);
            }
        }

        private void SpawnCloneFor(IPlayer player)
        {
            if (player?.Entity == null || sapi == null || entity == null) return;

            var type = sapi.World.GetEntityType(new AssetLocation(cloneEntityCode));
            if (type == null) return;

            Entity clone = null;
            try
            {
                clone = sapi.World.ClassRegistry.CreateEntity(type);
                if (clone == null) return;

                ApplyCloneFlags(clone, player);

                var spawnPos = GetSpawnPositionNear(player.Entity.ServerPos.XYZ);
                int dim = entity.ServerPos.Dimension;
                clone.ServerPos.SetPosWithDimension(new Vec3d(spawnPos.X, spawnPos.Y + dim * 32768.0, spawnPos.Z));
                clone.ServerPos.Yaw = player.Entity.ServerPos.Yaw;
                clone.Pos.SetFrom(clone.ServerPos);

                sapi.World.SpawnEntity(clone);

                // Important: many behaviors (including seraphinventory) finish initialization during SpawnEntity.
                // Copying inventory/appearance before SpawnEntity can silently do nothing.
                sapi.Event.EnqueueMainThreadTask(() =>
                {
                    try
                    {
                        if (clone == null || !clone.Alive) return;

                        CopyAppearanceAttributes(player, clone);
                        CopyPlayerInventory(player, clone);
                        clone.MarkShapeModified();
                    }
                    catch
                    {
                    }
                }, "bossplayerclone-copy");

                cloneByPlayerUid[player.PlayerUID] = clone.EntityId;
            }
            catch
            {
                if (clone != null)
                {
                    try
                    {
                        sapi.World.DespawnEntity(clone, new EntityDespawnData { Reason = EnumDespawnReason.Removed });
                    }
                    catch
                    {
                    }
                }
            }
        }

        private Vec3d GetSpawnPositionNear(Vec3d basePos)
        {
            if (basePos == null) return entity.ServerPos.XYZ.Clone();

            double angle = sapi.World.Rand.NextDouble() * Math.PI * 2.0;
            double dist = 1.5 + sapi.World.Rand.NextDouble() * 1.5;
            return new Vec3d(basePos.X + Math.Cos(angle) * dist, basePos.Y, basePos.Z + Math.Sin(angle) * dist);
        }

        private void ApplyCloneFlags(Entity clone, IPlayer player)
        {
            if (clone?.WatchedAttributes == null) return;

            try
            {
                clone.WatchedAttributes.SetBool(CloneFlagKey, true);
                clone.WatchedAttributes.SetLong(CloneOwnerIdKey, entity.EntityId);
                clone.WatchedAttributes.SetString(ClonePlayerUidKey, player.PlayerUID ?? string.Empty);
                clone.WatchedAttributes.SetString(ClonePlayerNameKey, player.PlayerName ?? string.Empty);

                clone.WatchedAttributes.SetBool("showHealthbar", false);

                clone.WatchedAttributes.MarkPathDirty(CloneFlagKey);
                clone.WatchedAttributes.MarkPathDirty(CloneOwnerIdKey);
                clone.WatchedAttributes.MarkPathDirty(ClonePlayerUidKey);
                clone.WatchedAttributes.MarkPathDirty(ClonePlayerNameKey);
                clone.WatchedAttributes.MarkPathDirty("showHealthbar");
            }
            catch
            {
            }

            try
            {
                var tag = clone.WatchedAttributes.GetTreeAttribute("nametag") ?? new TreeAttribute();
                tag.SetString("name", player.PlayerName ?? "");
                clone.WatchedAttributes.SetAttribute("nametag", tag);
                clone.WatchedAttributes.MarkPathDirty("nametag");
            }
            catch
            {
            }

            CopyAppearanceAttributes(player, clone);
        }

        private void CopyAppearanceAttributes(IPlayer player, Entity clone)
        {
            if (player?.Entity == null || clone?.WatchedAttributes == null) return;

            TryCopyTreeAttribute(player.Entity, clone, "wearablesInv");
            TryCopyTreeAttribute(player.Entity, clone, "skinConfig");
            TryCopyTreeAttribute(player.Entity, clone, "skinParts");
            TryCopyTreeAttribute(player.Entity, clone, "skinnableParts");
        }

        private void TryCopyTreeAttribute(Entity source, Entity target, string key)
        {
            if (source?.WatchedAttributes == null || target?.WatchedAttributes == null) return;

            try
            {
                var tree = source.WatchedAttributes.GetTreeAttribute(key);
                if (tree == null) return;

                target.WatchedAttributes.SetAttribute(key, tree.Clone());
                target.WatchedAttributes.MarkPathDirty(key);
            }
            catch
            {
            }
        }

        private void CopyPlayerInventory(IPlayer player, Entity clone)
        {
            if (player?.Entity == null || clone == null) return;

            try
            {
                var invBehavior = clone.GetBehavior<EntityBehaviorSeraphInventory>();
                if (invBehavior?.Inventory == null) return;

                var targetInv = invBehavior.Inventory;
                var sourceInv = player.InventoryManager?.GetOwnInventory("character");

                if (sourceInv != null)
                {
                    int count = Math.Min(targetInv.Count, sourceInv.Count);
                    for (int i = 0; i < count; i++)
                    {
                        var sourceSlot = sourceInv[i];
                        var targetSlot = targetInv[i];
                        if (targetSlot == null) continue;

                        try
                        {
                            if (sourceSlot?.Itemstack != null)
                            {
                                targetSlot.Itemstack = sourceSlot.Itemstack.Clone();
                            }
                            else
                            {
                                targetSlot.Itemstack = null;
                            }
                            targetSlot.MarkDirty();
                        }
                        catch
                        {
                        }
                    }
                }

                TrySetHandItem(targetInv, HandSlotRight, player.Entity.RightHandItemSlot);
                TrySetHandItem(targetInv, HandSlotLeft, player.Entity.LeftHandItemSlot);
            }
            catch
            {
            }
        }

        private void TrySetHandItem(InventoryBase targetInv, int slotIndex, ItemSlot sourceSlot)
        {
            if (targetInv == null) return;
            if (slotIndex < 0 || slotIndex >= targetInv.Count) return;

            var targetSlot = targetInv[slotIndex];
            if (targetSlot == null) return;

            try
            {
                if (sourceSlot?.Itemstack != null)
                {
                    targetSlot.Itemstack = sourceSlot.Itemstack.Clone();
                }
                else
                {
                    targetSlot.Itemstack = null;
                }
                targetSlot.MarkDirty();
            }
            catch
            {
            }
        }

        private void SyncClone()
        {
            var owner = GetCloneOwner();
            if (owner == null || !owner.Alive)
            {
                DespawnClone();
                return;
            }

            try
            {
                if (BossBehaviorUtils.TryGetHealth(owner, out var healthTree, out float cur, out float max))
                {
                    if (BossBehaviorUtils.TryGetHealth(entity, out var cloneTree, out float cloneCur, out float cloneMax))
                    {
                        if (cloneTree != null)
                        {
                            cloneTree.SetFloat("maxhealth", max);
                            cloneTree.SetFloat("currenthealth", cur);
                            entity.WatchedAttributes.MarkPathDirty("health");
                        }
                    }
                }
            }
            catch
            {
            }
        }

        private Entity GetCloneOwner()
        {
            try
            {
                long ownerId = entity?.WatchedAttributes?.GetLong(CloneOwnerIdKey, 0) ?? 0;
                if (ownerId <= 0 || sapi == null) return null;
                return sapi.World.GetEntityById(ownerId);
            }
            catch
            {
                return null;
            }
        }

        private bool IsCloneEntity()
        {
            try
            {
                return entity?.WatchedAttributes?.GetBool(CloneFlagKey, false) ?? false;
            }
            catch
            {
                return false;
            }
        }

        private void DespawnClone()
        {
            if (sapi == null || entity == null) return;

            try
            {
                sapi.World.DespawnEntity(entity, new EntityDespawnData { Reason = EnumDespawnReason.Removed });
            }
            catch
            {
            }
        }

        private void CleanupClones()
        {
            if (sapi == null) return;

            foreach (var pair in cloneByPlayerUid)
            {
                if (pair.Value <= 0) continue;

                try
                {
                    var clone = sapi.World.GetEntityById(pair.Value);
                    if (clone != null)
                    {
                        sapi.World.DespawnEntity(clone, new EntityDespawnData { Reason = EnumDespawnReason.Removed });
                    }
                }
                catch
                {
                }
            }

            cloneByPlayerUid.Clear();
        }
    }
}
