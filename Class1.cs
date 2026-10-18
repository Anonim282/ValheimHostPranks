using System;
using System.Collections;
using System.Collections.Generic;
using BepInEx;
using BepInEx.Configuration;
using UnityEngine;

namespace ValheimHostPranks
{
    [BepInPlugin(ModGUID, ModName, ModVersion)]
    public class HostPranksPlugin : BaseUnityPlugin
    {
        public const string ModGUID = "com.anonim.valheimhostpranks";
        public const string ModName = "Valheim Host Pranks";
        public const string ModVersion = "1.1.0";

        public static HostPranksPlugin Instance { get; private set; } = null!;

        private ConfigEntry<KeyCode> _toggleKeyConfig = null!;
        private bool _isGuiOpen = false;
        private Rect _windowRect = new Rect(40, 40, 430, 480);
        private Vector2 _scrollPos = Vector2.zero;
        private string _statusMessage = "Готов к пранкам. Выберите игрока.";
        private float _statusTimer = 0f;

        private void Awake()
        {
            Instance = this;
            _toggleKeyConfig = Config.Bind("General", "ToggleMenuKey", KeyCode.F8, "Клавиша открытия меню пранков хоста");
            Logger.LogInfo($"{ModName} v{ModVersion} загружен! Меню на [{_toggleKeyConfig.Value}].");
        }

        private void Update()
        {
            if (Input.GetKeyDown(_toggleKeyConfig.Value))
            {
                if (Player.m_localPlayer != null && ZNet.instance != null && ZNet.instance.IsServer())
                {
                    _isGuiOpen = !_isGuiOpen;
                }
                else if (_isGuiOpen)
                {
                    _isGuiOpen = false;
                }
            }

            if (_statusTimer > 0f)
            {
                _statusTimer -= Time.deltaTime;
                if (_statusTimer <= 0f)
                {
                    _statusMessage = "Готов к пранкам. Выберите игрока.";
                }
            }
        }

        private void OnGUI()
        {
            if (!_isGuiOpen) return;
            if (Player.m_localPlayer == null || ZNet.instance == null || !ZNet.instance.IsServer())
            {
                _isGuiOpen = false;
                return;
            }

            GUI.backgroundColor = new Color(0.12f, 0.12f, 0.15f, 0.95f);
            _windowRect = GUI.Window(889911, _windowRect, DrawWindow, $"⚡ Хост-Пранки v{ModVersion} [F8]");
        }

        private void DrawWindow(int windowId)
        {
            GUI.DragWindow(new Rect(0, 0, 390, 25));

            GUILayout.Space(5);
            GUILayout.BeginHorizontal();
            GUILayout.Label("<color=#FFD700><b>Панель управления пранками (Только Хост)</b></color>");
            if (GUILayout.Button("✕", GUILayout.Width(25), GUILayout.Height(20)))
            {
                _isGuiOpen = false;
            }
            GUILayout.EndHorizontal();

            GUILayout.Label("<color=#AAAAAA>Нажмите кнопку напротив игрока для вызова Одина:</color>");
            GUILayout.Space(4);

            List<Player> allPlayers = Player.GetAllPlayers();
            if (allPlayers == null || allPlayers.Count == 0)
            {
                GUILayout.Label("<color=#FF6666>Игроки не найдены в мире.</color>");
            }
            else
            {
                _scrollPos = GUILayout.BeginScrollView(_scrollPos, GUILayout.Height(270));

                foreach (Player player in allPlayers)
                {
                    if (player == null) continue;

                    bool isLocal = (player == Player.m_localPlayer);
                    string nameText = player.GetPlayerName();
                    if (string.IsNullOrEmpty(nameText)) nameText = "Безымянный викинг";

                    float dist = Player.m_localPlayer != null ? Vector3.Distance(Player.m_localPlayer.transform.position, player.transform.position) : 0f;
                    string tag = isLocal ? " <color=#44FF44>(Вы)</color>" : $" <color=#AAAAAA>[{dist:F0}м]</color>";

                    GUILayout.BeginVertical(GUI.skin.box);
                    GUILayout.Label($"<b>{nameText}</b>{tag}");

                    GUILayout.BeginHorizontal();

                    if (GUILayout.Button("⚡ Явление Одина (Стая 13с + Молния)", GUILayout.Height(28)))
                    {
                        TriggerOdinCinemaPrank(player);
                    }

                    GUILayout.EndHorizontal();
                    GUILayout.EndVertical();
                    GUILayout.Space(3);
                }

                GUILayout.EndScrollView();
            }

            GUILayout.Space(6);

            // Кнопка очистки застрявших ворон
            if (GUILayout.Button("🧹 Очистить лишних ворон возле дома", GUILayout.Height(26)))
            {
                CleanExistingCrowsAroundLocalPlayer();
            }

            GUILayout.Space(4);
            GUILayout.Label($"<color=#E0E0E0>Статус:</color> <color=#FFFF66>{_statusMessage}</color>");
        }

        public void SetStatus(string message)
        {
            _statusMessage = message;
            _statusTimer = 5f;
            Logger.LogInfo(message);
        }

        private void CleanExistingCrowsAroundLocalPlayer()
        {
            if (Player.m_localPlayer == null) return;
            Vector3 center = Player.m_localPlayer.transform.position;
            int count = 0;

            // Находим всех птиц RandomFlyingBird в радиусе 120 метров
            var allBirds = UnityEngine.Object.FindObjectsByType<RandomFlyingBird>(FindObjectsSortMode.None);
            foreach (var bird in allBirds)
            {
                if (bird != null && Vector3.Distance(bird.transform.position, center) <= 120f)
                {
                    SafelyDestroyValheimObject(bird.gameObject);
                    count++;
                }
            }

            var ravens = UnityEngine.Object.FindObjectsByType<ScatteringRaven>(FindObjectsSortMode.None);
            foreach (var r in ravens)
            {
                if (r != null)
                {
                    UnityEngine.Object.Destroy(r.gameObject);
                    count++;
                }
            }

            var actors = UnityEngine.Object.FindObjectsByType<RavenActor>(FindObjectsSortMode.None);
            foreach (var a in actors)
            {
                if (a != null)
                {
                    UnityEngine.Object.Destroy(a.gameObject);
                    count++;
                }
            }

            SetStatus($"🧹 Удалено {count} ворон в радиусе 120м от вас!");
        }

        public static void SafelyDestroyValheimObject(GameObject go)
        {
            if (go == null) return;

            try
            {
                ZNetView znv = go.GetComponent<ZNetView>();
                if (znv != null && ZNetScene.instance != null)
                {
                    // Точный вызов как в штатной консольной команде forcedelete
                    ZNetScene.instance.Destroy(go);
                    return;
                }
            }
            catch { }

            UnityEngine.Object.Destroy(go);
        }

        private void TriggerOdinCinemaPrank(Player target)
        {
            if (target == null) return;

            GameObject? odinPrefab = FindOdinPrefab();
            if (odinPrefab == null)
            {
                SetStatus("Ошибка: Модель Одина не найдена в ресурсах.");
                return;
            }

            Vector3 forward = target.transform.forward;
            forward.y = 0f;
            if (forward.sqrMagnitude < 0.01f) forward = Vector3.forward;
            forward.Normalize();

            Vector3 summonPos = target.transform.position + forward * 10f;
            if (ZoneSystem.instance != null)
            {
                float groundHeight = ZoneSystem.instance.GetGroundHeight(summonPos);
                if (groundHeight > 0f) summonPos.y = groundHeight;
            }

            GameObject directorObj = new GameObject("OdinCinemaDirector");
            directorObj.transform.position = target.transform.position + Vector3.up * 1.5f;
            OdinCinemaDirector director = directorObj.AddComponent<OdinCinemaDirector>();
            director.StartRitual(target, summonPos, odinPrefab);

            SetStatus($"🦅 Стая из 30 черных воронов окружает '{target.GetPlayerName()}' (13 сек)...");
        }

        public static GameObject? FindOdinPrefab()
        {
            if (ZNetScene.instance != null)
            {
                var p = ZNetScene.instance.GetPrefab("Odin") ?? ZNetScene.instance.GetPrefab("odin");
                if (p != null) return p;

                if (ZNetScene.instance.m_prefabs != null)
                {
                    foreach (var go in ZNetScene.instance.m_prefabs)
                    {
                        if (go != null && (go.GetComponent<Odin>() != null || go.name.Equals("Odin", StringComparison.OrdinalIgnoreCase)))
                        {
                            return go;
                        }
                    }
                }

                if (ZNetScene.instance.m_nonNetViewPrefabs != null)
                {
                    foreach (var go in ZNetScene.instance.m_nonNetViewPrefabs)
                    {
                        if (go != null && (go.GetComponent<Odin>() != null || go.name.Equals("Odin", StringComparison.OrdinalIgnoreCase)))
                        {
                            return go;
                        }
                    }
                }
            }

            SpawnSystem[] spawnSystems = UnityEngine.Object.FindObjectsByType<SpawnSystem>(FindObjectsSortMode.None);
            if (spawnSystems != null)
            {
                foreach (var sys in spawnSystems)
                {
                    if (sys == null || sys.m_spawnLists == null) continue;
                    foreach (var spawnList in sys.m_spawnLists)
                    {
                        if (spawnList == null || spawnList.m_spawners == null) continue;
                        foreach (var sp in spawnList.m_spawners)
                        {
                            if (sp != null && sp.m_prefab != null)
                            {
                                if (sp.m_prefab.GetComponent<Odin>() != null || sp.m_prefab.name.Equals("Odin", StringComparison.OrdinalIgnoreCase))
                                {
                                    return sp.m_prefab;
                                }
                            }
                        }
                    }
                }
            }

            try
            {
                var allOdins = Resources.FindObjectsOfTypeAll<Odin>();
                if (allOdins != null && allOdins.Length > 0) return allOdins[0].gameObject;

                var allGos = Resources.FindObjectsOfTypeAll<GameObject>();
                foreach (var go in allGos)
                {
                    if (go != null && (go.GetComponent<Odin>() != null || go.name.Equals("Odin", StringComparison.OrdinalIgnoreCase)))
                    {
                        return go;
                    }
                }
            }
            catch { }

            return null;
        }

        // Возвращает ТОЛЬКО чистую визуальную модель птицы (БЕЗ ZNetView, БЕЗ сети!)
        public static GameObject? CreatePureVisualCrow()
        {
            GameObject? crowPrefab = null;
            if (ZNetScene.instance != null)
            {
                crowPrefab = ZNetScene.instance.GetPrefab("Crow") ?? ZNetScene.instance.GetPrefab("crow") ?? ZNetScene.instance.GetPrefab("Seagal");
            }

            if (crowPrefab != null)
            {
                RandomFlyingBird birdComp = crowPrefab.GetComponent<RandomFlyingBird>();
                GameObject template = (birdComp != null && birdComp.m_flyingModel != null) ? birdComp.m_flyingModel : crowPrefab;

                GameObject visual = UnityEngine.Object.Instantiate(template);

                // Очищаем любые сетевые скрипты и коллайдеры
                var znv = visual.GetComponent<ZNetView>();
                if (znv != null) UnityEngine.Object.DestroyImmediate(znv);
                var ai = visual.GetComponent<RandomFlyingBird>();
                if (ai != null) UnityEngine.Object.DestroyImmediate(ai);
                var cols = visual.GetComponentsInChildren<Collider>(true);
                foreach (var c in cols) UnityEngine.Object.DestroyImmediate(c);

                foreach (var r in visual.GetComponentsInChildren<Renderer>(true))
                {
                    if (r != null && r.material != null)
                    {
                        r.material.color = new Color(0.06f, 0.06f, 0.06f, 1.0f);
                    }
                }

                return visual;
            }

            GameObject fallback = GameObject.CreatePrimitive(PrimitiveType.Cube);
            fallback.name = "ProceduralRaven";
            fallback.transform.localScale = new Vector3(0.35f, 0.08f, 0.6f);
            var col = fallback.GetComponent<Collider>();
            if (col != null) UnityEngine.Object.DestroyImmediate(col);
            return fallback;
        }
    }

    // =========================================================================
    // РЕЖИССЕРСКИЙ КОНТРОЛЛЕР: СТАЯ (13 СЕК) -> СХОЖДЕНИЕ В ТОЧКУ -> ОДИН
    // =========================================================================
    public class OdinCinemaDirector : MonoBehaviour
    {
        private Player _target = null!;
        private Vector3 _summonPos;
        private GameObject _odinPrefab = null!;
        private readonly List<RavenActor> _ravens = new List<RavenActor>();
        private float _timer = 0f;
        private int _stage = 0; 
        private AudioSource _audio = null!;
        private float _audioTimer = 0f;

        public void StartRitual(Player target, Vector3 summonPos, GameObject odinPrefab)
        {
            _target = target;
            _summonPos = summonPos;
            _odinPrefab = odinPrefab;

            // Создаем стаю из 30 ЧИСТО ВИЗУАЛЬНЫХ ворон (никаких ZDO и сети!)
            int birdCount = 30;
            for (int i = 0; i < birdCount; i++)
            {
                float angle = (float)i / birdCount * Mathf.PI * 2f;
                float dist = UnityEngine.Random.Range(25f, 44f);
                float height = UnityEngine.Random.Range(8f, 24f);
                float orbitSpeed = UnityEngine.Random.Range(1.0f, 1.9f) * (i % 2 == 0 ? 1f : -1f);

                Vector3 startPos = _target.transform.position + new Vector3(Mathf.Cos(angle) * dist, height, Mathf.Sin(angle) * dist);

                GameObject? birdObj = HostPranksPlugin.CreatePureVisualCrow();
                if (birdObj == null) continue;
                birdObj.transform.position = startPos;

                RavenActor actor = birdObj.AddComponent<RavenActor>();
                actor.Initialize(_target, angle, dist, height, orbitSpeed);
                _ravens.Add(actor);
            }

            // Звук стаи: играем в 2D стерео вокруг игрока на 100% громкости, чтобы карканье отчетливо слышалось отовсюду
            _audio = gameObject.AddComponent<AudioSource>();
            _audio.spatialBlend = 0.0f; // 2D звук - слышен идеально чисто и громко независимо от поворота камеры
            _audio.volume = 1.0f;

            PlayCrowSound();
        }

        private void PlayCrowSound()
        {
            AudioClip? crowClip = FindCrowAudioClip();
            if (crowClip != null && _audio != null)
            {
                _audio.pitch = UnityEngine.Random.Range(0.92f, 1.08f);
                _audio.PlayOneShot(crowClip, 1.0f);
            }
        }

        private void Update()
        {
            if (_target == null)
            {
                CleanupRavens();
                Destroy(gameObject);
                return;
            }

            // Следуем за игроком, чтобы звук всегда был рядом с ним
            transform.position = _target.transform.position + Vector3.up * 1.5f;

            _timer += Time.deltaTime;
            _audioTimer += Time.deltaTime;

            // Вороны периодически каркают во время кружения (каждые 2.6 сек)
            if (_stage == 0 && _audioTimer >= 2.6f)
            {
                _audioTimer = 0f;
                PlayCrowSound();
            }

            if (_stage == 0)
            {
                // Стая кружит ровно 13.0 секунд
                if (_timer >= 13.0f)
                {
                    _stage = 1;
                    _timer = 0f;
                    PlayCrowSound();
                    foreach (var r in _ravens)
                    {
                        if (r != null) r.DiveInto(_summonPos);
                    }
                }
            }
            else if (_stage == 1)
            {
                // Вороны плавно сходятся в точку за 2.4 секунды
                if (_timer >= 2.4f)
                {
                    _stage = 2;
                    SpawnOdinAtConvergence();
                }
            }
        }

        private void SpawnOdinAtConvergence()
        {
            CleanupRavens();

            Quaternion rot = Quaternion.LookRotation((_target.transform.position - _summonPos).normalized);
            GameObject odinInstance = UnityEngine.Object.Instantiate(_odinPrefab, _summonPos, rot);
            odinInstance.SetActive(true);

            Odin vanillaOdin = odinInstance.GetComponent<Odin>();
            EffectList? despawnEffects = null;
            if (vanillaOdin != null)
            {
                despawnEffects = vanillaOdin.m_despawn;
                UnityEngine.Object.Destroy(vanillaOdin);
            }

            OdinPrankController controller = odinInstance.AddComponent<OdinPrankController>();
            controller.Initialize(_target, despawnEffects);

            Destroy(gameObject);
        }

        private void CleanupRavens()
        {
            foreach (var r in _ravens)
            {
                if (r != null)
                {
                    UnityEngine.Object.Destroy(r.gameObject);
                }
            }
            _ravens.Clear();
        }

        private static AudioClip? _cachedGameCrowClip;

        public static AudioClip FindCrowAudioClip()
        {
            if (_cachedGameCrowClip != null) return _cachedGameCrowClip;

            try
            {
                // 1. Попробуем префаб ворона
                if (ZNetScene.instance != null)
                {
                    GameObject crow = ZNetScene.instance.GetPrefab("Crow") ?? ZNetScene.instance.GetPrefab("crow");
                    if (crow != null)
                    {
                        var bird = crow.GetComponent<RandomFlyingBird>();
                        if (bird != null && bird.m_randomNoise != null && bird.m_randomNoise.m_effectPrefabs != null)
                        {
                            foreach (var ep in bird.m_randomNoise.m_effectPrefabs)
                            {
                                if (ep != null && ep.m_prefab != null)
                                {
                                    var zsfx = ep.m_prefab.GetComponentInChildren<ZSFX>(true);
                                    if (zsfx != null && zsfx.m_audioClips != null && zsfx.m_audioClips.Length > 0 && zsfx.m_audioClips[0] != null)
                                    {
                                        _cachedGameCrowClip = zsfx.m_audioClips[0];
                                        return _cachedGameCrowClip;
                                    }
                                    var src = ep.m_prefab.GetComponentInChildren<AudioSource>(true);
                                    if (src != null && src.clip != null)
                                    {
                                        _cachedGameCrowClip = src.clip;
                                        return _cachedGameCrowClip;
                                    }
                                }
                            }
                        }
                    }

                    // 2. Попробуем прямой префаб звука sfx_crow_idle
                    GameObject sfx = ZNetScene.instance.GetPrefab("sfx_crow_idle");
                    if (sfx != null)
                    {
                        var zsfx = sfx.GetComponentInChildren<ZSFX>(true);
                        if (zsfx != null && zsfx.m_audioClips != null && zsfx.m_audioClips.Length > 0 && zsfx.m_audioClips[0] != null)
                        {
                            _cachedGameCrowClip = zsfx.m_audioClips[0];
                            return _cachedGameCrowClip;
                        }
                        var src = sfx.GetComponentInChildren<AudioSource>(true);
                        if (src != null && src.clip != null)
                        {
                            _cachedGameCrowClip = src.clip;
                            return _cachedGameCrowClip;
                        }
                    }
                }

                // 3. Поиск среди всех загруженных AudioClip
                var allClips = Resources.FindObjectsOfTypeAll<AudioClip>();
                foreach (var clip in allClips)
                {
                    if (clip != null && (clip.name.IndexOf("crow", StringComparison.OrdinalIgnoreCase) >= 0 ||
                                         clip.name.IndexOf("raven", StringComparison.OrdinalIgnoreCase) >= 0))
                    {
                        _cachedGameCrowClip = clip;
                        return _cachedGameCrowClip;
                    }
                }
            }
            catch { }

            // 4. Безотказный синтезатор реалистичного карканья вороны (100% гарантия звука!)
            return SoundSynthesizer.GetCrowCawClip();
        }
    }

    // =========================================================================
    // АКТЕР ВОРОНА (ХАОТИЧНОЕ ДВИЖЕНИЕ И ПЛАВНОЕ ПИКИРОВАНИЕ В ТОЧКУ)
    // =========================================================================
    public class RavenActor : MonoBehaviour
    {
        private Player _target = null!;
        private float _angle;
        private float _radius;
        private float _baseHeight;
        private float _speed;
        private bool _isDiving = false;
        private Vector3 _diveTarget;
        private Vector3 _diveStartPos;
        private float _diveProgress = 0f;
        private float _wobbleSeed;

        public void Initialize(Player target, float startAngle, float radius, float height, float speed)
        {
            _target = target;
            _angle = startAngle;
            _radius = radius;
            _baseHeight = height;
            _speed = speed;
            _wobbleSeed = UnityEngine.Random.Range(0.5f, 3.5f);
        }

        public void DiveInto(Vector3 point)
        {
            _isDiving = true;
            _diveTarget = point;
            _diveStartPos = transform.position;
            _diveProgress = 0f;
        }

        private void Update()
        {
            if (_isDiving)
            {
                _diveProgress += Time.deltaTime * 0.42f;
                float t = Mathf.Clamp01(_diveProgress);
                float smoothT = t * t * (3f - 2f * t);
                Vector3 current = Vector3.Lerp(_diveStartPos, _diveTarget, smoothT);
                Vector3 look = _diveTarget - transform.position;
                if (look.sqrMagnitude > 0.01f)
                {
                    transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(look.normalized), Time.deltaTime * 7f);
                }
                transform.position = current;
                return;
            }

            if (_target == null) return;

            _angle += Time.deltaTime * _speed;
            Vector3 center = _target.transform.position;

            float dynamicRadius = _radius + Mathf.Sin(_angle * 2.2f + _wobbleSeed) * 4.5f;
            float dynamicHeight = _baseHeight + Mathf.Sin(_angle * 3.1f + _wobbleSeed) * 2.8f;

            Vector3 targetPos = new Vector3(
                center.x + Mathf.Cos(_angle) * dynamicRadius,
                center.y + dynamicHeight,
                center.z + Mathf.Sin(_angle) * dynamicRadius
            );

            Vector3 moveDir = (targetPos - transform.position);
            if (moveDir.sqrMagnitude > 0.01f)
            {
                transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(moveDir.normalized), Time.deltaTime * 6f);
            }
            transform.position = targetPos;
        }
    }

    // =========================================================================
    // ОДИН: ПАУЗА 2.8с -> ВЗМАХ 1.6с СО ЗВУКОМ ЗАРЯДКИ -> МОЛНИЯ СО ЗВУКОМ -> РАЗЛЕТ
    // =========================================================================
    public class OdinPrankController : MonoBehaviour
    {
        private Player _target = null!;
        private EffectList? _despawnEffects = null;
        private float _stateTimer = 0f;
        private int _phase = 0; 
        private Light _lightningLight = null!;
        private GameObject? _staffSparkGlow = null;
        private AudioSource _audio = null!;

        public void Initialize(Player target, EffectList? despawnFx)
        {
            _target = target;
            _despawnEffects = despawnFx;

            _audio = gameObject.AddComponent<AudioSource>();
            _audio.spatialBlend = 0.6f;
            _audio.minDistance = 6f;
            _audio.maxDistance = 100f;
            _audio.volume = 1f;

            if (_despawnEffects != null)
            {
                _despawnEffects.Create(transform.position, transform.rotation);
            }
        }

        private void Update()
        {
            _stateTimer += Time.deltaTime;

            if (_phase == 0)
            {
                if (_target != null)
                {
                    Vector3 lookDir = _target.transform.position - transform.position;
                    lookDir.y = 0f;
                    if (lookDir.sqrMagnitude > 0.01f)
                    {
                        transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(lookDir.normalized), Time.deltaTime * 5f);
                    }
                }

                if (_stateTimer >= 2.8f)
                {
                    _phase = 1;
                    _stateTimer = 0f;
                    PrepareStaffStrike();
                }
            }
            else if (_phase == 1)
            {
                transform.Rotate(Vector3.right, -4.5f * Time.deltaTime);

                if (_stateTimer >= 1.6f)
                {
                    _phase = 2;
                    _stateTimer = 0f;
                    if (_staffSparkGlow != null) Destroy(_staffSparkGlow);
                    ExecuteCinematicStrikeAndDisperse();
                }
            }
            else if (_phase == 2)
            {
                if (_lightningLight != null)
                {
                    _lightningLight.intensity = Mathf.MoveTowards(_lightningLight.intensity, 0f, Time.deltaTime * 35f);
                }

                if (_stateTimer >= 0.6f)
                {
                    if (_lightningLight != null) Destroy(_lightningLight.gameObject);
                    Destroy(gameObject);
                }
            }
        }

        private void PrepareStaffStrike()
        {
            _staffSparkGlow = new GameObject("StaffPlasmaGlow");
            _staffSparkGlow.transform.position = transform.position + transform.up * 1.8f + transform.right * 0.4f;
            _staffSparkGlow.transform.parent = transform;

            Light staffLight = _staffSparkGlow.AddComponent<Light>();
            staffLight.type = LightType.Point;
            staffLight.color = new Color(0.45f, 0.85f, 1.0f);
            staffLight.intensity = 10f;
            staffLight.range = 14f;

            AudioClip chargeClip = SoundSynthesizer.GetElectricChargeClip();
            _audio.PlayOneShot(chargeClip, 0.45f);
        }

        private void ExecuteCinematicStrikeAndDisperse()
        {
            Vector3 strikePos;
            if (_target != null)
            {
                strikePos = _target.transform.position + (_target.transform.forward * 2.5f);
            }
            else
            {
                strikePos = transform.position + transform.forward * 4f;
            }

            if (ZoneSystem.instance != null)
            {
                float gh = ZoneSystem.instance.GetGroundHeight(strikePos);
                if (gh > 0f) strikePos.y = gh;
            }

            // 1. Ветвистая 3D-молния
            GameObject lightningObj = new GameObject("ProceduralLightningBolt");
            ProceduralLightningBolt bolt = lightningObj.AddComponent<ProceduralLightningBolt>();
            bolt.Initialize(strikePos);

            // 2. Вспышка света
            GameObject lightObj = new GameObject("OdinSafeLightningFlash");
            lightObj.transform.position = strikePos + Vector3.up * 3f;
            _lightningLight = lightObj.AddComponent<Light>();
            _lightningLight.type = LightType.Point;
            _lightningLight.color = new Color(0.8f, 0.94f, 1.0f);
            _lightningLight.intensity = 25f;
            _lightningLight.range = 60f;

            // 3. Тряска экрана
            if (GameCamera.instance != null)
            {
                GameCamera.instance.AddShake(strikePos, 19f, 1.5f, true);
            }

            // 4. Безопасная ударная волна
            if (_target != null)
            {
                Vector3 pushDir = (_target.transform.position - strikePos).normalized;
                pushDir.y = 0.15f;
                _target.ApplyPushback(pushDir, 14f);
            }

            // 5. Звук молнии
            AudioClip lightningStrikeClip = SoundSynthesizer.GetFullLightningStrikeClip();
            _audio.PlayOneShot(lightningStrikeClip, 0.65f);

            TriggerGameThunderEffect(strikePos);

            // 6. Один исчезает в клубах черного дыма
            Vector3 odinPos = transform.position;
            if (_despawnEffects != null)
            {
                _despawnEffects.Create(odinPos, transform.rotation);
            }

            foreach (var r in GetComponentsInChildren<Renderer>())
            {
                if (r != null) r.enabled = false;
            }

            // 7. Веерный разлет 24 воронов
            SpawnExplodingCrowFlock(odinPos);
        }

        private void TriggerGameThunderEffect(Vector3 pos)
        {
            try
            {
                Thunder[] thunders = Resources.FindObjectsOfTypeAll<Thunder>();
                if (thunders != null && thunders.Length > 0 && thunders[0].m_thunderEffect != null)
                {
                    thunders[0].m_thunderEffect.Create(pos, Quaternion.identity);
                }
            }
            catch { }
        }

        private void SpawnExplodingCrowFlock(Vector3 originPos)
        {
            int flockCount = 24;

            for (int i = 0; i < flockCount; i++)
            {
                float angle = (float)i / flockCount * Mathf.PI * 2f + UnityEngine.Random.Range(-0.2f, 0.2f);
                Vector3 spreadDir = new Vector3(Mathf.Cos(angle), UnityEngine.Random.Range(0.25f, 0.75f), Mathf.Sin(angle)).normalized;

                GameObject? raven = HostPranksPlugin.CreatePureVisualCrow();
                if (raven == null) continue;

                raven.transform.position = originPos + Vector3.up * 1.5f;
                raven.transform.rotation = Quaternion.LookRotation(spreadDir);

                ScatteringRaven scattering = raven.AddComponent<ScatteringRaven>();
                scattering.Launch(spreadDir, UnityEngine.Random.Range(13f, 17f));
            }
        }
    }

    // =========================================================================
    // СИНТЕЗАТОР ЗВУКОВ МОЛНИИ И ЭЛЕКТРИЧЕСКОГО РАЗРЯДА
    // =========================================================================
    public static class SoundSynthesizer
    {
        private static AudioClip? _cachedChargeClip;
        private static AudioClip? _cachedStrikeClip;

        public static AudioClip GetElectricChargeClip()
        {
            if (_cachedChargeClip != null) return _cachedChargeClip;
            int sampleRate = 44100;
            float duration = 1.6f;
            int samples = (int)(sampleRate * duration);
            float[] data = new float[samples];
            System.Random rand = new System.Random(777);

            for (int i = 0; i < samples; i++)
            {
                float t = (float)i / sampleRate;
                float progress = t / duration;

                float freq = Mathf.Lerp(120f, 600f, progress * progress);
                float hum = Mathf.Sin(2f * Mathf.PI * freq * t) * 0.35f;
                float crackle = (float)(rand.NextDouble() * 2.0 - 1.0) * (rand.NextDouble() > (0.95 - progress * 0.35) ? 0.5f : 0.0f);

                data[i] = (hum + crackle) * progress * 0.45f;
            }

            _cachedChargeClip = AudioClip.Create("ElectricChargeUp", samples, 1, sampleRate, false);
            _cachedChargeClip.SetData(data, 0);
            return _cachedChargeClip;
        }

        public static AudioClip GetFullLightningStrikeClip()
        {
            if (_cachedStrikeClip != null) return _cachedStrikeClip;
            int sampleRate = 44100;
            float duration = 2.0f;
            int samples = (int)(sampleRate * duration);
            float[] data = new float[samples];
            System.Random rand = new System.Random(999);

            for (int i = 0; i < samples; i++)
            {
                float t = (float)i / sampleRate;

                float snap = (t < 0.07f) ? (float)(rand.NextDouble() * 2.0 - 1.0) * Mathf.Exp(-t * 60f) * 1.1f : 0f;
                float boom = (Mathf.Sin(2f * Mathf.PI * 48f * t) + Mathf.Sin(2f * Mathf.PI * 86f * t) * 0.5f) * Mathf.Exp(-t * 2.2f) * 0.65f;
                float sizzle = (float)(rand.NextDouble() * 2.0 - 1.0) * Mathf.Exp(-t * 5.5f) * 0.2f;

                data[i] = Mathf.Clamp((snap + boom + sizzle) * 0.75f, -1f, 1f);
            }

            _cachedStrikeClip = AudioClip.Create("FullLightningStrike", samples, 1, sampleRate, false);
            _cachedStrikeClip.SetData(data, 0);
            return _cachedStrikeClip;
        }

        private static AudioClip? _cachedCrowCawClip;

        public static AudioClip GetCrowCawClip()
        {
            if (_cachedCrowCawClip != null) return _cachedCrowCawClip;

            int sampleRate = 44100;
            float duration = 0.55f;
            int samples = (int)(sampleRate * duration);
            float[] data = new float[samples];
            System.Random rand = new System.Random(42);

            for (int i = 0; i < samples; i++)
            {
                float t = (float)i / sampleRate;
                float progress = t / duration;

                // Характерное воронье карканье: две основные хриплые форманты ~520Hz и ~1040Hz с амплитудной модуляцией (хрипотцой 45Hz)
                float baseFreq = Mathf.Lerp(620f, 480f, progress);
                float envelope = Mathf.Sin(progress * Mathf.PI);
                envelope = envelope * envelope; // плавная огибающая

                float caw1 = Mathf.Sin(2f * Mathf.PI * baseFreq * t);
                float caw2 = Mathf.Sin(2f * Mathf.PI * (baseFreq * 1.95f) * t) * 0.6f;
                float rasp = Mathf.Sin(2f * Mathf.PI * 48f * t) * 0.5f + 0.5f; // хриплый горловой тон
                float noise = (float)(rand.NextDouble() * 2.0 - 1.0) * 0.25f;

                data[i] = Mathf.Clamp((caw1 + caw2 + noise) * rasp * envelope * 0.85f, -1f, 1f);
            }

            _cachedCrowCawClip = AudioClip.Create("ProceduralCrowCaw", samples, 1, sampleRate, false);
            _cachedCrowCawClip.SetData(data, 0);
            return _cachedCrowCawClip;
        }
    }

    // =========================================================================
    // ВОРОН, РАЗЛЕТАЮЩИЙСЯ В ДАЛЬ ПРИ ИСЧЕЗНОВЕНИИ ОДИНА
    // =========================================================================
    public class ScatteringRaven : MonoBehaviour
    {
        private Vector3 _flyDirection;
        private float _speed;
        private float _timeAlive = 0f;
        private const float MaxLifetime = 5.5f;

        public void Launch(Vector3 direction, float speed)
        {
            _flyDirection = direction;
            _speed = speed;
            transform.rotation = Quaternion.LookRotation(_flyDirection);
        }

        private void Update()
        {
            _timeAlive += Time.deltaTime;
            transform.position += _flyDirection * _speed * Time.deltaTime;

            if (_timeAlive >= MaxLifetime)
            {
                UnityEngine.Object.Destroy(gameObject);
            }
        }
    }

    // =========================================================================
    // ПРОЦЕДУРНЫЙ 3D-РАЗРЯД МОЛНИИ С ВЕТВЛЕНИЕМ И МЕРЦАНИЕМ (0 УРОНА)
    // =========================================================================
    public class ProceduralLightningBolt : MonoBehaviour
    {
        private LineRenderer _mainLine = null!;
        private readonly List<LineRenderer> _branches = new List<LineRenderer>();
        private Vector3 _startPos;
        private Vector3 _endPos;
        private float _timeAlive = 0f;
        private float _duration = 0.45f;
        private float _flickerTimer = 0f;
        private const int SegmentCount = 20;

        public void Initialize(Vector3 groundStrikePos)
        {
            _endPos = groundStrikePos;
            _startPos = groundStrikePos + Vector3.up * 58f + UnityEngine.Random.insideUnitSphere * 6f;
            _startPos.y = groundStrikePos.y + 58f;

            Shader shader = Shader.Find("Particles/Standard Unlit")
                         ?? Shader.Find("Sprites/Default")
                         ?? Shader.Find("Unlit/Color");
            Material boltMaterial = new Material(shader);

            _mainLine = gameObject.AddComponent<LineRenderer>();
            _mainLine.material = boltMaterial;
            _mainLine.startColor = new Color(0.95f, 0.98f, 1.0f, 1.0f);
            _mainLine.endColor = new Color(0.6f, 0.85f, 1.0f, 1.0f);
            _mainLine.startWidth = 0.7f;
            _mainLine.endWidth = 0.35f;
            _mainLine.positionCount = SegmentCount;

            for (int i = 0; i < 2; i++)
            {
                GameObject branchObj = new GameObject($"Branch_{i}");
                branchObj.transform.parent = transform;
                LineRenderer branchLine = branchObj.AddComponent<LineRenderer>();
                branchLine.material = boltMaterial;
                branchLine.startColor = new Color(0.7f, 0.9f, 1.0f, 0.9f);
                branchLine.endColor = new Color(0.4f, 0.7f, 1.0f, 0.0f);
                branchLine.startWidth = 0.35f;
                branchLine.endWidth = 0.08f;
                branchLine.positionCount = 8;
                _branches.Add(branchLine);
            }

            RegenerateBoltGeometry();
            SpawnGroundSparks(groundStrikePos);
        }

        private void Update()
        {
            _timeAlive += Time.deltaTime;
            _flickerTimer += Time.deltaTime;

            if (_flickerTimer >= 0.05f)
            {
                _flickerTimer = 0f;
                RegenerateBoltGeometry();
            }

            float progress = _timeAlive / _duration;
            float fadeWidth = Mathf.Lerp(1f, 0f, progress * progress);
            _mainLine.widthMultiplier = fadeWidth;
            foreach (var b in _branches)
            {
                if (b != null) b.widthMultiplier = fadeWidth;
            }

            if (_timeAlive >= _duration)
            {
                Destroy(gameObject);
            }
        }

        private void RegenerateBoltGeometry()
        {
            Vector3 diff = _endPos - _startPos;
            Vector3 sideAxis1 = Vector3.Cross(diff.normalized, Vector3.up).normalized;
            if (sideAxis1.sqrMagnitude < 0.01f) sideAxis1 = Vector3.right;
            Vector3 sideAxis2 = Vector3.Cross(diff.normalized, sideAxis1).normalized;

            Vector3[] mainPoints = new Vector3[SegmentCount];
            mainPoints[0] = _startPos;
            mainPoints[SegmentCount - 1] = _endPos;

            for (int i = 1; i < SegmentCount - 1; i++)
            {
                float t = (float)i / (SegmentCount - 1);
                Vector3 basePoint = Vector3.Lerp(_startPos, _endPos, t);
                float roughness = Mathf.Sin(t * Mathf.PI) * 2.8f;
                float offset1 = UnityEngine.Random.Range(-roughness, roughness);
                float offset2 = UnityEngine.Random.Range(-roughness, roughness);
                mainPoints[i] = basePoint + sideAxis1 * offset1 + sideAxis2 * offset2;
            }

            _mainLine.SetPositions(mainPoints);

            int branchIdx = 0;
            foreach (var branch in _branches)
            {
                if (branch == null) continue;

                int forkStartIdx = (branchIdx == 0) ? 6 : 12;
                Vector3 branchStart = mainPoints[Mathf.Clamp(forkStartIdx, 0, SegmentCount - 1)];
                Vector3 branchDir = (sideAxis1 * (branchIdx == 0 ? 1f : -1f) + Vector3.down * 0.8f + sideAxis2 * UnityEngine.Random.Range(-0.5f, 0.5f)).normalized;

                Vector3[] bPoints = new Vector3[8];
                bPoints[0] = branchStart;
                Vector3 currentPos = branchStart;

                for (int j = 1; j < 8; j++)
                {
                    currentPos += branchDir * UnityEngine.Random.Range(1.5f, 2.8f) + UnityEngine.Random.insideUnitSphere * 0.8f;
                    bPoints[j] = currentPos;
                }

                branch.SetPositions(bPoints);
                branchIdx++;
            }
        }

        private void SpawnGroundSparks(Vector3 groundPos)
        {
            GameObject sparkObj = new GameObject("GroundSparks");
            sparkObj.transform.position = groundPos + Vector3.up * 0.2f;

            ParticleSystem ps = sparkObj.AddComponent<ParticleSystem>();
            var main = ps.main;
            main.duration = 0.5f;
            main.startLifetime = 0.6f;
            main.startSpeed = 15f;
            main.startSize = 0.16f;
            main.startColor = new Color(0.8f, 0.95f, 1f, 1f);
            main.gravityModifier = 2.5f;
            main.loop = false;

            var emission = ps.emission;
            emission.rateOverTime = 0f;
            emission.SetBursts(new ParticleSystem.Burst[] { new ParticleSystem.Burst(0f, 45) });

            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Hemisphere;
            shape.radius = 0.5f;

            var renderer = sparkObj.GetComponent<ParticleSystemRenderer>();
            renderer.renderMode = ParticleSystemRenderMode.Billboard;

            ps.Play();
            Destroy(sparkObj, 1.2f);
        }
    }
}
