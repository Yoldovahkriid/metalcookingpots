using System;
using System.Collections.Generic;
using System.Diagnostics.Metrics;
using System.Text;
using MetalPots.BlockEntityRenderer;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.MathTools;
using Vintagestory.API.Util;
using Vintagestory.GameContent;

namespace MetalPots.Blocks
{
    internal class MPBlockCookedContainer : BlockCookedContainer, IInFirepitRendererSupplier, IContainedMeshSource, IContainedInteractable
    {
        new public static SimpleParticleProperties smokeHeld;
        new public static SimpleParticleProperties foodSparks;

        WorldInteraction[] interactions;


        public override void OnLoaded(ICoreAPI api)
        {
            base.OnLoaded(api);

            if (api.Side != EnumAppSide.Client) return;
            ICoreClientAPI capi = api as ICoreClientAPI;

            meshCache = api.ModLoader.GetModSystem<MPMealMeshCache>();

            interactions = ObjectCacheUtil.GetOrCreate(api, "cookedContainerBlockInteractions", () =>
            {
                List<ItemStack> fillableStacklist = new List<ItemStack>();

                foreach (CollectibleObject obj in api.World.Collectibles)
                {
                    if (obj.Attributes?.IsTrue("mealContainer") == true)
                    {
                        List<ItemStack> stacks = obj.GetHandBookStacks(capi);
                        if (stacks != null) fillableStacklist.AddRange(stacks);
                    }
                }

                return new WorldInteraction[] {
                    new WorldInteraction()
                    {
                        ActionLangCode = "blockhelp-cookedcontainer-takefood",
                        HotKeyCode = null,
                        MouseButton = EnumMouseButton.Right,
                        Itemstacks = fillableStacklist.ToArray()
                    },
                    new WorldInteraction()
                    {
                        ActionLangCode = "blockhelp-cookedcontainer-pickup",
                        HotKeyCode = null,
                        MouseButton = EnumMouseButton.Right
                    }
                };
            });
        }

        static MPBlockCookedContainer()
        {
            smokeHeld = new SimpleParticleProperties(
                1, 1,
                ColorUtil.ToRgba(50, 220, 220, 220),
                new Vec3d(),
                new Vec3d(),
                new Vec3f(-0.05f, 0.1f, -0.05f),
                new Vec3f(0.05f, 0.15f, 0.05f),
                1.5f,
                0,
                0.25f,
                0.35f,
                EnumParticleModel.Quad
            );
            smokeHeld.SelfPropelled = true;
            smokeHeld.AddPos.Set(0.1, 0.1, 0.1);


            foodSparks = new SimpleParticleProperties(
                1, 1,
                ColorUtil.ToRgba(255, 83, 233, 255),
                new Vec3d(), new Vec3d(),
                new Vec3f(-3f, 1f, -3f),
                new Vec3f(3f, 8f, 3f),
                0.5f,
                1f,
                0.25f, 0.25f
            );
            foodSparks.VertexFlags = 0;
        }

        MPMealMeshCache meshCache;
        new float yoff = 6.0f;

        public override void OnBeforeRender(ICoreClientAPI capi, ItemStack itemstack, EnumItemRenderTarget target, ref ItemRenderInfo renderinfo)
        {
            if (meshCache == null) meshCache = capi.ModLoader.GetModSystem<MPMealMeshCache>();

            CookingRecipe recipe = GetCookingRecipe(capi.World, itemstack);
            ItemStack[] contents = GetNonEmptyContents(capi.World, itemstack);

            MultiTextureMeshRef meshref = meshCache.GetOrCreateMealInContainerMeshRef(this, recipe, contents, new Vec3f(0, yoff / 16f, 0));
            if (meshref != null) renderinfo.ModelRef = meshref;
        }


        new public virtual string GetMeshCacheKey(ItemStack itemstack)
        {
            return "" + meshCache.GetMealHashCode(itemstack);
        }


        new public MeshData GenMesh(ItemStack itemstack, ITextureAtlasAPI targetAtlas, BlockPos forBlockPos = null)
        {
            return meshCache.GenMealInContainerMesh(this, GetCookingRecipe(api.World, itemstack), GetNonEmptyContents(api.World, itemstack), new Vec3f(0, yoff / 16f, 0));
        }

        public override string GetHeldItemName(ItemStack itemStack)
        {
            ItemStack[] contentStacks = GetContents(api.World, itemStack);
            if (MPMealMeshCache.ContentsRotten(contentStacks))
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