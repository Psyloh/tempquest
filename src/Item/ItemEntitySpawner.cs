using System;
using System.Collections.Generic;
using System.Text;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.MathTools;
using Vintagestory.API.Util;

namespace VsQuest
{
    public class ItemEntitySpawner : Item, ITexPositionSource
    {
        private ICoreClientAPI capi;

        private EntityProperties nowTesselatingEntityType;

        public Size2i AtlasSize => capi.ItemTextureAtlas.Size;

        public TextureAtlasPosition this[string textureCode]
        {
            get
            {
                if (nowTesselatingEntityType?.Client == null) return null;

                nowTesselatingEntityType.Client.Textures.TryGetValue(textureCode, out var cTex);
                AssetLocation texPath = null;
                if (cTex == null)
                {
                    nowTesselatingEntityType.Client.LoadedShape?.Textures?.TryGetValue(textureCode, out texPath);
                }
                else
                {
                    texPath = cTex.Base;
                }

                if (texPath != null)
                {
                    capi.ItemTextureAtlas.GetOrInsertTexture(texPath, out var _, out var texPos);
                    return texPos;
                }

                return null;
            }
        }

        public override void OnLoaded(ICoreAPI api)
        {
            base.OnLoaded(api);
            capi = api as ICoreClientAPI;
        }

        public override void OnUnloaded(ICoreAPI api)
        {
            var dict = ObjectCacheUtil.TryGet<Dictionary<string, MultiTextureMeshRef>>(api, "vsquest-entityspawner-entityicon-meshes");
            if (dict != null)
            {
                foreach (var value in dict.Values)
                {
                    value?.Dispose();
                }
            }

            base.OnUnloaded(api);
        }

        public override void OnBeforeRender(ICoreClientAPI capi, ItemStack itemstack, EnumItemRenderTarget target, ref ItemRenderInfo renderinfo)
        {
            if (target == EnumItemRenderTarget.Gui)
            {
                string code = itemstack?.Attributes?.GetString("type");
                if (!string.IsNullOrWhiteSpace(code))
                {
                    var meshrefs = ObjectCacheUtil.GetOrCreate(capi, "vsquest-entityspawner-entityicon-meshes", () => new Dictionary<string, MultiTextureMeshRef>());
                    if (!meshrefs.TryGetValue(code, out var meshref) || meshref == null)
                    {
                        EntityProperties type = (nowTesselatingEntityType = api.World.GetEntityType(new AssetLocation(code)));
                        if (type?.Client?.LoadedShape != null)
                        {
                            capi.Tesselator.TesselateShape("vsquest-entityspawner-entityicon", type.Client.LoadedShape, out var meshdata, this, null, 0, 0, 0);

                            ModelTransform tf = type.Attributes?["guiTransform"]?.AsObject<ModelTransform>();
                            if (tf == null)
                            {
                                tf = new ModelTransform
                                {
                                    Translation = new Vec3f(0f, -0.2f, 0f),
                                    Rotation = new Vec3f(0f, 180f, 0f),
                                    Origin = new Vec3f(0.5f, 0f, 0.5f),
                                    Rotate = true,
                                    ScaleXYZ = new FastVec3f(1f, 1f, 1f)
                                };
                            }

                            // Scale down tall entities for GUI so they don't clip. Keep it safe even if ScaleXYZ is unset.
                            float h = type.CollisionBoxSize.Y;
                            float heightScale = 1f / Math.Max(1f, h);
                            float baseScale = tf.ScaleXYZ.X;
                            if (baseScale <= 0f) baseScale = 1f;
                            float newScale = baseScale * heightScale;

                            tf = new ModelTransform
                            {
                                Translation = tf.Translation,
                                Rotation = tf.Rotation,
                                Origin = tf.Origin,
                                Rotate = tf.Rotate,
                                ScaleXYZ = new FastVec3f(newScale, newScale, newScale)
                            };

                            meshdata.ModelTransform(tf);

                            meshref = (meshrefs[code] = capi.Render.UploadMultiTextureMesh(meshdata));
                        }
                    }

                    if (meshref != null)
                    {
                        renderinfo.ModelRef = meshref;

                        // Important: entityspawner.json guiTransform is tuned for the wrench model.
                        // When we render an entity mesh, ensure the item guiTransform doesn't push it out of view.
                        renderinfo.Transform = new ModelTransform
                        {
                            Origin = new Vec3f(0.5f, 0.5f, 0.5f),
                            Translation = new Vec3f(0f, 0f, 0f),
                            Rotation = new Vec3f(0f, 0f, 0f),
                            ScaleXYZ = new FastVec3f(1f, 1f, 1f),
                            Rotate = true
                        };
                    }
                }
            }

            base.OnBeforeRender(capi, itemstack, target, ref renderinfo);
        }

        public override string GetHeldItemName(ItemStack itemStack)
        {
            string typeCode = itemStack?.Attributes?.GetString("type");
            if (string.IsNullOrWhiteSpace(typeCode))
            {
                return base.GetHeldItemName(itemStack);
            }

            string name = GetEntityTypeDisplayName(typeCode);
            if (!string.IsNullOrWhiteSpace(name))
            {
                return name;
            }

            return typeCode;
        }

        public override void GetHeldItemInfo(ItemSlot inSlot, StringBuilder dsc, IWorldAccessor world, bool withDebugInfo)
        {
            base.GetHeldItemInfo(inSlot, dsc, world, withDebugInfo);

            string typeCode = inSlot?.Itemstack?.Attributes?.GetString("type");
            if (string.IsNullOrWhiteSpace(typeCode)) return;

            string name = GetEntityTypeDisplayName(typeCode);
            if (!string.IsNullOrWhiteSpace(name))
            {
                dsc.AppendLine(name);
            }
        }

        private static string GetEntityTypeDisplayName(string typeCode)
        {
            if (string.IsNullOrWhiteSpace(typeCode)) return null;

            string key = "entity-" + typeCode;
            string name = LocalizationUtils.GetSafe(key);
            if (name != key) return name;

            int idx = typeCode.IndexOf(':');
            if (idx > 0 && idx + 1 < typeCode.Length)
            {
                string path = typeCode.Substring(idx + 1);
                key = "entity-" + path;
                name = LocalizationUtils.GetSafe(key);
                if (name != key) return name;
            }

            return null;
        }

        public override void OnHeldInteractStart(ItemSlot slot, EntityAgent byEntity, BlockSelection blockSel, EntitySelection entitySel, bool firstEvent, ref EnumHandHandling handling)
        {
            if (blockSel == null || byEntity?.World == null)
            {
                return;
            }

            string typeCode = slot?.Itemstack?.Attributes?.GetString("type");
            if (string.IsNullOrWhiteSpace(typeCode))
            {
                return;
            }

            IPlayer player = byEntity.World.PlayerByUid((byEntity as EntityPlayer)?.PlayerUID);
            if (player == null)
            {
                return;
            }

            if (!byEntity.World.Claims.TryAccess(player, blockSel.Position, EnumBlockAccessFlags.BuildOrBreak))
            {
                return;
            }

            if (byEntity.World.Side == EnumAppSide.Client)
            {
                handling = EnumHandHandling.PreventDefaultAction;
                return;
            }

            if (player.WorldData.CurrentGameMode != EnumGameMode.Creative)
            {
                slot.TakeOut(1);
                slot.MarkDirty();
            }

            var typeLocation = new AssetLocation(typeCode);
            EntityProperties type = byEntity.World.GetEntityType(typeLocation);
            if (type == null)
            {
                byEntity.World.Logger.Error("[vsquest] ItemEntitySpawner: No such entity - {0}", typeLocation);
                return;
            }

            Entity entity = byEntity.World.ClassRegistry.CreateEntity(type);
            if (entity == null)
            {
                return;
            }

            entity.ServerPos.X = (float)(blockSel.Position.X + ((!blockSel.DidOffset) ? blockSel.Face.Normali.X : 0)) + 0.5f;
            entity.ServerPos.Y = blockSel.Position.Y + ((!blockSel.DidOffset) ? blockSel.Face.Normali.Y : 0);
            entity.ServerPos.Z = (float)(blockSel.Position.Z + ((!blockSel.DidOffset) ? blockSel.Face.Normali.Z : 0)) + 0.5f;
            entity.ServerPos.Yaw = byEntity.Pos.Yaw + (float)Math.PI;
            entity.ServerPos.Dimension = blockSel.Position.dimension;
            entity.Pos.SetFrom(entity.ServerPos);
            entity.PositionBeforeFalling.Set(entity.ServerPos.X, entity.ServerPos.Y, entity.ServerPos.Z);
            entity.Attributes.SetString("origin", "playerplaced");

            byEntity.World.SpawnEntity(entity);
            handling = EnumHandHandling.PreventDefaultAction;
        }
    }
}
