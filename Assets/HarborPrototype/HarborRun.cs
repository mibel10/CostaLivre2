using UnityEngine;
using UnityEngine.AI;
using UnityEngine.InputSystem;
using static CostaLivre.CostaText;

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
        [SerializeField] private InputActionAsset inputActions;
        [SerializeField] private Shader blockoutShader;

        private static readonly Vector3[] GuardPosts = { new(18, 0, 18), new(7, 0, 0), new(-47, 0, 96), new(-63, 0, 95), new(-30, 0, 73), new(-81, 0, 94) };
        private const float InteractionRadius = 1.8f;
        private const float FullStamina = 3.8f;
        private const float DefaultFov = 66f;
        private const float RollDuration = 0.69f;
        private static readonly Vector3 MenuActorPosition = new(18.9f, 0, -10.8f);

        private readonly Guard[] guards = new Guard[GuardPosts.Length];
        private readonly InputAction[] weaponSlots = new InputAction[2];
        private HarborBlockout harbor;
        private CostaCity city;
        private CostaJobs jobs;
        private CostaRobberies robberies;
        private CostaMap cityMap;
        private CostaGunShop gunShop;
        private FieldOverlay overlay;
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
        private bool soloResumable;
        private bool soloHarborJob;
        private RunState soloState;
        private Vector3 soloPosition;
        private float soloYaw;
        private float soloPitch;
        private float soloEscapeElapsed;
        private bool pausedForFocus;
        private bool wasPolice;
        private bool crouching;
        private bool walking;
        private bool runningPose;
        private int netRevision = -1;
        private int pendingBuy = -1;
        private int appliedSelection;
        private CostaBag CurrentBag => session.Online ? session.Bag : jobs.Bag;
        private int CurrentCash => session.Online ? session.Cash : jobs.Credits;
        private bool IsPolice => session.Online && session.Local.police;
        private System.Action closeOverlay;
        private CostaVehicle[] cars;
        private CostaVehicle car;
        private CostaBindings bindings;
        private InputAction jobsBoard;
        private System.Action<int> selectJob;
        private bool harborJob;
        private HarborArsenal arsenal;
        private GameObject cargo;
        private GameObject cargoPack;
        private CharacterController player;
        private Transform playerModel;
        private HarborCharacter playerBody;
        private HarborCharacter menuBody;
        private HarborMenu menu;
        private Transform menuWeaponMount;
        private Transform[] menuWeapons;
        private System.Action deploy;
        private System.Action resume;
        private System.Action quit;
        private bool inMenu;
        private bool enteredRun;
        private NavMeshData navMesh;
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
        private bool independentAlert;

        private void Awake()
        {
            Application.targetFrameRate = 60;
            Application.runInBackground = true;
            harbor = new HarborBlockout(transform, blockoutShader);
            harbor.Build();
            city = new CostaCity(harbor, transform);
            city.Build();
            jobs = new CostaJobs(city.HouseDoors);
            robberies = new CostaRobberies(harbor);
            cityMap = new CostaMap(city.Footprints);
            gunShop = new CostaGunShop();
            hud = new HarborHud(cityMap);
            bagPanel = new CostaBagPanel();
            closeOverlay = CloseOverlay;

            navMesh = harbor.BuildNavigation();
            if (navMesh != null)
                navMeshInstance = NavMesh.AddNavMeshData(navMesh);

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

            arsenal = new HarborArsenal(harbor, playerModel, blockoutShader, jobs);
            cargo = harbor.Case();
            for (int i = 0; i < guards.Length; i++)
                AddGuard(i);

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
                new(harbor, transform, new Vector3(-78, 0, 115), 180, new Color(0.25f, 0.27f, 0.28f), style: CostaVehicle.Style.Cruiser)
            };

            session = new CostaSession(cars);
            onlinePanel = new CostaOnlinePanel(session);
            remotes = new CostaRemoteView(harbor, transform);
            BindControls();
            followCamera.farClipPlane = 320f;
            followCamera.nearClipPlane = 0.07f;
            menu = new HarborMenu(bindings);
            CreateMenuPreview();
            deploy = StartRun;
            resume = ResumeRun;
            quit = () => Application.Quit();
            selectJob = SelectJob;
            Physics.SyncTransforms();
            ResetRun();
            ShowMenu();
        }

        private void CreateMenuPreview()
        {
            menuBody = harbor.Person("Loadout preview", transform, MenuActorPosition, new Color(0.94f, 0.93f, 0.87f));
            menuLook = new CostaLook(harbor, menuBody);
            menuBody.Root.rotation = Quaternion.Euler(0, 215f, 0);
            menuWeaponMount = new GameObject("Preview weapons").transform;
            menuWeaponMount.SetParent(menuBody.Root, false);
            menuWeapons = new Transform[HarborArsenal.WeaponNames.Length];
            for (int i = 0; i < menuWeapons.Length; i++)
            {
                Transform model = new GameObject(HarborArsenal.WeaponNames[i]).transform;
                model.SetParent(menuWeaponMount, false);
                HarborArsenal.BuildModel(harbor, model, i);
                menuWeapons[i] = model;
            }
        }

        private void UpdateMenuPreview()
        {
            float angle = Mathf.Sin(Time.unscaledTime * 0.38f) * 0.26f;
            Vector3 cameraPosition = MenuActorPosition + new Vector3(-2.85f + angle, 1.57f, -3.45f);
            Vector3 cameraRight = Vector3.Cross(Vector3.up, (MenuActorPosition - cameraPosition).normalized);
            Vector3 focus = MenuActorPosition + Vector3.up * 1.0f - cameraRight * 1.17f;
            followCamera.transform.SetPositionAndRotation(cameraPosition, Quaternion.LookRotation(focus - cameraPosition));
            followCamera.fieldOfView = 35f;
            menuBody.Animate(HarborCharacter.Motion.Idle);
            menuLook.Apply(menu.OnlinePage || session.Online ? onlinePanel.Outfit : 0, IsPolice);
            int previewSlot = arsenal.Owned(menu.SelectedWeapon) ? menu.SelectedWeapon : arsenal.Slot;
            bool equipped = previewSlot >= 0;
            if (equipped)
            {
                menuBody.HoldWeapon(false, 12f, false);
                menuWeaponMount.SetPositionAndRotation(menuBody.WeaponHand,
                    menuBody.Root.rotation * Quaternion.Euler(15, -9, 0));
            }
            for (int i = 0; i < menuWeapons.Length; i++)
                menuWeapons[i].gameObject.SetActive(equipped && previewSlot == i);
        }

        private void ShowMenu()
        {
            inMenu = true;
            menuBody.Root.gameObject.SetActive(true);
            Time.timeScale = 0;
            SetCursor(false);
            UpdateMenuPreview();
        }

        private void StartRun()
        {
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
                arsenal.Select(menu.SelectedWeapon);
            }
            inMenu = false;
            menuBody.Root.gameObject.SetActive(false);
            Time.timeScale = 1;
            PositionCamera(false, true);
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
            mapKey = AddButton(map, "City map", "<Keyboard>/m");
            bagKey = AddButton(map, "Backpack", "<Keyboard>/i", "<Gamepad>/dpad/up");
            crouchKey = AddButton(map, "Crouch stance", "<Keyboard>/leftCtrl", "<Gamepad>/dpad/down");
            for (int i = 0; i < weaponSlots.Length; i++)
                weaponSlots[i] = AddButton(map, $"Weapon slot {i + 1}", $"<Keyboard>/{i + 1}");
            bindings = new CostaBindings(controls, sprint, jump, roll, interact, reload);
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
            remotes?.Clear();
            menu?.Release();
            menuBody?.Release();
            playerBody?.Release();
            foreach (Guard guard in guards)
                guard?.Character?.Release();
            if (navMeshInstance.valid)
                navMeshInstance.Remove();
            Destroy(navMesh);
            Destroy(controls);
            arsenal?.Dispose();
            harbor?.ReleaseMaterials();
        }

        private void OnApplicationFocus(bool focused)
        {
            if (!focused)
            {
                SetCursor(false);
                pausedForFocus = !session.Online && !inMenu && overlay == FieldOverlay.None;
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

        private void TickNetwork()
        {
            if (!session.Online && !session.Connecting && !session.Hosting && !wasOnline)
                return;

            var pose = new CostaSession.Avatar
            {
                pos = player.transform.position,
                yaw = car != null ? car.Root.eulerAngles.y : playerModel.eulerAngles.y,
                revision = netRevision,
                playing = enteredOnlineWorld,
                standing = grounded && !crouching && rollClock <= 0 && car == null,
                crouched = crouching,
                jumpPose = car == null && (!grounded || landing > 0)
                    ? grounded ? Mathf.Lerp(0.78f, 1f, 1 - landing / 0.14f) : Mathf.Min(jumpClock / 0.8f, 0.77f)
                    : -1,
                rollPose = rollClock > 0 ? 1 - rollClock / RollDuration : -1,
                moving = !inMenu && overlay == FieldOverlay.None && walking,
                running = !inMenu && overlay == FieldOverlay.None && runningPose,
                held = car != null ? Mathf.RoundToInt(car.SignedSpeed * 10f) : 0
            };
            session.Tick(pose);

            if (!session.Online)
            {
                if (wasOnline)
                {
                    wasOnline = false;
                    enteredOnlineWorld = false;
                    netRevision = -1;
                    pendingBuy = -1;
                    remotes.Clear();
                    arsenal.SetOnlineWeapons(null);
                    localLook.Apply(0, false);
                    jobs.SetOnlineDoors(false);
                    jobs.ResumeAfterSession();
                    CloseOverlay();
                    ResetRun(resetJob: false);
                    if (soloResumable)
                    {
                        player.enabled = false;
                        player.transform.position = soloPosition;
                        player.enabled = true;
                        yaw = soloYaw;
                        pitch = soloPitch;
                        playerModel.rotation = Quaternion.Euler(0, yaw, 0);
                    }
                    harborJob = soloHarborJob;
                    enteredRun = soloResumable;
                    state = soloState;
                    escapeStartedAt = Time.time - soloEscapeElapsed;
                    cargo.SetActive(harborJob && state == RunState.FindCargo);
                    if (state == RunState.Escape || jobs.Active == 4 && jobs.StepNumber == 2)
                        StartPursuit(0.45f);
                    menu.ShowOnline();
                    ShowMenu();
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
                jobs.PauseForSession();

                wasOnline = true;
                jobs.SetOnlineDoors(true);
                pursuit = false;
                harborJob = false;
                cargo.SetActive(false);
                foreach (Guard guard in guards)
                    guard.Agent.gameObject.SetActive(false);
                session.Request("outfit", onlinePanel.Outfit);
            }

            CostaSession.Avatar avatar = session.Local;
            if (avatar.held > 0 && overlay != FieldOverlay.None)
                CloseOverlay();
            for (int i = 0; i < cars.Length; i++)
            {
                CostaSession.Vehicle vehicle = session.Cars[i];
                if (i != avatar.vehicle || netRevision != avatar.revision)
                    cars[i].ApplyNetwork(vehicle.pos, vehicle.yaw, vehicle.speed, joined || i == avatar.vehicle);
            }

            if (netRevision != avatar.revision)
            {
                netRevision = avatar.revision;
                state = RunState.FindCargo;
                car = avatar.vehicle >= 0 ? cars[avatar.vehicle] : null;
                player.enabled = false;
                player.transform.position = car != null ? car.Root.position : avatar.pos;
                playerModel.localPosition = Vector3.zero;
                playerModel.rotation = Quaternion.Euler(0, avatar.yaw, 0);
                playerModel.gameObject.SetActive(car == null || car.IsBike);
                arsenal.SetVisible(car == null);
                ResizeController(1.8f);
                player.enabled = car == null;
                crouching = walking = runningPose = false;
                grounded = true;
                verticalSpeed = -2.3f;
                rollClock = landing = 0;
                yaw = avatar.yaw;
                Physics.SyncTransforms();
                if (!inMenu)
                    PositionCamera(false, true);
            }

            arsenal.SetOnlineWeapons(session.WeaponMask);
            if (joined || wasPolice != avatar.police)
            {
                wasPolice = avatar.police;
                pendingBuy = -1;
                arsenal.Reset();
            }
            if (pendingBuy >= 0 && arsenal.Owned(pendingBuy))
            {
                arsenal.Select(pendingBuy);
                pendingBuy = -1;
            }
            localLook.Apply(avatar.outfit, avatar.police);
            remotes.Refresh(session, cars);
            robberies.Refresh(session.Sites);
        }

        private void Update()
        {
            TickNetwork();
            if (pausedForFocus)
                return;
            if (inMenu)
            {
                if (menu.CapturingKey)
                {
                    menu.ReadBinding();
                    UpdateMenuPreview();
                    return;
                }
                if ((!menu.OnlinePage || session.Online)
                    && (restart.WasPressedThisFrame() || Keyboard.current?.enterKey.wasPressedThisFrame == true))
                {
                    gamepad = restart.activeControl?.device is Gamepad;
                    if (CanResume())
                        ResumeRun();
                    else
                        StartRun();
                }
                else if ((openMenu.WasPressedThisFrame() || jobsBoard.WasPressedThisFrame()) && CanResume())
                {
                    ResumeRun();
                }
                else
                {
                    UpdateMenuPreview();
                }
                return;
            }

            if (overlay != FieldOverlay.None)
            {
                if (openMenu.WasPressedThisFrame() || (overlay == FieldOverlay.Map && mapKey.WasPressedThisFrame())
                    || (overlay == FieldOverlay.Bag && bagKey.WasPressedThisFrame()))
                    CloseOverlay();
                return;
            }

            if (openMenu.WasPressedThisFrame() || (!session.Online && jobsBoard.WasPressedThisFrame()))
            {
                if (jobsBoard.WasPressedThisFrame())
                    menu.ShowJobs();
                ShowMenu();
                return;
            }

            if (!session.Online && restart.WasPressedThisFrame())
            {
                gamepad = restart.activeControl?.device is Gamepad;
                ResetRun();
                return;
            }

            if (state == RunState.Delivered || state == RunState.Caught)
                return;

            if (mapKey.WasPressedThisFrame())
            {
                OpenOverlay(FieldOverlay.Map);
                return;
            }

            if (bagKey.WasPressedThisFrame())
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
                cameraInput *= Cursor.lockState == CursorLockMode.Locked && !recapturing ? 0.14f * menu.Sensitivity : 0;

            yaw = Mathf.Repeat(yaw + cameraInput.x, 360f);
            pitch = Mathf.Clamp(pitch - cameraInput.y, -25f, 55f);

            city.Animate();
            if (!session.Online)
            {
                robberies.Refresh();
                foreach (CostaVehicle vehicle in cars)
                    if (vehicle != car && vehicle.Speed > 0.05f)
                        vehicle.Drive(Vector2.zero, false);
            }
            cargoPack.SetActive(CurrentBag.Count > 0 || state == RunState.Escape);
            cityMap.CheckWaypoint(player.transform.position);
            bool interaction = interact.WasPressedThisFrame();
            if (interaction && car == null)
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

            if (car != null)
            {
                car.Drive(move.ReadValue<Vector2>(), jump.IsPressed());
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
                Physics.SyncTransforms();
                PositionCamera(false);
                UpdateCityJobs(false);
                if (pursuit)
                    UpdatePursuit();
                AnimatePolice();
                return;
            }

            CycleWeapons();
            if (reload.WasPressedThisFrame())
                arsenal.Reload();

            bool aiming = arsenal.Armed && rollClock <= 0 && aim.IsPressed()
                && (Cursor.lockState == CursorLockMode.Locked || aim.activeControl?.device is Gamepad);
            MovePlayer(aiming);
            Physics.SyncTransforms();
            PositionCamera(aiming && rollClock <= 0);

            bool fireEnabled = rollClock <= 0 && !recapturing
                && (Cursor.lockState == CursorLockMode.Locked || shoot.activeControl?.device is Gamepad);
            if (arsenal.Update(followCamera, playerBody.WeaponHand, pitch, fireEnabled && shoot.WasPressedThisFrame(),
                fireEnabled && shoot.IsPressed(), aiming, HitGuard))
            {
                StartPursuit(0.25f);
            }

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

        private void OpenOverlay(FieldOverlay screen)
        {
            overlay = screen;
            Time.timeScale = 0;
            SetCursor(false);
        }

        private void CloseOverlay()
        {
            overlay = FieldOverlay.None;
            Time.timeScale = 1;
            SetCursor(true);
        }

        private void AnimatePolice()
        {
            foreach (Guard guard in guards)
            {
                if (guard.Active && (guard.Agent.transform.position - player.transform.position).sqrMagnitude < 4900)
                {
                    guard.Character.Animate(guard.Agent.velocity.sqrMagnitude > 0.35f
                        ? HarborCharacter.Motion.Run : HarborCharacter.Motion.Idle, 0.82f);
                }
            }
        }

        private void SelectJob(int index)
        {
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
                    if (PlayerPrefs.GetInt("CostaPortPaid", 0) == 0)
                    {
                        PlayerPrefs.SetInt("CostaPortPaid", 1);
                        jobs.Award(3200);
                    }
                    EndRun(RunState.Delivered);
                    return;
                }
            }

            jobs.CheckClock();
            bool finishingEscape = jobs.Active == 4 && jobs.StepNumber == 2;
            bool missionInteraction = jobs.InProgress && takeAction
                && CostaJobs.Near(player.transform.position, jobs.Current.Point, jobs.Current.Radius);
            if (jobs.Advance(player.transform.position, car != null, takeAction))
                StartPursuit(0.50f);
            if (finishingEscape && !jobs.InProgress)
                StopPursuit();

            if (takeAction && !missionInteraction)
            {
                if (TryLoot())
                    return;
                int property = jobs.NearbyProperty(player.transform.position);
                if (property >= 0 && !jobs.Owned(property))
                    jobs.Buy(property);
            }
        }

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
                StartPursuit(0.45f, lootOnly: true);
            }
            return true;
        }

        private void SellLoot(int item)
        {
            if (session.Online)
                session.Request("sell", item);
            else
            {
                jobs.SellLoot(item);
                if (jobs.Bag.Count == 0 && !independentAlert)
                    StopPursuit();
            }
        }

        private bool BuyWeapon(int index)
        {
            if (!session.Online)
                return jobs.BuyWeapon(index);
            if (!IsPolice && !arsenal.Owned(index) && session.Cash >= CostaJobs.WeaponPrices[index])
            {
                pendingBuy = index;
                session.Request("buy", index);
            }
            return false;
        }

        private void StopPursuit()
        {
            independentAlert = false;
            pursuit = false;
            foreach (Guard guard in guards)
                if (guard.Active)
                    guard.Agent.isStopped = true;
        }

        private bool TryUseVehicle()
        {
            if (car != null)
            {
                for (int side = -1; side <= 1; side += 2)
                {
                    Vector3 exit = car.Root.position + car.Root.right * side * car.ExitOffset;
                    if (Physics.CheckCapsule(exit + Vector3.up * 0.38f, exit + Vector3.up * 1.46f, 0.34f,
                        Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
                    {
                        continue;
                    }

                    if (!Physics.Raycast(exit + Vector3.up, Vector3.down, out RaycastHit ground, 1.45f,
                        Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
                    {
                        continue;
                    }

                    exit.y = ground.point.y + 0.03f;
                    if (session.Online)
                    {
                        session.ExitVehicle(exit);
                        return true;
                    }

                    car = null;
                    player.transform.position = exit;
                    playerModel.gameObject.SetActive(true);
                    playerModel.localPosition = Vector3.zero;
                    arsenal.SetVisible(true);
                    player.enabled = true;
                    grounded = true;
                    verticalSpeed = -2.3f;
                    return true;
                }
                blockedExitUntil = Time.unscaledTime + 2;
                return true;
            }

            if (!grounded || crouching || rollClock > 0
                || (!session.Online && jobs.NearbyProperty(player.transform.position) >= 0)
                || (!IsPolice && state != RunState.Escape && robberies.Nearby(player.transform.position) >= 0)
                || (harborJob && state == RunState.FindCargo && IsNear(HarborBlockout.Cargo, InteractionRadius))
                || (!session.Online && jobs.InProgress && jobs.Current.Mode != CostaJobs.Action.Drive
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

            car = nearby;
            player.enabled = false;
            playerModel.gameObject.SetActive(car.IsBike);
            arsenal.SetVisible(false);
            return true;
        }

        private CostaVehicle NearbyVehicle()
        {
            CostaVehicle result = null;
            float distance = 3.25f * 3.25f;
            for (int i = 0; i < cars.Length; i++)
            {
                if (session.Online && session.Cars[i].driver != 0)
                    continue;
                CostaVehicle candidate = cars[i];
                float separation = (candidate.Root.position - player.transform.position).sqrMagnitude;
                if (separation <= distance && (!Physics.Linecast(player.transform.position + Vector3.up * 0.8f,
                    candidate.Root.position + Vector3.up * 0.8f, out RaycastHit obstruction,
                    Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore) || obstruction.transform == candidate.Root))
                {
                    distance = separation;
                    result = candidate;
                }
            }
            return result;
        }

        private bool CanResume() => enteredRun && (state == RunState.FindCargo || state == RunState.Escape);

        private void CycleWeapons()
        {
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
            else if (nextWeapon.WasPressedThisFrame() || previousWeapon.WasPressedThisFrame())
            {
                gamepad = true;
                arsenal.Cycle();
            }
        }

        private void PositionCamera(bool aiming, bool snap = false)
        {
            Quaternion rotation = Quaternion.Euler(pitch, yaw, 0);
            Vector3 pivot = player.transform.position + Vector3.up * (car != null ? 2.05f : crouching ? 0.97f : 1.42f);
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
            Vector2 input = Vector2.ClampMagnitude(move.ReadValue<Vector2>(), 1f);
            Quaternion heading = Quaternion.Euler(0, yaw, 0);
            Vector3 direction = heading * new Vector3(input.x, 0, input.y);

            bool holdingSprint = sprint.IsPressed();
            if (!holdingSprint)
                needsSprintRelease = false;

            if (rollClock <= 0)
            {
                bool wantsCrouch = grounded && crouchKey.IsPressed();
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
            if (grounded && !crouching && rollClock <= 0 && rollCooldown <= 0 && stamina >= 0.75f && roll.WasPressedThisFrame())
            {
                rollDirection = direction.sqrMagnitude > 0.01f ? direction.normalized : heading * Vector3.forward;
                rollClock = RollDuration;
                rollCooldown = RollDuration + 0.56f;
                stamina -= 0.75f;
                sprintRecoveryDelay = 0.75f;
                ResizeController(1.0f);
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
                if (!rolling && !crouching && jump.WasPressedThisFrame())
                {
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
            float speed = crouching ? 2f : running ? 7.9f : aiming ? 2.65f : 4.15f;
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
            CollisionFlags hit = player.Move((velocity + Vector3.up * verticalSpeed) * step);
            grounded = (hit & CollisionFlags.Below) != 0;
            if ((hit & CollisionFlags.Above) != 0 && verticalSpeed > 0)
                verticalSpeed = 0;
            if (!wasGrounded && grounded)
                landing = 0.14f;

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
                playerBody.Animate(motion, aiming ? 0.79f : 1f);
            }

            if (crouching)
                playerBody.CrouchPose();
            if (arsenal.Armed)
                playerBody.HoldWeapon(aiming, pitch, arsenal.Slot != 0);
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
            foreach (Guard guard in guards)
            {
                if (guard.Hitbox != hit)
                    continue;

                guard.Health -= damage;
                arsenal.ShowHit();
                if (guard.Health <= 0)
                    guard.Agent.gameObject.SetActive(false);
                return;
            }
        }

        private void StartPursuit(float delay, bool lootOnly = false)
        {
            if (!session.Online && !lootOnly)
                independentAlert = true;
            if (session.Online || pursuit || state == RunState.Delivered || state == RunState.Caught)
                return;

            pursuit = true;
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

            foreach (Guard guard in guards)
            {
                if (!guard.Active)
                    continue;

                if (refreshPaths)
                {
                    guard.Agent.isStopped = false;
                    guard.Agent.SetDestination(target);
                }

                Vector3 position = guard.Agent.transform.position;
                if ((car == null || car.Speed < 1.45f) && IsNear(position, 1.02f) && !Physics.Linecast(position + Vector3.up * 0.85f,
                    player.transform.position + Vector3.up * 0.85f, Physics.DefaultRaycastLayers,
                    QueryTriggerInteraction.Ignore))
                {
                    EndRun(RunState.Caught);
                    return;
                }
            }
        }

        private void ResetRun(bool resetJob = true)
        {
            independentAlert = false;
            state = RunState.FindCargo;
            pursuit = false;
            if (resetJob)
                jobs.Select(jobs.Active);
            crouching = walking = runningPose = false;
            car = null;
            foreach (CostaVehicle vehicle in cars)
                vehicle.Reset();
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
                arsenal.Select(menu.SelectedWeapon);
            playerBody.Animate(HarborCharacter.Motion.Idle, progress: 0);
            PositionCamera(false, true);
            if (Application.isFocused)
                SetCursor(true);

            foreach (Guard guard in guards)
            {
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
                if (guard.Active)
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
            if (inMenu)
            {
                menu.Draw(CanResume(), deploy, resume, quit, jobs, selectJob, onlinePanel, arsenal, session.Online);
                return;
            }
            if (overlay == FieldOverlay.Map)
            {
                cityMap.DrawFull(player.transform.position, yaw, MissionLocation(), closeOverlay);
                return;
            }
            if (overlay == FieldOverlay.Shop)
            {
                gunShop.Draw(CurrentCash, arsenal, BuyWeapon, closeOverlay);
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
                    gamepad ? L("START   Retry     SELECT   Menu", "START   Reiniciar     SELECT   Menu")
                        : L("F5   Retry     ESC   Menu", "F5   Reiniciar     ESC   Menu"),
                    delivered ? HarborBlockout.Mint : HarborBlockout.Amber);
                return;
            }

            Vector3? missionLocation = MissionLocation();
            Vector3 target = cityMap.Waypoint ?? missionLocation ?? player.transform.position;
            bool marked = missionLocation.HasValue || cityMap.Waypoint.HasValue;
            string objective = L("Explore Santa Luz", "Explore Santa Luz");
            string marker = "";
            if (cityMap.Waypoint.HasValue)
            {
                objective = cityMap.WaypointTitle;
                marker = L("WAYPOINT", "MARCADOR");
            }
            else if (harborJob)
            {
                objective = escaping ? L("Reach the safehouse", "Vá ao esconderijo") : L("Retrieve the case", "Recolha a maleta");
                marker = escaping ? L("SAFEHOUSE", "ESCONDERIJO") : L("CARGO CASE", "MALETA");
            }
            else if (!session.Online && jobs.InProgress)
            {
                objective = jobs.Current.Title;
                marker = $"{jobs.StepNumber:00} / {CostaJobs.List[jobs.Active].Steps.Length:00}";
            }
            else if (CurrentBag.Count > 0)
            {
                objective = L("Sell loot at a fence", "Venda o saque num receptador");
                marker = L("FENCE", "RECEPTADOR");
            }
            else if (IsPolice)
            {
                objective = L("Patrol Santa Luz", "Patrulhe Santa Luz");
                marker = L("POLICE", "POLÍCIA");
            }

            string prompt = InteractionPrompt(missionLocation ?? target);
            Vector3 viewport = followCamera.WorldToViewportPoint(target + Vector3.up * 1.35f);
            float side = Vector3.Dot(target - player.transform.position, followCamera.transform.right);
            string notice = session.Online ? session.CurrentNotice
                : robberies.Notice.Length > 0 ? robberies.Notice : jobs.Notice;
            hud.DrawRun(marked, session.Online ? session.Local.wanted : pursuit, stamina / FullStamina,
                viewport, side, prompt, arsenal, gamepad, player.transform.position, target, yaw, objective, marker,
                CurrentCash, session.Online ? -1 : jobs.Remaining, notice, car != null ? car.Speed : -1);
        }

        private Vector3? MissionLocation()
        {
            if (harborJob)
                return state == RunState.Escape ? HarborBlockout.Safehouse : HarborBlockout.Cargo;
            if (!session.Online && jobs.InProgress)
                return jobs.Current.Point;
            if (CurrentBag.Count == 0 || cityMap.Waypoint.HasValue)
                return null;
            Vector3 point = player.transform.position;
            Vector3 first = CostaCity.Vendors[0];
            Vector3 second = CostaCity.Vendors[1];
            return (first - point).sqrMagnitude < (second - point).sqrMagnitude ? first : second;
        }

        private string InteractionPrompt(Vector3 target)
        {
            string use = gamepad ? "Y" : bindings.Name(3);
            if (session.Online && session.Local.held > 0)
                return L($"Detained · {session.Local.held}s", $"Detido · {session.Local.held}s");
            if (car != null && Time.unscaledTime < blockedExitUntil)
                return L("Both sides are blocked", "Saída bloqueada dos dois lados");
            if (car != null)
                return L($"{use} Exit   {(gamepad ? "A" : bindings.Name(1))} Brake",
                    $"{use} Sair   {(gamepad ? "A" : bindings.Name(1))} Frear");
            if (IsPolice && remotes.ArrestCandidate(player.transform.position) != null)
                return L($"{use} Arrest", $"{use} Prender");
            if (!IsPolice && CostaSession.AtVendor(player.transform.position))
                return L($"{use} Sell loot", $"{use} Vender saque");
            if (!IsPolice && IsNear(CostaCity.GunCounter, 2f))
                return L($"{use} Open gun shop", $"{use} Abrir armaria");
            if (harborJob && state == RunState.FindCargo && IsNear(target, InteractionRadius))
                return L($"{use} Take the case", $"{use} Pegar a maleta");
            if (!session.Online && jobs.InProgress && IsNear(target, jobs.Current.Radius))
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
            if (!gamepad && Cursor.lockState != CursorLockMode.Locked)
                return L("Click to capture the mouse", "Clique para capturar o mouse");

            return gamepad ? L("↑ Backpack   ↓ Crouch   A Jump   B Roll", "↑ Mochila   ↓ Agachar   A Pular   B Rolar")
                : L($"M Map   I Backpack   CTRL Crouch   {bindings.Name(1)} Jump   {bindings.Name(2)} Roll   ESC Menu",
                    $"M Mapa   I Mochila   CTRL Agachar   {bindings.Name(1)} Pular   {bindings.Name(2)} Rolar   ESC Menu");
        }
    }
}
