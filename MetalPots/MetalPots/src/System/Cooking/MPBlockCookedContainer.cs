using MetalPots.BlockEntityRenderer;
using MetalPots.Systems;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.MathTools;
using Vintagestory.GameContent;

namespace MetalPots.Blocks
{
    internal class MPBlockCookedContainer : BlockCookedContainer, IInFirepitRendererSupplier, IContainedMeshSource, IContainedInteractable
    {
        MPMealMeshCache meshCache;

        public MPBlockCookedContainer()
        {
            this.yoff = 6.0f;
        }

        public override void OnLoaded(ICoreAPI api)
        {
            base.OnLoaded(api);
            if (api.Side == EnumAppSide.Client)
            {
                meshCache = api.ModLoader.GetModSystem<MPMealMeshCache>();
            }
        }

        public override void OnBeforeRender(ICoreClientAPI capi, ItemStack itemstack, EnumItemRenderTarget target, ref ItemRenderInfo renderinfo)
        {
            if (meshCache == null) meshCache = capi.ModLoader.GetModSystem<MPMealMeshCache>();

            CookingRecipe recipe = GetCookingRecipe(capi.World, itemstack);
            ItemStack[] contents = GetNonEmptyContents(capi.World, itemstack);

            // Note: We now pass itemstack at the end to help our new caching logic
            MultiTextureMeshRef meshref = meshCache.GetOrCreateMealInContainerMeshRef(this, recipe, contents, new Vec3f(0, yoff / 16f, 0), itemstack);
            if (meshref != null) renderinfo.ModelRef = meshref;
        }

        public override string GetMeshCacheKey(ItemSlot slot)
        {
            if (slot.Itemstack == null) return "empty";

            MealMeshCache baseCache = api.ModLoader.GetModSystem<MealMeshCache>();

            return baseCache.GetMealHashCode(slot.Itemstack, new Vec3f(0, yoff / 16f, 0)).ToString();
        }

        new public MeshData GenMesh(ItemSlot slot, ITextureAtlasAPI targetAtlas, BlockPos forBlockPos = null)
        {
            return meshCache.GenMealInContainerMesh(this, GetCookingRecipe(api.World, slot.Itemstack), GetNonEmptyContents(api.World, slot.Itemstack), new Vec3f(0, yoff / 16f, 0));
        }

        public override string GetHeldItemName(ItemStack itemStack)
        {
            ItemStack[] contentStacks = GetContents(api.World, itemStack);
            if (MealMeshCache.ContentsRotten(contentStacks)) // Use base logic
            {
                return Lang.Get("Pot of rotten food");
            }
            string metal = itemStack.Collectible.Variant["metal"];
            return Lang.Get("metalpots:cookedmetalpottemplate", Lang.Get("metalpots:metal-" + metal));
        }

        new public IInFirepitRenderer GetRendererWhenInFirepit(ItemStack stack, BlockEntityFirepit firepit, bool forOutputSlot)
        {
            return new MetalPotInFirepitRenderer(api as ICoreClientAPI, stack, firepit.Pos, forOutputSlot);
        }

        new public EnumFirepitModel GetDesiredFirepitModel(ItemStack stack, BlockEntityFirepit firepit, bool forOutputSlot)
        {
            return EnumFirepitModel.Wide;
        }
    }
}