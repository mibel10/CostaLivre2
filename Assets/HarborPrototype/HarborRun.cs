using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.AI;
using UnityEngine.InputSystem;
using UnityEngine.Rendering;
using static CostaLivre.CostaText;
using TouchControl = CostaLivre.CostaTouch.Control;
using Cue = CostaLivre.CostaEffects.Cue;

namespace CostaLivre
{
    public sealed partial class HarborRun : MonoBehaviour
    {
        private enum FieldOverlay { None, Map, Shop, Bag, Vendor, Elevator, Bank, Workshop, Campaign, Dealership, Garage }
        private enum RunState { FindCargo, Escape, Delivered, Caught }

        private sealed class Guard
        {
            internal HarborCharacter Character;
            internal NavMeshAgent Agent;
            internal CapsuleCollider Hitbox;
            internal Vector3 Post;
            internal int Health;

            internal bool Active => Agent.gameObject.activeSelf && Agent.isOnNavMesh;
        }

        [SerializeField] private Camera followCamera;
        [SerializeField] private Light sun;
        [SerializeField] private Volume volume;
        [SerializeField] private InputActionAsset inputActions;
        [SerializeField] private Shader blockoutShader;

        private static readonly Vector3[] GuardPosts = { new(18, 0, 18), new(7, 0, 0), new(-47, 0, 96), new(-63, 0, 95), new(-30, 0, 73), new(-81, 0, 94) };
        private const float InteractionRadius = 1.8f;
        internal const float FullStamina = 3.8f;
        private const float DefaultFov = 66f;
        private const float RollDuration = 0.69f;
        private static readonly Vector3 MenuActorPosition = new(0, 0, -1200f);

        private readonly Guard[] guards = new Guard[GuardPosts.Length];
        private readonly InputAction[] weaponSlots = new InputAction[3];
        private HarborBlockout harbor;
        private CostaTraffic traffic;
        private CostaVenueLife venues;
        private CostaWorldInteractive worldInteractive;
        private CostaPoliceSystem policeSystem;
        private CostaTerritorySystem territories;
        private CostaLivingWorld livingWorld;
        private CostaMinigames minigames;
        private HarborWeather atmosphere;
        private CostaCity city;
        private CostaJobs jobs;
        private CostaEncounter encounter;
        private CostaRobberies robberies;
        private CostaMap cityMap;
        private CostaGunShop gunShop;
        private FieldOverlay overlay;
        private CostaTouch touch;
        private CostaPadBindings padBindings;
        private InputAction mapKey;
        private InputAction bagKey;
        private InputAction crouchKey;
        private CostaSession session;
        private int soloSeat = -1;
        private int soloLift = -1;
        private int liftPicker = -1;
        private float nextSoloTreatment;
        private string interiorNotice = "";
        private float interiorNoticeUntil;
        private int CurrentSeat => session.Online ? session.Local.seat : soloSeat;
        private int CurrentLift => session.Online ? session.Local.lift : soloLift;
        private GUIStyle elevatorButton;
        private CostaOnlinePanel onlinePanel;
        private CostaRemoteView remotes;
        private CostaBagPanel bagPanel;
        private CostaLook localLook;
        private CostaLook menuLook;
        private bool wasOnline;
        private bool enteredOnlineWorld;
        private int equipSequence;
        private int queuedEquip = -1;
        private bool soloResumable;
        private bool soloHarborJob;
        private RunState soloState;
        private Vector3 soloPosition;
        private float soloYaw;
        private float soloPitch;
        private float soloEscapeElapsed;
        private int soloWantedLevel;
        private float soloHeatRemaining;
        private float soloCrimeRemaining;
        private CostaVehicle soloContractCar;
        private Vector3 soloContractCarPosition;
        private float soloContractCarYaw;
        private CostaVehicle soloDrivenCar;
        private Vector3 soloDrivenPosition;
        private float soloDrivenYaw;
        private bool pausedForFocus;
        private bool wasPolice;
        private bool crouching;
        private bool walking;
        private bool runningPose;
        private bool aimingPose;
        private int netRevision = -1;
        private int staminaRefill;
        private int pendingBuy = -1;
        private float nextReloadRequest;
        private int appliedSelection;
        private CostaBag CurrentBag => session.Online ? session.Bag : jobs.Bag;
        private int CurrentCash => session.Online ? session.Cash : jobs.Credits;
        private bool IsPolice => session?.Online == true && session.Local.police;
        private System.Action closeOverlay;
        private CostaVehicle[] cars;
        private CostaVehicle car;
        private readonly CostaVehicleMotion vehicleMotion = new();
        private CostaBindings bindings;
        private InputAction jobsBoard;
        private System.Action<int> selectJob;
        private bool harborJob;
        private HarborArsenal arsenal;
        private CostaEffects effects;
        private CostaUrbanAudio urbanAudio;
        private HarborWeather.Conditions worldWeather;
        private float cityNight;
        private float lastCarNight = -1f;
        private float lastCarGrip = -1f;
        private float nextStreetLightUpdate;
        private Vector3 lastStreetLightViewer;
        private int lastStreetLightBudget = -1;
        private CostaScore score;
        private readonly CostaTransitions transitions = new();
        private readonly CostaDeathScreen deathScreen = new();
        private int shownCompletion;
        private Light menuKey, menuFill;
        private CostaEffects.Footfalls footfalls;
        private float engineThrottle;
        private GameObject cargo;
        private GameObject cargoPack;
        private CharacterController player;
        private Transform playerModel;
        private HarborCharacter playerBody;
        private HarborCharacter menuBody;
        private Transform menuStage;
        private bool worldReady;
        private double soloDaySeconds;
        private bool worldLoading;
        private bool worldLoadFailed;
        private HarborLoadingScreen loadingScreen;
        private CostaCinematics cinematics;
        private readonly CostaTitleBackdrop titleBackdrop = new();
        private System.Action afterWorldLoad;
        private bool blockingWorldLoad;
        private float titleShownAt;
        private bool cityTitleVisible;
        private bool soloConnectionPending;
        private string loadingPhase;
        private float loadingProgress;
        private HarborMenu menu;
        private Transform menuWeaponMount;
        private Transform[] menuWeapons;
        private System.Action deploy;
        private System.Action resume;
        private System.Action quit;
        private System.Action returnHome;
        private bool inMenu;
        private bool pauseMenu;
        private bool enteredRun;
        private NavMeshData navMesh;
        private AsyncOperation navigationBuild;
        private NavMeshDataInstance navMeshInstance;
        private InputActionAsset controls;
        private InputAction move;
        private InputAction sprint;
        private InputAction interact;
        private InputAction look;
        private InputAction shoot;
        private InputAction aim;
        private InputAction accelerate;
        private InputAction reverse;
        private InputAction reload;
        private InputAction nextWeapon;
        private InputAction previousWeapon;
        private InputAction restart;
        private InputAction openMenu;
        private InputAction jump;
        private InputAction roll;
        private HarborHud hud;
        private RunState state;
        private float yaw;
        private float verticalSpeed;
        private float jumpClock;
        private float landing;
        private float landingDuration = 0.14f;
        private float landingSeverity;
        private float airbornePeak;
        private Vector3 groundVelocity;
        private float wallReaction;
        private float cameraImpulse;
        private HarborCharacter.Gesture gesture;
        private Vector3 gestureTarget;
        private float gestureEnds;
        private float impactStrength;
        private float displayedReload = -1;
        private float rollClock;
        private float rollCooldown;
        private bool grounded;
        private Vector3 rollDirection;
        private float pitch;
        private float boomDistance;
        private float shoulder = 1;
        private float shoulderSide = 1;
        private float cameraAim;
        private float cameraHeight;
        private Vector3 cameraLead;
        private Vector3 cameraLeadVelocity;
        private readonly RaycastHit[] cameraHits = new RaycastHit[12];
        private InputAction shoulderSwap;
        private float stamina;
        private float sprintRecoveryDelay;
        private float nextPursuitUpdate;
        private float escapeStartedAt;
        private float nextPortSave;
        private float escapeSeconds;
        private bool needsSprintRelease;
        private bool gamepad;
        private bool pursuit;
        private float blockedExitUntil;
        private int wantedLevel;
        private bool recoveryMissionStar;
        private bool soloRecoveryMissionStar;
        private float heatUntil;
        private float nextCrime;
        private float arrestProgress;
        private const float SoloArrestTime = 1.25f;

        private void Awake()
        {
            Application.targetFrameRate = 60;
            Application.runInBackground = true;
            touch = new CostaTouch();
            BindControls();
#if UNITY_WSA
            followCamera.nearClipPlane = 0.18f;
            followCamera.useOcclusionCulling = false;
#else
            followCamera.nearClipPlane = 0.07f;
#endif
            followCamera.cullingMask &= ~CostaItemPreview.Mask;
            sun.cullingMask &= ~CostaItemPreview.Mask;
            menu = new HarborMenu(bindings, padBindings, followCamera, sun, volume);
            jobs = new CostaJobs();
            harbor = new HarborBlockout(transform, blockoutShader);
            harbor.Mirrors = new CostaMirrors(followCamera);
            atmosphere = new HarborWeather(transform);
            onlinePanel = new CostaOnlinePanel(Connect);
            loadingScreen = new HarborLoadingScreen();
            score = new CostaScore(transform);
            StartCoroutine(score.Load());
            closeOverlay = CloseOverlay;
            deploy = () => WithWorld(StartRun);
            resume = ResumeRun;
            quit = Application.Quit;
            returnHome = ReturnToMainMenu;
            selectJob = index => WithWorld(() => SelectJob(index));
            CreateMenuPreview();
            ShowMenu();
        }

        private void Connect(string name, string address, int port, bool host, string relay = null,
            string roomName = "", bool publicRoom = true, string password = "") =>
            WithWorld(() =>
            {
                CaptureSoloVehicles();
                soloConnectionPending = true;
                session.Start(name, address, port, host, relay, roomName, publicRoom, password);
            });

        private void CaptureSoloVehicles()
        {
            soloContractCar = encounter.Active && jobs.Current.Mode is CostaJobs.Action.Seize or CostaJobs.Action.DeliverVehicle
                ? cars[jobs.Current.Vehicle] : null;
            soloDrivenCar = car;
            if (soloDrivenCar != null)
            {
                soloDrivenPosition = soloDrivenCar.Root.position;
                soloDrivenYaw = soloDrivenCar.Root.eulerAngles.y;
            }
            if (soloContractCar != null)
            {
                soloContractCarPosition = soloContractCar.Root.position;
                soloContractCarYaw = soloContractCar.Root.eulerAngles.y;
            }
        }

        private void WithWorld(System.Action play)
        {
            if (worldReady)
                play();
            else if (!worldLoadFailed)
            {
                afterWorldLoad = play;
                if (play != null && !blockingWorldLoad)
                {
                    blockingWorldLoad = true;
                    loadingScreen.Begin();
                    SetCursor(false);
                }

                if (!worldLoading)
                {
                    worldLoading = true;
                    loadingPhase = L("Reading the port", "Abrindo o porto");
                    loadingProgress = 0;
                    StartCoroutine(LoadWorld());
                }
            }
        }

        private IEnumerator LoadWorld()
        {
            yield return null;
            yield return harbor.PreloadWorldTextures();
            using (var stages = BuildWorld().GetEnumerator())
            {
                while (true)
                {
                    bool more;
                    try
                    {
                        more = stages.MoveNext();
                    }
                    catch (System.Exception error)
                    {
                        Debug.LogException(error);
                        worldLoadFailed = true;
                        break;
                    }

                    if (!more)
                        break;
                    loadingProgress = stages.Current;
                    yield return null;
                }
            }

            worldLoading = false;
            if (worldLoadFailed)
            {
                if (!blockingWorldLoad)
                    loadingScreen.Begin();
                yield break;
            }

            worldReady = true;
            loadingScreen.Release();
            loadingScreen = null;
            ShowMenu();
            blockingWorldLoad = false;
            System.Action pending = afterWorldLoad;
            afterWorldLoad = null;
            pending?.Invoke();
        }

        private IEnumerable<float> BuildWorld()
        {
            harbor.Build();
            CostaDealership.BuildShowroomKiosk(harbor);
            loadingPhase = L("Building Santa Luz and the coastal highway", "Montando Santa Luz e a rodovia costeira");
            yield return 0.14f;

            city = new CostaCity(harbor, transform);
            foreach (float part in city.Build())
                yield return Mathf.Lerp(0.14f, 0.69f, part);

            loadingPhase = L("Preparing vehicles and contracts", "Preparando veículos e missões");
            yield return 0.69f;
            traffic = new CostaTraffic(harbor, transform);
            yield return .71f;
            venues = new CostaVenueLife(harbor, transform);
            minigames = new CostaMinigames();
            worldInteractive = new CostaWorldInteractive(harbor, transform);
            worldInteractive.BuildEstablishments(
                BuyWorldFood,
                () => StartMinigame(() => minigames.StartGym(RewardWorldActivity)),
                () =>
                {
                    if (!TrySpendSolo(20)) return;
                    StartMinigame(() => minigames.StartArcade(RewardWorldActivity));
                },
                () => StartMinigame(() => minigames.StartBasketball(RewardWorldActivity)),
                () => StartMinigame(() => minigames.StartFishing(RewardWorldActivity)),
                () =>
                {
                    if (car == null) { ShowWorldNotice(L("Need a vehicle for Drift!", "Precisa de um veículo para o Drift!")); return; }
                    minigames.StartDrift((reward, message) =>
                    {
                        if (reward > 0)
                            RewardWorldActivity(reward, message);
                        else
                            ShowWorldNotice(message);
                    });
                },
                () =>
                {
                    if (car == null) { ShowWorldNotice(L("Need a vehicle for the Race!", "Precisa de um veículo para a Corrida!")); return; }
                    minigames.StartStreetRace(RewardWorldActivity);
                },
                jobs.Account.relics,
                idx =>
                {
                    if (!jobs.Ledger.Apply(jobs.Account, CostaLedger.Operation.Reward, 500, relic: idx))
                    {
                        ShowWorldNotice(L("Could not save the relic. Try again.", "Não foi possível salvar a relíquia. Tente de novo."));
                        return false;
                    }
                    int found = worldInteractive.CollectiblesFound + 1;
                    RewardWorldActivity(0, L($"Relic {found}/{worldInteractive.CollectiblesTotal} found! (+$500)",
                        $"Relíquia {found}/{worldInteractive.CollectiblesTotal} encontrada! (+$500)"));
                    return true;
                },
                benchPos =>
                {
                    stamina = FullStamina;
                    encounter.RestoreHealth(15);
                    ShowWorldNotice(L("Rested on bench (+15 HP & stamina)", "Descansou no banco (+15 Vida e fôlego)"));
                },
                () =>
                {
                    if (!TrySpendSolo(45)) return;
                    encounter.RestoreHealth(40);
                    worldInteractive.AddReputation(player.transform.position, 25);
                    ShowWorldNotice(L("Vice Nights drink (+40 HP · +Rep)", "Drink na Vice Nights (+40 Vida · +Rep)"));
                },
                () =>
                {
                    if (car == null || !car.Customizable || car.Speed > .52f)
                    {
                        ShowWorldNotice(L("Stop a customizable vehicle here.", "Estacione aqui um veículo que possa ser tunado."));
                        return;
                    }
                    long tuned = CostaBuildTuning.Change(car.Build, CostaBuildTuning.Part.Grip,
                        Mathf.Min(3, CostaBuildTuning.Option(car.Build, CostaBuildTuning.Part.Grip) + 1));
                    if (tuned == car.Build && car.Damage == Vector2.zero)
                        return;
                    int vehicle = System.Array.IndexOf(cars, car);
                    if (!jobs.Ledger.Apply(jobs.Account, CostaLedger.Operation.Workshop, 120, vehicle: vehicle, build: tuned))
                    {
                        ShowWorldNotice(L("Repair could not be purchased.", "Não foi possível comprar o reparo."));
                        return;
                    }
                    car.ApplyBuild(tuned);
                    car.SetDamage(Vector2.zero);
                    worldInteractive.AddReputation(player.transform.position, 35);
                    ShowWorldNotice(L("Vehicle tuned & repaired! (+Rep)", "Veículo ajustado e reparado! (+Rep)"));
                });
            livingWorld = new CostaLivingWorld(harbor, transform);
            jobs.AttachDoors(city.HouseDoors);
            city.UpdateHouseDoors(jobs, false, snap: true);
            robberies = new CostaRobberies(harbor);
            cityMap = new CostaMap(city.Footprints);
            cityMap.Prepare();
            gunShop = new CostaGunShop();
            BuildAtms();
            harbor.Sign("OFICINA COSTEIRA",CostaBuildTuning.CityWorkshop+new Vector3(0,2.2f,3),.18f,new(.82f,.91f,.83f));
            hud = new HarborHud(cityMap);
            cinematics = new CostaCinematics(followCamera, transform);
            bagPanel = new CostaBagPanel(harbor, transform);
            yield return 0.74f;

            foreach (float progress in CreateWorldActors())
                yield return progress;
            urbanAudio = new CostaUrbanAudio(transform, effects.WorldAudio, traffic.VehiclesForAudio, cars, venues);
            for (int floor = 0; floor < CostaFootstepBank.Floors; floor++)
            {
                effects.WarmSteps(floor);
                yield return Mathf.Lerp(.79f, .81f, (floor + 1f) / CostaFootstepBank.Floors);
            }
            loadingPhase = L("Preparing city navigation", "Preparando a navegação da cidade");
            yield return 0.82f;

            navMesh = new NavMeshData();
            navigationBuild = harbor.BuildNavigation(navMesh);
            while (!navigationBuild.isDone)
                yield return Mathf.Lerp(0.82f, 0.95f, navigationBuild.progress);

            navigationBuild = null;
            harbor.ReleaseNavigationSources();
            navMeshInstance = NavMesh.AddNavMeshData(navMesh);
            policeSystem = new CostaPoliceSystem(harbor, transform, effects, GuardPosts);
            territories = new CostaTerritorySystem(harbor, transform, jobs.Account.territoryMask);
            cityMap.Territories = territories;
            ResetRun();
            SetCursor(false);
            loadingPhase = L("Santa Luz is ready", "Santa Luz está pronta");
            yield return 1f;
        }

        private IEnumerable<float> CreateWorldActors()
        {
            GameObject runner = new GameObject("Runner");
            runner.layer = LayerMask.NameToLayer("Ignore Raycast");
            runner.transform.SetParent(transform, false);
            runner.transform.position = HarborBlockout.Spawn;
            player = runner.AddComponent<CharacterController>();
            player.height = 1.8f;
            player.radius = 0.36f;
            player.center = Vector3.up * 0.9f;
            player.stepOffset = 0.2f;
            playerBody = harbor.Person("Runner model", runner.transform, HarborBlockout.Spawn, new Color(0.89f, 0.90f, 0.86f));
            playerBody.EnableDirectionalBlendTree();
            playerModel = playerBody.Root;
            cargoPack = harbor.Pack(playerModel, playerBody.Chest);
            playerBody.RegisterAttachment(cargoPack.transform, 1f);
            localLook = new CostaLook(harbor, playerBody);

            effects = new CostaEffects(transform, followCamera.transform, harbor);
            arsenal = new HarborArsenal(harbor, playerModel, blockoutShader, jobs);
            cargo = harbor.Case();

            yield return .75f;
            cars = new CostaVehicle[16];
            cars[0] = new(harbor, transform, new Vector3(-30, 0, 27.5f), 0, new Color(0.34f, 0.36f, 0.39f));
            cars[1] = new(harbor, transform, new Vector3(-77, 0, 43), 0, new Color(0.30f, 0.36f, 0.31f));
            cars[2] = new(harbor, transform, new Vector3(18, 0, 44), 180, new Color(0.38f, 0.18f, 0.16f));
            cars[3] = new(harbor, transform, new Vector3(-61, 0, 93), 90, new Color(0.24f, 0.31f, 0.39f), true);
            yield return .76f;
            cars[4] = new(harbor, transform, new Vector3(-13, 0, -18), 90, new Color(0.34f, 0.41f, 0.47f));
            cars[5] = new(harbor, transform, new Vector3(65, 0, 104), 0, new Color(0.45f, 0.43f, 0.36f));
            cars[6] = new(harbor, transform, new Vector3(-40, 0, 51), 270, new Color(0.63f, 0.23f, 0.14f), style: CostaVehicle.Style.Super);
            cars[7] = new(harbor, transform, new Vector3(59, 0, 87), 90, new Color(0.28f, 0.40f, 0.52f), style: CostaVehicle.Style.Track);
            yield return .77f;
            cars[8] = new(harbor, transform, new Vector3(-39, 0, 34), 90, new Color(0.64f, 0.64f, 0.58f), style: CostaVehicle.Style.Compact);
            cars[9] = new(harbor, transform, new Vector3(-88, 0, 85), 90, new Color(0.18f, 0.23f, 0.25f), style: CostaVehicle.Style.Executive);
            cars[10] = new(harbor, transform, new Vector3(-9, 0, 71), 90, new Color(0.31f, 0.45f, 0.39f));
            cars[11] = new(harbor, transform, new Vector3(-35, 0, 25.5f), 0, new Color(0.61f, 0.32f, 0.11f), style: CostaVehicle.Style.StreetBike);
            yield return .78f;
            cars[12] = new(harbor, transform, new Vector3(-78, 0, 115), 180, new Color(0.25f, 0.27f, 0.28f), style: CostaVehicle.Style.Cruiser);
            cars[13] = new(harbor, transform, new Vector3(-45, 0, CostaCity.Districts[0].Center - 3), 90, new Color(0.36f, 0.52f, 0.57f));
            cars[14] = new(harbor, transform, new Vector3(-45, 0, CostaCity.Districts[1].Center - 3), 90, new Color(0.34f, 0.39f, 0.53f), style: CostaVehicle.Style.Executive);
            cars[15] = new(harbor, transform, new Vector3(-45, 0, CostaCity.Districts[2].Center - 3), 90, new Color(0.65f, 0.22f, 0.35f), style: CostaVehicle.Style.Super);
            yield return .79f;

            for (int i=0;i<cars.Length;i++)
                cars[i].ApplyBuild(jobs.Account.mods[i]);
            encounter = new CostaEncounter(harbor, transform, jobs, cars, effects);
            session = new CostaSession(cars, harbor.Damage, traffic, city.Interiors)
            {
                InteriorEffects = effects,
                Campaign = CostaCampaign.Load()
            };
            onlinePanel.AttachSession(session);
            remotes = new CostaRemoteView(harbor, transform, effects);
        }

        private void CreateMenuPreview()
        {
            menuStage = new GameObject("Menu preview set").transform;
            menuStage.SetParent(transform);
            menuStage.position = MenuActorPosition;
            Color deck = new(0.84f, 0.78f, 0.68f);
            Color stucco = new(0.90f, 0.63f, 0.52f);
            Color cream = new(0.94f, 0.89f, 0.80f);
            Color teal = new(0.14f, 0.62f, 0.66f);
            harbor.Part(menuStage, PrimitiveType.Cube, "Preview floor", new(0, -0.16f, 0), new(28, 0.3f, 20), deck);
            harbor.Part(menuStage, PrimitiveType.Cube, "Preview terrace wall", new(0, 3.1f, 5), new(27, 6.2f, 0.33f), stucco);
            for (int i = -6; i <= 6; i++)
                harbor.Part(menuStage, PrimitiveType.Cube, "Art Deco pilaster", new(i * 1.65f, 3.1f, 4.78f),
                    new(0.22f, 6.1f, 0.18f), cream);
            harbor.Part(menuStage, PrimitiveType.Cube, "Terrace dado band", new(0, 0.62f, 4.74f),
                new(24, 0.22f, 0.20f), teal);
            harbor.Part(menuStage, PrimitiveType.Cube, "Neon accent coral", new(0, 4.55f, 4.72f),
                new(16, 0.12f, 0.10f), new Color(0.98f, 0.44f, 0.56f), finish: HarborBlockout.Finish.Paint);
            harbor.Part(menuStage, PrimitiveType.Cube, "Neon accent cyan", new(0, 4.28f, 4.72f),
                new(12, 0.09f, 0.10f), new Color(0.20f, 0.86f, 0.86f), finish: HarborBlockout.Finish.Paint);

            menuKey = MenuLight("Warm tropical key", new(-3, 3.2f, -2.2f), new Color(1f, 0.80f, 0.56f), 6.8f, true);
            menuFill = MenuLight("Turquoise sea rim", new(2.9f, 2.7f, 3.4f), new Color(0.26f, 0.82f, 0.92f), 4.0f, false);

            // Stylized coastal planter + mini tropical palm beside the protagonist
            harbor.Part(menuStage, PrimitiveType.Cube, "Terrace planter", new(2.35f, 0.36f, 2.65f), new(1.25f, 0.72f, 1.25f), cream);
            var palmTrunk = harbor.Part(menuStage, PrimitiveType.Cylinder, "Preview palm trunk", new(2.35f, 1.95f, 2.65f),
                new(0.18f, 1.55f, 0.18f), new Color(0.45f, 0.34f, 0.23f)).transform;
            palmTrunk.localRotation = Quaternion.Euler(0, 0, -6f);
            for (int i = 0; i < 5; i++)
            {
                float a = i * (Mathf.PI * 2f / 5f);
                Vector3 dir = new(Mathf.Cos(a), 0f, Mathf.Sin(a));
                var frond = harbor.Part(menuStage, PrimitiveType.Cube, "Preview palm frond",
                    new Vector3(2.48f, 3.45f, 2.65f) + dir * 0.92f,
                    new Vector3(0.42f, 0.06f, 1.85f), i % 2 == 0 ? new Color(0.16f, 0.52f, 0.26f) : new Color(0.24f, 0.62f, 0.31f)).transform;
                frond.localRotation = Quaternion.LookRotation(dir) * Quaternion.Euler(22f, 0f, 0f);
            }

            menuBody = harbor.Person("Loadout preview", menuStage, MenuActorPosition, new Color(0.95f, 0.93f, 0.88f));
            menuLook = new CostaLook(harbor, menuBody);
            menuBody.Root.rotation = Quaternion.Euler(0, 212f, 0);
            menuWeaponMount = new GameObject("Preview weapons").transform;
            menuWeaponMount.SetParent(menuBody.Root, false);
            menuWeapons = new Transform[HarborArsenal.WeaponNames.Length];
        }

        private Light MenuLight(string name, Vector3 position, Color color, float intensity, bool focused)
        {
            Light lamp = new GameObject(name).AddComponent<Light>();
            lamp.transform.SetParent(menuStage, false);
            lamp.transform.localPosition = position;
            lamp.type = focused ? LightType.Spot : LightType.Point;
            lamp.range = focused ? 12 : 9;
            lamp.color = color;
            lamp.intensity = intensity;
            if (focused)
            {
                lamp.spotAngle = 69;
                lamp.innerSpotAngle = 44;
            }
            lamp.shadows = LightShadows.None;
            return lamp;
        }

        private bool OwnsWeapon(int slot) => arsenal?.Owned(slot) ?? (slot == HarborArsenal.Knife || jobs.OwnsWeapon(slot));

        private void UpdateMenuPreview()
        {
            bool live = worldReady && menu.LiveTitle && !pauseMenu && !menu.OnlinePage && !menu.LoadoutPage
                && session?.Online != true;
            if (live != cityTitleVisible)
            {
                cityTitleVisible = live;
                menuStage.gameObject.SetActive(!live);
                if (live)
                    titleBackdrop.Begin();
            }
            if (live)
            {
                titleBackdrop.Position(followCamera);
                city.Animate(followCamera.transform.position, followCamera.farClipPlane);
                return;
            }

            float time = Time.unscaledTime;
            float arc = -37 + Mathf.Sin(time * .23f) * 8f;
            Vector3 dolly = Quaternion.Euler(0, arc, 0) * Vector3.back * (4.66f + Mathf.Sin(time * .17f) * .24f);
            dolly.x *= -1;
            Vector3 cameraPosition = MenuActorPosition + dolly + Vector3.up * (1.58f + .1f * Mathf.Sin(time * .21f));
            float fieldOfView = 34.8f + .65f * Mathf.Sin(time * .22f);
            menuKey.intensity = 5.9f + Mathf.Sin(time * .49f) * .38f;
            menuKey.transform.localPosition = new Vector3(-3 + .58f * Mathf.Sin(time * .28f), 3.1f, -2);
            menuKey.transform.rotation = Quaternion.LookRotation(menuBody.Chest.position - menuKey.transform.position);
            menuFill.intensity = 3.4f + Mathf.Sin(time * .31f + 1.1f) * .56f;
            Vector3 center = MenuActorPosition + Vector3.up * 1.05f;
            Vector3 forward = center - cameraPosition;
            Vector3 right = Vector3.Cross(Vector3.up, forward).normalized;
            Vector3 up = Vector3.Cross(forward.normalized, right);
            float halfHeight = forward.magnitude * Mathf.Tan(fieldOfView * Mathf.Deg2Rad / 2f);
            Vector2 offset = (menu.PreviewViewport - Vector2.one * 0.5f) * 2f;
            Vector3 focus = center - right * (offset.x * halfHeight * followCamera.aspect)
                - up * (offset.y * halfHeight);
            followCamera.transform.SetPositionAndRotation(cameraPosition, Quaternion.LookRotation(focus - cameraPosition));
            followCamera.fieldOfView = fieldOfView;
            menuBody.Animate(HarborCharacter.Motion.Idle);
            menuLook.Apply(menu.OnlinePage || session?.Online == true ? onlinePanel.Outfit : 0, IsPolice);
            int previewSlot = OwnsWeapon(menu.SelectedWeapon) ? menu.SelectedWeapon : arsenal?.Slot ?? HarborArsenal.Knife;
            if (previewSlot >= 0)
            {
                if (menuWeapons[previewSlot] == null)
                {
                    Transform model = new GameObject(HarborArsenal.WeaponNames[previewSlot]).transform;
                    model.SetParent(menuWeaponMount, false);
                    HarborArsenal.BuildModel(harbor, model, previewSlot);
                    menuWeapons[previewSlot] = model;
                }
                if (previewSlot == HarborArsenal.Knife)
                    menuBody.HoldKnife(false, -1);
                else
                    menuBody.HoldWeapon(false, 12f, false);
                menuWeaponMount.SetPositionAndRotation(menuBody.WeaponHand,
                    menuBody.Root.rotation * Quaternion.Euler(15, -9, 0));
            }
            for (int i = 0; i < menuWeapons.Length; i++)
                menuWeapons[i]?.gameObject.SetActive(previewSlot == i);
        }

        private void ShowMenu(bool pause = false)
        {
            deathScreen.Cancel();
            inMenu = true;
            pauseMenu = pause;
            cityTitleVisible = false;
            titleShownAt = Time.unscaledTime;
            menuStage.gameObject.SetActive(!pause);
            Time.timeScale = session?.Online == true ? 1 : 0;
            SetCursor(false);
            if (!pause)
                UpdateMenuPreview();
            ApplyWorldHour();
        }

        private void ReturnToMainMenu()
        {
            menu.ShowDeploy();
            ShowMenu();
        }

        private void StartRun()
        {
            if (!session.Online && navigationBuild != null)
                return;
            bool restored = !session.Online && !harborJob && RestoreCheckpoint();
            if (!session.Online && !restored)
                ResetRun();
            enteredRun = true;
            ResumeRun(restored);
        }

        private void ResumeRun() => ResumeRun(false);

        private void ResumeRun(bool restored)
        {
            bool deployFromMenu = !pauseMenu;
            if (session.Online)
                enteredRun = enteredOnlineWorld = true;
            if (deployFromMenu && !restored)
                transitions.Begin(session.Online ? L("Entering session", "Entrando na partida") : "Santa Luz", 1.5f, .98f);
            if (menu.SelectionVersion != appliedSelection)
            {
                appliedSelection = menu.SelectionVersion;
                EquipWeapon(menu.SelectedWeapon);
            }
            inMenu = false;
            cityTitleVisible = false;
            menuStage.gameObject.SetActive(false);
            Time.timeScale = 1;
            PositionCamera(false, true);
            ApplyWorldHour();
            SetCursor(true);
            if (deployFromMenu && !restored && car == null)
                TryCinematic("SantaLuzArrival");
        }

        private bool TryCinematic(string id)
        {
            if (string.IsNullOrEmpty(id) || cinematics == null || session.Online || session.Hosting || session.Connecting ||
                wantedLevel > 0 || pursuit || encounter.Active || jobs.InProgress || harborJob && state == RunState.Escape ||
                PlayerPrefs.GetInt("CostaCinematic." + id, 0) != 0)
                return false;
            CostaCinematic asset = Resources.Load<CostaCinematic>("Harbor/Cinematics/" + id);
            if (!cinematics.Play(asset, playerBody, player, () =>
            {
                PlayerPrefs.SetInt("CostaCinematic." + id, 1);
                PlayerPrefs.Save();
                SetCursor(true);
            }))
                return false;
            SetCursor(false);
            return true;
        }

        private void BindControls()
        {
            controls = Instantiate(inputActions);
            InputActionMap map = controls.FindActionMap("Player", true);
            move = map.FindAction("Move", true);
            sprint = map.FindAction("Sprint", true);
            interact = map.FindAction("Interact", true);
            look = map.FindAction("Look", true);
            jump = map.FindAction("Jump", true);
            roll = map.FindAction("Crouch", true);
            shoot = AddButton(map, "Fire weapon", "<Mouse>/leftButton", "<Gamepad>/rightTrigger");
            aim = AddButton(map, "Aim weapon", "<Mouse>/rightButton", "<Gamepad>/leftTrigger");
            accelerate = map.AddAction("Accelerate vehicle", InputActionType.Value, "<Gamepad>/rightTrigger");
            reverse = map.AddAction("Reverse vehicle", InputActionType.Value, "<Gamepad>/leftTrigger");
            reload = AddButton(map, "Reload weapon", "<Keyboard>/r", "<Gamepad>/buttonWest");
            nextWeapon = AddButton(map, "Next weapon", "<Gamepad>/rightShoulder");
            previousWeapon = AddButton(map, "Previous weapon", "<Gamepad>/leftShoulder");
            restart = AddButton(map, "Restart run", "<Keyboard>/f5", "<Gamepad>/start");
            openMenu = AddButton(map, "Open menu", "<Keyboard>/escape", "<Gamepad>/select");
            jobsBoard = AddButton(map, "Jobs board", "<Keyboard>/tab", "<Gamepad>/rightStickPress");
            mapKey = AddButton(map, "City map", "<Keyboard>/m", "<Gamepad>/dpad/right");
            bagKey = AddButton(map, "Backpack", "<Keyboard>/i", "<Gamepad>/dpad/up");
            crouchKey = AddButton(map, "Crouch stance", "<Keyboard>/leftCtrl", "<Gamepad>/dpad/down");
            shoulderSwap = AddButton(map, "Swap shoulder", "<Keyboard>/v", "<Gamepad>/dpad/left");
            for (int i = 0; i < weaponSlots.Length; i++)
                weaponSlots[i] = AddButton(map, $"Weapon slot {i + 1}", $"<Keyboard>/{i + 1}");
            bindings = new CostaBindings(sprint, jump, roll, interact, reload);
            padBindings = new CostaPadBindings(sprint, jump, roll, interact, reload, shoot, aim,
                nextWeapon, previousWeapon, crouchKey, mapKey, bagKey, openMenu, jobsBoard, shoulderSwap);
        }

        private static InputAction AddButton(InputActionMap map, string name, string binding, string secondary = null)
        {
            InputAction action = map.AddAction(name, InputActionType.Button, binding);
            if (secondary != null)
                action.AddBinding(secondary);
            return action;
        }

        private void OnEnable() => move.actionMap.Enable();

        private void OnDisable()
        {
            move?.actionMap.Disable();
            SetCursor(false);
        }

        private void OnDestroy()
        {
            deathScreen.Release();
            if (inMenu || overlay != FieldOverlay.None || minigames?.BlocksWorld == true)
                Time.timeScale = 1;
            cinematics?.Release();
            harbor?.Mirrors?.Release();
            session?.Dispose();
            onlinePanel?.Release();
            jobs?.Ledger.FinishBank(wait:true);
            remotes?.Dispose();
            menu?.Release();
            loadingScreen?.Release();
            touch?.Release();
            menuBody?.Release();
            playerBody?.Release();
            foreach (Guard guard in guards)
                guard?.Character?.Release();
            if (navMeshInstance.valid)
                navMeshInstance.Remove();
            if (navigationBuild != null)
                NavMeshBuilder.Cancel(navMesh);
            Destroy(navMesh);
            Destroy(controls);
            arsenal?.Dispose();
            urbanAudio?.Release();
            effects?.Release();
            bagPanel?.Release();
            cityMap?.Release();
            encounter?.Dispose();
            atmosphere?.Release();
            venues?.Release();
            livingWorld?.Release();
            policeSystem?.Release();
            territories?.Release();
            ReleaseWorkshop();
            city?.Interiors.Release();
            traffic?.Release();
            harbor?.ReleaseMaterials();
        }

        private void OnApplicationFocus(bool focused)
        {
            cinematics?.SetFocused(focused);
            if (!focused)
            {
                menu?.FlushGraphics();
                SetCursor(false);
                pausedForFocus = session?.Online != true && !inMenu && overlay == FieldOverlay.None;
                if (pausedForFocus)
                    Time.timeScale = 0;
            }
            else if (pausedForFocus)
            {
                pausedForFocus = false;
                if (!inMenu && overlay == FieldOverlay.None && minigames?.BlocksWorld != true && !deathScreen.BlocksGameplay)
                    Time.timeScale = 1;
            }
        }

        private readonly CostaSession.Avatar networkPose = new();

        private CostaSession.Avatar CaptureNetworkPose()
        {
            CostaSession.Avatar pose = networkPose;
            pose.pos = player.transform.position;
            pose.yaw = car != null ? car.Root.eulerAngles.y : playerModel.eulerAngles.y;
            pose.revision = netRevision;
            pose.playing = enteredOnlineWorld;
            pose.aiming = aimingPose;
            pose.aimPitch = pitch;
            pose.standing = grounded && !crouching && rollClock <= 0 && car == null;
            pose.crouched = crouching;
            pose.jumpPose = car == null && (!grounded || landing > 0)
                    ? grounded ? Mathf.Lerp(0.78f, 1f, 1 - landing / landingDuration) : Mathf.Min(jumpClock / 0.8f, 0.77f)
                    : -1;
            pose.rollPose = rollClock > 0 ? 1 - rollClock / RollDuration : -1;
            pose.moving = !inMenu && overlay == FieldOverlay.None && walking;
            pose.running = !inMenu && overlay == FieldOverlay.None && runningPose;
            pose.bikeLean = car != null ? car.RiderLean : 0;
            pose.held = car != null ? CostaVehicle.EncodeSpeed(car.SignedSpeed) : 0;
            return pose;
        }

        private void TickNetwork()
        {
            if (!session.Online && !session.Connecting && !session.Hosting && !session.Migrating && !wasOnline)
                return;

            int priorHeat = session.Local?.wantedLevel ?? 0;
            session.InteriorListener = followCamera.transform.position;
            session.Tick(CaptureNetworkPose());
            int impactBudget = 16;
            while (impactBudget-- > 0 && session.ReadImpact(out var impact))
                effects.SurfaceHit((CostaWorldDamage.Surface)impact.material, impact.point, impact.normal, impact.broken,
                    followCamera.transform.position);
            var harm = session.Harm;
            if (harm.Amount > 0)
            {
                effects.CombatFeedback(harm.Source, harm.Amount);
                score.Combat();
                CameraKick(Mathf.Lerp(.15f, .68f, Mathf.Clamp01(harm.Amount / 60f)));
            }
            if (session.Pressure.HasValue && harm.Amount == 0)
            {
                effects.CombatFeedback(session.Pressure.Value, 0);
                score.Combat(2.8f);
            }
            switch (session.NoticeEvent)
            {
                case 3: case 4: effects.Play(Cue.Cuffs); break;
                case 5: effects.Play(Cue.Loot); break;
                case 7: effects.Play(Cue.Sale); break;
                case 8: effects.Play(Cue.Purchase); break;
                case 11:
                    effects.Play(Cue.Wasted, 0.72f);
                    deathScreen.BeginMultiplayer();
                    break;
            }
            if (session.Online && priorHeat == 0 && session.Local.wantedLevel > 0)
                effects.Play(Cue.Alert, 0.32f);

            if (!session.Online)
            {
                if (session.Migrating)
                    return;
                if (soloConnectionPending && !session.Connecting && !session.Hosting)
                {
                    soloConnectionPending = false;
                    if (!wasOnline)
                    {
                        for (int i = 0; i < cars.Length; i++)
                            cars[i].ApplyBuild(jobs.Account.mods[i]);
                        soloContractCar = soloDrivenCar = null;
                    }
                }
                if (wasOnline)
                {
                    deathScreen.Cancel();
                    wasOnline = false;
                    for (int i=0;i<cars.Length;i++)
                    {
                        cars[i].Root.gameObject.SetActive(true);
                        cars[i].ApplyBuild(jobs.Account.mods[i]);
                    }
                    enteredOnlineWorld = false;
                    equipSequence = 0;
                    queuedEquip = -1;
                    netRevision = -1;
                    staminaRefill = 0;
                    pendingBuy = -1;
                    remotes.Clear();
                    arsenal.SetOnlineWeapons(null);
                    localLook.Apply(0, false);
                    jobs.SetOnlineDoors(false);
                    city.UpdateHouseDoors(jobs, false, snap: true);
                    jobs.ResumeAfterSession();
                    CloseOverlay();
                    policeSystem?.SetActiveForOnline(false);
                    ResetRun(resetJob: false);
                    if (soloContractCar != null)
                        soloContractCar.ApplyNetwork(soloContractCarPosition, soloContractCarYaw, 0, true);
                    encounter.SetOnline(false);
                    UpdateContractWeapon();
                    if (soloResumable)
                    {
                        player.enabled = false;
                        player.transform.position = soloPosition;
                        player.enabled = true;
                        yaw = soloYaw;
                        pitch = soloPitch;
                        playerModel.rotation = Quaternion.Euler(0, yaw, 0);
                        if (soloDrivenCar != null)
                        {
                            soloDrivenCar.ApplyNetwork(soloDrivenPosition, soloDrivenYaw, 0, true);
                            player.transform.position = soloDrivenPosition;
                            EnterVehicle(soloDrivenCar);
                        }
                        Physics.SyncTransforms();
                    }
                    soloContractCar = soloDrivenCar = null;
                    harborJob = soloHarborJob;
                    enteredRun = soloResumable;
                    state = soloState;
                    escapeStartedAt = Time.time - soloEscapeElapsed;
                    cargo.SetActive(harborJob && state == RunState.FindCargo);
                    wantedLevel = soloWantedLevel;
                    recoveryMissionStar = soloRecoveryMissionStar;
                    heatUntil = Time.time + soloHeatRemaining;
                    nextCrime = Time.time + soloCrimeRemaining;
                    if (wantedLevel > 0)
                    {
                        pursuit = true;
                        nextPursuitUpdate = Time.time + 0.45f;
                    }
                    menu.ShowOnline();
                    ShowMenu(pause: soloResumable);
                }
                return;
            }

            bool joined = !wasOnline;
            if (joined)
            {
                deathScreen.Cancel();
                soloConnectionPending = false;
                soloResumable = CanResume();
                soloHarborJob = harborJob;
                soloState = state;
                soloPosition = player.transform.position;
                soloYaw = yaw;
                soloPitch = pitch;
                soloEscapeElapsed = state == RunState.Escape ? Time.time - escapeStartedAt : 0;
                soloWantedLevel = wantedLevel;
                soloRecoveryMissionStar = recoveryMissionStar;
                soloHeatRemaining = Mathf.Max(0, heatUntil - Time.time);
                soloCrimeRemaining = Mathf.Max(0, nextCrime - Time.time);
                jobs.PauseForSession();
                encounter.SetOnline(true);
                arsenal.SetLoanerPistol(false);

                wasOnline = true;
                soloSeat = soloLift = -1;
                jobs.SetOnlineDoors(true);
                city.UpdateHouseDoors(jobs, true, snap: true);
                pursuit = false;
                harborJob = false;
                cargo.SetActive(false);
                policeSystem?.SetActiveForOnline(true);
                session.Request("outfit", onlinePanel.Outfit);
                // A connection can finish while a solo session is active.
                CloseOverlay();
                menu.ShowOnline();
                ShowMenu();
            }

            CostaSession.Avatar avatar = session.Local;
            if (staminaRefill != session.StaminaRefill)
            {
                staminaRefill = session.StaminaRefill;
                stamina = FullStamina;
                needsSprintRelease = false;
            }
            if (avatar.held > 0 && overlay != FieldOverlay.None)
                CloseOverlay();
            bool syncCars = joined;
            for (int i = 0; i < cars.Length; i++)
            {
                bool relevant = session.CarRelevant(i);
                if (!session.Hosting && cars[i].Root.gameObject.activeSelf != relevant)
                {
                    cars[i].Root.gameObject.SetActive(relevant);
                    syncCars = true;
                }
                if (!relevant) continue;
                CostaSession.Vehicle vehicle = session.Cars[i];
                cars[i].ApplyBuild(vehicle.tune);
                if (i != avatar.vehicle || netRevision != avatar.revision)
                    syncCars |= cars[i].ApplyNetwork(vehicle.pos, vehicle.yaw, vehicle.speed, joined || i == avatar.vehicle, vehicle.lean);
                cars[i].SetDamage(new Vector2(vehicle.frontDamage, vehicle.rearDamage));
            }

            if (syncCars)
                Physics.SyncTransforms();

            if (netRevision != avatar.revision)
            {
                netRevision = avatar.revision;
                vehicleMotion.Begin(avatar, cars);
                state = RunState.FindCargo;
                car = avatar.vehicle >= 0 ? cars[avatar.vehicle] : null;
                player.enabled = false;
                player.transform.position = car != null ? car.Root.position : avatar.pos;
                playerModel.localPosition = Vector3.zero;
                playerModel.rotation = Quaternion.Euler(0, avatar.yaw, 0);
                playerModel.gameObject.SetActive(car == null || car.IsBike);
                arsenal.SetVisible(car == null);
                ResizeController(1.8f);
                ApplyVehiclePresentation();
                crouching = walking = runningPose = false;
                grounded = true;
                verticalSpeed = -2.3f;
                rollClock = 0;
                ResetFootMotion();
                yaw = avatar.yaw;
                if (!inMenu && !vehicleMotion.Active)
                    PositionCamera(false, true);
            }

            arsenal.SetOnlineWeapons(session.WeaponMask);
            if (joined || wasPolice != avatar.police)
            {
                wasPolice = avatar.police;
                pendingBuy = -1;
                arsenal.Reset();
                arsenal.Select(avatar.equipped);
            }
            if (avatar.playing && queuedEquip >= 0)
            {
                session.Equip(queuedEquip, ++equipSequence);
                queuedEquip = -1;
            }
            else if (queuedEquip < 0 && session.EquipAcknowledged == equipSequence && arsenal.Slot != avatar.equipped)
            {
                arsenal.Select(avatar.equipped);
            }
            arsenal.SyncAmmo(session.Loaded, session.Reloading);
            if (pendingBuy >= 0 && arsenal.Owned(pendingBuy))
            {
                EquipWeapon(pendingBuy);
                pendingBuy = -1;
            }
            localLook.Apply(avatar.outfit, avatar.police);
            remotes.Refresh(session, cars, followCamera);
            robberies.Refresh(session.Sites);
        }

        private bool Pressed(InputAction action, TouchControl control) => action.WasPressedThisFrame() || touch.Pressed(control);
        private bool Held(InputAction action, TouchControl control) => action.IsPressed() || touch.Down(control);
        private Vector2 MovementInput() => touch.Down(TouchControl.Move) ? touch.Move : move.ReadValue<Vector2>();

        private void ApplyWorldHour()
        {
            float? hour = null;
            if (!inMenu || pauseMenu)
                hour = session.Online ? session.WorldHour : HarborDaylight.Advance(HarborDaylight.StartHour, soloDaySeconds);
            else if (cityTitleVisible)
                hour = titleBackdrop.Hour;
            var weather = hour.HasValue ? HarborWeather.Conditions.At(hour.Value) : default;
            worldWeather = weather;
            menu.SetWorldHour(hour, weather);
            atmosphere.Apply(hour, weather, followCamera, menu.GraphicsPreset, harbor);
            harbor.SetWaterDaylight(hour ?? HarborDaylight.StartHour);
            float night = hour.HasValue ? HarborDaylight.CityNight(hour.Value) : 0;
            cityNight = night;
            Vector3 viewerPos = followCamera.transform.position;
            int lightBudget = menu.StreetLightBudget;
            if (Time.unscaledTime >= nextStreetLightUpdate || lightBudget != lastStreetLightBudget
                || (viewerPos - lastStreetLightViewer).sqrMagnitude > 6.25f)
            {
                nextStreetLightUpdate = Time.unscaledTime + 0.16f;
                lastStreetLightViewer = viewerPos;
                lastStreetLightBudget = lightBudget;
                harbor.UpdateStreetLights(night, viewerPos, lightBudget);
            }
            float grip = Mathf.Lerp(1, .64f, weather.Wet);
            if (cars != null && (Mathf.Abs(night - lastCarNight) > 0.005f || Mathf.Abs(grip - lastCarGrip) > 0.005f))
            {
                lastCarNight = night;
                lastCarGrip = grip;
                foreach (CostaVehicle vehicle in cars)
                {
                    vehicle.Grip = grip;
                    vehicle.SetRunningLights(night);
                }
            }
            if (hour.HasValue && venues != null)
            {
                venues.Update(hour.Value, followCamera.transform.position);
                traffic.PresentCrew(followCamera.transform.position);
                if (!session.Online && !session.Hosting)
                {
                    if (!inMenu && overlay == FieldOverlay.None && !pausedForFocus)
                        traffic.Simulate(Time.deltaTime, hour.Value, pursuit ? player.transform.position : null, Time.unscaledDeltaTime);
                    traffic.UpdateSignals(hour.Value);
                }
            }
        }

        private void Update()
        {
            menu.SaveGraphicsIfDue();
            harbor.SetUltraVisuals(menu.Ultra, menu.FullTextures);
            if (worldLoadFailed)
                CostaUi.Update(false);
            if (blockingWorldLoad || worldLoadFailed)
                return;
            if (cinematics?.Active == true)
            {
                CostaUi.Update(false);
                if (!pausedForFocus)
                    cinematics.Tick();
                return;
            }

            if (worldReady)
            {
                deathScreen.Tick(pausedForFocus || inMenu);
                TickNetwork();
                UpdatePropertyBilling();
                if (!session.Online && !inMenu && overlay == FieldOverlay.None)
                    soloDaySeconds += Time.deltaTime;
                ApplyWorldHour();
                UpdateInteriors();
            }
            touch.Update(!inMenu && overlay == FieldOverlay.None && !pausedForFocus && !deathScreen.BlocksGameplay,
                car != null, worldReady && !session.Online && (state == RunState.Caught || state == RunState.Delivered));
            if (inMenu || overlay != FieldOverlay.None)
                CostaUi.Update(menu.CapturingKey);
            if (pausedForFocus)
                return;
            if (inMenu)
            {
                if (!worldReady && !worldLoading && menu.LiveTitle && Time.unscaledTime > titleShownAt + 1.2f)
                    WithWorld(null);
                if (menu.CapturingKey)
                {
                    menu.ReadBinding();
                    if (!pauseMenu)
                        UpdateMenuPreview();
                    return;
                }
                if (menu.DeployPage
                    && (Pressed(restart, TouchControl.Retry) || Keyboard.current?.enterKey.wasPressedThisFrame == true))
                {
                    gamepad = restart.activeControl?.device is Gamepad;
                    if (CanResume())
                        ResumeRun();
                    else
                        deploy();
                }
                else if (Pressed(openMenu, TouchControl.Menu) || jobsBoard.WasPressedThisFrame())
                {
                    if (pauseMenu && !menu.DeployPage)
                        menu.ShowDeploy();
                    else if (CanResume())
                        ResumeRun();
                }
                else if (!pauseMenu)
                    UpdateMenuPreview();
                return;
            }

            if (overlay != FieldOverlay.None)
            {
                if (overlay == FieldOverlay.Workshop) UpdateWorkshopCamera();
                else if (overlay is FieldOverlay.Dealership or FieldOverlay.Garage) UpdateShowroomCamera();
                if (Pressed(openMenu, TouchControl.Menu) || (overlay == FieldOverlay.Map && Pressed(mapKey, TouchControl.Map))
                    || (overlay == FieldOverlay.Bag && Pressed(bagKey, TouchControl.Bag)))
                    CloseOverlay();
                return;
            }

            if (minigames != null && minigames.BlocksWorld)
            {
                minigames.UpdateUiActivity();
                if (!minigames.BlocksWorld)
                {
                    Time.timeScale = 1f;
                    SetCursor(true);
                }
                return;
            }
            if (deathScreen.BlocksGameplay)
            {
                deathScreen.UpdateCameraAndBody(followCamera, player.transform, playerModel, yaw, pitch, car != null);
                if (!session.Online)
                    CoastIdleVehicles();
                return;
            }
            if (HandleChatInput())
                return;
            if (!session.Online)
            {
                livingWorld?.Update(player.transform.position, InteriorHour, pursuit);
                minigames?.UpdateWorldActivity(player.transform.position, car);
                UpdateTerritories();
            }
            if (vehicleMotion.Active)
            {
                if (vehicleMotion.Sample(playerBody, effects))
                {
                    PositionCamera(false);
                    if (!session.Online)
                    {
                        CoastIdleVehicles();
                        if (pursuit)
                            UpdatePursuit();
                    }
                    return;
                }
                ApplyVehiclePresentation();
            }

            if (Pressed(openMenu, TouchControl.Menu) || (!session.Online && jobsBoard.WasPressedThisFrame()))
            {
                if (jobsBoard.WasPressedThisFrame() && !session.Online)
                    menu.ShowJobs();
                else
                    menu.ShowDeploy();
                ShowMenu(pause: true);
                return;
            }

            if (!session.Online && Pressed(restart, TouchControl.Retry))
            {
                gamepad = restart.activeControl?.device is Gamepad;
                if (harborJob || !RestoreCheckpoint())
                {
                    ResetRun();
                    transitions.Begin(L("Back in Santa Luz", "De volta a Santa Luz"), 1.45f, .91f);
                }
                return;
            }

            if (state == RunState.Delivered || state == RunState.Caught)
                return;

            if (Pressed(mapKey, TouchControl.Map))
            {
                OpenOverlay(FieldOverlay.Map);
                return;
            }

            if (Pressed(bagKey, TouchControl.Bag))
            {
                OpenOverlay(FieldOverlay.Bag);
                return;
            }
            if (car == null && Keyboard.current?.gKey.wasPressedThisFrame == true)
            {
                SummonOwnedVehicle(FindFirstOwnedVehicle());
            }
            if (session.Online && session.Local.held > 0)
                return;

            bool recapturing = Cursor.lockState != CursorLockMode.Locked && Mouse.current?.leftButton.wasPressedThisFrame == true;
            if (recapturing)
                SetCursor(true);

            InputDevice device = look.activeControl?.device ?? move.activeControl?.device ?? shoot.activeControl?.device;
            if (device != null)
                gamepad = device is Gamepad;

            Vector2 cameraInput = look.ReadValue<Vector2>();
            if (look.activeControl?.device is Gamepad)
                cameraInput *= 150f * Time.deltaTime;
            else
                cameraInput *= look.activeControl?.device is not Touchscreen && Cursor.lockState == CursorLockMode.Locked && !recapturing ? 0.14f * menu.Sensitivity : 0;

            cameraInput += touch.Look * (100f / Mathf.Max(1, Screen.height)) * menu.Sensitivity;
            yaw = Mathf.Repeat(yaw + cameraInput.x, 360f);
            pitch = Mathf.Clamp(pitch - cameraInput.y, -25f, 55f);

            if (Pressed(shoulderSwap, TouchControl.Shoulder))
                shoulderSide = -shoulderSide;
            city.Animate(followCamera.transform.position, followCamera.farClipPlane);
            city.UpdateHouseDoors(jobs, session.Online);
            bool interaction = Pressed(interact, TouchControl.Use);
            if (car == null && (CurrentSeat >= 0 || CurrentLift >= 0))
            {
                if (interaction && CurrentSeat >= 0)
                    UseInterior(CurrentSeat);
                PositionCamera(false);
                return;
            }
            if (interaction && car == null && CostaSession.AtBank(player.transform.position))
            {
                bankingMessage = "";
                OpenOverlay(FieldOverlay.Bank);
                return;
            }
            if (interaction && CanOpenWorkshop())
            {
                OpenWorkshop();
                return;
            }
            if (interaction && CanOpenDealership())
            {
                OpenDealership();
                return;
            }
            if (interaction && CanOpenGarage())
            {
                OpenGarage();
                return;
            }
            TrySaveStage();
            if (interaction && CanOpenCampaign())
            {
                OpenOverlay(FieldOverlay.Campaign);
                return;
            }
            bool interiorAction = car == null && interaction && rollClock <= 0 && !UsingContractObjective()
                && TryInteriorInteraction();
            if (interiorAction)
            {
                interaction = false;
                if (overlay == FieldOverlay.Elevator)
                    return;
            }
            bool pendingVehicleSync = false;
            if (!session.Online)
            {
                robberies.Refresh();
                pendingVehicleSync = CoastIdleVehicles(defer: car != null && !interaction);
            }
            cargoPack.SetActive(CurrentBag.Count > 0 || state == RunState.Escape
                || !session.Online && encounter.CarryingLoot);
            cityMap.CheckWaypoint(player.transform.position);
            if (interaction && car == null && !UsingContractObjective())
            {
                if (IsPolice)
                {
                    CostaSession.Avatar suspect = remotes.ArrestCandidate(player.transform.position);
                    if (suspect != null)
                    {
                        session.Request("arrest", suspect.id);
                        interaction = false;
                    }
                }
                else if (CostaSession.AtVendor(player.transform.position))
                {
                    OpenOverlay(FieldOverlay.Vendor);
                    return;
                }
                else if (IsNear(CostaCity.GunCounter, 2f))
                {
                    OpenOverlay(FieldOverlay.Shop);
                    return;
                }
            }
            if (interaction && session.CrewInteraction(player.transform.position, car != null) is int support && support >= 0)
            {
                session.Request("crew", support);
                interaction = false;
            }
            if (interaction && car != null && !session.Online && TryWorldInteraction())
                interaction = false;
            if (interaction && TryUseVehicle())
                interaction = false;
            if (interaction && car == null && grounded && !UsingContractObjective()
                && robberies.Nearby(player.transform.position) < 0 && venues.Use(player.transform.position))
            {
                GestureAt(HarborCharacter.Gesture.Reach);
                effects.Play(Cue.PanelOpen, .25f);
                interaction = false;
            }
            if (interaction && !session.Online)
            {
                if (livingWorld != null && livingWorld.TryClaimEvent(player.transform.position, TryRewardWorldActivity))
                {
                    GestureAt(HarborCharacter.Gesture.PickUp);
                    interaction = false;
                }
                else if (TryWorldInteraction())
                    interaction = false;
            }
            if (vehicleMotion.Active)
                return;

            if (car != null)
            {
                Vector2 steering = MovementInput();
                if (!touch.Down(TouchControl.Move) && Gamepad.current != null && move.activeControl?.device is not Keyboard)
                    steering.y = accelerate.ReadValue<float>() - reverse.ReadValue<float>();
                engineThrottle = steering.y;
                float previousSpeed = car.Speed;
                bool braking = Held(jump, TouchControl.Jump);
                bool nitro = Held(sprint, TouchControl.Sprint);
                if (car.Drive(steering, braking, nitro) || pendingVehicleSync)
                    Physics.SyncTransforms();
                if (previousSpeed - car.Speed > Mathf.Max(2.6f, Time.deltaTime * 24f))
                    CameraKick(Mathf.Clamp01((previousSpeed - car.Speed) / 16f) * 0.62f);
                effects.Brake(previousSpeed, braking && Pressed(jump, TouchControl.Jump) ? 0 : car.Speed);
                effects.Skid(car.Slip, car.Speed);
                player.transform.position = car.Root.position;
                if (car.IsBike)
                {
                    Quaternion attitude = car.Root.rotation * Quaternion.Euler(11, 0, car.RiderLean);
                    Vector3 seat = car.Root.TransformPoint(new Vector3(0, 0.85f, -0.27f));
                    playerModel.SetPositionAndRotation(seat - attitude * Vector3.up * 0.94f, attitude);
                    playerBody.Ride();
                }
                yaw = Mathf.LerpAngle(yaw, car.Root.eulerAngles.y, 1 - Mathf.Exp(-Time.deltaTime * 6f));
                pitch = Mathf.Lerp(pitch, 15f, 1 - Mathf.Exp(-Time.deltaTime * 4f));
                PositionCamera(false);
                UpdateCityJobs(false);
                if (pursuit)
                    UpdatePursuit();
                AnimatePolice();
                return;
            }

            CycleWeapons();

            bool aiming = arsenal.HasFirearm && !arsenal.Reloading && rollClock <= 0 && Held(aim, TouchControl.Aim)
                && (touch.Down(TouchControl.Aim) || Cursor.lockState == CursorLockMode.Locked || aim.activeControl?.device is Gamepad);
            aimingPose = aiming;
            MovePlayer(aiming);
            PositionCamera(aiming && rollClock <= 0);

            bool fireEnabled = rollClock <= 0 && (!arsenal.KnifeEquipped || !crouching) && !recapturing
                && (touch.Down(TouchControl.Fire) || Cursor.lockState == CursorLockMode.Locked || shoot.activeControl?.device is Gamepad);
            bool firePressed = fireEnabled && Pressed(shoot, TouchControl.Fire);
            bool fireHeld = fireEnabled && Held(shoot, TouchControl.Fire);
            if (Pressed(reload, TouchControl.Reload) || arsenal.Loaded == 0 && (firePressed || fireHeld))
            {
                if (!session.Online)
                    arsenal.Reload();
                else if (arsenal.HasFirearm && !arsenal.Reloading && arsenal.Loaded < arsenal.Capacity
                    && Time.unscaledTime >= nextReloadRequest)
                {
                    nextReloadRequest = Time.unscaledTime + 0.25f;
                    session.Request("reload", arsenal.Slot);
                }
            }
            bool contractCombat = !session.Online && encounter.Combat;
            float acknowledgedReload = session.Online && session.Local.equipped == arsenal.Slot
                ? session.Local.reloadProgress : -1;
            if (acknowledgedReload >= 0 && arsenal.Slot >= 0 && arsenal.HasFirearm)
                displayedReload = displayedReload < 0 ? acknowledgedReload : Mathf.Min(acknowledgedReload + .075f,
                    Mathf.Max(acknowledgedReload, displayedReload + Time.unscaledDeltaTime / HarborArsenal.ShotRules(arsenal.Slot).reload));
            else
                displayedReload = -1;
            if (arsenal.Update(followCamera, playerBody, pitch, firePressed,
                fireHeld, aiming, crouching, ResolveSoloHit, effects, displayedReload, rollClock <= 0))
            {
                if (arsenal.HasFirearm)
                {
                    score.Combat();
                    livingWorld?.AlertDanger(player.transform.position, 32f);
                }
                if (session.Online)
                    session.Fire(arsenal.Slot, arsenal.ShotDirection, CaptureNetworkPose());
                else if (!contractCombat)
                    StartPursuit(0.25f, combat: true);
            }

            bool originalInteraction = harborJob && state == RunState.FindCargo
                && IsNear(HarborBlockout.Cargo, InteractionRadius) && interaction;
            if (originalInteraction)
            {
                GestureAt(HarborCharacter.Gesture.PickUp, cargo.transform.position);
                state = RunState.Escape;
                escapeStartedAt = Time.time;
                cargo.SetActive(false);
                cargoPack.SetActive(true);
                StartPursuit(0.65f);
            }

            UpdateCityJobs(interaction && !originalInteraction);
            if (pursuit)
                UpdatePursuit();
            AnimatePolice();
        }

        private bool TryWorldInteraction()
        {
            var node = worldInteractive?.FindNearby(player.transform.position, car != null);
            if (node == null || !CostaWorldInteractive.IsOpen(node, InteriorHour))
                return false;
            node.OnInteract?.Invoke();
            GestureAt(HarborCharacter.Gesture.Reach);
            effects.Play(Cue.PanelOpen, .25f);
            return true;
        }

        private bool CoastIdleVehicles(bool defer = false)
        {
            bool moved = false;
            foreach (CostaVehicle vehicle in cars)
            {
                if (vehicle == car || vehicle.Speed <= 0.05f)
                    continue;

                Vector3 before = vehicle.Root.position;
                if (!vehicle.Drive(Vector2.zero, false))
                    continue;
                moved = true;
                if (defer)
                {
                    // Include both collider extents, the driven car sweep and the idle car displacement.
                    float reach = 6f + car.TopSpeed * Time.deltaTime + Vector3.Distance(before, vehicle.Root.position);
                    if ((before - car.Root.position).sqrMagnitude <= reach * reach)
                        defer = false;
                }
            }
            if (moved && !defer)
                Physics.SyncTransforms();
            return moved && defer;
        }

        private void OpenOverlay(FieldOverlay screen)
        {
            effects.Play(Cue.PanelOpen, 0.32f);
            overlay = screen;
            if (screen == FieldOverlay.Map)
                cityMap.ResetPan();
            if (screen is FieldOverlay.Bag or FieldOverlay.Vendor)
                bagPanel?.Open();
            Time.timeScale = session.Online || session.Hosting ? 1 : 0;
            SetCursor(false);
        }

        private void CloseOverlay()
        {
            if (overlay is FieldOverlay.Workshop or FieldOverlay.Dealership or FieldOverlay.Garage) ReleaseWorkshop();
            if (overlay != FieldOverlay.None)
                effects.Play(Cue.PanelClose, 0.27f);
            bagPanel?.Hide();
            overlay = FieldOverlay.None;
            Time.timeScale = 1;
            SetCursor(true);
        }

        private float InteriorHour => session.Online ? session.WorldHour
            : HarborDaylight.Advance(HarborDaylight.StartHour,soloDaySeconds);

        private void UpdateInteriors()
        {
            CostaInteriors interiors = city.Interiors;
            float delta = Mathf.Min(Time.unscaledDeltaTime,.1f);
            float hour = InteriorHour;
            bool running = !inMenu && !pausedForFocus;
            if (!session.Online && !session.Hosting && !session.Connecting && running && overlay != FieldOverlay.Elevator)
            {
                interiors.ObserveDoor(player.transform.position, Time.unscaledTime,hour);
                interiors.Step(Time.unscaledTime,delta);
            }
            if (!session.Hosting)
                interiors.Present(delta,!session.Online,effects,followCamera.transform.position);
            if (CurrentLift >= 0)
            {
                int lift = CurrentLift;
                player.enabled = false;
                Vector3 position = player.transform.position;
                position.y = interiors.VisualHeight(lift);
                player.transform.position = position;
                if (!session.Online && !interiors.Moving(lift))
                {
                    soloLift = -1;
                    player.enabled = true;
                }
            }
            if (CurrentSeat >= 0)
            {
                player.enabled = false;
                playerBody.Sit();
                arsenal.SetVisible(false);
            }
            interiors.Stream(player.transform.position,hour,running || pauseMenu,menu.GraphicsPreset <= 1 || menu.AdaptiveWorldTier >= 2,
                running && overlay == FieldOverlay.None ? 1 : 0);
        }

        private bool TryInteriorInteraction()
        {
            Vector3 position = player.transform.position;
            int key = city.Interiors.FindAction(position,CurrentSeat,CurrentLift,InteriorHour,out _,out int picker);
            if (key < 0)
                return false;
            if (picker >= 0)
            {
                liftPicker = picker;
                OpenOverlay(FieldOverlay.Elevator);
                return true;
            }
            UseInterior(key);
            return true;
        }

        private void UseInterior(int key)
        {
            if (session.Online)
            {
                session.Request("interior",key);
                return;
            }

            CostaInteriors interiors = city.Interiors;
            Vector3 position = player.transform.position;
            int prior = soloSeat;
            bool accepted = interiors.Use(key,Time.unscaledTime,InteriorHour,ref position,ref soloSeat,out var action,out int notice);
            if (accepted && action == CostaInteriors.Action.Sit && prior != soloSeat)
            {
                player.enabled = false;
                player.transform.position = position;
                if (soloSeat >= 0)
                    playerModel.rotation = Quaternion.Euler(0,180,0);
                ApplyVehiclePresentation();
                player.enabled = soloSeat < 0;
            }
            if (accepted && action is CostaInteriors.Action.Call or CostaInteriors.Action.Travel)
            {
                int lift = CostaInteriors.BuildingOf(key);
                if (interiors.Boarded(lift,position))
                    soloLift = lift;
            }
            if (accepted && action == CostaInteriors.Action.Service)
            {
                if (Time.unscaledTime < nextSoloTreatment)
                    notice = 35;
                else
                {
                    nextSoloTreatment = Time.unscaledTime + 35;
                    bool hospital = CostaInteriorPlan.All[CostaInteriors.BuildingOf(key)].Purpose == CostaInteriorPlan.Use.Hospital;
                    encounter.RestoreHealth(hospital ? 65 : 12);
                    stamina = FullStamina;
                    notice = 34;
                }
            }
            if (accepted)
                effects.Play(Cue.PanelOpen,.18f);
            if (notice > 0)
            {
                interiorNotice = CostaInteriors.Notice(notice);
                interiorNoticeUntil = Time.unscaledTime + 4;
            }
        }

        private void DrawElevator()
        {
            if (liftPicker < 0)
                return;
            float scale = Mathf.Min(Screen.width/430f,Screen.height/480f,1.5f);
            Matrix4x4 previous = GUI.matrix;
            GUI.matrix = Matrix4x4.Scale(new Vector3(scale,scale,1));
            float width = Screen.width/scale;
            float height = Screen.height/scale;
            var plan = CostaInteriorPlan.All[liftPicker];
            float panelHeight = 114 + 38 * Mathf.CeilToInt((plan.Levels+1)/2f);
            Rect panel = new((width-310)/2,(height-panelHeight)/2,310,panelHeight);
            Color old = GUI.color;
            GUI.color = new Color(.055f,.09f,.10f,.98f);
            GUI.DrawTexture(panel,Texture2D.whiteTexture);
            GUI.color = old;
            elevatorButton ??= new GUIStyle(GUI.skin.button) { fontSize = 15 };
            GUI.Label(new Rect(panel.x+16,panel.y+11,265,30), L("ELEVATOR · SELECT A FLOOR","ELEVADOR · SELECIONE UM ANDAR"));
            for (int level = 0; level <= plan.Levels; level++)
            {
                Rect cell = new(panel.x+15+(level%2)*140,panel.y+48+(level/2)*38,134,32);
                string label = level == 0 ? L("Ground floor","Térreo")
                    : level == plan.Levels ? L("Roof terrace","Cobertura") : L($"Floor {level}",$"{level}º andar");
                if (!CostaUi.Button(cell,label,elevatorButton))
                    continue;
                UseInterior(CostaInteriors.Encode(liftPicker,CostaInteriors.Action.Travel,level));
                CloseOverlay();
                break;
            }
            if (overlay == FieldOverlay.Elevator && CostaUi.Button(new Rect(panel.x+15,panel.yMax-42,274,30),
                L("Close","Fechar"),elevatorButton))
                CloseOverlay();
            GUI.matrix = previous;
        }

        private void AnimatePolice()
        {
            foreach (Guard guard in guards)
            {
                if (guard == null || !guard.Active)
                    continue;
                float distSqr = (guard.Agent.transform.position - player.transform.position).sqrMagnitude;
                if (distSqr < 4900)
                {
                    guard.Character.AnimateDistance(guard.Agent.velocity.sqrMagnitude > 0.35f
                        ? HarborCharacter.Motion.Run : HarborCharacter.Motion.Idle, distSqr, 0.82f);
                }
            }
        }

        private void SelectJob(int index)
        {
            if (navigationBuild != null)
                return;
            if (CanResume() && (index >= 0 && jobs.Active == index || index == -2 && harborJob))
            {
                ResumeRun();
                return;
            }

            bool start = !enteredRun || state == RunState.Caught || state == RunState.Delivered;
            harborJob = index == -2;
            if (start)
                ResetRun();

            enteredRun = true;
            jobs.Select(index >= 0 ? index : -1);
            encounter.Reset();
            UpdateContractWeapon();
            state = RunState.FindCargo;
            cargo.SetActive(harborJob);
            cargoPack.SetActive(CurrentBag.Count > 0);
            ResumeRun();
        }

        private void UpdateCityJobs(bool takeAction)
        {
            if (session.Online)
            {
                if (takeAction)
                    TryLoot();
                return;
            }

            if (harborJob && state == RunState.Escape)
            {
                escapeSeconds = Time.time - escapeStartedAt;
                if (IsNear(HarborBlockout.Safehouse, InteractionRadius))
                {
                    if (Time.unscaledTime < nextPortSave)
                        return;
                    nextPortSave = Time.unscaledTime + 1f;
                    if (!jobs.FinishPort())
                        return;
                    effects.Play(Cue.Reward);
                    EndRun(RunState.Delivered);
                    return;
                }
            }

            int cashBefore = jobs.Credits;
            jobs.CheckClock();
            bool missionInteraction;
            if (encounter.Active)
            {
                bool canUse = car == null && grounded && !crouching && rollClock <= 0;
                missionInteraction = takeAction && UsingContractObjective();
                if (missionInteraction && canUse && jobs.Current.Mode != CostaJobs.Action.Seize)
                    GestureAt(jobs.Current.Mode == CostaJobs.Action.Collect ? HarborCharacter.Gesture.PickUp
                        : jobs.Current.Mode == CostaJobs.Action.Breach ? HarborCharacter.Gesture.OpenDoor
                        : HarborCharacter.Gesture.Reach, encounter.Location(player.transform.position));
                CostaVehicle seized = encounter.Advance(player.transform.position, car, takeAction && canUse,
                    canUse && Held(interact, TouchControl.Use), rollClock > 0, Time.deltaTime);
                if (encounter.CombatSource.HasValue)
                {
                    Vector3 source = encounter.CombatSource.Value;
                    effects.CombatFeedback(source, encounter.DamageTaken);
                    if (encounter.DamageTaken > 0)
                    {
                        GestureAt(HarborCharacter.Gesture.Impact, source);
                        impactStrength = Mathf.Clamp01(encounter.DamageTaken / 24f);
                        CameraKick(.24f);
                    }
                }
                if (encounter.BreachFlash.HasValue)
                {
                    effects.Explosion(encounter.BreachFlash.Value + Vector3.up);
                    CameraKick(.62f);
                }
                if (seized != null && !BeginSoloBoarding(seized))
                    EnterVehicle(seized);
                if (encounter.Health <= 0)
                {
                    TriggerSoloDeath(fromContract: true);
                    return;
                }
            }
            else
            {
                bool finishingEscape = jobs.Active == 4 && jobs.StepNumber == 2;
                missionInteraction = jobs.InProgress && takeAction
                    && CostaJobs.Near(player.transform.position, jobs.Current.Point, jobs.Current.Radius);
                if (missionInteraction && car == null && jobs.Current.Mode is CostaJobs.Action.Alert or CostaJobs.Action.Inspect)
                    GestureAt(jobs.Current.Mode == CostaJobs.Action.Alert ? HarborCharacter.Gesture.PickUp
                        : HarborCharacter.Gesture.Reach, jobs.Current.Point);
                if (jobs.Advance(player.transform.position, car != null, takeAction))
                {
                    int previousHeat = wantedLevel;
                    StartPursuit(0.50f);
                    recoveryMissionStar |= wantedLevel > previousHeat;
                }
                if (finishingEscape && !jobs.InProgress && recoveryMissionStar)
                {
                    recoveryMissionStar = false;
                    wantedLevel = Mathf.Max(jobs.Bag.Count > 0 ? 1 : 0, wantedLevel - 1);
                    if (wantedLevel == 0)
                        StopPursuit();
                }
            }
            UpdateContractWeapon();
            if (jobs.Credits > cashBefore)
                effects.Play(Cue.Reward);

            if (takeAction && !missionInteraction)
            {
                if (TryLoot())
                    return;
                int property = jobs.NearbyProperty(player.transform.position);
                if (property >= 0 && !jobs.Owned(property))
                    if (jobs.Buy(property))
                    {
                        GestureAt(HarborCharacter.Gesture.OpenDoor, city.HouseDoors[property].transform.position
                            + new Vector3(0.95f, -0.19f, -0.13f));
                        effects.Play(Cue.Purchase);
                    }
            }
        }

        private bool UsingContractObjective() => !session.Online && encounter.Active
            && encounter.ClaimsUse(player.transform.position);

        private void UpdateContractWeapon() =>
            arsenal.SetLoanerPistol(!session.Online && encounter.Combat && !jobs.OwnsWeapon(0));

        private bool TryLoot()
        {
            if (IsPolice || car != null || state == RunState.Escape)
                return false;
            int site = robberies.Nearby(player.transform.position);
            if (site < 0)
                return false;
            if (session.Online)
                session.Request("loot", site);
            else if (robberies.Take(site, jobs))
            {
                GestureAt(HarborCharacter.Gesture.PickUp, CostaRobberies.PickupTarget(site));
                cargoPack.SetActive(true);
                effects.Play(Cue.Loot);
                StartPursuit(0.45f);
            }
            return true;
        }

        private void SellLoot(int item)
        {
            if (session.Online)
                session.Request("sell", item);
            else
            {
                int previousCash = jobs.Credits;
                jobs.SellLoot(item);
                if (jobs.Credits > previousCash)
                {
                    GestureAt(HarborCharacter.Gesture.Reach);
                    effects.Play(Cue.Sale);
                }
            }
        }

        private bool BuyWeapon(int index)
        {
            if (!session.Online)
            {
                if (!jobs.BuyWeapon(index))
                    return false;
                effects.Play(Cue.Purchase);
                return true;
            }
            if (!IsPolice && !arsenal.Owned(index) && session.Cash >= CostaJobs.WeaponPrices[index])
            {
                pendingBuy = index;
                session.Request("buy", index);
            }
            return false;
        }

        private void StopPursuit()
        {
            wantedLevel = 0;
            recoveryMissionStar = false;
            arrestProgress = 0;
            pursuit = false;
            policeSystem?.ResetAll();
            foreach (Guard guard in guards)
                if (guard != null && guard.Active)
                    guard.Agent.isStopped = true;
        }

        private bool TryUseVehicle()
        {
            if (car != null)
            {
                for (int side = -1; side <= 1; side += 2)
                {
                    if (!car.TryExit(car.Root.position, car.Root.rotation, side, out Vector3 exit))
                        continue;
                    if (session.Online)
                    {
                        session.ExitVehicle(side);
                        return true;
                    }

                    CostaVehicle leaving = car;
                    car = null;
                    player.enabled = false;
                    player.transform.position = exit;
                    ResetFootMotion();
                    grounded = true;
                    verticalSpeed = -2.3f;
                    vehicleMotion.Begin(leaving, false, side, exit, exit, leaving.Root.position,
                        leaving.Root.eulerAngles.y, CostaVehicleMotion.ExitTime);
                    ApplyVehiclePresentation();
                    return true;
                }
                blockedExitUntil = Time.unscaledTime + 2;
                return true;
            }

            if (!grounded || crouching || rollClock > 0
                || (!session.Online && jobs.NearbyProperty(player.transform.position) is int property
                    && property >= 0 && !jobs.Owned(property))
                || (!IsPolice && state != RunState.Escape && robberies.Nearby(player.transform.position) >= 0)
                || (harborJob && state == RunState.FindCargo && IsNear(HarborBlockout.Cargo, InteractionRadius))
                || UsingContractObjective()
                || (!session.Online && jobs.InProgress && !encounter.Active && jobs.Current.Mode != CostaJobs.Action.Drive
                    && CostaJobs.Near(player.transform.position, jobs.Current.Point, jobs.Current.Radius)))
            {
                return false;
            }

            CostaVehicle nearby = NearbyVehicle();
            if (nearby == null)
                return false;

            if (session.Online)
            {
                session.Request("enter", System.Array.IndexOf(cars, nearby));
                return true;
            }

            return BeginSoloBoarding(nearby);
        }

        private bool BeginSoloBoarding(CostaVehicle vehicle)
        {
            Vector3 start = player.transform.position;
            if (!vehicle.TryBoard(start, vehicle.Root.position, vehicle.Root.rotation, out int side, out Vector3 step))
                return false;

            vehicle.StopForBoarding();
            EnterVehicle(vehicle);
            player.transform.position = vehicle.Root.position;
            vehicleMotion.Begin(vehicle, true, side, start, step, vehicle.Root.position,
                vehicle.Root.eulerAngles.y, CostaVehicleMotion.EnterTime);
            ApplyVehiclePresentation();
            return true;
        }

        private void ApplyVehiclePresentation()
        {
            player.enabled = car == null && !vehicleMotion.Active;
            playerModel.localPosition = Vector3.zero;
            playerModel.gameObject.SetActive(vehicleMotion.Active || car == null || car.IsBike);
            arsenal.SetVisible(car == null && !vehicleMotion.Active);
        }

        private void LateUpdate()
        {
            if (worldReady && !inMenu && shownCompletion != jobs.CompletionSerial)
            {
                shownCompletion = jobs.CompletionSerial;
                transitions.Begin(L("Mission complete", "Missão concluída"), 2.2f, .27f);
            }
            bool quiet = !worldReady || inMenu || worldLoading || pausedForFocus || deathScreen.BlocksGameplay ||
                overlay != FieldOverlay.None || state == RunState.Delivered || state == RunState.Caught;
            int heat = session?.Online == true ? session.Local.wantedLevel : wantedLevel;
            float danger = session?.Online == true ? Mathf.Max(session.Local.arrestProgress, session.Local.arrestThreat)
                : Mathf.Max(arrestProgress, pursuit ? .30f : 0);
            score.Update(menu.MusicVolume, quiet, heat, danger, !quiet && encounter?.Combat == true);
            menu.UpdateAdaptiveResolution(!quiet);
            if (!worldReady)
                return;
            foreach (CostaVehicle vehicle in cars)
                vehicle.CloseIdleDoors();
            bool driving = !inMenu && overlay == FieldOverlay.None && !pausedForFocus && !vehicleMotion.Active && !deathScreen.BlocksGameplay
                && state != RunState.Caught && state != RunState.Delivered;
            effects.UpdateWorldAudio(!inMenu || pauseMenu);
            effects.Drive(driving ? car : null, driving && car != null ? engineThrottle : 0);
            float ambience = inMenu && !pauseMenu || pausedForFocus ? 0 : quiet ? .31f : 1;
            Vector3 listenerArea = car != null ? car.Root.position : player.transform.position;
            urbanAudio.Update(listenerArea, ambience, cityNight, worldWeather.Rain, heat, car,
                ambience > .05f && heat > 0 ? NearbyPoliceVehicle(listenerArea) : null);
            harbor.Damage.Animate();
            harbor.Mirrors.Tick(menu.GraphicsPreset, menu.MirrorQuality, !quiet && cinematics?.Active != true, car);
        }

        private Vector3? NearbyPoliceVehicle(Vector3 observer)
        {
            if (!session.Online)
                return null;
            Vector3? result = null;
            float range = 130 * 130;
            foreach (CostaSession.Avatar avatar in session.Players)
            {
                if (avatar.id == session.Local.id || !avatar.playing || !avatar.police
                    || avatar.vehicle < 0 || avatar.vehicle >= cars.Length)
                    continue;
                Vector3 position = cars[avatar.vehicle].Root.position + Vector3.up * 1.37f;
                float distance = (position - observer).sqrMagnitude;
                if (distance < range)
                {
                    range = distance;
                    result = position;
                }
            }
            return result;
        }

        private void EnterVehicle(CostaVehicle vehicle)
        {
            aimingPose = false;
            car = vehicle;
            ResetFootMotion();
            player.enabled = false;
            playerModel.gameObject.SetActive(car.IsBike);
            arsenal.SetVisible(false);
        }

        private CostaVehicle NearbyVehicle()
        {
            CostaVehicle result = null;
            float distance = 3.25f * 3.25f;
            for (int i = 0; i < cars.Length; i++)
            {
                if (session.Online && (!session.CarRelevant(i) || session.Cars[i].driver != 0))
                    continue;
                if (!session.Online && encounter.Active && encounter.ReservesVehicle(i))
                    continue;
                CostaVehicle candidate = cars[i];
                if (candidate.Speed >= 3f)
                    continue;
                float separation = (candidate.Root.position - player.transform.position).sqrMagnitude;
                if (separation <= distance && (!Physics.Linecast(player.transform.position + Vector3.up * 0.8f,
                    candidate.Root.position + Vector3.up * 0.8f, out RaycastHit obstruction,
                    Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore) || candidate.OwnsCollider(obstruction.collider)))
                {
                    distance = separation;
                    result = candidate;
                }
            }
            return result;
        }

        private bool CanResume() => enteredRun && (state == RunState.FindCargo || state == RunState.Escape);

        private void EquipWeapon(int index)
        {
            int previous = arsenal.Slot;
            arsenal.Select(index);
            if (previous != arsenal.Slot)
            {
                effects.Play(Cue.Equip, 0.27f);
                if (session.Online)
                    QueueOnlineEquip();
            }
        }

        private void QueueOnlineEquip()
        {
            if (session.Local.playing)
                session.Equip(arsenal.Slot, ++equipSequence);
            else
                queuedEquip = arsenal.Slot;
        }

        private void CycleWeapons()
        {
            int previous = arsenal.Slot;
            if (weaponSlots[0].WasPressedThisFrame())
            {
                gamepad = false;
                if (arsenal.Slot != 0)
                    arsenal.Select(0);
            }
            else if (weaponSlots[1].WasPressedThisFrame())
            {
                gamepad = false;
                arsenal.SelectHeavy();
            }
            else if (weaponSlots[2].WasPressedThisFrame())
            {
                gamepad = false;
                arsenal.Select(HarborArsenal.Knife);
            }
            else if (Pressed(nextWeapon, TouchControl.Swap))
            {
                gamepad = !touch.Pressed(TouchControl.Swap);
                arsenal.Cycle(1);
            }
            else if (previousWeapon.WasPressedThisFrame())
            {
                gamepad = true;
                arsenal.Cycle(-1);
            }
            if (previous != arsenal.Slot)
            {
                effects.Play(Cue.Equip, 0.27f);
                if (session.Online)
                    QueueOnlineEquip();
            }
        }

        private void PositionCamera(bool aiming, bool snap = false)
        {
            float dt = Mathf.Min(Time.deltaTime, 0.10f);
            float quick = 1f - Mathf.Exp(-dt * 16f);
            Quaternion rotation = Quaternion.Euler(pitch, yaw, 0);
            Vector3 focus = vehicleMotion.Active ? playerModel.position : player.transform.position;
            bool driving = car != null && !vehicleMotion.Active;
            float height = driving ? 2.05f : crouching ? 0.97f : 1.42f;
            cameraHeight = snap ? height : Mathf.Lerp(cameraHeight, height, quick);
            cameraAim = snap ? (aiming ? 1 : 0) : Mathf.Lerp(cameraAim, aiming ? 1 : 0, quick);
            shoulder = snap ? shoulderSide : Mathf.MoveTowards(shoulder, shoulderSide, dt * 5.5f);
            Vector3 lead = driving ? car.Root.forward * Mathf.Clamp(car.SignedSpeed * 0.045f, -0.42f, 1.15f)
                : vehicleMotion.Active ? Vector3.zero : groundVelocity * (aiming ? 0.023f : 0.10f);
            if (snap)
                cameraLead = cameraLeadVelocity = Vector3.zero;
            if (dt > 0)
                cameraLead = Vector3.SmoothDamp(cameraLead, lead, ref cameraLeadVelocity, 0.115f, 14f, dt);
            Vector3 pivot = focus + Vector3.up * cameraHeight;
            Vector3 cameraOffset = driving ? new Vector3(0, 0.68f, -6.45f)
                : Vector3.Lerp(new Vector3(0.80f * shoulder, 0.32f, -3.65f),
                    new Vector3(0.54f * shoulder, 0.10f, -2.05f), cameraAim);

            cameraImpulse = snap ? 0 : Mathf.MoveTowards(cameraImpulse, 0, dt * 2.4f);
            float amplitude = cameraImpulse * cameraImpulse * menu.CameraShake * (aiming ? 0.027f : 0.105f);
            float clock = Time.unscaledTime;
            cameraOffset += new Vector3(Mathf.Sin(clock * 59f), Mathf.Sin(clock * 43f + 1f), 0) * amplitude;
            Vector3 offset = rotation * cameraOffset + cameraLead;
            float distance = offset.magnitude;
            Vector3 direction = offset / Mathf.Max(distance, 0.001f);
            int hitCount = Physics.SphereCastNonAlloc(pivot, 0.17f, direction, cameraHits, distance,
                Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < hitCount; i++)
            {
                RaycastHit obstacle = cameraHits[i];
                if (obstacle.collider == player || (car != null && car.OwnsCollider(obstacle.collider)))
                    continue;
                distance = Mathf.Min(distance, Mathf.Max(0.14f, obstacle.distance - 0.07f));
            }

            boomDistance = snap || distance < boomDistance ? distance : Mathf.Lerp(boomDistance, distance, quick);
            followCamera.transform.SetPositionAndRotation(pivot + direction * boomDistance, rotation);
            float speedFov = driving ? Mathf.InverseLerp(2, car.TopSpeed, car.Speed) * 10f
                : Mathf.InverseLerp(2.6f, 7.3f, groundVelocity.magnitude) * 6.5f;
            float fov = Mathf.Lerp(DefaultFov + speedFov, arsenal.AimFov, cameraAim);
            followCamera.fieldOfView = snap ? fov : Mathf.Lerp(followCamera.fieldOfView, fov, quick);
        }

        private void MovePlayer(bool aiming)
        {
            float step = Time.deltaTime;
            Vector2 input = Vector2.ClampMagnitude(MovementInput(), 1f);
            Quaternion heading = Quaternion.Euler(0, yaw, 0);
            Vector3 direction = heading * new Vector3(input.x, 0, input.y);

            bool holdingSprint = Held(sprint, TouchControl.Sprint);
            if (!holdingSprint)
                needsSprintRelease = false;

            if (rollClock <= 0)
            {
                bool wantsCrouch = grounded && Held(crouchKey, TouchControl.Crouch);
                if (wantsCrouch && !crouching)
                {
                    crouching = true;
                    ResizeController(1.16f);
                }
                else if (!wantsCrouch && crouching && !Physics.CheckCapsule(
                    player.transform.position + Vector3.up * 1.06f, player.transform.position + Vector3.up * 1.43f,
                    player.radius - 0.03f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
                {
                    crouching = false;
                    ResizeController(1.8f);
                }
            }

            rollCooldown = Mathf.Max(0, rollCooldown - step);
            if (grounded && !crouching && rollClock <= 0 && rollCooldown <= 0 && stamina >= 0.75f && Pressed(roll, TouchControl.Roll))
            {
                rollDirection = direction.sqrMagnitude > 0.01f ? direction.normalized : heading * Vector3.forward;
                rollClock = RollDuration;
                rollCooldown = RollDuration + 0.56f;
                stamina -= 0.75f;
                sprintRecoveryDelay = 0.75f;
                ResizeController(1.0f);
                effects.PlayAt(Cue.Roll, player.transform.position + Vector3.up * .25f, .45f);
            }

            bool rolling = rollClock > 0;
            bool running = !rolling && !crouching && !aiming && holdingSprint && !needsSprintRelease
                && direction.sqrMagnitude > 0.01f && stamina > 0;
            if (running && grounded)
            {
                stamina = Mathf.Max(0, stamina - step);
                sprintRecoveryDelay = 0.75f;
                needsSprintRelease = stamina == 0;
            }
            else
            {
                sprintRecoveryDelay -= step;
                if (sprintRecoveryDelay <= 0)
                    stamina = Mathf.Min(FullStamina, stamina + step * 0.85f);
            }

            if (grounded)
            {
                verticalSpeed = -2.3f;
                airbornePeak = player.transform.position.y;
                if (!rolling && !crouching && Pressed(jump, TouchControl.Jump))
                {
                    effects.PlayAt(Cue.Jump, player.transform.position + Vector3.up * .3f, .24f);
                    verticalSpeed = 5.8f;
                    grounded = false;
                    jumpClock = 0;
                    landing = 0;
                }
            }
            else
            {
                verticalSpeed += -19f * step;
            }

            float speed = crouching ? 2f : running ? HarborArsenal.SprintSpeed(arsenal.Slot)
                : HarborArsenal.WalkSpeed(arsenal.Slot, aiming);
            float landingRecovery = landing > 0 ? landingSeverity * (landing / landingDuration) : 0;
            Vector3 desired = direction * (speed * Mathf.Lerp(1f, 0.69f, landingRecovery));
            Vector3 previousVelocity = groundVelocity;
            float turnAngle = 0;
            float brakeWeight = 0;
            Vector3 velocity;
            if (rolling)
            {
                float progress = 1 - rollClock / RollDuration;
                velocity = rollDirection * Mathf.Lerp(7.3f, 2.0f, progress);
                playerModel.rotation = Quaternion.LookRotation(rollDirection);
            }
            else
            {
                // The server uses this same stance speed as a hard travel ceiling.
                groundVelocity = Vector3.ClampMagnitude(groundVelocity, speed);
                bool braking = desired.sqrMagnitude < groundVelocity.sqrMagnitude
                    || Vector3.Dot(desired, groundVelocity) < 0;
                float velocityRatio = groundVelocity.magnitude / Mathf.Max(speed, .01f);
                float alignment = Vector3.Dot(groundVelocity.normalized, desired.normalized);
                brakeWeight = Mathf.Clamp01(velocityRatio * (braking ? 1.15f - .32f * alignment : 0));
                float acceleration = grounded
                    ? (braking ? Mathf.Lerp(29, 43, velocityRatio) : Mathf.Lerp(running ? 34 : 37, 25, velocityRatio))
                    : 13f;
                if (aiming && grounded)
                    acceleration = 46f;
                velocity = Vector3.MoveTowards(groundVelocity, desired, acceleration * step);
                float turnRate = aiming ? 1350f : running ? 620f : 790f;
                Quaternion facing = aiming ? heading : direction.sqrMagnitude > 0.015f
                    ? Quaternion.LookRotation(direction) : playerModel.rotation;
                if (!aiming && direction.sqrMagnitude <= 0.015f)
                {
                    Vector3 target = session.Online && session.Local.gesture is > 0 and < (int)HarborCharacter.Gesture.Impact
                        ? session.Local.gestureTarget : Time.unscaledTime < gestureEnds ? gestureTarget : default;
                    if (target != default)
                    {
                        Vector3 toward = Vector3.ProjectOnPlane(target - playerModel.position, Vector3.up);
                        if (toward.sqrMagnitude > 0.12f)
                            facing = Quaternion.LookRotation(toward);
                    }
                }
                turnAngle = Mathf.DeltaAngle(playerModel.eulerAngles.y, facing.eulerAngles.y);
                playerModel.rotation = Quaternion.RotateTowards(playerModel.rotation, facing, turnRate * step);
            }

            bool wasGrounded = grounded;
            Vector3 from = player.transform.position;
            CollisionFlags hit = player.Move((velocity + Vector3.up * verticalSpeed) * step);
            grounded = (hit & CollisionFlags.Below) != 0;
            if ((hit & CollisionFlags.Above) != 0 && verticalSpeed > 0)
            {
                verticalSpeed = 0;
                wallReaction = Mathf.Max(wallReaction, 0.35f);
                CameraKick(0.12f);
            }

            Vector3 moved = player.transform.position - from;
            Vector3 planarMoved = new Vector3(moved.x, 0, moved.z);
            groundVelocity = step > 0.0001f && !rolling
                ? Vector3.ClampMagnitude(planarMoved / step, speed)
                : Vector3.zero;
            if (!grounded)
                airbornePeak = Mathf.Max(airbornePeak, player.transform.position.y);
            if (!wasGrounded && grounded)
            {
                float fall = Mathf.Max(0, airbornePeak - player.transform.position.y);
                landingSeverity = HarborCharacter.FallSeverity(fall);
                landingDuration = Mathf.Lerp(0.14f, 0.43f, landingSeverity);
                landing = landingDuration;
                effects.PlayAt(Cue.Land, player.transform.position + Vector3.up * .2f, Mathf.Lerp(.28f, .68f, landingSeverity));
                CameraKick(Mathf.Lerp(0.075f, 0.56f, landingSeverity));
            }
            if ((hit & CollisionFlags.Sides) != 0 && velocity.sqrMagnitude > 8 &&
                Vector3.Dot(groundVelocity, velocity.normalized) < velocity.magnitude * 0.52f)
            {
                float loss = Mathf.Clamp01((previousVelocity - groundVelocity).magnitude / 6);
                if (loss > 0.24f)
                {
                    wallReaction = Mathf.Max(wallReaction, loss);
                    CameraKick(loss * 0.35f);
                }
            }
            walking = groundVelocity.sqrMagnitude > 0.10f || rolling;
            runningPose = running && walking;
            footfalls.Update(effects, playerModel, planarMoved.magnitude, grounded && wasGrounded && !rolling, runningPose, crouching);

            if (rolling)
            {
                playerBody.Animate(HarborCharacter.Motion.Roll, progress: 1 - rollClock / RollDuration);
                rollClock = Mathf.Max(0, rollClock - step);
                if (rollClock <= 0)
                    ResizeController(1.8f);
                return;
            }

            if (!grounded || landing > 0)
            {
                jumpClock += step;
                float pose = grounded ? Mathf.Lerp(0.78f, 1f, 1 - landing / landingDuration) : Mathf.Min(jumpClock / 0.80f, 0.77f);
                playerBody.Animate(HarborCharacter.Motion.Jump, progress: pose);
                landing = Mathf.Max(0, landing - step);
            }
            else
            {
                HarborCharacter.Motion motion = !walking ? HarborCharacter.Motion.Idle
                    : runningPose ? HarborCharacter.Motion.Run : HarborCharacter.Motion.Walk;
                float gait = HarborArsenal.GaitSpeed(arsenal.Slot, runningPose, aiming, crouching);
                playerBody.Animate(motion, gait * Mathf.Lerp(0.55f, 1f, groundVelocity.magnitude / speed));
            }

            wallReaction = Mathf.MoveTowards(wallReaction, 0, step * 3.4f);
            float compression = grounded && landing > 0
                ? Mathf.Sin(Mathf.PI * (1f - landing / landingDuration)) * Mathf.Lerp(0.23f, 1f, landingSeverity) : 0;
            playerBody.BodyMotion(groundVelocity, (groundVelocity - previousVelocity) / Mathf.Max(step, 0.001f),
                grounded, crouching, compression, wallReaction);
            if (grounded && landing <= 0)
                playerBody.DirectionalGait(groundVelocity, HarborArsenal.SprintSpeed(arsenal.Slot), turnAngle, brakeWeight);
            if (crouching)
                playerBody.CrouchPose();
            if (grounded)
                playerBody.PlantFeet(player);

            if (arsenal.KnifeEquipped)
                playerBody.HoldKnife(runningPose && grounded, arsenal.KnifeSwing);
            else if (arsenal.HasFirearm)
                playerBody.HoldWeapon(aiming, pitch, HarborArsenal.IsHeavy(arsenal.Slot));

            playerBody.BraceLanding(compression);
            bool freeToReach = !aiming && grounded && groundVelocity.sqrMagnitude < 10;
            if (session.Online)
            {
                var avatar = session.Local;
                var currentGesture = (HarborCharacter.Gesture)avatar.gesture;
                if (freeToReach || currentGesture == HarborCharacter.Gesture.Impact)
                    playerBody.PlayGesture(currentGesture, avatar.gestureProgress, avatar.gestureTarget, avatar.impactStrength);
            }
            else if ((freeToReach || gesture == HarborCharacter.Gesture.Impact) && Time.unscaledTime < gestureEnds)
                playerBody.PlayGesture(gesture, 1 - (gestureEnds - Time.unscaledTime)
                    / HarborCharacter.GestureDuration(gesture), gestureTarget, impactStrength);
        }

        private void GestureAt(HarborCharacter.Gesture kind, Vector3 target = default)
        {
            if (car != null || !grounded || rollClock > 0)
                return;
            gesture = kind;
            gestureTarget = target;
            gestureEnds = Time.unscaledTime + HarborCharacter.GestureDuration(kind);
        }

        private void ResetFootMotion()
        {
            playerBody?.ClearFootContacts();
            groundVelocity = Vector3.zero;
            airbornePeak = player.transform.position.y;
            landing = landingSeverity = wallReaction = cameraImpulse = gestureEnds = 0;
        }

        private void CameraKick(float strength) => cameraImpulse = Mathf.Max(cameraImpulse, Mathf.Clamp01(strength));

        private void ResizeController(float height)
        {
            player.height = height;
            player.center = Vector3.up * (height * 0.5f);
        }

        private void AddGuard(int index)
        {
            Vector3 post = GuardPosts[index];
            HarborCharacter character = harbor.Person($"Harbor guard {index + 1}", transform, post,
                new Color(0.51f, 0.59f, 0.62f));
            Transform model = character.Root;
            harbor.PoliceUniform(character);
            NavMeshAgent agent = model.gameObject.AddComponent<NavMeshAgent>();
            agent.speed = index == 0 ? 6.4f : 6f;
            agent.acceleration = 14;
            agent.angularSpeed = 720;
            agent.radius = 0.42f;
            agent.height = 1.8f;
            agent.stoppingDistance = 0.65f;

            CapsuleCollider hitbox = model.gameObject.AddComponent<CapsuleCollider>();
            hitbox.center = Vector3.up * 0.91f;
            hitbox.radius = 0.31f;
            hitbox.height = 1.82f;
            hitbox.isTrigger = true;
            guards[index] = new Guard { Character = character, Agent = agent, Hitbox = hitbox, Post = post };
        }

        private void ResolveSoloHit(RaycastHit hit, int damage, bool primary)
        {
            if (territories != null && territories.TryDamageCollider(hit.collider, damage, player.transform.position, ShowWorldNotice))
                return;
            if (HitGuard(hit.collider, damage))
                return;

            bool broken = harbor.Damage.Hit(hit.collider, damage);
            if (primary || broken)
                effects.SurfaceHit(harbor.Damage.Resolve(hit.collider), hit.point, hit.normal, broken,
                    followCamera.transform.position);
        }

        private bool HitGuard(Collider hit, int damage)
        {
            if (!session.Online && encounter.Hit(hit, damage))
            {
                arsenal.ShowHit();
                return true;
            }
            if (policeSystem != null && policeSystem.TryHit(hit, damage))
            {
                arsenal.ShowHit();
                return true;
            }
            return false;
        }

        private void StartPursuit(float delay, bool combat = false)
        {
            if (session.Online || state == RunState.Delivered || state == RunState.Caught)
                return;
            heatUntil = Time.time + CostaSession.HeatDecayTime;
            if (!combat || Time.time >= nextCrime)
            {
                policeSystem?.OnCrimeCommitted(player.transform.position, ref wantedLevel, combat);
                if (policeSystem == null)
                    wantedLevel = Mathf.Min(CostaSession.MaxWanted, wantedLevel + 1);
                nextCrime = Time.time + 6f;
            }
            if (pursuit)
                return;

            pursuit = true;
            effects.Play(Cue.Alert, 0.42f);
            nextPursuitUpdate = Time.time + delay;
        }

        private void UpdatePursuit()
        {
            if (policeSystem == null)
                return;

            bool active = policeSystem.Update(player.transform.position, car != null, car?.Speed ?? 0f,
                rollClock > 0, ref wantedLevel, out bool arrested);
            arrestProgress = policeSystem.ArrestProgress;

            if (policeSystem.DamageThisFrame > 0)
            {
                encounter.TakeDamage(policeSystem.DamageThisFrame);
                effects.CombatFeedback(policeSystem.DamageSource ?? player.transform.position, policeSystem.DamageThisFrame);
                CameraKick(Mathf.Clamp01(policeSystem.DamageThisFrame / 22f) * 0.45f);
                impactStrength = Mathf.Clamp01(policeSystem.DamageThisFrame / 24f);
                if (encounter.Health <= 0)
                {
                    TriggerSoloDeath(fromContract: false);
                    return;
                }
            }

            if (arrested)
            {
                effects.Play(Cue.Cuffs);
                EndRun(RunState.Caught);
                return;
            }

            if (!active && wantedLevel == 0)
            {
                pursuit = false;
                if (jobs.Bag.Count == 0)
                    worldInteractive?.AddReputation(player.transform.position, 20);
                ShowWorldNotice(L("Police evaded!", "Polícia despistada!"));
            }
        }

        private void UpdateTerritories()
        {
            if (territories == null)
                return;
            territories.Update(
                player.transform.position,
                rollClock > 0,
                session.Online,
                (mask, bonus, message) =>
                {
                    jobs.Ledger.SaveTerritoryState(jobs.Account, mask);
                    if (bonus > 0)
                        jobs.Ledger.Apply(jobs.Account, CostaLedger.Operation.Reward, bonus);
                    worldInteractive?.AddReputation(player.transform.position, 45);
                    effects.Play(bonus > 0 ? Cue.Reward : Cue.Alert);
                    ShowWorldNotice(message);
                },
                (income, message) =>
                {
                    jobs.Ledger.SaveTerritoryState(jobs.Account, territories.PlayerMask(), income);
                    ShowWorldNotice(message);
                },
                ShowWorldNotice);

            if (territories.DamageThisFrame > 0)
            {
                encounter.TakeDamage(territories.DamageThisFrame);
                effects.CombatFeedback(territories.DamageSource ?? player.transform.position, territories.DamageThisFrame);
                CameraKick(Mathf.Clamp01(territories.DamageThisFrame / 22f) * 0.4f);
                impactStrength = Mathf.Clamp01(territories.DamageThisFrame / 24f);
                if (encounter.Health <= 0)
                    TriggerSoloDeath(fromContract: false);
            }
        }

        private void TriggerSoloDeath(bool fromContract)
        {
            if (deathScreen.BlocksGameplay)
                return;
            territories?.ResetActiveWar();
            aimingPose = false;
            vehicleMotion.Clear();
            arsenal.HideEffects();
            arsenal.SetVisible(false);
            effects.Play(Cue.Wasted, 0.78f);
            bool canAfford = jobs.Credits >= 150;
            string detail = fromContract
                ? L("Contract failed  ·  Taken to Coastal Hospital", "Contrato falhou  ·  Levado ao Hospital Costeiro")
                : canAfford
                    ? L("Taken to Coastal Hospital  ·  -$150", "Levado ao Hospital Costeiro  ·  -$150")
                    : L("Emergency care at Coastal Hospital", "Atendimento de emergência no Hospital Costeiro");
            deathScreen.BeginSolo(detail, () => RespawnAtHospital(fromContract));
        }

        private void RespawnAtHospital(bool fromContract)
        {
            wantedLevel = 0;
            pursuit = false;
            arrestProgress = 0;
            recoveryMissionStar = false;
            policeSystem?.ResetAll();
            encounter.Reset();
            if (fromContract && jobs.InProgress)
                jobs.Select(-1);
            effects.ResetCombat();
            soloSeat = soloLift = -1;
            car = null;
            crouching = walking = runningPose = aimingPose = false;
            rollClock = rollCooldown = 0;
            grounded = true;
            verticalSpeed = -2.3f;
            stamina = FullStamina;
            sprintRecoveryDelay = 0;
            needsSprintRelease = false;
            ResetFootMotion();

            player.enabled = false;
            ResizeController(1.8f);
            player.transform.position = CostaInteriorPlan.Hospital.Entrance + Vector3.back * 2f;
            player.enabled = true;
            yaw = 180f;
            pitch = 4f;
            playerModel.gameObject.SetActive(true);
            playerModel.localPosition = Vector3.zero;
            playerModel.rotation = Quaternion.Euler(0, yaw, 0);
            arsenal.SetVisible(true);
            playerBody.Animate(HarborCharacter.Motion.Idle, progress: 0, snap: true);
            PositionCamera(false, true);
            Physics.SyncTransforms();

            bool paid = jobs.Ledger.Apply(jobs.Account, CostaLedger.Operation.Purchase, 150);
            ShowWorldNotice(paid
                ? L("Treated at Coastal Hospital (-$150)", "Atendido no Hospital Costeiro (-$150)")
                : L("Emergency care at Coastal Hospital", "Atendimento de emergência no Hospital Costeiro"));
        }

        private void ResetRun(bool resetJob = true)
        {
            deathScreen.Cancel();
            territories?.ResetActiveWar();
            if (!session.Online)
                harbor.Damage.Reset();
            effects.ResetCombat();
            score.Reset();
            vehicleMotion.Clear();
            soloSeat = soloLift = -1;
            footfalls = default;
            engineThrottle = 0;
            wantedLevel = 0;
            recoveryMissionStar = false;
            nextCrime = 0;
            arrestProgress = 0;
            state = RunState.FindCargo;
            pursuit = false;
            if (resetJob)
                jobs.Select(jobs.Active);
            crouching = walking = runningPose = false;
            car = null;
            foreach (CostaVehicle vehicle in cars)
                vehicle.Reset();
            if (resetJob)
                encounter.Reset();
            playerModel.gameObject.SetActive(true);
            playerModel.localPosition = Vector3.zero;
            arsenal.SetVisible(true);
            player.enabled = false;
            ResizeController(1.8f);
            verticalSpeed = -2.3f;
            grounded = true;
            jumpClock = 0;
            ResetFootMotion();
            rollClock = 0;
            rollCooldown = 0;
            player.transform.position = harborJob ? HarborBlockout.Spawn : CostaCity.CitySpawn + Vector3.left * 2.8f;
            airbornePeak = player.transform.position.y;
            player.enabled = true;
            yaw = harborJob ? 78f : 350f;
            pitch = 5f;
            playerModel.rotation = Quaternion.Euler(0, yaw, 0);
            cargo.SetActive(harborJob);
            cargoPack.SetActive(jobs.Bag.Count > 0);
            stamina = FullStamina;
            sprintRecoveryDelay = 0;
            needsSprintRelease = false;
            escapeSeconds = 0;
            arsenal.Reset();
            if (menu != null)
                EquipWeapon(menu.SelectedWeapon);
            UpdateContractWeapon();
            playerBody.Animate(HarborCharacter.Motion.Idle, progress: 0, snap: true);
            PositionCamera(false, true);
            if (Application.isFocused)
                SetCursor(true);

            Physics.SyncTransforms();
            ResetGuards();
        }

        private void ResetGuards()
        {
            policeSystem?.ResetAll();
        }

        private void EndRun(RunState result)
        {
            state = result;
            transitions.Begin("", .59f, .76f);
            if (result == RunState.Caught && jobs.Bag.Count > 0)
                jobs.ConfiscateLoot();
            SetCursor(false);
            arsenal.HideEffects();
            foreach (Guard guard in guards)
            {
                if (guard != null && guard.Active)
                    guard.Agent.isStopped = true;
            }
        }

        private void BuyWorldFood(string item, int basePrice, int healAmount, float staminaBoost)
        {
            int price = Mathf.RoundToInt(basePrice * (worldInteractive?.PriceMultiplier(player.transform.position) ?? 1f));
            if (!TrySpendSolo(price))
                return;
            encounter.RestoreHealth(healAmount);
            stamina = Mathf.Min(FullStamina, stamina + FullStamina * staminaBoost);
            worldInteractive?.AddReputation(player.transform.position, 10);
            ShowWorldNotice(L($"{item} (-${price} · +{healAmount} HP)", $"{item} (-${price} · +{healAmount} Vida)"));
        }

        private bool TrySpendSolo(int amount)
        {
            if (amount <= 0)
                return true;
            if (jobs.Credits < amount)
            {
                ShowWorldNotice(L("Not enough cash!", "Dinheiro insuficiente!"));
                return false;
            }
            if (jobs.Ledger.Apply(jobs.Account, CostaLedger.Operation.Purchase, amount))
                return true;
            ShowWorldNotice(L("Payment could not be saved. Try again.", "Não foi possível salvar o pagamento. Tente de novo."));
            return false;
        }

        private void RewardWorldActivity(int cash, string message)
        {
            TryRewardWorldActivity(cash, message);
        }

        private bool TryRewardWorldActivity(int cash, string message)
        {
            if (cash > 0 && !jobs.Ledger.Apply(jobs.Account, CostaLedger.Operation.Reward, cash))
            {
                ShowWorldNotice(L("Reward could not be saved.", "Não foi possível salvar a recompensa."));
                return false;
            }
            worldInteractive?.AddReputation(player.transform.position, 20);
            ShowWorldNotice(message);
            return true;
        }

        private void StartMinigame(System.Action startAction)
        {
            Time.timeScale = 0f;
            SetCursor(false);
            startAction();
        }

        private void ShowWorldNotice(string text)
        {
            interiorNotice = text;
            interiorNoticeUntil = Time.unscaledTime + 4.2f;
        }

        private void SetCursor(bool locked)
        {
            Cursor.lockState = locked ? CursorLockMode.Locked : CursorLockMode.None;
            Cursor.visible = !locked;
        }

        private bool IsNear(Vector3 target, float radius)
        {
            Vector3 offset = target - player.transform.position;
            return offset.x * offset.x + offset.z * offset.z <= radius * radius;
        }

        private void OnGUI()
        {
            if (blockingWorldLoad || worldLoadFailed)
            {
                loadingScreen.Draw(loadingPhase, loadingProgress, worldLoadFailed, ReloadScene);
                if (worldLoadFailed)
                    CostaUi.DrawPointer();
                return;
            }

            if (cityMap != null)
            {
                cityMap.Alarms = session?.Alarms;
                if (session != null && session.TryMapOpportunity(out Vector3 point, out string title))
                {
                    cityMap.EventPoint = point;
                    cityMap.EventTitle = title;
                }
                else
                {
                    cityMap.EventPoint = session?.Online == true ? null : livingWorld?.ActiveEventPoint;
                    cityMap.EventTitle = livingWorld?.ActiveEventTitle;
                }
            }
            if (cinematics?.Active == true)
            {
                cinematics.Draw();
                CostaUi.DrawPointer();
                return;
            }
            if (cityTitleVisible)
                titleBackdrop.DrawTransition();
            if (!deathScreen.BlocksGameplay)
                DrawInterface();
            if (!inMenu && overlay == FieldOverlay.None)
            {
                DrawOnlineChat();
                transitions.Draw();
                deathScreen.Draw();
            }
            if (inMenu || overlay != FieldOverlay.None)
                CostaUi.DrawPointer();
            else if (CostaTouch.Available && minigames?.BlocksWorld != true && !deathScreen.BlocksGameplay)
                touch.Draw(car != null, state == RunState.Caught || state == RunState.Delivered);
        }

        private void ReloadScene() => SceneManager.LoadSceneAsync(gameObject.scene.buildIndex);

        private void DrawInterface()
        {
            if (inMenu)
            {
                bool online = session?.Online == true;
                if (pauseMenu)
                    menu.DrawPause(CanResume(), online, deploy, resume, returnHome, jobs, selectJob, onlinePanel, OwnsWeapon, touch);
                else
                    menu.Draw(CanResume(), deploy, resume, quit, jobs, selectJob, onlinePanel, OwnsWeapon, online, touch);
                return;
            }
            if (overlay == FieldOverlay.Map)
            {
                cityMap.DrawFull(player.transform.position, yaw, MissionLocation(), closeOverlay,
                    session.Campaign != null ? () => OpenOverlay(FieldOverlay.Campaign) : null);
                return;
            }
            if (overlay == FieldOverlay.Campaign)
            {
                DrawCampaign();
                return;
            }
            if (overlay == FieldOverlay.Workshop)
            {
                DrawWorkshop();
                return;
            }
            if (overlay == FieldOverlay.Dealership)
            {
                DrawDealership();
                return;
            }
            if (overlay == FieldOverlay.Garage)
            {
                DrawGarage();
                return;
            }
            if (overlay == FieldOverlay.Bank)
            {
                DrawBank();
                return;
            }
            if (overlay == FieldOverlay.Elevator)
            {
                DrawElevator();
                return;
            }
            if (overlay == FieldOverlay.Shop)
            {
                gunShop.Draw(CurrentCash, arsenal, BuyWeapon, EquipWeapon, closeOverlay);
                return;
            }
            if (overlay == FieldOverlay.Bag || overlay == FieldOverlay.Vendor)
            {
                bagPanel.Draw(CurrentBag, CurrentCash, overlay == FieldOverlay.Vendor, SellLoot, closeOverlay);
                return;
            }
            if (minigames != null && minigames.BlocksWorld)
            {
                minigames.DrawUi();
                return;
            }
            if (Event.current.type != EventType.Repaint)
                return;

            effects.DrawCombat(followCamera);
            if (session.Online)
                remotes.DrawNames(followCamera);

            bool escaping = state == RunState.Escape;
            if (state == RunState.Delivered || state == RunState.Caught)
            {
                bool delivered = state == RunState.Delivered;
                hud.DrawResult(delivered ? L("Cargo secure", "Carga entregue") : L("Caught", "Você foi detido"),
                    delivered
                        ? L($"Safehouse reached in {escapeSeconds:0.0} seconds.", $"Você chegou ao esconderijo em {escapeSeconds:0.0} s.")
                        : L("The police intercepted you.", "A polícia interceptou você."),
                    CostaTouch.Available ? L("Tap Retry or Menu", "Toque em Repetir ou Menu")
                        : gamepad ? L($"START   Retry     {padBindings.Name(12)}   Menu", $"START   Reiniciar     {padBindings.Name(12)}   Menu")
                        : L("F5   Retry     ESC   Menu", "F5   Reiniciar     ESC   Menu"),
                    delivered ? HarborBlockout.Mint : HarborBlockout.Amber);
                return;
            }

            Vector3? missionLocation = minigames?.CurrentWaypoint ?? MissionLocation();
            Vector3? destination = cityMap.Waypoint ?? missionLocation;
            string objective = "";
            if (minigames != null && minigames.WorldHudStatus.Length > 0)
                objective = minigames.WorldHudStatus;
            else if (cityMap.Waypoint.HasValue)
                objective = cityMap.WaypointTitle;
            else if (harborJob)
                objective = escaping ? L("Reach the safehouse", "Vá ao esconderijo") : L("Retrieve the case", "Recolha a maleta");
            else if (!session.Online && jobs.InProgress)
                objective = encounter.Active ? encounter.Status : jobs.Current.Title;
            else if (session.TryActivity(out _, out string activity))
                objective = activity;
            else if (CurrentBag.Count == 0 && session.Campaign != null && CampaignProgress is { node: not null })
                objective = session.Campaign.Current(CampaignProgress).Title;
            else if (CurrentBag.Count > 0)
                objective = L("Sell loot at a fence", "Venda o saque num receptador");

            Vector3 position = player.transform.position;
            Vector3 target = destination ?? position;
            string prompt = InteractionPrompt(missionLocation ?? position);
            if (prompt.Length == 0 && car == null)
                prompt = venues.Prompt(position);
            Vector3 viewport = followCamera.WorldToViewportPoint(target + Vector3.up * 1.35f);
            float side = Vector3.Dot(target - position, followCamera.transform.right);
            string notice = session.Online ? session.CurrentNotice
                : robberies.Notice.Length > 0 ? robberies.Notice : jobs.Notice;
            if (notice.Length == 0 && Time.unscaledTime < interiorNoticeUntil)
                notice = interiorNotice;
            if (notice.Length == 0)
                notice = venues.Notice;
            if (notice.Length == 0 && traffic.NearbyEvent(position, out int incident))
                notice = incident switch
                {
                    0 => L("Temporary roadworks", "Obras temporárias na via"),
                    1 => L("Stranded vehicle ahead", "Veículo avariado à frente"),
                    _ => L("Street delivery in progress", "Entrega em andamento na via")
                };
            if (notice.Length == 0 && pursuit && policeSystem != null && policeSystem.StatusBanner.Length > 0)
                notice = policeSystem.StatusBanner;
            if (notice.Length == 0 && !session.Online && territories != null && territories.Banner.Length > 0)
                notice = territories.Banner;
            if (notice.Length == 0 && car != null && car.HasNitro)
                notice = car.NitroActive
                    ? L($"NITRO BOOST · {car.NitroCharge:P0}", $"NITRO ATIVO · {car.NitroCharge:P0}")
                    : L($"Nitro ready ({car.NitroCharge:P0}) · Hold Shift", $"Nitro pronto ({car.NitroCharge:P0}) · Segure Shift");
            cityMap.PoliceSearchCenter = !session.Online && pursuit && policeSystem != null && !policeSystem.PlayerSpotted
                ? policeSystem.LastKnownPosition : null;
            hud.DrawRun(position, yaw, destination, viewport, side, objective, session.Online ? session.ActivityTime : jobs.Remaining,
                session.Online ? session.Local.wantedLevel : wantedLevel, notice, prompt, stamina / FullStamina,
                session.Online ? Mathf.Max(session.Local.arrestProgress, session.Local.arrestThreat) : arrestProgress,
                car != null ? car.Speed : -1, session.Online ? session.Local.health : encounter.Health,
                IsPolice ? session.Local.armor : -1, arsenal, CostaTouch.Available, gamepad, menu.HudMode, aimingPose);
        }

        private Vector3? MissionLocation()
        {
            if (harborJob)
                return state == RunState.Escape ? HarborBlockout.Safehouse : HarborBlockout.Cargo;
            if (!session.Online && jobs.InProgress)
                return encounter.Active ? encounter.Location(player.transform.position) : jobs.Current.Point;
            if (session.TryActivity(out Vector3 activity, out _))
                return activity;
            if (CurrentBag.Count == 0 && session.Campaign != null && !cityMap.Waypoint.HasValue
                && CostaCampaign.TryPoint(session.Campaign.Current(CampaignProgress).anchor,out Vector3 campaign))
                return campaign;
            if (CurrentBag.Count == 0 || cityMap.Waypoint.HasValue)
                return null;
            Vector3 point = player.transform.position;
            Vector3 first = CostaCity.Vendors[0];
            Vector3 second = CostaCity.Vendors[1];
            return (first - point).sqrMagnitude < (second - point).sqrMagnitude ? first : second;
        }

        private string InteractionPrompt(Vector3 target)
        {
            string use = CostaTouch.Available ? L("Use", "Usar") : gamepad ? padBindings.Name(3) : bindings.Name(3);
            if (session.Online && session.Local.held > 0)
                return L($"Detained · {session.Local.held}s", $"Detido · {session.Local.held}s");
            if (vehicleMotion.Active)
                return vehicleMotion.Entering ? L("Getting in", "Entrando no veículo") : L("Getting out", "Saindo do veículo");
            if (car != null && Time.unscaledTime < blockedExitUntil)
                return L("Both sides are blocked", "Saída bloqueada dos dois lados");
            if (car != null && !session.Online && encounter.Active && jobs.Current.Mode == CostaJobs.Action.DeliverVehicle
                && car == cars[jobs.Current.Vehicle] && IsNear(target, jobs.Current.Radius))
                return L("Slow down to deliver the vehicle", "Reduza a velocidade para entregar o veículo");
            if (session.CrewInteraction(player.transform.position, car != null) >= 0)
            {
                if (session.CrewTask?.getaway == true)
                    return L($"{use} Hand loot to driver", $"{use} Entregar saque ao motorista");
                return car != null ? L($"{use} Join as driver", $"{use} Entrar como motorista")
                    : L($"{use} Join as lookout", $"{use} Entrar como vigia");
            }

            if (CanOpenCampaign())
                return L($"{use} Campaign",$"{use} Campanha");
            if (CanOpenWorkshop())
                return L($"{use} Workshop",$"{use} Oficina");
            if (CanOpenDealership())
                return L($"{use} Costa Motors Dealership", $"{use} Concessionária Costa Motors");
            if (CanOpenGarage())
                return L($"{use} Personal Garage", $"{use} Garagem Própria");
            if (!session.Online && car == null && livingWorld != null)
            {
                string evPrompt = livingWorld.EventPrompt(player.transform.position, use);
                if (evPrompt.Length > 0)
                    return evPrompt;
            }
            if (!session.Online && worldInteractive != null)
            {
                var node = worldInteractive.FindNearby(player.transform.position, car != null);
                if (node != null)
                    return worldInteractive.Prompt(node, use, InteriorHour);
            }
            if (car != null)
                return L($"{use} Exit", $"{use} Sair");
            if (!session.Online && encounter.Active)
            {
                string contractPrompt = encounter.Prompt(player.transform.position, use);
                if (contractPrompt.Length > 0)
                    return contractPrompt;
            }
            if (IsPolice && remotes.ArrestCandidate(player.transform.position) != null)
                return L($"{use} Arrest", $"{use} Prender");
            if (!IsPolice && CostaSession.AtVendor(player.transform.position))
                return L($"{use} Sell loot", $"{use} Vender saque");
            if (!IsPolice && IsNear(CostaCity.GunCounter, 2f))
                return L($"{use} Open gun shop", $"{use} Abrir armaria");
            if (harborJob && state == RunState.FindCargo && IsNear(target, InteractionRadius))
                return L($"{use} Take the case", $"{use} Pegar a maleta");
            if (!session.Online && jobs.InProgress && !encounter.Active && IsNear(target, jobs.Current.Radius))
            {
                if (jobs.Current.Mode == CostaJobs.Action.Drive)
                    return L("Cross this checkpoint in a car", "Passe por este ponto em um carro");
                if (jobs.Current.Mode != CostaJobs.Action.Checkpoint)
                    return $"{use} {jobs.Current.Title}";
            }

            if (CostaSession.AtBank(player.transform.position))
                return L($"{use} Bank ATM",$"{use} Caixa eletrônico");
            city.Interiors.FindAction(player.transform.position, CurrentSeat, CurrentLift, InteriorHour, out string interior, out _);
            if (interior.Length > 0)
                return CurrentLift >= 0 ? interior : $"{use} {interior}";

            int robbery = robberies.Nearby(player.transform.position);
            if (!IsPolice && state != RunState.Escape && robbery >= 0)
            {
                if (!robberies.Ready(robbery, session.Online ? session.Sites : null))
                    return L("This location is restocking", "Local repondo os itens");
                if (!CurrentBag.CanLoot(robbery))
                    return L("Not enough room in the backpack", "Mochila sem espaço para o saque");
                if (session.Online && session.CrewTask is { preparing: true } order && order.site == robbery)
                    return L("Robbery preparation", "Preparação do roubo");
                if (session.Online && session.Alarms?[robbery] == true)
                    return L("Robbery in progress", "Roubo em andamento");
                if (session.Online)
                    return L($"{use} Start robbery", $"{use} Iniciar roubo");
                string title = CostaRobberies.Sites[robbery].Name;
                return L($"{use} Take loot · {title}", $"{use} Pegar saque · {title}");
            }

            int property = session.Online ? -1 : jobs.NearbyProperty(player.transform.position);
            if (property >= 0 && !jobs.Owned(property))
                return L($"{use} Buy Vila {property + 1:00}  ·  {Money(CostaJobs.Prices[property])}",
                    $"{use} Comprar Vila {property + 1:00}  ·  {Money(CostaJobs.Prices[property])}");
            if (NearbyVehicle() != null)
                return L($"{use} Drive vehicle", $"{use} Usar veículo");
            if (!CostaTouch.Available && !gamepad && Cursor.lockState != CursorLockMode.Locked)
                return L("Click to capture the mouse", "Clique para capturar o mouse");
            return "";
        }
    }
}
