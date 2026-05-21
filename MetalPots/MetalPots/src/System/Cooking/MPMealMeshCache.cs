using System.Collections.Generic;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;
using Vintagestory.API.Util;
using Vintagestory.GameContent;

namespace MetalPots.Systems
{
    public class MPMealMeshCache : ModSystem
    {
        ICoreClientAPI capi;
        MealMeshCache baseCache;

        public override bool ShouldLoad(EnumAppSide forSide) => forSide == EnumAppSide.Client;

        public override void StartClientSide(ICoreClientAPI api)
        {
            capi = api;
            baseCache = api.ModLoader.GetModSystem<MealMeshCache>();
            api.Event.LeaveWorld += Event_LeaveWorld;
        }

        public MultiTextureMeshRef GetOrCreateMealInContainerMeshRef(Block containerBlock, CookingRecipe forRecipe, ItemStack[] contentStacks, Vec3f foodTranslate, ItemStack potStack)
        {
            var meshrefs = ObjectCacheUtil.GetOrCreate(capi, "mpCookedMeshRefs", () => new Dictionary<int, MultiTextureMeshRef>());

            int mealhashcode = baseCache.GetMealHashCode(potStack, foodTranslate);

            if (!meshrefs.TryGetValue(mealhashcode, out MultiTextureMeshRef mealMeshRef))
            {
                MeshData mesh = GenMealInContainerMesh(containerBlock, forRecipe, contentStacks, foodTranslate);
                meshrefs[mealhashcode] = mealMeshRef = capi.Render.UploadMultiTextureMesh(mesh);
            }
            return mealMeshRef;
        }

        public MeshData GenMealInContainerMesh(Block containerBlock, CookingRecipe forRecipe, ItemStack[] contentStacks, Vec3f foodTranslate)
        {
            CompositeShape cShape = containerBlock.Shape;
            var loc = cShape.Base.Clone().WithPathPrefixOnce("shapes/").WithPathAppendixOnce(".json");
            Shape shape = Shape.TryGet(capi, loc);
            capi.Tesselator.TesselateShape("meal", shape, out MeshData wholeMesh, capi.Tesselator.GetTextureSource(containerBlock), new Vec3f(cShape.rotateX, cShape.rotateY, cShape.rotateZ));

            MeshData foodMesh = baseCache.GenMealMesh(forRecipe, contentStacks, foodTranslate);

            if (foodMesh != null)
            {
                foodMesh.Scale(new Vec3f(0.5f, 0.5f, 0.5f), 2.0f, 2.0f, 2.0f);
                wholeMesh.AddMeshData(foodMesh);
            }

            return wholeMesh;
        }

        private void Event_LeaveWorld()
        {
            if (capi?.ObjectCache.TryGetValue("mpCookedMeshRefs", out object obj) == true && obj is Dictionary<int, MultiTextureMeshRef> meshrefs)
            {
                foreach (var val in meshrefs.Values) val.Dispose();
                capi.ObjectCache.Remove("mpCookedMeshRefs");
            }
        }
    }
}