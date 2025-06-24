using MetalPots.Blocks;
using MetalPots.src.Block;
using MetalPots.System.Cooking;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.Server;

namespace MetalPots
{
    public class MetalPotsModSystem : ModSystem
    {

        // Called on server and client
        // Useful for registering block/entity classes on both sides
        public override void Start(ICoreAPI api)
        {
            api.RegisterBlockClass(Mod.Info.ModID + ".MPBlockCookingContainer", typeof(MPBlockCookingContainers));
            api.RegisterBlockClass(Mod.Info.ModID + ".MPBlockCookedContainer", typeof(MPBlockCookedContainer));
            api.RegisterBlockClass(Mod.Info.ModID + ".MPXSkillBlockCookingContainer", typeof(MPXSkillBlockCookingContainer));
            api.RegisterBlockClass(Mod.Info.ModID + ".MPBlockCrock", typeof(MPBlockCrock));
        }
    }
}
