using System;
using UnityEngine;
using static CostaLivre.CostaText;
using Part = CostaLivre.CostaBuildTuning.Part;

namespace CostaLivre
{
    public sealed partial class HarborRun
    {
        private Camera workshopCamera;
        private CostaVehicle workshopModel;
        private RenderTexture workshopImage;
        private int workshopCar = -1;
        private Part selectedPart;
        private int selectedValue;
        private bool workshopBank = true;
        private float cameraOrbit;
        private string workshopMessage;
        private static readonly Vector3 PreviewSite = new(290,-360,600);

        private bool CanOpenWorkshop() => car != null && car.Customizable && car.Speed < .52f
            && CostaBuildTuning.AtWorkshop(car.Root.position);

        private void OpenWorkshop()
        {
            workshopCar = Array.IndexOf(cars,car);
            if (workshopCar < 0) return;
            workshopMessage = "";
            selectedPart = Part.Wheels;
            selectedValue = CostaBuildTuning.Option(car.Build,selectedPart);
            workshopModel = new CostaVehicle(harbor,transform,PreviewSite,0,car.OriginalPaint,style:car.VisualStyle,preview:true);
            workshopImage = new RenderTexture(496,280,16) { antiAliasing = 1 };
            workshopCamera = new GameObject("Workshop inspection camera").AddComponent<Camera>();
            workshopCamera.transform.SetParent(transform,false);
            workshopCamera.targetTexture = workshopImage;
            workshopCamera.clearFlags = CameraClearFlags.SolidColor;
            workshopCamera.backgroundColor = new(.07f,.105f,.11f);
            workshopCamera.nearClipPlane = .08f;
            workshopCamera.farClipPlane = 14;
            workshopCamera.fieldOfView = 42;
            workshopCamera.allowHDR = false;
            workshopCamera.allowMSAA = false;
            cameraOrbit = 0;
            UpdateWorkshopCamera();
            OpenOverlay(FieldOverlay.Workshop);
        }

        private void UpdateWorkshopCamera()
        {
            if (workshopCamera == null || workshopCar < 0) return;
            float heading = selectedPart switch
            {
                Part.Wheels or Part.Tires or Part.Mirrors => 85,
                Part.Exhaust or Part.Wing => 155,
                Part.Seats or Part.CabinLight => 105,
                _ => 36
            };
            cameraOrbit = Mathf.LerpAngle(cameraOrbit,heading,1-Mathf.Exp(-Time.unscaledDeltaTime*8));
            Vector3 focus = PreviewSite + (selectedPart switch
            {
                Part.Exhaust or Part.Wing => new Vector3(0,.65f,-.86f),
                Part.Lights or Part.Bumper or Part.Hood => new Vector3(0,.62f,.92f),
                Part.Wheels or Part.Tires => new Vector3(0,.39f,.34f),
                _ => new Vector3(0,.75f,0)
            });
            float distance = selectedPart is Part.Hood or Part.Paint or Part.Color or >= Part.Engine and <= Part.Grip ? 5.4f : 4.05f;
            Vector3 eye = focus + Quaternion.Euler(0,cameraOrbit,0)*new Vector3(0,1.42f,distance);
            workshopCamera.transform.SetPositionAndRotation(eye,Quaternion.LookRotation(focus-eye,Vector3.up));
            workshopModel.ApplyBuild(CostaBuildTuning.Change(cars[workshopCar].Build,selectedPart,selectedValue));
        }

        private void ReleaseWorkshop()
        {
            if (workshopCamera != null)
            {
                workshopCamera.enabled = false;
                Destroy(workshopCamera.gameObject);
            }
            if (workshopModel != null)
            {
                workshopModel.Root.gameObject.SetActive(false);
                Destroy(workshopModel.Root.gameObject);
            }
            if (workshopImage != null) { workshopImage.Release(); Destroy(workshopImage); }
            workshopCamera = null; workshopModel = null; workshopImage = null; workshopCar = -1;
        }

        private void DrawWorkshop()
        {
            if (workshopCar < 0) return;
            var vehicle = cars[workshopCar];
            DrawServiceFrame(L("COASTAL MOTOR WORKS","OFICINA COSTEIRA"),467);
            if (overlay != FieldOverlay.Workshop) { EndServiceFrame(); return; }
            if (workshopImage != null) GUI.DrawTexture(new Rect(215,54,385,219),workshopImage,ScaleMode.ScaleToFit);
            for (int i=0;i<CostaBuildTuning.Count;i++)
            {
                Part p = (Part)i;
                Rect row = new(12+(i/9)*99,53+(i%9)*36,96,32);
                GUI.enabled = CostaBuildTuning.Allowed(p,0,vehicle.IsBike);
                if (CostaUi.Button(row,WorkshopLabel(p),serviceButton))
                {
                    selectedPart = p;
                    selectedValue = CostaBuildTuning.Option(vehicle.Build,p);
                }
                GUI.enabled = true;
            }
            GUI.Label(new Rect(219,279,372,27),WorkshopLabel(selectedPart)+" · "+VariantName());
            if (selectedPart == Part.Color)
            {
                for (int c=0;c<3;c++)
                {
                    int shift = (2-c)*8;
                    int v = (selectedValue>>shift)&255;
                    if (CostaUi.Button(new Rect(223+c*122,307,111,30),$"{("RGB"[c])}  {v}  +",serviceButton))
                        selectedValue = (selectedValue & ~(255<<shift)) | (((v+17)&255)<<shift);
                }
            }
            else
            {
                if (CostaUi.Button(new Rect(219,307,70,30),"◂",serviceButton)) selectedValue = (selectedValue+3)%4;
                if (CostaUi.Button(new Rect(520,307,70,30),"▸",serviceButton)) selectedValue = (selectedValue+1)%4;
            }
            long old = vehicle.Build;
            long next = CostaBuildTuning.Change(old,selectedPart,selectedValue);
            GUI.Label(new Rect(215,346,388,22),$"{L("Engine","Motor")} {CostaBuildTuning.Engine(old):P0} → {CostaBuildTuning.Engine(next):P0}     "
                +$"{L("Grip","Aderência")} {CostaBuildTuning.Grip(old):P0} → {CostaBuildTuning.Grip(next):P0}");
            GUI.Label(new Rect(215,371,388,22),$"{L("Speed","Velocidade")} {(vehicle.TopSpeed/CostaBuildTuning.Gearbox(old)*3.6f):0} → "
                +$"{(vehicle.TopSpeed/CostaBuildTuning.Gearbox(old)*CostaBuildTuning.Gearbox(next)*3.6f):0} km/h");
            if (CostaUi.Button(new Rect(13,405,149,33),workshopBank ? L("Bank account","Conta bancária") : L("Cash","Dinheiro"),serviceButton))
                workshopBank = !workshopBank;
            GUI.enabled = old != next;
            if (CostaUi.Button(new Rect(171,405,190,33),L("Buy","Comprar")+$"  ${CostaBuildTuning.Price(selectedPart,selectedValue):N0}",serviceButton))
                BuyWorkshop(vehicle,old,next);
            GUI.enabled = true;
            GUI.Label(new Rect(370,406,231,22),session.Online ? session.CurrentNotice : workshopMessage);
            if (CostaUi.Button(new Rect(463,432,139,29),L("Exit vehicle","Sair do veículo"),serviceButton))
            {
                CloseOverlay();
                TryUseVehicle();
            }
            EndServiceFrame();
        }

        private void BuyWorkshop(CostaVehicle v,long old,long next)
        {
            if (session.Online)
                session.WorkshopRequest(workshopCar,selectedPart,selectedValue,workshopBank);
            else if (CostaSession.CanTune(old,selectedPart,selectedValue,jobs.Account) &&
                jobs.Ledger.Apply(jobs.Account,CostaLedger.Operation.Workshop,CostaBuildTuning.Price(selectedPart,selectedValue),
                    vehicle:workshopCar,build:next,fromBank:workshopBank))
            {
                v.ApplyBuild(next);
                workshopMessage = L("Applied","Alteração aplicada");
            }
            else
                workshopMessage = L("Insufficient funds or tier locked","Saldo insuficiente ou nível bloqueado");
        }

        private string VariantName() => selectedPart switch
        {
            Part.Color => selectedValue == 0 ? L("Original","Original") : $"#{selectedValue:X6}",
            Part.Paint => selectedValue switch { 1 => L("Metallic","Metálica"),2 => L("Matte","Fosca"),
                3 => L("Pearl","Perolada"),_ => L("Original","Original") },
            _ => selectedValue == 0 ? L("Original","Original") : $"{L("Level","Nível")} {selectedValue}"
        };

        private static string WorkshopLabel(Part p) => p switch
        {
            Part.Wheels => L("Wheels","Rodas"),Part.Tires => L("Tires","Pneus"),Part.Bumper => L("Bumpers","Para-choque"),
            Part.Wing => L("Wing","Aerofólio"),Part.Hood => L("Hood","Capô"),Part.Exhaust => L("Exhaust","Escape"),
            Part.Lights => L("Lights","Faróis"),Part.Mirrors => L("Mirrors","Retrovisores"),Part.Paint => L("Paint","Pintura"),
            Part.Stripe => L("Livery","Faixa"),Part.Seats => L("Seats","Bancos"),Part.CabinLight => L("Cabin light","Luz cabine"),
            Part.Engine => L("Engine","Motor"),Part.Gearbox => L("Gearbox","Câmbio"),Part.Brakes => L("Brakes","Freios"),
            Part.Suspension => L("Spring","Suspensão"),Part.Grip => L("Grip","Aderência"),_ => L("Color","Cor")
        };
    }
}
