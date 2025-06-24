using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.GameContent;

namespace MetalPots.System.Cooking
{
    internal class MPMealMeshCache : ModSystem, ITexPositionSource
    {
        public override bool ShouldLoad(EnumAppSide forSide)
        {
            return forSide == EnumAppSide.Client;
        }

        ICoreClientAPI capi;
        Block mealtextureSourceBlock;

        AssetLocation[] pieShapeLocByFillLevel = new AssetLocation[]
        {
            new AssetLocation("game:block/food/pie/full-fill0"),
            new AssetLocation("game:block/food/pie/full-fill1"),
            new AssetLocation("game:block/food/pie/full-fill2"),
            new AssetLocation("game:block/food/pie/full-fill3"),
            new AssetLocation("game:block/food/pie/full-fill4"),
        };

        AssetLocation[] pieShapeBySize = new AssetLocation[]
        {
            new AssetLocation("game:block/food/pie/quarter"),
            new AssetLocation("game:block/food/pie/half"),
            new AssetLocation("game:block/food/pie/threefourths"),
            new AssetLocation("game:block/food/pie/full"),
        };

        #region Pie Stuff
        public Size2i AtlasSize => capi.BlockTextureAtlas.Size;
        protected Shape nowTesselatingShape;

        BlockPie nowTesselatingBlock;
        ItemStack[] contentStacks;
        AssetLocation crustTextureLoc;
        AssetLocation fillingTextureLoc;
        AssetLocation topCrustTextureLoc;

        public TextureAtlasPosition this[string textureCode]
        {
            get
            {
                AssetLocation texturePath = crustTextureLoc;
                if (textureCode == "filling") texturePath = fillingTextureLoc;
                if (textureCode == "topcrust")
                {
                    texturePath = topCrustTextureLoc;
                }

                if (texturePath == null)
                {
                    capi.World.Logger.Warning("Missing texture path for pie mesh texture code {0}, seems like a missing texture definition or invalid pie block.", textureCode);
                    return capi.BlockTextureAtlas.UnknownTexturePosition;
                }

                TextureAtlasPosition texpos = capi.BlockTextureAtlas[texturePath];

                if (texpos == null)
                {
                    IAsset texAsset = capi.Assets.TryGet(texturePath.Clone().WithPathPrefixOnce("textures/").WithPathAppendixOnce(".png"));
                    if (texAsset != null)
                    {
                        BitmapRef bmp = texAsset.ToBitmap(capi);
                        capi.BlockTextureAtlas.GetOrInsertTexture(texturePath, out _, out texpos, () => bmp);
                    }
                    else
                    {
                        capi.World.Logger.Warning("Pie mesh texture {1} not found.", nowTesselatingBlock.Code, texturePath);
                        texpos = capi.BlockTextureAtlas.UnknownTexturePosition;
                    }
                }


                return texpos;
            }
        }

        #endregion

        public override void StartClientSide(ICoreClientAPI api)
        {
            base.StartClientSide(api);

            capi = api;

            api.Event.LeaveWorld += Event_LeaveWorld;
            api.Event.BlockTexturesLoaded += Event_BlockTexturesLoaded;
        }

        private void Event_BlockTexturesLoaded()
        {
            mealtextureSourceBlock = capi.World.GetBlock(new AssetLocation("claypot-cooked"));
        }

        public override void Dispose()
        {
            if (capi != null && capi.ObjectCache.TryGetValue("pieMeshRefs", out var objPi) && objPi is Dictionary<int, MultiTextureMeshRef> meshRefs)
            {
                foreach (var (_, meshRef) in meshRefs)
                {
                    meshRef.Dispose();
                }
                capi.ObjectCache.Remove("pieMeshRefs");
            }
        }

        public MultiTextureMeshRef GetOrCreateMealInContainerMeshRef(Block containerBlock, CookingRecipe forRecipe, ItemStack[] contentStacks, Vec3f foodTranslate = null)
        {
            Dictionary<int, MultiTextureMeshRef> meshrefs;

            object obj;
            if (capi.ObjectCache.TryGetValue("cookedMeshRefs", out obj))
            {
                meshrefs = obj as Dictionary<int, MultiTextureMeshRef>;
            }
            else
            {
                capi.ObjectCache["cookedMeshRefs"] = meshrefs = new Dictionary<int, MultiTextureMeshRef>();
            }

            if (contentStacks == null) return null;

            int mealhashcode = GetMealHashCode(containerBlock, contentStacks, foodTranslate);

            MultiTextureMeshRef mealMeshRef;

            if (!meshrefs.TryGetValue(mealhashcode, out mealMeshRef))
            {
                MeshData mesh = GenMealInContainerMesh(containerBlock, forRecipe, contentStacks, foodTranslate);

                meshrefs[mealhashcode] = mealMeshRef = capi.Render.UploadMultiTextureMesh(mesh);
            }

            return mealMeshRef;
        }

        public MeshData GenMealInContainerMesh(Block containerBlock, CookingRecipe forRecipe, ItemStack[] contentStacks, Vec3f foodTranslate = null)
        {
            CompositeShape cShape = containerBlock.Shape;
            var loc = cShape.Base.Clone().WithPathPrefixOnce("shapes/").WithPathAppendixOnce(".json");

            Shape shape = Shape.TryGet(capi, loc);
            MeshData wholeMesh;
            capi.Tesselator.TesselateShape("meal", shape, out wholeMesh, capi.Tesselator.GetTextureSource(containerBlock), new Vec3f(cShape.rotateX, cShape.rotateY, cShape.rotateZ));

            MeshData mealMesh = GenMealMesh(forRecipe, contentStacks, foodTranslate);
            if (mealMesh != null)
            {
                wholeMesh.AddMeshData(mealMesh);
            }

            return wholeMesh;
        }

        public MeshData GenMealMesh(CookingRecipe forRecipe, ItemStack[] contentStacks, Vec3f foodTranslate = null)
        {
            MealTextureSource source = new MealTextureSource(capi, mealtextureSourceBlock);

            if (forRecipe != null)
            {
                MeshData foodMesh = GenFoodMixMesh(contentStacks, forRecipe, foodTranslate);
                if (foodMesh != null)
                {
                    foodMesh.Scale(new Vec3f(0.5f, 0.5f, 0.5f), 2.0f, 2.0f, 2.0f);
                    return foodMesh;
                }
            }

            if (contentStacks != null && contentStacks.Length > 0)
            {
                bool rotten = ContentsRotten(contentStacks);
                if (rotten)
                {
                    Shape contentShape = Shape.TryGet(capi, "game:shapes/block/food/meal/rot.json");

                    MeshData contentMesh;
                    capi.Tesselator.TesselateShape("rotcontents", contentShape, out contentMesh, source);

                    if (foodTranslate != null)
                    {
                        contentMesh.Translate(foodTranslate);
                    }

                    return contentMesh;
                }
                else
                {


                    JsonObject obj = contentStacks[0]?.ItemAttributes?["inContainerTexture"];
                    if (obj != null && obj.Exists)
                    {
                        source.ForStack = contentStacks[0];

                        CompositeShape cshape = contentStacks[0]?.ItemAttributes?["inBowlShape"].AsObject<CompositeShape>(new CompositeShape() { Base = new AssetLocation("game:shapes/block/food/meal/pickled.json") });

                        Shape contentShape = Shape.TryGet(capi, cshape.Base.WithPathAppendixOnce(".json").WithPathPrefixOnce("game:shapes/"));
                        MeshData contentMesh;
                        capi.Tesselator.TesselateShape("picklednmealcontents", contentShape, out contentMesh, source);

                        return contentMesh;
                    }
                }
            }

            return null;
        }


        public static bool ContentsRotten(ItemStack[] contentStacks)
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


        public MeshData GenFoodMixMesh(ItemStack[] contentStacks, CookingRecipe recipe, Vec3f foodTranslate)
        {
            MeshData mergedmesh = null;
            MealTextureSource texSource = new MealTextureSource(capi, mealtextureSourceBlock);

            var shapePath = recipe.Shape.Base.Clone().WithPathPrefixOnce("shapes/").WithPathAppendixOnce(".json");

            bool rotten = ContentsRotten(contentStacks);
            if (rotten)
            {
                shapePath = new AssetLocation("game:shapes/block/food/meal/rot.json");
            }

            Shape shape = Shape.TryGet(capi, shapePath);
            Dictionary<CookingRecipeIngredient, int> usedIngredQuantities = new Dictionary<CookingRecipeIngredient, int>();

            if (rotten)
            {
                capi.Tesselator.TesselateShape(
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
                    CookingRecipeIngredient ingred = recipe.GetIngrendientFor(
                        contentStacks[i],
                        usedIngredQuantities.Where(val => val.Key.MaxQuantity <= val.Value).Select(val => val.Key).ToArray()
                    );

                    if (ingred == null)
                    {
                        ingred = recipe.GetIngrendientFor(contentStacks[i]);
                    }
                    else
                    {
                        int cnt = 0;
                        usedIngredQuantities.TryGetValue(ingred, out cnt);
                        cnt++;
                        usedIngredQuantities[ingred] = cnt;
                    }

                    if (ingred == null) continue;


                    MeshData meshpart;
                    string[] selectiveElements = null;

                    CookingRecipeStack recipestack = ingred.GetMatchingStack(contentStacks[i]);

                    if (recipestack.ShapeElement != null) selectiveElements = new string[] { recipestack.ShapeElement };
                    texSource.customTextureMapping = recipestack.TextureMapping;

                    if (drawnMeshes.Contains(recipestack.ShapeElement + recipestack.TextureMapping)) continue;
                    drawnMeshes.Add(recipestack.ShapeElement + recipestack.TextureMapping);

                    capi.Tesselator.TesselateShape(
                        "mealpart", shape, out meshpart, texSource,
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

            object obj;
            if (capi.ObjectCache.TryGetValue("cookedMeshRefs", out obj))
            {
                Dictionary<int, MultiTextureMeshRef> meshrefs = obj as Dictionary<int, MultiTextureMeshRef>;

                foreach (var val in meshrefs)
                {
                    val.Value.Dispose();
                }

                capi.ObjectCache.Remove("cookedMeshRefs");
            }
        }

        public int GetMealHashCode(ItemStack stack, Vec3f translate = null, string extraKey = "")
        {
            ItemStack[] contentStacks = (stack.Block as BlockContainer).GetContents(capi.World, stack);

            if (stack.Block is BlockPie)
            {
                extraKey += "ct" + stack.Attributes.GetAsInt("topCrustType") + "-bl" + stack.Attributes.GetAsInt("bakeLevel", 0) + "-ps" + stack.Attributes.GetAsInt("pieSize");
            }

            return GetMealHashCode(stack.Block, contentStacks, translate, extraKey);
        }

        protected int GetMealHashCode(Block block, ItemStack[] contentStacks, Vec3f translate = null, string extraKey = null)
        {
            string shapestring = block.Shape.ToString() + block.Code.ToShortString();
            if (translate != null) shapestring += translate.X + "/" + translate.Y + "/" + translate.Z;

            string contentstring = "";
            for (int i = 0; i < contentStacks.Length; i++)
            {
                if (contentStacks[i] == null) continue;

                if (contentStacks[i].Collectible.Code.Path == "rot")
                {
                    return (shapestring + "rotten").GetHashCode();
                }

                contentstring += contentStacks[i].Collectible.Code.ToShortString();
            }

            return (shapestring + contentstring + extraKey).GetHashCode();
        }
    }
}
