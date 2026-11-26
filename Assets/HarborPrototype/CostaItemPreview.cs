using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace CostaLivre
{
    internal sealed class CostaItemPreview
    {
        private static readonly Vector3 Stage = new(0, -4800, 0);
        private const int Layer = 31;
        private readonly HarborBlockout art;
        private readonly Transform anchor;
        private Transform stage;
        private Camera camera;
        private RenderTexture target;
        private readonly Transform[] models = new Transform[CostaBag.Catalog.Length];
        private Transform item;
        private int showing = -1;

        internal CostaItemPreview(HarborBlockout art, Transform anchor)
        {
            this.art = art;
            this.anchor = anchor;
        }

        internal Texture Render(int index, float angle)
        {
            if (stage == null)
                Create();
            if (!stage.gameObject.activeSelf)
                stage.gameObject.SetActive(true);
            if (index != showing)
            {
                if (item != null)
                    item.gameObject.SetActive(false);
                showing = index;
                if (index >= 0)
                {
                    item = models[index] ??= Build(index);
                    item.gameObject.SetActive(true);
                }
                else
                    item = null;
            }
            if (item != null)
                item.localRotation = Quaternion.Euler(0, angle, 0);
            return target;
        }

        private void Create()
        {
            stage = new GameObject("Inventory preview stage").transform;
            stage.SetParent(anchor, false);
            stage.position = Stage;
            target = new RenderTexture(256, 256, 16, RenderTextureFormat.ARGB32)
                { name = "Inventory preview surface", antiAliasing = 1 };
            target.Create();
            camera = new GameObject("Inventory camera").AddComponent<Camera>();
            camera.transform.SetParent(stage, false);
            camera.transform.localPosition = new Vector3(1.2f, 1.13f, -3.0f);
            camera.transform.rotation = Quaternion.LookRotation(stage.position + Vector3.up * .12f - camera.transform.position);
            camera.cullingMask = 1 << Layer;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(.057f,.103f,.114f,1);
            camera.nearClipPlane = .1f;
            camera.farClipPlane = 8;
            camera.fieldOfView = 28;
            camera.depth = -20;
            camera.allowHDR = false;
            camera.allowMSAA = false;
            camera.useOcclusionCulling = false;
            camera.targetTexture = target;
            var cameraData = camera.GetUniversalAdditionalCameraData();
            cameraData.renderPostProcessing = false;
            cameraData.renderShadows = false;
            cameraData.volumeLayerMask = 0;
            AddLight(new Vector3(-1.0f, 1.4f, -1.5f), new Color(1,.81f,.60f), 7);
            AddLight(new Vector3(1f,.64f,1f), new Color(.44f,.78f,1), 4.1f);
        }

        private void AddLight(Vector3 position, Color color, float power)
        {
            Light lamp = new GameObject("Preview light").AddComponent<Light>();
            lamp.transform.SetParent(stage,false);
            lamp.transform.localPosition = position;
            lamp.type = LightType.Point;
            lamp.range = 4;
            lamp.intensity = power;
            lamp.color = color;
            lamp.cullingMask = 1 << Layer;
        }

        private Transform Build(int index)
        {
            var model = new GameObject("Inspected cargo").transform;
            model.SetParent(stage, false);
            Color bronze = new(.65f,.46f,.22f), black = new(.14f,.19f,.20f), ivory = new(.81f,.74f,.56f);
            switch (index)
            {
                case 0:
                    Piece("Watch band", new(0,0,0), new(.33f,.10f,1.15f), bronze);
                    Piece("Watch movement", new(0,.07f,0), new(.53f,.07f,.53f), bronze, PrimitiveType.Cylinder);
                    Piece("Watch face", new(0,.15f,0), new(.39f,.014f,.39f), black, PrimitiveType.Cylinder);
                    Piece("Watch hands", new(.012f,.174f,.04f), new(.017f,.009f,.22f), ivory).transform.localRotation = Quaternion.Euler(0,29,0);
                    break;
                case 1:
                    for (int i = -1; i <= 1; i++)
                    {
                        var fork = Piece("Cutlery handle", new(i*.30f,0,.12f), new(.11f,.043f,.75f), ivory);
                        fork.transform.localRotation = Quaternion.Euler(0,i*8,0);
                        Piece("Cutlery bowl", new(i*.30f,.018f,-.35f), new(.26f,.075f,.35f), ivory, PrimitiveType.Sphere);
                    }
                    break;
                case 2:
                    Piece("Picture frame", new(0,.21f,0), new(.98f,1.17f,.11f), bronze);
                    Piece("Canvas", new(0,.21f,-.066f), new(.81f,.97f,.023f), new(.26f,.47f,.46f));
                    for (int i=0;i<3;i++)
                        Piece("Painted skyline", new((i-1)*.23f,.05f+i*.10f,-.081f),new(.24f,.49f-i*.04f,.009f),
                            Color.Lerp(new Color(.63f,.31f,.22f),ivory,i*.24f));
                    break;
                case 3:
                    Piece("Ceramic vessel", new(0,.03f,0), new(.73f,.43f,.73f), bronze, PrimitiveType.Capsule);
                    Piece("Ceramic neck", new(0,.55f,0), new(.41f,.072f,.41f), ivory, PrimitiveType.Cylinder);
                    break;
                case 4:
                case 5:
                    Color stone = index == 4 ? new Color(.49f,.81f,.86f) : new Color(.66f,.094f,.19f);
                    for (int i = -1; i <= 1; i++)
                    {
                        var gem = art.FinishedModel(model, "Cut gemstone", stone, HarborBlockout.Finish.Glass,
                            new(-.24f,.006f,-.018f,.02f),new(-.08f,.21f,-.10f,.16f),new(.13f,.18f,-.08f,.115f),new(.25f,.006f,-.01f,.016f));
                        gem.localPosition = new Vector3(i*.30f, .16f, i == 0 ? -.14f : .12f);
                        gem.localRotation = Quaternion.Euler(15f, i*28f, i*12f);
                        gem.gameObject.layer = Layer;
                    }
                    break;
                case 6:
                    Piece("Notebook base", new(0,0,.10f), new(1.12f,.085f,.72f), black);
                    Piece("Notebook lid", new(0,.32f,.43f), new(1.1f,.63f,.065f), black)
                        .transform.localRotation = Quaternion.Euler(12,0,0);
                    Piece("Keyboard", new(0,.048f,.087f), new(.84f,.015f,.42f), ivory);
                    break;
                default:
                    Piece("Graphics board", new(0,0,0), new(1.08f,.088f,.51f), black);
                    for (int i=-1;i<=1;i+=2)
                        Piece("Graphics fan", new(i*.26f,.07f,0),new(.32f,.038f,.32f),bronze,PrimitiveType.Cylinder);
                    break;
            }
            return model;

            GameObject Piece(string name, Vector3 at, Vector3 size, Color color, PrimitiveType type = PrimitiveType.Cube)
            {
                GameObject shape = art.Part(model,type,name,at,size,color);
                shape.layer = Layer;
                return shape;
            }
        }

        internal void Hide()
        {
            if (stage != null)
                stage.gameObject.SetActive(false);
        }

        internal void Release()
        {
            if (stage != null)
                Object.Destroy(stage.gameObject);
            if (target != null)
            {
                target.Release();
                Object.Destroy(target);
            }
        }
    }
}
