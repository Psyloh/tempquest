using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.Server;

namespace VsQuest
{
    public class ActionItemHotbarEnforcer
    {
        private readonly ICoreServerAPI sapi;

        public ActionItemHotbarEnforcer(ICoreServerAPI sapi)
        {
            this.sapi = sapi;
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

                bool foundOutside = false;

                // Find one blocked action item outside hotbar and move it into first free hotbar slot.
                foreach (var kvp in invMgr.Inventories)
                {
                    var inv = kvp.Value;

                    if (inv == null) continue;
                    if (inv == hotbarInv) continue;
                    if (inv.ClassName == GlobalConstants.creativeInvClassName) continue;

                    for (int i = 0; i < inv.Count; i++)
                    {
                        var sourceSlot = inv[i];
                        if (sourceSlot?.Empty != false) continue;

                        var stack = sourceSlot.Itemstack;
                        if (!ItemAttributeUtils.IsActionItemBlockedMove(stack)) continue;

                        foundOutside = true;

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

                        // No free hotbar slot => do nothing (never delete items).
                        if (freeHotbarSlot == null) break;

                        var op = new ItemStackMoveOperation(sapi.World, EnumMouseButton.Left, 0, EnumMergePriority.AutoMerge, sourceSlot.StackSize)
                        {
                            ActingPlayer = sp
                        };

                        sourceSlot.TryPutInto(freeHotbarSlot, ref op);
                        sourceSlot.MarkDirty();
                        freeHotbarSlot.MarkDirty();

                        // Only move one per tick per player to reduce churn
                        foundOutside = false;
                        break;
                    }

                    if (!foundOutside)
                    {
                        // either nothing found, or we moved one and want to stop scanning inventories for this player
                        break;
                    }
                }
            }
        }
    }
}
