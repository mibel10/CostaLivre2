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
    public sealed class HarborRun : MonoBehaviour
    {
        private enum FieldOverlay { None, Map, Shop, Bag, Vendor }
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
        private float rollClock;
        private float rollCooldown;
        private bool grounded;
        private Vector3 rollDirection;
        private float pitch;
        private float boomDistance;
        private float stamina;
        private float sprintRecoveryDelay;
        private float nextPursuitUpdate;
        private float escapeStartedAt;
        private float escapeSeconds;
        private bool needsSprintRelease;
        private bool gamepad;
        private bool pursuit;
        private float blockedExitUntil;
        private int wantedLevel;
        private float heatUntil;
        private float nextCrime;
        private float arrestProgress;
        private Guard arrestingGuard;
        private const float SoloArrestTime = 1.25f;

        private void Awake()
        {
            Application.targetFrameRate = 60;
            Application.runInBackground = true;
            touch = new CostaTouch();
            BindControls();
            followCamera.nearClipPlane = 0.07f;
            menu = new HarborMenu(bindings, padBindings, followCamera, sun, volume);
            jobs = new CostaJobs();
            harbor = new HarborBlockout(transform, blockoutShader);
            onlinePanel = new CostaOnlinePanel(Connect);
            loadingScreen = new HarborLoadingScreen();
            closeOverlay = CloseOverlay;
            deploy = () => WithWorld(StartRun);
            resume = ResumeRun;
            quit = Application.Quit;
            returnHome = ReturnToMainMenu;
            selectJob = index => WithWorld(() => SelectJob(index));
            CreateMenuPreview();
            ShowMenu();
        }

        private void Connect(string name, string address, int port, bool host, string relay = null) =>
            WithWorld(() => session.Start(name, address, port, host, relay));

        private void WithWorld(System.Action play)
        {
            if (worldReady)
                play();
            else if (!worldLoading && !worldLoadFailed)
            {
                worldLoading = true;
                loadingPhase = L("Reading the port", "Abrindo o porto");
                loadingProgress = 0;
                loadingScreen.Begin();
                SetCursor(false);
                StartCoroutine(LoadWorld(play));
            }
        }

        private IEnumerator LoadWorld(System.Action play)
        {
            // Draw one full frame of the loading screen before constructing scene objects.
            yield return null;
            menuStage.gameObject.SetActive(false);
            int culling = followCamera.cullingMask;
            followCamera.cullingMask = 0;
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
                yield break;

            followCamera.cullingMask = culling;
            worldReady = true;
            loadingScreen.Release();
            loadingScreen = null;
            ShowMenu();
            play();
        }

        private IEnumerable<float> BuildWorld()
        {
            harbor.Build();
            loadingPhase = L("Building Santa Luz and the coastal highway", "Montando Santa Luz e a rodovia costeira");
            yield return 0.14f;

            city = new CostaCity(harbor, transform);
            foreach (float part in city.Build())
                yield return Mathf.Lerp(0.14f, 0.69f, part);

            loadingPhase = L("Preparing vehicles and contracts", "Preparando veículos e missões");
            yield return 0.69f;
            jobs.AttachDoors(city.HouseDoors);
            robberies = new CostaRobberies(harbor);
            cityMap = new CostaMap(city.Footprints);
            gunShop = new CostaGunShop();
            hud = new HarborHud(cityMap);
            bagPanel = new CostaBagPanel();
            yield return 0.74f;

            CreateWorldActors();
            loadingPhase = L("Preparing city navigation", "Preparando a navegação da cidade");
            yield return 0.82f;

            navMesh = new NavMeshData();
            navigationBuild = harbor.BuildNavigation(navMesh);
            while (!navigationBuild.isDone)
                yield return Mathf.Lerp(0.82f, 0.95f, navigationBuild.progress);

            navigationBuild = null;
            navMeshInstance = NavMesh.AddNavMeshData(navMesh);
            for (int i = 0; i < guards.Length; i++)
                AddGuard(i);
            ResetRun();
            SetCursor(false);
            loadingPhase = L("Santa Luz is ready", "Santa Luz está pronta");
            yield return 1f;
        }

        private void CreateWorldActors()
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
            playerModel = playerBody.Root;
            cargoPack = harbor.Pack(playerModel, playerBody.Chest);
            localLook = new CostaLook(harbor, playerBody);

            effects = new CostaEffects(transform);
            arsenal = new HarborArsenal(harbor, playerModel, blockoutShader, jobs);
            cargo = harbor.Case();

            cars = new CostaVehicle[]
            {
                new(harbor, transform, new Vector3(-30, 0, 27.5f), 0, new Color(0.34f, 0.36f, 0.39f)),
                new(harbor, transform, new Vector3(-77, 0, 43), 0, new Color(0.30f, 0.36f, 0.31f)),
                new(harbor, transform, new Vector3(18, 0, 44), 180, new Color(0.38f, 0.18f, 0.16f)),
                new(harbor, transform, new Vector3(-61, 0, 93), 90, new Color(0.24f, 0.31f, 0.39f), true),
                new(harbor, transform, new Vector3(-13, 0, -18), 90, new Color(0.34f, 0.41f, 0.47f)),
                new(harbor, transform, new Vector3(65, 0, 104), 0, new Color(0.45f, 0.43f, 0.36f)),
                new(harbor, transform, new Vector3(-40, 0, 51), 270, new Color(0.63f, 0.23f, 0.14f), style: CostaVehicle.Style.Super),
                new(harbor, transform, new Vector3(59, 0, 87), 90, new Color(0.28f, 0.40f, 0.52f), style: CostaVehicle.Style.Track),
                new(harbor, transform, new Vector3(-39, 0, 34), 90, new Color(0.64f, 0.64f, 0.58f), style: CostaVehicle.Style.Compact),
                new(harbor, transform, new Vector3(-88, 0, 85), 90, new Color(0.18f, 0.23f, 0.25f), style: CostaVehicle.Style.Executive),
                new(harbor, transform, new Vector3(-9, 0, 71), 90, new Color(0.31f, 0.45f, 0.39f)),
                new(harbor, transform, new Vector3(-35, 0, 25.5f), 0, new Color(0.61f, 0.32f, 0.11f), style: CostaVehicle.Style.StreetBike),
                new(harbor, transform, new Vector3(-78, 0, 115), 180, new Color(0.25f, 0.27f, 0.28f), style: CostaVehicle.Style.Cruiser),
                new(harbor, transform, new Vector3(-45, 0, CostaCity.Districts[0].Center - 3), 90, new Color(0.36f, 0.52f, 0.57f)),
                new(harbor, transform, new Vector3(-45, 0, CostaCity.Districts[1].Center - 3), 90, new Color(0.34f, 0.39f, 0.53f), style: CostaVehicle.Style.Executive),
                new(harbor, transform, new Vector3(-45, 0, CostaCity.Districts[2].Center - 3), 90, new Color(0.65f, 0.22f, 0.35f), style: CostaVehicle.Style.Super)
            };

            encounter = new CostaEncounter(harbor, transform, jobs, cars);
            session = new CostaSession(cars);
            onlinePanel.AttachSession(session);
            remotes = new CostaRemoteView(harbor, transform, effects);
        }

        private void CreateMenuPreview()
        {
            menuStage = new GameObject("Menu preview set").transform;
            menuStage.SetParent(transform);
            menuStage.position = MenuActorPosition;
            Color pavement = new(0.21f, 0.25f, 0.27f);
            Color wall = new(0.19f, 0.25f, 0.26f);
            Color steel = new(0.30f, 0.36f, 0.36f);
            harbor.Part(menuStage, PrimitiveType.Cube, "Preview floor", new(0, -0.16f, 0), new(28, 0.3f, 20), pavement);
            harbor.Part(menuStage, PrimitiveType.Cube, "Preview warehouse", new(0, 3.1f, 5), new(27, 6.2f, 0.33f), wall);
            for (int i = -6; i <= 6; i++)
                harbor.Part(menuStage, PrimitiveType.Cube, "Warehouse cladding", new(i * 1.35f, 3f, 4.78f),
                    new(0.14f, 6, 0.16f), steel);
            harbor.Part(menuStage, PrimitiveType.Cube, "Warehouse safety band", new(0, 0.57f, 4.75f),
                new(21, 0.13f, 0.18f), new Color(0.63f, 0.43f, 0.25f));

            menuBody = harbor.Person("Loadout preview", menuStage, MenuActorPosition, new Color(0.94f, 0.93f, 0.87f));
            menuLook = new CostaLook(harbor, menuBody);
            menuBody.Root.rotation = Quaternion.Euler(0, 215f, 0);
            menuWeaponMount = new GameObject("Preview weapons").transform;
            menuWeaponMount.SetParent(menuBody.Root, false);
            menuWeapons = new Transform[HarborArsenal.WeaponNames.Length];
        }

        private bool OwnsWeapon(int slot) => arsenal?.Owned(slot) ?? (slot == HarborArsenal.Knife || jobs.OwnsWeapon(slot));

        private void UpdateMenuPreview()
        {
            float angle = Mathf.Sin(Time.unscaledTime * 0.38f) * 0.26f;
            Vector3 cameraPosition = MenuActorPosition + new Vector3(-2.85f + angle, 1.57f, -3.45f);
            const float fieldOfView = 35f;
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
                    menuBody.HoldKnife(false, false, -1);
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
            inMenu = true;
            pauseMenu = pause;
            menuStage.gameObject.SetActive(!pause);
            Time.timeScale = session?.Online == true ? 1 : 0;
            SetCursor(false);
            ApplyWorldHour();
            if (!pause)
                UpdateMenuPreview();
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
            if (!session.Online)
                ResetRun();
            enteredRun = true;
            ResumeRun();
        }

        private void ResumeRun()
        {
            if (session.Online)
                enteredRun = enteredOnlineWorld = true;
            if (menu.SelectionVersion != appliedSelection)
            {
                appliedSelection = menu.SelectionVersion;
                EquipWeapon(menu.SelectedWeapon);
            }
            inMenu = false;
            menuStage.gameObject.SetActive(false);
            Time.timeScale = 1;
            PositionCamera(false, true);
            ApplyWorldHour();
            SetCursor(true);
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
            reload = AddButton(map, "Reload weapon", "<Keyboard>/r", "<Gamepad>/buttonWest");
            nextWeapon = AddButton(map, "Next weapon", "<Gamepad>/rightShoulder");
            previousWeapon = AddButton(map, "Previous weapon", "<Gamepad>/leftShoulder");
            restart = AddButton(map, "Restart run", "<Keyboard>/f5", "<Gamepad>/start");
            openMenu = AddButton(map, "Open menu", "<Keyboard>/escape", "<Gamepad>/select");
            jobsBoard = AddButton(map, "Jobs board", "<Keyboard>/tab", "<Gamepad>/rightStickPress");
            mapKey = AddButton(map, "City map", "<Keyboard>/m", "<Gamepad>/dpad/right");
            bagKey = AddButton(map, "Backpack", "<Keyboard>/i", "<Gamepad>/dpad/up");
            crouchKey = AddButton(map, "Crouch stance", "<Keyboard>/leftCtrl", "<Gamepad>/dpad/down");
            for (int i = 0; i < weaponSlots.Length; i++)
                weaponSlots[i] = AddButton(map, $"Weapon slot {i + 1}", $"<Keyboard>/{i + 1}");
            bindings = new CostaBindings(sprint, jump, roll, interact, reload);
            padBindings = new CostaPadBindings(sprint, jump, roll, interact, reload, shoot, aim,
                nextWeapon, previousWeapon, crouchKey, mapKey, bagKey, openMenu, jobsBoard);
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
            if (inMenu || overlay != FieldOverlay.None)
                Time.timeScale = 1;
            session?.Dispose();
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
            effects?.Release();
            encounter?.Dispose();
            harbor?.ReleaseMaterials();
        }

        private void OnApplicationFocus(bool focused)
        {
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
                if (!inMenu && overlay == FieldOverlay.None)
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
            pose.standing = grounded && !crouching && rollClock <= 0 && car == null;
            pose.crouched = crouching;
            pose.jumpPose = car == null && (!grounded || landing > 0)
                    ? grounded ? Mathf.Lerp(0.78f, 1f, 1 - landing / 0.14f) : Mathf.Min(jumpClock / 0.8f, 0.77f)
                    : -1;
            pose.rollPose = rollClock > 0 ? 1 - rollClock / RollDuration : -1;
            pose.moving = !inMenu && overlay == FieldOverlay.None && walking;
            pose.running = !inMenu && overlay == FieldOverlay.None && runningPose;
            pose.bikeLean = car != null ? car.RiderLean : 0;
            pose.held = car != null ? Mathf.RoundToInt(car.SignedSpeed * 10f) : 0;
            return pose;
        }

        private void TickNetwork()
        {
            if (!session.Online && !session.Connecting && !session.Hosting && !wasOnline)
                return;

            int priorHeat = session.Local?.wantedLevel ?? 0;
            session.Tick(CaptureNetworkPose());
            switch (session.NoticeEvent)
            {
                case 3: case 4: effects.Play(Cue.Cuffs); break;
                case 5: effects.Play(Cue.Loot); break;
                case 7: effects.Play(Cue.Sale); break;
                case 8: effects.Play(Cue.Purchase); break;
            }
            if (session.Online && priorHeat == 0 && session.Local.wantedLevel > 0)
                effects.Play(Cue.Alert, 0.32f);

            if (!session.Online)
            {
                if (wasOnline)
                {
                    wasOnline = false;
                    enteredOnlineWorld = false;
                    equipSequence = 0;
                    queuedEquip = -1;
                    netRevision = -1;
                    pendingBuy = -1;
                    remotes.Clear();
                    arsenal.SetOnlineWeapons(null);
                    localLook.Apply(0, false);
                    jobs.SetOnlineDoors(false);
                    jobs.ResumeAfterSession();
                    CloseOverlay();
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
                soloResumable = CanResume();
                soloHarborJob = harborJob;
                soloState = state;
                soloPosition = player.transform.position;
                soloYaw = yaw;
                soloPitch = pitch;
                soloEscapeElapsed = state == RunState.Escape ? Time.time - escapeStartedAt : 0;
                soloWantedLevel = wantedLevel;
                soloHeatRemaining = Mathf.Max(0, heatUntil - Time.time);
                soloCrimeRemaining = Mathf.Max(0, nextCrime - Time.time);
                soloContractCar = encounter.Active && (jobs.Current.Mode is CostaJobs.Action.Seize or CostaJobs.Action.DeliverVehicle)
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
                jobs.PauseForSession();
                encounter.SetOnline(true);
                arsenal.SetLoanerPistol(false);

                wasOnline = true;
                jobs.SetOnlineDoors(true);
                pursuit = false;
                harborJob = false;
                cargo.SetActive(false);
                foreach (Guard guard in guards)
                    guard?.Agent.gameObject.SetActive(false);
                session.Request("outfit", onlinePanel.Outfit);
                // A connection can finish while a solo session is active.
                CloseOverlay();
                menu.ShowOnline();
                ShowMenu();
            }

            CostaSession.Avatar avatar = session.Local;
            if (avatar.held > 0 && overlay != FieldOverlay.None)
                CloseOverlay();
            bool syncCars = joined;
            for (int i = 0; i < cars.Length; i++)
            {
                CostaSession.Vehicle vehicle = session.Cars[i];
                if (i != avatar.vehicle || netRevision != avatar.revision)
                    syncCars |= cars[i].ApplyNetwork(vehicle.pos, vehicle.yaw, vehicle.speed, joined || i == avatar.vehicle, vehicle.lean);
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
                rollClock = landing = 0;
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
            bool reloadWasActive = arsenal.Reloading;
            arsenal.SyncAmmo(session.Loaded, session.Reloading);
            if (!reloadWasActive && arsenal.Reloading)
                effects.Play(Cue.Reload);
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
            menu.SetWorldHour(hour);
            harbor.SetWaterDaylight(hour ?? HarborDaylight.StartHour);
            bool night = hour.HasValue && (session.Online ? session.WorldIsNight : HarborDaylight.IsNight(hour.Value));
            harbor.UpdateStreetLights(night, followCamera.transform.position, menu.StreetLightBudget);
        }

        private void Update()
        {
            menu.SaveGraphicsIfDue();
            harbor.SetUltraVisuals(menu.Ultra, menu.FullTextures);
            if (worldLoadFailed)
                CostaUi.Update(false);
            if (worldLoading || worldLoadFailed)
                return;

            if (worldReady)
            {
                TickNetwork();
                if (!session.Online && !inMenu && overlay == FieldOverlay.None)
                    soloDaySeconds += Time.deltaTime;
                ApplyWorldHour();
            }
            touch.Update(!inMenu && overlay == FieldOverlay.None && !pausedForFocus,
                car != null, worldReady && !session.Online && (state == RunState.Caught || state == RunState.Delivered));
            if (inMenu || overlay != FieldOverlay.None)
                CostaUi.Update(menu.CapturingKey);
            if (pausedForFocus)
                return;
            if (inMenu)
            {
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
                if (Pressed(openMenu, TouchControl.Menu) || (overlay == FieldOverlay.Map && Pressed(mapKey, TouchControl.Map))
                    || (overlay == FieldOverlay.Bag && Pressed(bagKey, TouchControl.Bag)))
                    CloseOverlay();
                return;
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
                ResetRun();
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

            city.Animate();
            if (!session.Online)
            {
                robberies.Refresh();
                CoastIdleVehicles();
            }
            cargoPack.SetActive(CurrentBag.Count > 0 || state == RunState.Escape
                || !session.Online && encounter.CarryingLoot);
            cityMap.CheckWaypoint(player.transform.position);
            bool interaction = Pressed(interact, TouchControl.Use);
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
            if (interaction && TryUseVehicle())
                interaction = false;
            if (vehicleMotion.Active)
                return;

            if (car != null)
            {
                Vector2 steering = MovementInput();
                engineThrottle = steering.y;
                float previousSpeed = car.Speed;
                bool braking = Held(jump, TouchControl.Jump);
                if (car.Drive(steering, braking))
                    Physics.SyncTransforms();
                effects.Brake(previousSpeed, braking && Pressed(jump, TouchControl.Jump) ? 0 : car.Speed);
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
            bool wasReloading = arsenal.Reloading;


            bool aiming = arsenal.HasFirearm && rollClock <= 0 && Held(aim, TouchControl.Aim)
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
            if (arsenal.Update(followCamera, playerBody.WeaponHand, pitch, firePressed,
                fireHeld, aiming, crouching, HitGuard))
            {
                if (session.Online)
                    session.Fire(arsenal.Slot, arsenal.ShotDirection, CaptureNetworkPose());
                else if (!contractCombat)
                    StartPursuit(0.25f, combat: true);
            }

            if (!session.Online && !wasReloading && arsenal.Reloading)
                effects.Play(Cue.Reload);

            bool originalInteraction = harborJob && state == RunState.FindCargo
                && IsNear(HarborBlockout.Cargo, InteractionRadius) && interaction;
            if (originalInteraction)
            {
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

        private void CoastIdleVehicles()
        {
            bool moved = false;
            foreach (CostaVehicle vehicle in cars)
                if (vehicle != car && vehicle.Speed > 0.05f)
                    moved |= vehicle.Drive(Vector2.zero, false);
            if (moved)
                Physics.SyncTransforms();
        }

        private void OpenOverlay(FieldOverlay screen)
        {
            effects.Play(Cue.PanelOpen, 0.32f);
            overlay = screen;
            Time.timeScale = 0;
            SetCursor(false);
        }

        private void CloseOverlay()
        {
            if (overlay != FieldOverlay.None)
                effects.Play(Cue.PanelClose, 0.27f);
            overlay = FieldOverlay.None;
            Time.timeScale = 1;
            SetCursor(true);
        }

        private void AnimatePolice()
        {
            foreach (Guard guard in guards)
            {
                if (guard != null && guard.Active && (guard.Agent.transform.position - player.transform.position).sqrMagnitude < 4900)
                {
                    guard.Character.Animate(guard.Agent.velocity.sqrMagnitude > 0.35f
                        ? HarborCharacter.Motion.Run : HarborCharacter.Motion.Idle, 0.82f);
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
                    effects.Play(Cue.Reward);
                    if (PlayerPrefs.GetInt("CostaPortPaid", 0) == 0)
                    {
                        PlayerPrefs.SetInt("CostaPortPaid", 1);
                        jobs.Award(3200);
                    }
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
                CostaVehicle seized = encounter.Advance(player.transform.position, car, takeAction && canUse,
                    canUse && Held(interact, TouchControl.Use), rollClock > 0, Time.deltaTime);
                if (seized != null && !BeginSoloBoarding(seized))
                    EnterVehicle(seized);
                if (encounter.Health <= 0)
                {
                    EndRun(RunState.Caught);
                    return;
                }
            }
            else
            {
                bool finishingEscape = jobs.Active == 4 && jobs.StepNumber == 2;
                missionInteraction = jobs.InProgress && takeAction
                    && CostaJobs.Near(player.transform.position, jobs.Current.Point, jobs.Current.Radius);
                if (jobs.Advance(player.transform.position, car != null, takeAction))
                    StartPursuit(0.50f);
                if (finishingEscape && !jobs.InProgress)
                    StopPursuit();
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
                        effects.Play(Cue.Purchase);
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
                    effects.Play(Cue.Sale);
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
            arrestProgress = 0;
            arrestingGuard = null;
            pursuit = false;
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
            if (!worldReady)
                return;
            foreach (CostaVehicle vehicle in cars)
                vehicle.CloseIdleDoors();
            bool driving = !inMenu && overlay == FieldOverlay.None && !pausedForFocus && !vehicleMotion.Active
                && state != RunState.Caught && state != RunState.Delivered;
            effects.Drive(driving ? car : null, driving && car != null ? engineThrottle : 0);
        }

        private void EnterVehicle(CostaVehicle vehicle)
        {
            car = vehicle;
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
                if (session.Online && session.Cars[i].driver != 0)
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
            Quaternion rotation = Quaternion.Euler(pitch, yaw, 0);
            Vector3 focus = vehicleMotion.Active ? playerModel.position : player.transform.position;
            Vector3 pivot = focus + Vector3.up * (car != null ? 2.05f : crouching ? 0.97f : 1.42f);
            Vector3 cameraOffset = car != null ? new Vector3(0, 0.68f, -6.45f)
                : aiming ? new Vector3(0.54f, 0.10f, -2.05f) : new Vector3(0.80f, 0.32f, -3.65f);
            Vector3 offset = rotation * cameraOffset;
            float distance = offset.magnitude;
            Vector3 direction = offset / distance;
            if (Physics.SphereCast(pivot, 0.17f, direction, out RaycastHit obstacle, distance,
                Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
            {
                distance = Mathf.Max(0.14f, obstacle.distance - 0.07f);
            }

            float blend = 1f - Mathf.Exp(-Time.deltaTime * 15f);
            boomDistance = snap || distance < boomDistance ? distance : Mathf.Lerp(boomDistance, distance, blend);
            followCamera.transform.SetPositionAndRotation(pivot + direction * boomDistance, rotation);
            float fov = aiming ? arsenal.AimFov : DefaultFov;
            followCamera.fieldOfView = snap ? fov : Mathf.Lerp(followCamera.fieldOfView, fov, blend);
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
                effects.Play(Cue.Roll);
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
                if (!rolling && !crouching && Pressed(jump, TouchControl.Jump))
                {
                    effects.Play(Cue.Jump, 0.24f);
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

            walking = direction.sqrMagnitude > 0.015f || rolling;
            runningPose = running;
            float speed = crouching ? 2f : running ? HarborArsenal.SprintSpeed(arsenal.Slot)
                : HarborArsenal.WalkSpeed(arsenal.Slot, aiming);
            Vector3 velocity = direction * speed;
            if (rolling)
            {
                float progress = 1 - rollClock / RollDuration;
                velocity = rollDirection * Mathf.Lerp(7.3f, 2.0f, progress);
                playerModel.rotation = Quaternion.LookRotation(rollDirection);
            }
            else
            {
                playerModel.rotation = !aiming && direction.sqrMagnitude > 0.015f
                    ? Quaternion.LookRotation(direction) : heading;
            }

            bool wasGrounded = grounded;
            Vector3 from = player.transform.position;
            CollisionFlags hit = player.Move((velocity + Vector3.up * verticalSpeed) * step);
            grounded = (hit & CollisionFlags.Below) != 0;
            if ((hit & CollisionFlags.Above) != 0 && verticalSpeed > 0)
                verticalSpeed = 0;
            if (!wasGrounded && grounded)
            {
                landing = 0.14f;
                effects.Play(Cue.Land, Mathf.Lerp(0.28f, 0.65f, Mathf.Clamp01(-verticalSpeed / 14f)));
            }
            Vector3 moved = player.transform.position - from;
            footfalls.Update(effects, new Vector2(moved.x, moved.z).magnitude, grounded && wasGrounded && !rolling,
                running, crouching);

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
                float pose = grounded ? Mathf.Lerp(0.78f, 1f, 1 - landing / 0.14f) : Mathf.Min(jumpClock / 0.80f, 0.77f);
                playerBody.Animate(HarborCharacter.Motion.Jump, progress: pose);
                landing = Mathf.Max(0, landing - step);
            }
            else
            {
                HarborCharacter.Motion motion = direction.sqrMagnitude < 0.015f ? HarborCharacter.Motion.Idle
                    : running ? HarborCharacter.Motion.Run : HarborCharacter.Motion.Walk;
                playerBody.Animate(motion, HarborArsenal.GaitSpeed(arsenal.Slot, running, aiming, crouching));
            }

            if (crouching)
                playerBody.CrouchPose();
            if (arsenal.KnifeEquipped)
                playerBody.HoldKnife(running && grounded, crouching, arsenal.KnifeSwing);
            else if (arsenal.HasFirearm)
                playerBody.HoldWeapon(aiming, pitch, HarborArsenal.IsHeavy(arsenal.Slot));
        }

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

        private void HitGuard(Collider hit, int damage)
        {
            if (!session.Online && encounter.Hit(hit, damage))
            {
                arsenal.ShowHit();
                return;
            }
            foreach (Guard guard in guards)
            {
                if (guard == null || guard.Hitbox != hit)
                    continue;

                guard.Health -= damage;
                arsenal.ShowHit();
                if (guard.Health <= 0)
                    guard.Agent.gameObject.SetActive(false);
                return;
            }
        }

        private void StartPursuit(float delay, bool combat = false)
        {
            if (session.Online || state == RunState.Delivered || state == RunState.Caught)
                return;
            heatUntil = Time.time + CostaSession.HeatDecayTime;
            if (!combat || Time.time >= nextCrime)
            {
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
            bool refreshPaths = Time.time >= nextPursuitUpdate;
            Vector3 target = player.transform.position;
            if (refreshPaths)
            {
                nextPursuitUpdate = Time.time + 0.2f;
                if (NavMesh.SamplePosition(target, out NavMeshHit point, 1.25f, NavMesh.AllAreas))
                    target = point.position;
            }

            Guard nearby = null;
            bool spotted = false;
            Vector3 position = player.transform.position;
            foreach (Guard guard in guards)
            {
                if (guard == null || !guard.Active)
                    continue;

                if (refreshPaths)
                {
                    guard.Agent.isStopped = false;
                    guard.Agent.speed = Mathf.Lerp(5.1f, 7.2f,
                        Mathf.Clamp01((wantedLevel - 1f) / (CostaSession.MaxWanted - 1)));
                    guard.Agent.SetDestination(target);
                }

                Vector3 feet = guard.Agent.transform.position;
                float range = (feet - position).sqrMagnitude;
                Vector3 sightTarget = position + Vector3.up * 0.85f;
                if (car != null)
                    sightTarget = car.NearestBodyPoint(feet + Vector3.up * 0.85f);
                if (range > 28f * 28f || Physics.Linecast(feet + Vector3.up * 0.85f, sightTarget,
                    out RaycastHit obstruction, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore)
                    && (car == null || !car.OwnsCollider(obstruction.collider)))
                    continue;

                spotted = true;
                Vector3 arrestOffset = sightTarget - (feet + Vector3.up * 0.85f);
                if (arrestOffset.sqrMagnitude <= 1.75f * 1.75f
                    && (car == null || car.Speed < 1.45f) && rollClock <= 0
                    && (nearby == null || guard == arrestingGuard))
                    nearby = guard;
            }

            if (nearby != arrestingGuard)
                arrestProgress = 0;
            arrestingGuard = nearby;
            if (nearby != null)
            {
                arrestProgress += Time.deltaTime / SoloArrestTime;
                if (arrestProgress >= 1)
                {
                    effects.Play(Cue.Cuffs);
                    EndRun(RunState.Caught);
                    return;
                }
            }

            if (spotted)
                heatUntil = Time.time + CostaSession.HeatDecayTime;
            else if (Time.time >= heatUntil && wantedLevel > (jobs.Bag.Count > 0 ? 1 : 0))
            {
                wantedLevel--;
                heatUntil = Time.time + CostaSession.HeatDecayTime;
                if (wantedLevel == 0)
                    StopPursuit();
            }
        }

        private void ResetRun(bool resetJob = true)
        {
            vehicleMotion.Clear();
            footfalls = default;
            engineThrottle = 0;
            wantedLevel = 0;
            nextCrime = 0;
            arrestProgress = 0;
            arrestingGuard = null;
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
            landing = 0;
            rollClock = 0;
            rollCooldown = 0;
            player.transform.position = harborJob ? HarborBlockout.Spawn : CostaCity.CitySpawn + Vector3.left * 2.8f;
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
            foreach (Guard guard in guards)
            {
                if (guard == null)
                    continue;
                bool hasPost = NavMesh.SamplePosition(guard.Post, out NavMeshHit post, 2f, NavMesh.AllAreas);
                guard.Health = 100;
                if (!guard.Agent.gameObject.activeSelf)
                {
                    guard.Agent.transform.position = hasPost ? post.position : guard.Post;
                    guard.Agent.gameObject.SetActive(true);
                }
                if (hasPost && guard.Agent.Warp(post.position))
                {
                    guard.Agent.ResetPath();
                    guard.Agent.isStopped = true;
                    guard.Agent.transform.rotation = Quaternion.identity;
                }
            }
        }

        private void EndRun(RunState result)
        {
            state = result;
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

        private static void SetCursor(bool locked)
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
            if (worldLoading || worldLoadFailed)
            {
                loadingScreen.Draw(loadingPhase, loadingProgress, worldLoadFailed, ReloadScene);
                if (worldLoadFailed)
                    CostaUi.DrawPointer();
                return;
            }

            DrawInterface();
            if (inMenu || overlay != FieldOverlay.None)
                CostaUi.DrawPointer();
            else if (CostaTouch.Available)
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
                cityMap.DrawFull(player.transform.position, yaw, MissionLocation(), closeOverlay);
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
            if (Event.current.type != EventType.Repaint)
                return;

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

            Vector3? missionLocation = MissionLocation();
            Vector3? destination = cityMap.Waypoint ?? missionLocation;
            string objective = "";
            if (cityMap.Waypoint.HasValue)
                objective = cityMap.WaypointTitle;
            else if (harborJob)
                objective = escaping ? L("Reach the safehouse", "Vá ao esconderijo") : L("Retrieve the case", "Recolha a maleta");
            else if (!session.Online && jobs.InProgress)
                objective = encounter.Active ? encounter.Status : jobs.Current.Title;
            else if (CurrentBag.Count > 0)
                objective = L("Sell loot at a fence", "Venda o saque num receptador");

            Vector3 position = player.transform.position;
            Vector3 target = destination ?? position;
            string prompt = InteractionPrompt(missionLocation ?? position);
            Vector3 viewport = followCamera.WorldToViewportPoint(target + Vector3.up * 1.35f);
            float side = Vector3.Dot(target - position, followCamera.transform.right);
            string notice = session.Online ? session.CurrentNotice
                : robberies.Notice.Length > 0 ? robberies.Notice : jobs.Notice;
            hud.DrawRun(position, yaw, destination, viewport, side, objective, session.Online ? -1 : jobs.Remaining,
                session.Online ? session.Local.wantedLevel : wantedLevel, notice, prompt, stamina / FullStamina,
                session.Online ? Mathf.Max(session.Local.arrestProgress, session.Local.arrestThreat) : arrestProgress,
                car != null ? car.Speed : -1, session.Online ? session.Local.health : encounter.Active ? encounter.Health : -1,
                IsPolice ? session.Local.armor : -1, arsenal, CostaTouch.Available, gamepad);
        }

        private Vector3? MissionLocation()
        {
            if (harborJob)
                return state == RunState.Escape ? HarborBlockout.Safehouse : HarborBlockout.Cargo;
            if (!session.Online && jobs.InProgress)
                return encounter.Active ? encounter.Location(player.transform.position) : jobs.Current.Point;
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

            int robbery = robberies.Nearby(player.transform.position);
            if (!IsPolice && state != RunState.Escape && robbery >= 0)
            {
                if (!robberies.Ready(robbery, session.Online ? session.Sites : null))
                    return L("This location is restocking", "Local repondo os itens");
                if (!CurrentBag.CanLoot(robbery))
                    return L("Not enough room in the backpack", "Mochila sem espaço para o saque");
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
