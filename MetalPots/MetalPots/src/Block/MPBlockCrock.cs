using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.GameContent;

namespace MetalPots.src.Block
{
    internal class MPBlockCrock : BlockCrock
    {
        public override string GetHeldItemName(ItemStack itemStack)
        {

            string metal = itemStack.Collectible.Variant["metal"];
            return Lang.Get("metalpots:metalcrocktemplate", Lang.Get("metalpots:metal-" + metal));
            
        }
    }
}
