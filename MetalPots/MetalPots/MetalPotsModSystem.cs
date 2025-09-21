using MetalPots.Blocks;
using MetalPots.src.Block;
using MetalPots.src.System.cooking;
using Vintagestory.API.Common;

namespace MetalPots
{
    public class MetalPotsModSystem : ModSystem
    {

        // Called on server and client
        // Useful for registering block/entity classes on both sides
        public override void Start(ICoreAPI api)
        {
            api.RegisterBlockClass( "MPBlockCookingContainer", typeof(MPBlockCookingContainer));
            api.RegisterBlockClass("MPBlockCookedContainer", typeof(MPBlockCookedContainer));
            api.RegisterBlockClass("MPBlockCrock", typeof(MPBlockCrock));
        }
    }
}
