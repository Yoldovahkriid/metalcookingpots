using System.Collections.Generic;
using System.Linq;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.API.Util;


namespace Vintagestory.GameContent
{
    public class MPMealMeshCache : ModSystem
    {
        public override bool ShouldLoad(EnumAppSide forSide)
        {
            return forSide == EnumAppSide.Client;
        }

        ICoreClientAPI? capi;
        Block? mealtextureSourceBlock;

        public override void StartClientSide(ICoreClientAPI api)
        {
            base.StartClientSide(api);

            capi = api;

            api.Event.LeaveWorld += Event_LeaveWorld;
            api.Event.BlockTexturesLoaded += Event_BlockTexturesLoaded;
        }

        private void Event_BlockTexturesLoaded()
        {
            mealtextureSourceBlock = capi!.World.GetBlock(new AssetLocation("claypot-blue-cooked"));
        }

        public MultiTextureMeshRef? GetOrCreateMealInContainerMeshRef(Block containerBlock, CookingRecipe? forRecipe, ItemStack?[]? contentStacks, Vec3f? foodTranslate = null)
        {
            Dictionary<int, MultiTextureMeshRef> meshrefs;

            if (capi!.ObjectCache.TryGetValue("cookedMeshRefs", out object? obj))
            {
                meshrefs = obj as Dictionary<int, MultiTextureMeshRef> ?? [];
            }
            else
            {
                capi.ObjectCache["cookedMeshRefs"] = meshrefs = [];
            }

            if (contentStacks == null) return null;

            int mealhashcode = GetMealHashCode(containerBlock, contentStacks, foodTranslate);


            if (!meshrefs.TryGetValue(mealhashcode, out MultiTextureMeshRef? mealMeshRef))
            {
                MeshData mesh = GenMealInContainerMesh(containerBlock, forRecipe, contentStacks, foodTranslate);

                meshrefs[mealhashcode] = mealMeshRef = capi.Render.UploadMultiTextureMesh(mesh);
            }

            return mealMeshRef;
        }

        public MeshData GenMealInContainerMesh(Block containerBlock, CookingRecipe? forRecipe, ItemStack?[] contentStacks, Vec3f? foodTranslate = null)
        {
            CompositeShape cShape = containerBlock.Shape;
            var loc = cShape.Base.Clone().WithPathPrefixOnce("shapes/").WithPathAppendixOnce(".json");

            Shape shape = API.Common.Shape.TryGet(capi, loc);
            capi!.Tesselator.TesselateShape("meal", shape, out MeshData wholeMesh, capi.Tesselator.GetTextureSource(containerBlock), new Vec3f(cShape.rotateX, cShape.rotateY, cShape.rotateZ));

            if (GenMealMesh(forRecipe, contentStacks, foodTranslate) is MeshData mealMesh) wholeMesh.AddMeshData(mealMesh);

            return wholeMesh;
        }

        public MeshData? GenMealMesh(CookingRecipe? forRecipe, ItemStack?[] contentStacks, Vec3f? foodTranslate = null)
        {
            MealTextureSource source;
            try
            {
                source = new MealTextureSource(capi!, mealtextureSourceBlock!);
            }
            catch
            {
                capi!.Logger.Error("Unable to create meal texture source for recipe: " + forRecipe?.Code + " for: " + mealtextureSourceBlock?.Code.ToShortString());
                throw;
            }

            if (forRecipe != null && GenFoodMixMesh(contentStacks, forRecipe, foodTranslate) is MeshData foodMesh)
            {
                foodMesh.Scale(new Vec3f(0.5f, 0.5f, 0.5f), 2.0f, 2.0f, 2.0f);
                return foodMesh;
            }

            if (contentStacks != null && contentStacks.Length > 0)
            {
                bool rotten = ContentsRotten(contentStacks);
                if (rotten)
                {
                    Shape contentShape = API.Common.Shape.TryGet(capi, "shapes/block/food/meal/rot.json");

                    capi!.Tesselator.TesselateShape("rotcontents", contentShape, out MeshData contentMesh, source);

                    if (foodTranslate != null)
                    {
                        contentMesh.Translate(foodTranslate);
                    }
                    contentMesh.Scale(new Vec3f(0.5f, 0.5f, 0.5f), 2.0f, 2.0f, 2.0f);
                    return contentMesh;
                }
                else
                {
                    if (contentStacks[0]?.ItemAttributes?["inContainerTexture"] is JsonObject obj)
                    {
                        source.ForStack = contentStacks[0]!;

                        CompositeShape? cshape = contentStacks[0]!.ItemAttributes["inBowlShape"]?.AsObject(new CompositeShape() { Base = new("shapes/block/food/meal/pickled.json") });

                        Shape contentShape = API.Common.Shape.TryGet(capi, cshape?.Base.WithPathAppendixOnce(".json").WithPathPrefixOnce("shapes/"));
                        capi!.Tesselator.TesselateShape("picklednmealcontents", contentShape, out MeshData contentMesh, source);

                        return contentMesh;
                    }
                }
            }

            return null;
        }


        public static bool ContentsRotten(ItemStack?[] contentStacks)
        {
            for (int i = 0; i < contentStacks.Length; i++)
            {
                if (contentStacks[i]?.Collectible.Code.Path == "rot") return true;
            }
            return false;
        }
        public static bool ContentsRotten(InventoryBase inv)
        {
            foreach (var slot in inv)
            {
                if (slot.Itemstack?.Collectible.Code.Path == "rot") return true;
            }
            return false;
        }


        public MeshData? GenFoodMixMesh(ItemStack?[] contentStacks, CookingRecipe recipe, Vec3f? foodTranslate)
        {
            MeshData? mergedmesh = null;
            MealTextureSource texSource = new MealTextureSource(capi!, mealtextureSourceBlock!);

            var shapePath = recipe.Shape!.Base.Clone().WithPathPrefixOnce("shapes/").WithPathAppendixOnce(".json");

            bool rotten = ContentsRotten(contentStacks);
            if (rotten)
            {
                shapePath = new AssetLocation("shapes/block/food/meal/rot.json");
            }

            Shape shape = Shape.TryGet(capi, shapePath);
            Dictionary<CookingRecipeIngredient, int> usedIngredQuantities = new Dictionary<CookingRecipeIngredient, int>();

            if (rotten)
            {
                capi!.Tesselator.TesselateShape(
                    "mealpart", shape, out mergedmesh, texSource,
                    new Vec3f(recipe.Shape.rotateX, recipe.Shape.rotateY, recipe.Shape.rotateZ)
                );
            }
            else
            {
                HashSet<string> drawnMeshes = new HashSet<string>();

                for (int i = 0; i < contentStacks.Length; i++)
                {
                    texSource.ForStack = contentStacks[i];
                    CookingRecipeIngredient? ingred = recipe.GetIngrendientFor(
                        contentStacks[i],
                        usedIngredQuantities.Where(val => val.Key.MaxQuantity <= val.Value).Select(val => val.Key).ToArray()
                    );

                    if (ingred == null)
                    {
                        ingred = recipe.GetIngrendientFor(contentStacks[i]);
                    }
                    else
                    {
                        usedIngredQuantities.TryGetValue(ingred, out int cnt);
                        cnt++;
                        usedIngredQuantities[ingred] = cnt;
                    }

                    if (ingred == null) continue;


                    string[]? selectiveElements = null;

                    if (ingred.GetMatchingStack(contentStacks[i]) is not CookingRecipeStack recipestack) continue;

                    if (recipestack.ShapeElement != null) selectiveElements = [recipestack.ShapeElement];
                    texSource.customTextureMapping = recipestack.TextureMapping;

                    if (drawnMeshes.Contains(recipestack.ShapeElement + recipestack.TextureMapping)) continue;
                    drawnMeshes.Add(recipestack.ShapeElement + recipestack.TextureMapping);

                    capi!.Tesselator.TesselateShape(
                        "mealpart", shape, out MeshData meshpart, texSource,
                        new Vec3f(recipe.Shape.rotateX, recipe.Shape.rotateY, recipe.Shape.rotateZ), 0, 0, 0, null, selectiveElements
                    );

                    if (mergedmesh == null) mergedmesh = meshpart;
                    else mergedmesh.AddMeshData(meshpart);
                }

            }


            if (foodTranslate != null && mergedmesh != null) mergedmesh.Translate(foodTranslate);

            return mergedmesh;
        }





        private void Event_LeaveWorld()
        {
            if (capi == null) return;

            if (capi.ObjectCache.TryGetValue("cookedMeshRefs", out object? obj) && obj is Dictionary<int, MultiTextureMeshRef> meshrefs)
            {
                foreach (var val in meshrefs)
                {
                    val.Value.Dispose();
                }

                capi.ObjectCache.Remove("cookedMeshRefs");
            }
        }

        public int GetMealHashCode(ItemStack stack, Vec3f? translate = null, string extraKey = "")
        {
            if ((stack.Block as BlockContainer)?.GetContents(capi!.World, stack) is not ItemStack?[] contentStacks) return 0;

            if (stack.Block is BlockPie)
            {
                extraKey += "ct" + (BlockPie.GetTopCrustType(stack) ?? "full") + "-bl" + stack.Attributes.GetAsInt("bakeLevel", 0) + "-ps" + stack.Attributes.GetAsInt("pieSize");
            }

            return GetMealHashCode(stack.Block, contentStacks, translate, extraKey);
        }

        protected int GetMealHashCode(Block block, ItemStack?[] contentStacks, Vec3f? translate = null, string? extraKey = null)
        {
            string shapestring = block.Shape.ToString() + block.Code.ToShortString();
            if (translate != null) shapestring += translate.X + "/" + translate.Y + "/" + translate.Z;

            string contentstring = "";
            for (int i = 0; i < contentStacks.Length; i++)
            {
                if (contentStacks[i] == null) continue;

                if (contentStacks[i]!.Collectible.Code.Path == "rot")
                {
                    return (shapestring + "rotten").GetHashCode();
                }

                contentstring += contentStacks[i]!.Collectible.Code.ToShortString();
            }

            return (shapestring + contentstring + extraKey).GetHashCode();
        }


    }
}