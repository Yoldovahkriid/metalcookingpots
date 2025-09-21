using MetalPots.BlockEntityRenderer;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.GameContent;

namespace MetalPots.src.System.cooking
{
    internal class MPBlockCookingContainer: BlockCookingContainer, IInFirepitRendererSupplier
    {
        new public IInFirepitRenderer GetRendererWhenInFirepit(ItemStack stack, BlockEntityFirepit firepit, bool forOutputSlot)
        {
            return new MetalPotInFirepitRenderer(api as ICoreClientAPI, stack, firepit.Pos, forOutputSlot);
        }

        public override float GetMeltingDuration(IWorldAccessor world, ISlotProvider cookingSlotsProvider, ItemSlot inputSlot)
        {
            float duration = 0;

            ItemStack[] stacks = GetCookingStacks(cookingSlotsProvider, false);
            for (int i = 0; i < stacks.Length; i++)
            {
                var stack = stacks[i];
                int portionSize = stack.StackSize;

                if (stack.Collectible?.CombustibleProps == null)
                {
                    if (stack.Collectible?.Attributes?["waterTightContainerProps"].Exists == true)
                    {
                        var props = BlockLiquidContainerBase.GetContainableProps(stack);
                        portionSize = (int)(stack.StackSize / props?.ItemsPerLitre ?? 1);
                    }

                    duration += 10 * portionSize;
                    continue;
                }

                float singleDuration = stack.Collectible.GetMeltingDuration(world, cookingSlotsProvider, inputSlot);
                duration += singleDuration * portionSize / stack.Collectible.CombustibleProps.SmeltedRatio;
            }

            duration = Math.Max(20, duration / 3);

            return duration;
        }
        public override string GetHeldItemName(ItemStack itemStack)
        {
            string metal = itemStack.Collectible.Variant["metal"];
            if (itemStack.Collectible.Attributes.IsTrue("isDirtyPot"))
            {
                return Lang.Get("metalpots:dirtymetalpottemplate", Lang.Get("metalpots:metal-" + metal));
            }
            else
            {
                return Lang.Get("metalpots:metalpottemplate", Lang.Get("metalpots:metal-" + metal));
            }
        }
    }
}
