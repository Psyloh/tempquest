using Vintagestory.API.Client;
using Vintagestory.API.Common;

namespace VsQuest
{
    public class ActionItemInputHandler
    {
        private readonly ICoreClientAPI capi;
        private readonly IClientNetworkChannel clientChannel;
        private readonly ActionItemAttributeResolver attributeResolver;
        private readonly ActionItemCastController castController;

        public ActionItemInputHandler(
            ICoreClientAPI capi,
            IClientNetworkChannel clientChannel,
            ActionItemAttributeResolver attributeResolver,
            ActionItemCastController castController)
        {
            this.capi = capi;
            this.clientChannel = clientChannel;
            this.attributeResolver = attributeResolver;
            this.castController = castController;
        }

        public void OnMouseDown(MouseEvent args)
        {
            if (args.Button != EnumMouseButton.Right) return;
            if (capi?.World?.Player?.InventoryManager == null) return;
            if (attributeResolver == null) return;

            var slot = capi.World.Player.InventoryManager.ActiveHotbarSlot;
            if (slot?.Itemstack == null) return;

            if (!attributeResolver.EnsureActionItemAttributes(slot)) return;

            var attributes = slot.Itemstack.Attributes;
            if (attributes == null) return;

            if (attributes.GetBool(ItemAttributeUtils.ActionItemTriggerOnInvAddKey, false))
            {
                return;
            }

            if (!attributeResolver.TryGetActionItemActionsFromAttributes(attributes, out var actions, out string sourceQuestId))
            {
                return;
            }

            if (castController != null && castController.TryHandleMouseDown(args))
            {
                return;
            }

            args.Handled = true;
            clientChannel.SendPacket(new ExecuteActionItemPacket());
        }

        public void OnMouseUp(MouseEvent args)
        {
            if (args.Button != EnumMouseButton.Right) return;
            castController?.HandleMouseUp(args);
        }

        public void OnClientTick(float dt)
        {
            castController?.HandleClientTick(dt);
        }
    }
}
