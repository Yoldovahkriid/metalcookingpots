using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.Server;
using Vintagestory.GameContent;

namespace MetalPots.src.Block
{
    internal class MPBlockCrock: BlockCrock
    {
        public override string GetHeldItemName(ItemStack itemStack)
        {
            string metal = itemStack.Collectible.Variant["metal"];
            return Lang.Get("metalpots:metalcrocktemplate", Lang.Get("metalpots:metal-" + metal));
        }

        public override void OnCreatedByCrafting(ItemSlot[] allInputslots, ItemSlot outputSlot, GridRecipe byRecipe)
        {
            if (outputSlot.Itemstack == null) return;

            for (int i = 0; i < allInputslots.Length; i++)
            {
                ItemSlot slot = allInputslots[i];
                if (slot.Itemstack?.Collectible is MPBlockCrock)
                {
                    outputSlot.Itemstack.Attributes = slot.Itemstack.Attributes.Clone();
                    outputSlot.Itemstack.Attributes.SetBool("sealed", true);
                }
            }
        }

        internal float GetServings(IWorldAccessor world, ItemStack byItemStack)
        {
            return (float)(byItemStack?.Attributes.GetDecimal("quantityServings") ?? 0);
        }
        public override int GetMergableQuantity(ItemStack sinkStack, ItemStack sourceStack, EnumMergePriority priority)
        {
            if (priority == EnumMergePriority.DirectMerge && sourceStack.ItemAttributes?["canSealMetalCrock"]?.AsBool() == true && IsFullAndUnsealed(sinkStack))
            {
                return 1;
            }
            return base.GetMergableQuantity(sinkStack, sourceStack, priority);
        }

        public override void TryMergeStacks(ItemStackMergeOperation op)
        {
            ItemSlot sourceSlot = op.SourceSlot;

            if (op.CurrentPriority == EnumMergePriority.DirectMerge && sourceSlot.Itemstack.ItemAttributes?["canSealMetalCrock"]?.AsBool() == true)
            {
                ItemSlot sinkSlot = op.SinkSlot;

                if (IsFullAndUnsealed(sinkSlot.Itemstack))
                {
                    sinkSlot.Itemstack.Attributes.SetBool("sealed", true);
                    op.MovedQuantity = 1;
                    sourceSlot.Itemstack.Item.DamageItem(api.World, op.ActingPlayer.Entity , sourceSlot);
                    sinkSlot.MarkDirty();
                }

                return;
            }
            if (op.SourceSlot.Itemstack!.Block is IBlockMealContainer || op.SourceSlot.Itemstack.Collectible.Attributes?.IsTrue("mealContainer") == true)
            {
                if (op.CurrentPriority != EnumMergePriority.DirectMerge)
                {
                    if (Math.Min(MaxStackSize - op.SinkSlot.Itemstack!.StackSize, op.SourceSlot.Itemstack.StackSize) > 0)
                        base.TryMergeStacks(op);
                    return;
                }

                ItemStack bufferStack = null;
                if (op.SourceSlot.Itemstack.StackSize > 1)
                {
                    bufferStack = op.SourceSlot.TakeOut(op.SourceSlot.Itemstack.StackSize - 1);
                }

                if (ServeIntoStack(op.SourceSlot, op.SinkSlot, op.World))
                {
                    // only do TryGive on server since it will sync to the client which otherwise causes duplicated ghost stacks
                    if (api is ICoreServerAPI && op.ActingPlayer?.Entity.TryGiveItemStack(bufferStack) == false)
                    {
                        op.World.SpawnItemEntity(bufferStack, op.ActingPlayer.Entity.Pos.AsBlockPos);
                    }
                }
                else
                {
                    DummySlot bufferSlot = new(bufferStack);
                    bufferSlot.TryPutInto(op.World, op.SourceSlot);
                    if (Math.Min(MaxStackSize - op.SinkSlot.Itemstack!.StackSize, op.SourceSlot.Itemstack.StackSize) > 0)
                        base.TryMergeStacks(op);
                }

                return;
            }
        }
        public override bool OnContainedInteractStart(BlockEntityContainer be, ItemSlot slot, IPlayer byPlayer, BlockSelection blockSel)
        {
            var handSlot = byPlayer.InventoryManager.ActiveHotbarSlot;
            if (handSlot.Itemstack?.Collectible.Attributes?["canSealMetalCrock"]?.AsBool() == true)
            {
                if (IsFullAndUnsealed(slot.Itemstack))
                {
                    slot.Itemstack.Attributes.SetBool("sealed", true);
                    handSlot.Itemstack.Item.DamageItem(api.World, byPlayer.Entity, handSlot);
                    handSlot.MarkDirty();
                }
                else (api as ICoreClientAPI)?.TriggerIngameError(this, "crockemptyorsealed", Lang.Get("ingameerror-crock-empty-or-sealed"));

                return true;
            }
            //Cannot access CookingContainerBase.OnContainedInteractStart() from here
            if (handSlot.Empty) return false;

            if ((handSlot.Itemstack.Collectible.Attributes?.IsTrue("mealContainer") == true || handSlot.Itemstack.Block is IBlockMealContainer) && GetServings(api.World, slot.Itemstack) > 0)
            {
                bool served = false;
                if (handSlot.StackSize > 1)
                {
                    handSlot = new DummySlot(handSlot.TakeOut(1));
                    byPlayer.InventoryManager.ActiveHotbarSlot.MarkDirty();
                    served = ServeIntoStack(handSlot, slot, api.World);
                    if (!byPlayer.InventoryManager.TryGiveItemstack(handSlot.Itemstack, true))
                    {
                        api.World.SpawnItemEntity(handSlot.Itemstack, byPlayer.Entity.ServerPos.XYZ);
                    }
                }
                else
                {
                    served = ServeIntoStack(handSlot, slot, api.World);
                }

                slot.MarkDirty();
                be.MarkDirty(true);
                return be is BlockEntityGroundStorage ? true : served;
            }



            return false;
        }
    }
}
