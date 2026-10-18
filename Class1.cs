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
        public const string ModVersion = "1.0.6";

        public static HostPranksPlugin Instance { get; private set; } = null!;

        private ConfigEntry<KeyCode> _toggleKeyConfig = null!;
        private bool _isGuiOpen = false;
        private Rect _windowRect = new Rect(40, 40, 420, 440);
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
            GUI.DragWindow(new Rect(0, 0, 380, 25));

            GUILayout.Space(5);
            GUILayout.BeginHorizontal();
            GUILayout.Label("<color=#FFD700><b>Панель управления пранками (Только Хост)</b></color>");
            if (GUILayout.Button("✕", GUILayout.Width(25), GUILayout.Height(20)))
            {
                _isGuiOpen = false;
            }
            GUILayout.EndHorizontal();

            GUILayout.Label("<color=#AAAAAA>Нажмите кнопку напротив игрока для вызова Одина:</color>");
            GUILayout.Space(5);

            List<Player> allPlayers = Player.GetAllPlayers();
            if (allPlayers == null || allPlayers.Count == 0)
            {
                GUILayout.Label("<color=#FF6666>Игроки не найдены в мире.</color>");
            }
            else
            {
                _scrollPos = GUILayout.BeginScrollView(_scrollPos, GUILayout.Height(280));

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

                    // Кнопка: Кинематографичное явление Одина
                    if (GUILayout.Button("⚡ Явление Одина (Вороны + Молния)", GUILayout.Height(28)))
                    {
                        TriggerOdinCinemaPrank(player);
                    }

                    GUILayout.EndHorizontal();
                    GUILayout.EndVertical();
                    GUILayout.Space(4);
                }

                GUILayout.EndScrollView();
            }

            GUILayout.Space(6);
            GUILayout.Label($"<color=#E0E0E0>Статус:</color> <color=#FFFF66>{_statusMessage}</color>");
        }

        public void SetStatus(string message)
        {
            _statusMessage = message;
            _statusTimer = 5f;
            Logger.LogInfo(message);
        }

        // ==========================================
        // КИНЕМАТОГРАФИЧНОЕ ЯВЛЕНИЕ ОДИНА
        // ==========================================
        private void TriggerOdinCinemaPrank(Player target)
        {
            if (target == null) return;

            GameObject? odinPrefab = FindOdinPrefab();
            if (odinPrefab == null)
            {
                SetStatus("Ошибка: Модель Одина не найдена в ресурсах.");
                return;
            }

            // Точка схождения и появления: 10 метров перед лицом игрока
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

            // Запускаем режиссерский контроллер
            GameObject directorObj = new GameObject("OdinCinemaDirector");
            OdinCinemaDirector director = directorObj.AddComponent<OdinCinemaDirector>();
            director.StartRitual(target, summonPos, odinPrefab);

            SetStatus($"🦅 Стая черных воронов кружит вокруг '{target.GetPlayerName()}'...");
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

        public static GameObject? FindCrowPrefab()
        {
            if (ZNetScene.instance != null)
            {
                var p = ZNetScene.instance.GetPrefab("Crow") ?? ZNetScene.instance.GetPrefab("crow") ?? ZNetScene.instance.GetPrefab("Seagal");
                if (p != null) return p;

                if (ZNetScene.instance.m_prefabs != null)
                {
                    foreach (var go in ZNetScene.instance.m_prefabs)
                    {
                        if (go != null && (go.GetComponent<RandomFlyingBird>() != null || go.name.Equals("Crow", StringComparison.OrdinalIgnoreCase)))
                        {
                            return go;
                        }
                    }
                }
            }
            return null;
        }
    }

    // =========================================================================
    // РЕЖИССЕРСКИЙ КОНТРОЛЛЕР: ХАОТИЧНАЯ СТАЯ ВОРОН -> СХОЖДЕНИЕ В ТОЧКУ -> ОДИН
    // =========================================================================
    public class OdinCinemaDirector : MonoBehaviour
    {
        private Player _target = null!;
        private Vector3 _summonPos;
        private GameObject _odinPrefab = null!;
        private readonly List<RavenActor> _ravens = new List<RavenActor>();
        private float _timer = 0f;
        private int _stage = 0; // 0: Хаотичное кружение (5.0с), 1: Пикирование в точку (1.2с), 2: Спавн Одина

        public void StartRitual(Player target, Vector3 summonPos, GameObject odinPrefab)
        {
            _target = target;
            _summonPos = summonPos;
            _odinPrefab = odinPrefab;

            GameObject? crowPrefab = HostPranksPlugin.FindCrowPrefab();

            // Создаем широкую стаю из 12 черных воронов на дистанции 32-38 метров и высоте 8-16 метров
            int birdCount = 12;
            for (int i = 0; i < birdCount; i++)
            {
                float angle = (float)i / birdCount * Mathf.PI * 2f;
                float dist = UnityEngine.Random.Range(30f, 38f);
                float height = UnityEngine.Random.Range(8f, 17f);
                float orbitSpeed = UnityEngine.Random.Range(1.2f, 2.2f) * (i % 2 == 0 ? 1f : -1f); // Часть летит по часовой, часть против!

                Vector3 startPos = _target.transform.position + new Vector3(Mathf.Cos(angle) * dist, height, Mathf.Sin(angle) * dist);

                GameObject birdObj;
                if (crowPrefab != null)
                {
                    birdObj = UnityEngine.Object.Instantiate(crowPrefab, startPos, Quaternion.identity);
                    var birdAi = birdObj.GetComponent<RandomFlyingBird>();
                    if (birdAi != null) UnityEngine.Object.Destroy(birdAi);
                    var zview = birdObj.GetComponent<ZNetView>();
                    if (zview != null) zview.enabled = false;
                }
                else
                {
                    birdObj = CreateProceduralRavenVisual();
                    birdObj.transform.position = startPos;
                }

                foreach (var renderer in birdObj.GetComponentsInChildren<Renderer>(true))
                {
                    if (renderer != null && renderer.material != null)
                    {
                        renderer.material.color = new Color(0.06f, 0.06f, 0.06f, 1.0f);
                    }
                }

                RavenActor actor = birdObj.AddComponent<RavenActor>();
                actor.Initialize(_target, angle, dist, height, orbitSpeed);
                _ravens.Add(actor);
            }

            // Нагнетание звука стаи
            AudioSource audio = gameObject.AddComponent<AudioSource>();
            audio.spatialBlend = 0.5f;
            audio.minDistance = 8f;
            audio.maxDistance = 80f;
            audio.volume = 1f;
            AudioClip? crowClip = FindAudio("sfx_crow_idle") ?? FindAudio("sfx_bird_flight");
            if (crowClip != null) audio.PlayOneShot(crowClip);
        }

        private void Update()
        {
            if (_target == null)
            {
                CleanupRavens();
                Destroy(gameObject);
                return;
            }

            _timer += Time.deltaTime;

            if (_stage == 0)
            {
                // Стая долго и тревожно кружит вокруг викинга (5.0 секунд)
                if (_timer >= 5.0f)
                {
                    _stage = 1;
                    _timer = 0f;
                    // Все вороны синхронно пикируют в точку призыва Одина
                    foreach (var r in _ravens)
                    {
                        if (r != null) r.DiveInto(_summonPos);
                    }
                }
            }
            else if (_stage == 1)
            {
                // Вороны стремительно сходятся в точку ровно за 1.15 секунды
                if (_timer >= 1.15f)
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
                if (r != null) Destroy(r.gameObject);
            }
            _ravens.Clear();
        }

        private GameObject CreateProceduralRavenVisual()
        {
            GameObject bird = GameObject.CreatePrimitive(PrimitiveType.Cube);
            bird.name = "ProceduralRaven";
            bird.transform.localScale = new Vector3(0.35f, 0.08f, 0.6f);
            var col = bird.GetComponent<Collider>();
            if (col != null) Destroy(col);
            return bird;
        }

        private AudioClip? FindAudio(string name)
        {
            if (ZNetScene.instance == null) return null;
            GameObject p = ZNetScene.instance.GetPrefab(name);
            if (p != null)
            {
                var a = p.GetComponentInChildren<AudioSource>(true);
                if (a != null && a.clip != null) return a.clip;
                var zsfx = p.GetComponentInChildren<ZSFX>(true);
                if (zsfx != null && zsfx.m_audioClips != null && zsfx.m_audioClips.Length > 0) return zsfx.m_audioClips[0];
            }
            return null;
        }
    }

    // =========================================================================
    // АКТЕР ВОРОНА (ХАОТИЧНОЕ ДВИЖЕНИЕ И ПИКИРОВАНИЕ В ТОЧКУ)
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
            _wobbleSeed = UnityEngine.Random.Range(0.5f, 2.5f);
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
                _diveProgress += Time.deltaTime * 0.92f;
                float t = Mathf.Clamp01(_diveProgress);
                Vector3 current = Vector3.Lerp(_diveStartPos, _diveTarget, t * t);
                Vector3 look = _diveTarget - transform.position;
                if (look.sqrMagnitude > 0.01f)
                {
                    transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(look.normalized), Time.deltaTime * 12f);
                }
                transform.position = current;
                return;
            }

            if (_target == null) return;

            // Хаотичное кружение с переменным радиусом и волнообразным взмахом высоты
            _angle += Time.deltaTime * _speed;
            Vector3 center = _target.transform.position;

            float dynamicRadius = _radius + Mathf.Sin(_angle * 2.5f + _wobbleSeed) * 3.5f;
            float dynamicHeight = _baseHeight + Mathf.Sin(_angle * 3.8f + _wobbleSeed) * 2.2f;

            Vector3 targetPos = new Vector3(
                center.x + Mathf.Cos(_angle) * dynamicRadius,
                center.y + dynamicHeight,
                center.z + Mathf.Sin(_angle) * dynamicRadius
            );

            Vector3 moveDir = (targetPos - transform.position);
            if (moveDir.sqrMagnitude > 0.01f)
            {
                transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(moveDir.normalized), Time.deltaTime * 7f);
            }
            transform.position = targetPos;
        }
    }

    // =========================================================================
    // ОДИН: ВЗМАХ ПОСОХОМ -> МОЛНИЯ -> ИСЧЕЗНОВЕНИЕ И ВЕЕРНЫЙ РАЗЛЕТ ВОРОНОВ
    // =========================================================================
    public class OdinPrankController : MonoBehaviour
    {
        private Player _target = null!;
        private EffectList? _despawnEffects = null;
        private float _stateTimer = 0f;
        private int _phase = 0; 
        private Light _lightningLight = null!;
        private GameObject? _staffSparkGlow = null;

        public void Initialize(Player target, EffectList? despawnFx)
        {
            _target = target;
            _despawnEffects = despawnFx;

            // Начальный взрыв дыма при появлении из стаи
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
                // Фаза 0 (0 to 1.5s): Стоит, поворачиваясь лицом к викингу
                if (_target != null)
                {
                    Vector3 lookDir = _target.transform.position - transform.position;
                    lookDir.y = 0f;
                    if (lookDir.sqrMagnitude > 0.01f)
                    {
                        transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(lookDir.normalized), Time.deltaTime * 6f);
                    }
                }

                if (_stateTimer >= 1.5f)
                {
                    _phase = 1; // Замах посохом
                    _stateTimer = 0f;
                    PrepareStaffStrike();
                }
            }
            else if (_phase == 1)
            {
                // Фаза 1 (1.5 to 2.3s): Вскидывает посох к небесам
                transform.Rotate(Vector3.right, -8f * Time.deltaTime);

                if (_stateTimer >= 0.75f)
                {
                    _phase = 2; // УДАР МОЛНИИ И РАСПАД НА СТАЮ ВОРОНОВ!
                    _stateTimer = 0f;
                    if (_staffSparkGlow != null) Destroy(_staffSparkGlow);
                    ExecuteCinematicStrikeAndDisperse();
                }
            }
            else if (_phase == 2)
            {
                // Фаза 2: Вспышка затухает, Один уже исчез, контроллер удаляет свет и объект
                if (_lightningLight != null)
                {
                    _lightningLight.intensity = Mathf.MoveTowards(_lightningLight.intensity, 0f, Time.deltaTime * 40f);
                }

                if (_stateTimer >= 0.5f)
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
            staffLight.color = new Color(0.4f, 0.8f, 1.0f);
            staffLight.intensity = 8f;
            staffLight.range = 10f;
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

            // 1. Ветвистая 3D-молния с небес
            GameObject lightningObj = new GameObject("ProceduralLightningBolt");
            ProceduralLightningBolt bolt = lightningObj.AddComponent<ProceduralLightningBolt>();
            bolt.Initialize(strikePos);

            // 2. Вспышка света
            GameObject lightObj = new GameObject("OdinSafeLightningFlash");
            lightObj.transform.position = strikePos + Vector3.up * 3f;
            _lightningLight = lightObj.AddComponent<Light>();
            _lightningLight.type = LightType.Point;
            _lightningLight.color = new Color(0.75f, 0.92f, 1.0f);
            _lightningLight.intensity = 22f;
            _lightningLight.range = 55f;

            // 3. Тряска камеры
            if (GameCamera.instance != null)
            {
                GameCamera.instance.AddShake(strikePos, 18f, 1.4f, true);
            }

            // 4. Безопасная ударная волна (отбрасывает игрока назад на 2 метра)
            if (_target != null)
            {
                Vector3 pushDir = (_target.transform.position - strikePos).normalized;
                pushDir.y = 0.15f;
                _target.ApplyPushback(pushDir, 14f);
            }

            // 5. Гром
            AudioSource audio = gameObject.AddComponent<AudioSource>();
            audio.spatialBlend = 0.6f;
            audio.minDistance = 6f;
            audio.maxDistance = 140f;
            audio.volume = 1f;
            AudioClip? thunderClip = FindThunderClip();
            if (thunderClip != null) audio.PlayOneShot(thunderClip);

            // 6. ОДИН ИСЧЕЗАЕТ В КЛУБАХ ЧЕРНОГО ДЫМА
            Vector3 odinPos = transform.position;
            if (_despawnEffects != null)
            {
                _despawnEffects.Create(odinPos, transform.rotation);
            }

            // Скрываем все рендереры Одина немедленно
            foreach (var r in GetComponentsInChildren<Renderer>())
            {
                if (r != null) r.enabled = false;
            }

            // 7. ИЗ ТОЧКИ ОДИНА ВЕЕРОМ ВО ВСЕ СТОРОНЫ РАЗЛЕТАЮТСЯ ВОРОНЫ («КТО КУДА ГЛЯДЯТ»)
            SpawnExplodingCrowFlock(odinPos);
        }

        private void SpawnExplodingCrowFlock(Vector3 originPos)
        {
            GameObject? crowPrefab = HostPranksPlugin.FindCrowPrefab();
            int flockCount = 10;

            for (int i = 0; i < flockCount; i++)
            {
                float angle = (float)i / flockCount * Mathf.PI * 2f + UnityEngine.Random.Range(-0.25f, 0.25f);
                Vector3 spreadDir = new Vector3(Mathf.Cos(angle), UnityEngine.Random.Range(0.35f, 0.9f), Mathf.Sin(angle)).normalized;

                GameObject raven;
                if (crowPrefab != null)
                {
                    raven = UnityEngine.Object.Instantiate(crowPrefab, originPos + Vector3.up * 1.5f, Quaternion.LookRotation(spreadDir));
                    var ai = raven.GetComponent<RandomFlyingBird>();
                    if (ai != null) Destroy(ai);
                    var zview = raven.GetComponent<ZNetView>();
                    if (zview != null) zview.enabled = false;
                }
                else
                {
                    raven = GameObject.CreatePrimitive(PrimitiveType.Cube);
                    raven.transform.position = originPos + Vector3.up * 1.5f;
                    raven.transform.localScale = new Vector3(0.35f, 0.08f, 0.6f);
                    var col = raven.GetComponent<Collider>();
                    if (col != null) Destroy(col);
                }

                foreach (var r in raven.GetComponentsInChildren<Renderer>(true))
                {
                    if (r != null && r.material != null) r.material.color = new Color(0.06f, 0.06f, 0.06f, 1.0f);
                }

                // Навешиваем скрипт скоростного веерного разлета в небо
                ScatteringRaven scattering = raven.AddComponent<ScatteringRaven>();
                scattering.Launch(spreadDir, UnityEngine.Random.Range(22f, 32f));
            }
        }

        private AudioClip? FindThunderClip()
        {
            if (ZNetScene.instance == null) return null;
            string[] searchPrefabs = new string[] { "vfx_eikthyr_lightning", "vfx_lightning", "sfx_thunder", "Thunder" };
            foreach (var name in searchPrefabs)
            {
                GameObject p = ZNetScene.instance.GetPrefab(name);
                if (p != null)
                {
                    var a = p.GetComponentInChildren<AudioSource>(true);
                    if (a != null && a.clip != null) return a.clip;
                    var zsfx = p.GetComponentInChildren<ZSFX>(true);
                    if (zsfx != null && zsfx.m_audioClips != null && zsfx.m_audioClips.Length > 0)
                    {
                        return zsfx.m_audioClips[0];
                    }
                }
            }
            return null;
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
        private const float MaxLifetime = 4.0f;

        public void Launch(Vector3 direction, float speed)
        {
            _flyDirection = direction;
            _speed = speed;
            transform.rotation = Quaternion.LookRotation(_flyDirection);
        }

        private void Update()
        {
            _timeAlive += Time.deltaTime;
            // Полет вдаль с легким взлетом вверх
            transform.position += _flyDirection * _speed * Time.deltaTime;

            if (_timeAlive >= MaxLifetime)
            {
                Destroy(gameObject);
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
        private float _duration = 0.4f;
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
            _mainLine.startWidth = 0.65f;
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
