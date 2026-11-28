using System;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using static CostaLivre.CostaText;

namespace CostaLivre
{
    public sealed partial class HarborRun
    {
        private int dealerSelection;
        private int garageSelection;
        private bool dealerBank = true;
        private string dealerMessage = "";
        private float nextGarageSummonAt;
        private int previewVehicleIndex = -1;
        private float previewSpin;

        private int CurrentOwnedVehicles => session.Online ? session.OwnedVehicles : jobs.Account.ownedVehicles;

        private bool CanOpenDealership() => car == null && CostaDealership.AtDealership(player.transform.position);
        private bool CanOpenGarage() => car == null && CostaDealership.AtGarage(player.transform.position, jobs, session.Online);

        private void OpenDealership()
        {
            dealerMessage = "";
            dealerSelection = Mathf.Clamp(dealerSelection, 0, CostaDealership.Catalog.Length - 1);
            SetupVehiclePreview(CostaDealership.Catalog[dealerSelection].VehicleIndex);
            OpenOverlay(FieldOverlay.Dealership);
        }

        private void OpenGarage()
        {
            dealerMessage = "";
            int firstOwned = FindFirstOwnedVehicle();
            if (firstOwned >= 0)
            {
                garageSelection = firstOwned;
                SetupVehiclePreview(firstOwned);
            }
            OpenOverlay(FieldOverlay.Garage);
        }

        private int FindFirstOwnedVehicle()
        {
            int mask = CurrentOwnedVehicles;
            if (!session.Online && jobs.Account.activeGarageVehicle >= 0
                && (mask & (1 << jobs.Account.activeGarageVehicle)) != 0)
                return jobs.Account.activeGarageVehicle;
            for (int i = 0; i < CostaDealership.Catalog.Length; i++)
            {
                int idx = CostaDealership.Catalog[i].VehicleIndex;
                if ((mask & (1 << idx)) != 0)
                    return idx;
            }
            return -1;
        }

        private void SetupVehiclePreview(int vehicleIndex)
        {
            ReleaseWorkshop();
            if (vehicleIndex < 0 || vehicleIndex >= cars.Length)
                return;
            previewVehicleIndex = vehicleIndex;
            CostaVehicle source = cars[vehicleIndex];
            workshopModel = new CostaVehicle(harbor, transform, PreviewSite, 0, source.OriginalPaint, style: source.VisualStyle, preview: true);
            long build = session.Online ? source.Build : jobs.Account.mods[vehicleIndex];
            workshopModel.ApplyBuild(build);
            SetWorkshopLayer(workshopModel.Root);

            workshopImage = new RenderTexture(496, 280, 16) { antiAliasing = 1 };
            workshopCamera = new GameObject("Showroom preview camera").AddComponent<Camera>();
            workshopCamera.transform.SetParent(transform, false);
            workshopCamera.targetTexture = workshopImage;
            workshopCamera.cullingMask = CostaItemPreview.Mask;
            workshopCamera.useOcclusionCulling = false;
            var cameraData = workshopCamera.GetUniversalAdditionalCameraData();
            cameraData.renderPostProcessing = false;
            cameraData.volumeLayerMask = 0;
            cameraData.renderShadows = false;
            workshopCamera.clearFlags = CameraClearFlags.SolidColor;
            workshopCamera.backgroundColor = new Color(0.065f, 0.105f, 0.135f);
            workshopCamera.nearClipPlane = 0.08f;
            workshopCamera.farClipPlane = 16f;
            workshopCamera.fieldOfView = 42f;
            workshopCamera.allowHDR = false;
            workshopCamera.allowMSAA = false;
            previewSpin = 34f;
            UpdateShowroomCamera();
        }

        private void UpdateShowroomCamera()
        {
            if (workshopCamera == null || workshopModel == null)
                return;
            previewSpin = Mathf.Repeat(previewSpin + Time.unscaledDeltaTime * 22f, 360f);
            Vector3 focus = PreviewSite + new Vector3(0, 0.78f, 0);
            Vector3 eye = focus + Quaternion.Euler(0, previewSpin, 0) * new Vector3(0, 1.48f, 5.25f);
            workshopCamera.transform.SetPositionAndRotation(eye, Quaternion.LookRotation(focus - eye, Vector3.up));
            workshopCamera.enabled = true;
        }

        private void DrawDealership()
        {
            DrawServiceFrame(L("COSTA MOTORS · DEALERSHIP", "COSTA MOTORS · CONCESSIONÁRIA"), 468);
            if (overlay != FieldOverlay.Dealership) { EndServiceFrame(); return; }

            int ownedMask = CurrentOwnedVehicles;
            var listing = CostaDealership.Catalog[dealerSelection];
            bool owned = (ownedMask & (1 << listing.VehicleIndex)) != 0;
            CostaVehicle vehicle = cars[listing.VehicleIndex];

            if (workshopImage != null)
                GUI.DrawTexture(new Rect(218, 52, 384, 218), workshopImage, ScaleMode.ScaleToFit);

            for (int i = 0; i < CostaDealership.Catalog.Length; i++)
            {
                var item = CostaDealership.Catalog[i];
                bool has = (ownedMask & (1 << item.VehicleIndex)) != 0;
                Rect row = new(12 + (i / 8) * 100, 52 + (i % 8) * 38, 96, 33);
                string tag = has ? "✓ " + item.Name : item.Name;
                if (CostaUi.Button(row, tag, serviceButton))
                {
                    dealerSelection = i;
                    dealerMessage = "";
                    SetupVehiclePreview(item.VehicleIndex);
                }
            }

            GUI.Label(new Rect(220, 276, 380, 26), $"{listing.Name}  ·  ${listing.Price:N0}");
            GUI.Label(new Rect(220, 304, 380, 22),
                $"{L("Top speed", "Vel. Máx")}: {vehicle.TopSpeed * 3.6f:0} km/h     " +
                $"{L("Class", "Classe")}: {vehicle.VisualStyle}");
            GUI.Label(new Rect(220, 328, 380, 22),
                owned
                    ? L("Owned · Available in your Garage (press G to summon)", "Comprado · Disponível na sua Garagem (tecla G)")
                    : L("Saved permanently to your Garage with tuning", "Salvo permanentemente na sua Garagem com tunagem"));

            if (CostaUi.Button(new Rect(14, 386, 155, 34), dealerBank ? L("Bank account", "Conta bancária") : L("Cash", "Dinheiro"), serviceButton))
                dealerBank = !dealerBank;

            GUI.enabled = !owned;
            if (CostaUi.Button(new Rect(178, 386, 200, 34),
                owned ? L("Already owned", "Já adquirido") : L("Buy vehicle", "Comprar") + $"  ${listing.Price:N0}", serviceButton))
                BuyDealershipVehicle(listing);
            GUI.enabled = true;

            if (owned && CostaUi.Button(new Rect(386, 386, 214, 34), L("Deliver outside", "Entregar na rua"), serviceButton))
            {
                SummonOwnedVehicle(listing.VehicleIndex);
                CloseOverlay();
            }

            GUI.Label(new Rect(16, 428, 584, 24), session.Online ? session.CurrentNotice : dealerMessage);
            EndServiceFrame();
        }

        private void BuyDealershipVehicle(CostaDealership.Listing listing)
        {
            if (session.Online)
            {
                session.DealerRequest(listing.VehicleIndex, dealerBank);
                return;
            }
            if (jobs.Ledger.BuyVehicle(jobs.Account, listing.VehicleIndex, listing.Price, 0, dealerBank))
            {
                effects.Play(Cue.Purchase);
                dealerMessage = L($"{listing.Name} delivered to your Garage!", $"{listing.Name} entregue na sua Garagem!");
            }
            else
                dealerMessage = L("Insufficient funds.", "Saldo insuficiente.");
        }

        private void DrawGarage()
        {
            DrawServiceFrame(L("PERSONAL GARAGE", "GARAGEM PRÓPRIA"), 468);
            if (overlay != FieldOverlay.Garage) { EndServiceFrame(); return; }

            int ownedMask = CurrentOwnedVehicles;
            int count = 0;
            for (int i = 0; i < CostaDealership.Catalog.Length; i++)
            {
                var item = CostaDealership.Catalog[i];
                if ((ownedMask & (1 << item.VehicleIndex)) == 0)
                    continue;
                Rect row = new(12 + (count / 8) * 100, 52 + (count % 8) * 38, 96, 33);
                if (CostaUi.Button(row, item.Name, serviceButton))
                {
                    garageSelection = item.VehicleIndex;
                    if (!session.Online)
                        jobs.Ledger.SetGarageVehicle(jobs.Account, garageSelection);
                    SetupVehiclePreview(garageSelection);
                }
                count++;
            }

            if (count == 0)
            {
                GUI.Label(new Rect(220, 120, 360, 60),
                    L("No owned vehicles yet.\nVisit Costa Motors Dealership in Santa Luz or Antutu!",
                      "Você ainda não comprou veículos.\nVisite a Concessionária Costa Motors em Santa Luz ou Antutu!"));
            }
            else
            {
                if (workshopImage != null)
                    GUI.DrawTexture(new Rect(218, 52, 384, 218), workshopImage, ScaleMode.ScaleToFit);
                string name = CostaDealership.FindListing(garageSelection, out var found) ? found.Name : "Vehicle";
                GUI.Label(new Rect(220, 278, 380, 26), $"{name}  ·  {L("Saved & tuned", "Salvo e tunado")}");

                if (CostaUi.Button(new Rect(220, 314, 185, 36), L("Deliver outside", "Retirar na rua"), serviceButton))
                {
                    SummonOwnedVehicle(garageSelection);
                    CloseOverlay();
                }
                if (CostaUi.Button(new Rect(414, 314, 185, 36), L("Open Workshop", "Abrir Oficina"), serviceButton))
                {
                    SummonOwnedVehicle(garageSelection, enterAfter: true);
                }
            }

            int vault = session.Online ? session.TerritoryVault : jobs.Account.territoryVault;
            if (vault > 0)
            {
                if (CostaUi.Button(new Rect(14, 382, 586, 34),
                    L($"Collect Territory Vault (+${vault:N0})", $"Coletar Cofre dos Territórios (+${vault:N0})"), serviceButton))
                    CollectTerritoryVault();
            }

            GUI.Label(new Rect(16, 426, 584, 24), session.Online ? session.CurrentNotice : dealerMessage);
            EndServiceFrame();
        }

        private void SummonOwnedVehicle(int vehicleIndex, bool enterAfter = false)
        {
            if (vehicleIndex < 0 || vehicleIndex >= cars.Length)
            {
                ShowWorldNotice(L("Buy a vehicle at Costa Motors first!", "Compre um veículo na Concessionária primeiro!"));
                return;
            }
            if (session.Online)
            {
                session.GarageSummonRequest(vehicleIndex);
                return;
            }
            if (Time.unscaledTime < nextGarageSummonAt)
            {
                ShowWorldNotice(L("Garage delivery cooling down...", "Aguarde para chamar o veículo novamente..."));
                return;
            }
            nextGarageSummonAt = Time.unscaledTime + 4f;
            CostaVehicle vehicle = cars[vehicleIndex];
            if (CostaDealership.TryFindDeliverySpot(player.transform.position, yaw, out Vector3 spot, out float heading))
            {
                vehicle.ApplyBuild(jobs.Account.mods[vehicleIndex]);
                vehicle.ApplyNetwork(spot, heading, 0f, true);
                Physics.SyncTransforms();
                jobs.Ledger.SetGarageVehicle(jobs.Account, vehicleIndex);
                effects.Play(Cue.Mount, 0.35f);
                if (enterAfter)
                {
                    CloseOverlay();
                    EnterVehicle(vehicle);
                    OpenWorkshop();
                }
                else
                    ShowWorldNotice(L("Personal vehicle delivered nearby!", "Seu veículo da garagem foi entregue por perto!"));
            }
            else
                ShowWorldNotice(L("No clear road space nearby.", "Sem espaço livre na rua por perto."));
        }

        private void CollectTerritoryVault()
        {
            if (session.Online)
            {
                session.CollectVaultRequest();
                return;
            }
            if (jobs.Ledger.CollectTerritoryVault(jobs.Account, 0, out int collected))
            {
                effects.Play(Cue.Sale);
                bankingMessage = dealerMessage = L($"Collected ${collected:N0} from territories!", $"Coletado ${collected:N0} dos territórios!");
                ShowWorldNotice(bankingMessage);
            }
        }
    }
}
