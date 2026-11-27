using System.Collections.Generic;
using UnityEngine;
using static CostaLivre.CostaBuildTuning;
using BuildPart = CostaLivre.CostaBuildTuning.Part;

namespace CostaLivre
{
    internal sealed partial class CostaVehicle
    {
        private long appliedBuild;
        private bool customizable;
        private HarborBlockout workshopArt;
        private Color originalPaint;
        private readonly List<(Renderer Mesh, Vector3 Scale, BuildPart Part)> styling = new();
        private Transform addOns;
        private readonly MaterialPropertyBlock paintBlock = new();
        private static readonly int BaseColor = Shader.PropertyToID("_BaseColor");
        private static readonly int Smoothness = Shader.PropertyToID("_Smoothness");
        private static readonly int Metallic = Shader.PropertyToID("_Metallic");
        private static readonly int Coat = Shader.PropertyToID("_ClearCoatMask");
        internal long Build => appliedBuild;
        internal Style VisualStyle => style;
        internal Color OriginalPaint => originalPaint;
        internal bool Customizable => customizable;

        private void SetupCustomization(HarborBlockout art, bool allowed, Color color, Renderer[] renderers)
        {
            customizable = allowed;
            if (!allowed) return;
            workshopArt = art;
            originalPaint = color;
            foreach (var renderer in renderers)
            {
                BuildPart? part = renderer.name switch
                {
                    "Sculpted body" or "Wedge body" or "Roof" or "Swept roof" or "Door skin" or "Tank and fairing" => BuildPart.Paint,
                    "Alloy rim" or "Brake rotor" => BuildPart.Wheels,
                    "Tire" => BuildPart.Tires,
                    "Deformable front trim" or "Deformable rear trim" => BuildPart.Bumper,
                    "Headlight" or "Headlamp" => BuildPart.Lights,
                    "Mirror" => BuildPart.Mirrors,
                    _ => null
                };
                if (part.HasValue) styling.Add((renderer,renderer.transform.localScale,part.Value));
            }
        }

        internal void ApplyBuild(long build)
        {
            if (!customizable || build == appliedBuild || !Valid(build))
                return;
            appliedBuild = build;
            int paint = Option(build,BuildPart.Paint);
            int rgb = Option(build,BuildPart.Color);
            Color bodyColor = rgb == 0 ? originalPaint : new Color32((byte)(rgb>>16),(byte)(rgb>>8),(byte)rgb,255);
            if (paint == 3)
                bodyColor = Color.Lerp(bodyColor,new Color(.71f,.62f,.88f),.24f);
            foreach (var target in styling)
            {
                int level = Option(build,target.Part);
                Transform mesh = target.Mesh.transform;
                mesh.localScale = target.Scale;
                target.Mesh.GetPropertyBlock(paintBlock);
                switch (target.Part)
                {
                    case BuildPart.Paint:
                        paintBlock.SetColor(BaseColor,bodyColor);
                        paintBlock.SetFloat(Metallic,paint == 1 || paint == 3 ? .78f : .16f);
                        paintBlock.SetFloat(Smoothness,paint == 2 ? .16f : paint == 0 ? .64f : .88f);
                        paintBlock.SetFloat(Coat,paint == 3 ? 1 : 0);
                        break;
                    case BuildPart.Tires: mesh.localScale = target.Scale * (1 + level*.028f); break;
                    case BuildPart.Wheels:
                        mesh.localScale = target.Scale * (1+level*.09f);
                        paintBlock.SetColor(BaseColor,level switch { 1 => new(.25f,.24f,.22f),2 => new(.61f,.44f,.23f),
                            3 => new(.69f,.74f,.79f), _ => target.Mesh.sharedMaterial.GetColor(BaseColor) });
                        break;
                    case BuildPart.Bumper:
                        mesh.localScale = Vector3.Scale(target.Scale,new Vector3(1 + level*.035f,1 + level*.30f,1+level*.22f));
                        paintBlock.SetColor(BaseColor,level == 0 ? bodyColor : new(.15f,.19f,.20f));
                        break;
                    case BuildPart.Lights:
                        mesh.localScale = Vector3.Scale(target.Scale,new Vector3(1+level*.13f,1-level*.11f,1));
                        paintBlock.SetColor(BaseColor,level == 2 ? new(.58f,.84f,1f) : level == 3 ? new(1f,.75f,.36f) : Color.white);
                        break;
                    case BuildPart.Mirrors: mesh.localScale = target.Scale * (1 + level*.11f); break;
                }
                target.Mesh.SetPropertyBlock(paintBlock);
                paintBlock.Clear();
            }

            if (addOns != null)
            {
                addOns.gameObject.SetActive(false);
                Object.Destroy(addOns.gameObject);
            }
            addOns = new GameObject("Modular accessories").transform;
            addOns.SetParent(shell,false);
            void Module(string label, Vector3 p, Vector3 size, Color c) =>
                workshopArt.Part(addOns,PrimitiveType.Cube,label,p,size,c);
            Color dark = new(.12f,.14f,.15f);
            int wing = Option(build,BuildPart.Wing);
            if (!IsBike && wing > 0)
            {
                float y = .91f+wing*.11f;
                for (int side=-1;side<=1;side+=2)
                    Module("Wing support",new(side*.44f,y*.5f+.38f,-1.83f),new(.09f,y-.76f,.12f),dark);
                Module("Wing blade",new(0,y,-1.82f),new(1.22f+wing*.13f,.08f,.19f+wing*.06f),bodyColor);
            }
            int hood = Option(build,BuildPart.Hood);
            if (!IsBike && hood > 0)
                for (int i=0;i<hood*2;i++)
                    Module("Hood intake",new((i%2==0?-1:1)*.23f,.795f,1.32f+i/2*.13f),
                        new(.21f,.105f,.094f),dark);
            int exhaust = Option(build,BuildPart.Exhaust);
            for (int i=0;i<exhaust;i++)
                workshopArt.Part(addOns,PrimitiveType.Cylinder,"Exhaust tip",
                    IsBike ? new(.29f,.41f,-.82f+i*.21f) : new(.61f-i*.23f,.26f,-2.23f),
                    new(.106f,.12f,.106f),new(.48f,.50f,.46f),finish:HarborBlockout.Finish.Metal).transform
                    .localRotation=Quaternion.Euler(90,0,0);
            int stripe = Option(build,BuildPart.Stripe);
            if (stripe > 0)
            {
                Color secondary = stripe == 1 ? new(.86f,.81f,.67f) : stripe == 2 ? new(.85f,.24f,.13f) : new(.16f,.54f,.50f);
                Module("Body stripe",new(0,IsBike?.86f:.817f,IsBike?.17f:1.39f),
                    new(stripe == 3 ? .36f : .17f,.012f,IsBike?.26f:1.26f),secondary);
            }
            int seats = Option(build,BuildPart.Seats);
            int cabin = Option(build,BuildPart.CabinLight);
            if (!IsBike && seats > 0)
                for (int side=-1;side<=1;side+=2)
                {
                    Color fabric = seats == 1 ? new(.65f,.37f,.19f) : seats == 2 ? new(.64f,.55f,.44f) : new(.20f,.24f,.26f);
                    Module("Cabin seat",new(side*.43f,.81f,-.12f),new(.39f,.15f,.47f),fabric);
                    Module("Seat back",new(side*.43f,1.00f,-.31f),new(.36f,.39f,.12f),fabric);
                }
            if (!IsBike && cabin > 0)
            {
                var light = new GameObject("Cabin lighting").AddComponent<Light>();
                light.transform.SetParent(addOns,false);
                light.transform.localPosition = new(0,1.24f,-.25f);
                light.color = cabin == 1 ? new(.79f,.33f,.12f) : cabin == 2 ? new(.12f,.54f,.9f) : new(.62f,.21f,.79f);
                light.range = 1.7f;
                light.intensity = .72f;
                light.shadows = LightShadows.None;
            }
        }

        internal float BrakePower => BrakeAcceleration * CostaBuildTuning.Brakes(appliedBuild);
        private float TireGrip => Mathf.Clamp(Grip * CostaBuildTuning.Grip(appliedBuild),0,1.25f);
    }
}
