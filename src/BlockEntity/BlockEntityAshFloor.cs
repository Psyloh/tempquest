using System;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;

namespace VsQuest
{
    public class BlockEntityAshFloor : BlockEntity
    {
        private const string AttrDespawnAtMs = "vsquest:ashfloor:despawnAtMs";
        private const string AttrOwnerId = "vsquest:ashfloor:ownerId";
        private const string AttrTickIntervalMs = "vsquest:ashfloor:tickIntervalMs";
        private const string AttrDamage = "vsquest:ashfloor:damage";
        private const string AttrDamageTier = "vsquest:ashfloor:damageTier";
        private const string AttrDamageType = "vsquest:ashfloor:damageType";
        private const string AttrVictimWalkSpeedMult = "vsquest:ashfloor:victimWalkSpeedMult";
        private const string AttrDisableJump = "vsquest:ashfloor:disableJump";
        private const string AttrDisableShift = "vsquest:ashfloor:disableShift";

        private const string VictimUntilKey = "alegacyvsquest:ashfloor:until";
        private const string VictimWalkSpeedMultKey = "alegacyvsquest:ashfloor:walkspeedmult";
        private const string VictimNoJumpUntilKey = "alegacyvsquest:ashfloor:nojumpuntil";
        private const string VictimNoShiftUntilKey = "alegacyvsquest:ashfloor:noshiftuntil";

        private long despawnAtMs;
        private long ownerId;
        private int tickIntervalMs;

        private float damage;
        private int damageTier;
        private string damageType;

        private float victimWalkSpeedMult;
        private bool disableJump;
        private bool disableShift;

        private bool ticking;
        private long nextTickAtMs;

        public override void Initialize(ICoreAPI api)
        {
            base.Initialize(api);

            if (Api?.Side != EnumAppSide.Server) return;

            if (!ticking)
            {
                ticking = true;
                RegisterGameTickListener(OnServerTick, 100);
            }
        }

        public override void FromTreeAttributes(ITreeAttribute tree, IWorldAccessor worldAccessForResolve)
        {
            base.FromTreeAttributes(tree, worldAccessForResolve);

            despawnAtMs = tree.GetLong(AttrDespawnAtMs, despawnAtMs);
            ownerId = tree.GetLong(AttrOwnerId, ownerId);
            tickIntervalMs = tree.GetInt(AttrTickIntervalMs, tickIntervalMs);

            damage = tree.GetFloat(AttrDamage, damage);
            damageTier = tree.GetInt(AttrDamageTier, damageTier);
            damageType = tree.GetString(AttrDamageType, damageType);

            victimWalkSpeedMult = tree.GetFloat(AttrVictimWalkSpeedMult, victimWalkSpeedMult);
            disableJump = tree.GetBool(AttrDisableJump, disableJump);
            disableShift = tree.GetBool(AttrDisableShift, disableShift);
        }

        public override void ToTreeAttributes(ITreeAttribute tree)
        {
            base.ToTreeAttributes(tree);

            tree.SetLong(AttrDespawnAtMs, despawnAtMs);
            tree.SetLong(AttrOwnerId, ownerId);
            tree.SetInt(AttrTickIntervalMs, tickIntervalMs);

            tree.SetFloat(AttrDamage, damage);
            tree.SetInt(AttrDamageTier, damageTier);
            if (!string.IsNullOrWhiteSpace(damageType)) tree.SetString(AttrDamageType, damageType);

            tree.SetFloat(AttrVictimWalkSpeedMult, victimWalkSpeedMult);
            tree.SetBool(AttrDisableJump, disableJump);
            tree.SetBool(AttrDisableShift, disableShift);
        }

        public void Arm(long ownerId, long despawnAtMs, int tickIntervalMs, float damage, int damageTier, string damageType, float victimWalkSpeedMult, bool disableJump, bool disableShift)
        {
            if (Api == null) return;

            this.ownerId = ownerId;
            this.despawnAtMs = despawnAtMs;
            this.tickIntervalMs = tickIntervalMs;

            this.damage = damage;
            this.damageTier = damageTier;
            this.damageType = damageType;

            this.victimWalkSpeedMult = victimWalkSpeedMult;
            this.disableJump = disableJump;
            this.disableShift = disableShift;

            nextTickAtMs = 0;
            MarkDirty(true);
        }

        private void OnServerTick(float dt)
        {
            if (Api?.Side != EnumAppSide.Server) return;
            if (Pos == null) return;

            var sapi = Api as ICoreServerAPI;
            if (sapi == null) return;

            long now = sapi.World.ElapsedMilliseconds;

            if (despawnAtMs > 0 && now >= despawnAtMs)
            {
                TryRemoveSelf(sapi);
                return;
            }

            int interval = Math.Max(50, tickIntervalMs <= 0 ? 350 : tickIntervalMs);
            if (nextTickAtMs != 0 && now < nextTickAtMs) return;
            nextTickAtMs = now + interval;

            try
            {
                int dim = Pos.dimension;
                var center = new Vec3d(Pos.X + 0.5, Pos.Y + 0.5 + dim * 32768.0, Pos.Z + 0.5);
                var entities = sapi.World.GetEntitiesAround(center, 2f, 2f, e => e is EntityPlayer);
                if (entities == null || entities.Length == 0) return;

                for (int i = 0; i < entities.Length; i++)
                {
                    if (entities[i] is not EntityPlayer plr) continue;
                    if (!plr.Alive) continue;
                    if (plr.ServerPos.Dimension != Pos.dimension) continue;

                    if (!IsPlayerOnThisBlock(sapi, plr)) continue;

                    ApplyVictimDebuffs(sapi, plr, interval);
                    DealDamage(sapi, plr);
                }
            }
            catch
            {
            }
        }

        private bool IsPlayerOnThisBlock(ICoreServerAPI sapi, EntityPlayer player)
        {
            if (sapi == null || player?.Pos == null || Pos == null) return false;

            int dim;
            int x;
            int y;
            int z;
            try
            {
                dim = player.ServerPos.Dimension;
                x = (int)Math.Floor(player.ServerPos.X);
                y = (int)Math.Floor(player.ServerPos.Y);
                z = (int)Math.Floor(player.ServerPos.Z);
            }
            catch
            {
                return false;
            }

            if (dim != Pos.dimension) return false;
            if (x != Pos.X || z != Pos.Z) return false;

            try
            {
                var myBlock = sapi.World.BlockAccessor.GetBlock(Pos);
                if (myBlock == null) return false;

                int[] ys = new int[] { y, y - 1, y + 1 };
                for (int i = 0; i < ys.Length; i++)
                {
                    if (ys[i] != Pos.Y) continue;

                    var at = sapi.World.BlockAccessor.GetBlock(Pos);
                    if (at != null && at.BlockId == myBlock.BlockId)
                    {
                        return true;
                    }
                }
            }
            catch
            {
            }

            return false;
        }

        private void ApplyVictimDebuffs(ICoreServerAPI sapi, EntityPlayer player, int intervalMs)
        {
            if (sapi == null || player?.WatchedAttributes == null) return;

            long now = sapi.World.ElapsedMilliseconds;
            long until = now + Math.Max(100, intervalMs * 2);

            try
            {
                long prev = player.WatchedAttributes.GetLong(VictimUntilKey, 0);
                if (prev != until)
                {
                    player.WatchedAttributes.SetLong(VictimUntilKey, until);
                    player.WatchedAttributes.MarkPathDirty(VictimUntilKey);
                }
            }
            catch
            {
            }

            try
            {
                float mult = GameMath.Clamp(victimWalkSpeedMult <= 0f ? 0.35f : victimWalkSpeedMult, 0f, 1f);
                float prev = player.WatchedAttributes.GetFloat(VictimWalkSpeedMultKey, float.NaN);
                if (float.IsNaN(prev) || Math.Abs(prev - mult) > 0.0001f)
                {
                    player.WatchedAttributes.SetFloat(VictimWalkSpeedMultKey, mult);
                    player.WatchedAttributes.MarkPathDirty(VictimWalkSpeedMultKey);
                }
            }
            catch
            {
            }

            if (disableJump)
            {
                try
                {
                    long prev = player.WatchedAttributes.GetLong(VictimNoJumpUntilKey, 0);
                    if (prev != until)
                    {
                        player.WatchedAttributes.SetLong(VictimNoJumpUntilKey, until);
                        player.WatchedAttributes.MarkPathDirty(VictimNoJumpUntilKey);
                    }
                }
                catch
                {
                }
            }

            if (disableShift)
            {
                try
                {
                    long prev = player.WatchedAttributes.GetLong(VictimNoShiftUntilKey, 0);
                    if (prev != until)
                    {
                        player.WatchedAttributes.SetLong(VictimNoShiftUntilKey, until);
                        player.WatchedAttributes.MarkPathDirty(VictimNoShiftUntilKey);
                    }
                }
                catch
                {
                }
            }
        }

        private void DealDamage(ICoreServerAPI sapi, EntityPlayer player)
        {
            if (sapi == null || player == null) return;
            if (damage <= 0f) return;

            EnumDamageType dmgType = EnumDamageType.Acid;
            try
            {
                if (!string.IsNullOrWhiteSpace(damageType) && Enum.TryParse(damageType, ignoreCase: true, out EnumDamageType parsed))
                {
                    dmgType = parsed;
                }
            }
            catch
            {
            }

            Entity owner = null;
            try
            {
                if (ownerId > 0)
                {
                    owner = sapi.World.GetEntityById(ownerId);
                }
            }
            catch
            {
                owner = null;
            }

            try
            {
                var src = new DamageSource()
                {
                    Source = owner != null ? EnumDamageSource.Entity : EnumDamageSource.Block,
                    SourceEntity = owner,
                    SourceBlock = owner == null ? sapi.World.BlockAccessor.GetBlock(Pos) : null,
                    SourcePos = new Vec3d(Pos.X + 0.5, Pos.Y + 0.1, Pos.Z + 0.5),
                    Type = dmgType,
                    DamageTier = Math.Max(0, damageTier),
                    KnockbackStrength = 0f
                };

                player.ReceiveDamage(src, damage);
            }
            catch
            {
            }
        }

        private void TryRemoveSelf(ICoreServerAPI sapi)
        {
            if (sapi?.World?.BlockAccessor == null || Pos == null) return;

            try
            {
                var myBlock = sapi.World.BlockAccessor.GetBlock(Pos);
                if (myBlock?.EntityClass == null) return;

                var expectedClass = sapi.World.ClassRegistry.GetBlockEntityClass(typeof(BlockEntityAshFloor));
                if (expectedClass == null) return;

                if (myBlock.EntityClass != expectedClass) return;

                sapi.World.BlockAccessor.SetBlock(0, Pos);
                sapi.World.BlockAccessor.RemoveBlockEntity(Pos);
            }
            catch
            {
            }
        }
    }
}
