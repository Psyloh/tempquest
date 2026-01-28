using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.Server;

namespace VsQuest
{
    public class ActionItemHotbarEnforcer
    {
        private readonly ICoreServerAPI sapi;
        private readonly int maxSlotsPerTick;
        private readonly System.Collections.Generic.Dictionary<string, (string invKey, int slot)> cursorByPlayerUid = new System.Collections.Generic.Dictionary<string, (string invKey, int slot)>(System.StringComparer.Ordinal);

        public ActionItemHotbarEnforcer(ICoreServerAPI sapi)
        {
            this.sapi = sapi;

			int max = 64;
			try
			{
				var qs = sapi?.ModLoader?.GetModSystem<QuestSystem>();
				int cfgMax = qs?.CoreConfig?.ActionItems?.HotbarEnforcerMaxSlotsPerTick ?? 64;
				if (cfgMax > 0) max = cfgMax;
			}
			catch
			{
				max = 64;
			}

			maxSlotsPerTick = max;
        }

        public void Tick(float dt)
        {
            if (sapi == null) return;

            var players = sapi.World.AllOnlinePlayers;
            if (players == null || players.Length == 0) return;

            foreach (var p in players)
            {
                if (p is not IServerPlayer sp) continue;

                var invMgr = sp.InventoryManager;
                if (invMgr?.Inventories == null) continue;

                var hotbarInv = invMgr.GetHotbarInventory();
                if (hotbarInv == null) continue;

                string uid = sp.PlayerUID;
                if (string.IsNullOrWhiteSpace(uid)) continue;

                int scanned = 0;

                cursorByPlayerUid.TryGetValue(uid, out var cursor);
                string resumeInvKey = cursor.invKey;
                int resumeSlot = cursor.slot;
                if (resumeSlot < 0) resumeSlot = 0;

                bool resuming = !string.IsNullOrWhiteSpace(resumeInvKey);
                bool resumeInvReached = !resuming;

                // Find one blocked action item outside hotbar and move it into first free hotbar slot.
                foreach (var kvp in invMgr.Inventories)
                {
                    // If we have a resume inventory key, skip inventories until we reach it.
                    if (!resumeInvReached)
                    {
                        if (!string.Equals(kvp.Key, resumeInvKey, System.StringComparison.Ordinal))
                        {
                            continue;
                        }

                        resumeInvReached = true;
                    }

                    var inv = kvp.Value;

                    if (inv == null) continue;
                    if (inv == hotbarInv) continue;
                    if (inv.ClassName == GlobalConstants.creativeInvClassName) continue;

                    int slotCount;
                    try
                    {
                        slotCount = inv.Count;
                    }
                    catch
                    {
                        continue;
                    }

                    if (slotCount <= 0) continue;

                    int startSlot = (resuming && string.Equals(kvp.Key, resumeInvKey, System.StringComparison.Ordinal)) ? resumeSlot : 0;
                    if (startSlot < 0) startSlot = 0;
                    if (startSlot >= slotCount) startSlot = 0;

                    for (int i = 0; i < slotCount; i++)
                    {
                        if (i < startSlot) continue;

                        var sourceSlot = inv[i];
                        scanned++;
                        if (scanned >= maxSlotsPerTick)
                        {
                            cursorByPlayerUid[uid] = (kvp.Key, i + 1);
                            goto nextPlayer;
                        }

                        if (sourceSlot?.Empty != false) continue;

                        var stack = sourceSlot.Itemstack;
                        if (!ItemAttributeUtils.IsActionItemBlockedMove(stack))
                        {
                            continue;
                        }

                        ItemSlot freeHotbarSlot = null;
                        for (int j = 0; j < hotbarInv.Count; j++)
                        {
                            var hs = hotbarInv[j];
                            if (hs?.Empty == true)
                            {
                                freeHotbarSlot = hs;
                                break;
                            }
                        }

                        // No free hotbar slot => do nothing (never delete items), but stop scanning.
                        if (freeHotbarSlot == null)
                        {
                            cursorByPlayerUid.Remove(uid);
                            goto nextPlayer;
                        }

                        var op = new ItemStackMoveOperation(sapi.World, EnumMouseButton.Left, 0, EnumMergePriority.AutoMerge, sourceSlot.StackSize)
                        {
                            ActingPlayer = sp
                        };

                        sourceSlot.TryPutInto(freeHotbarSlot, ref op);
                        sourceSlot.MarkDirty();
                        freeHotbarSlot.MarkDirty();

                        // Only move one per tick per player to reduce churn
                        cursorByPlayerUid.Remove(uid);
                        goto nextPlayer;
                    }
                }

                // Finished full scan => reset cursor.
                cursorByPlayerUid.Remove(uid);

            nextPlayer:
                continue;
            }
        }
    }
}
