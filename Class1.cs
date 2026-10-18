using System;
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
        public const string ModVersion = "1.0.4";

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
                // Меню доступно только в игре и только хосту
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

                    // Кнопка: Кара Одина
                    if (GUILayout.Button("⚡ Кара Одина (Эпичная молния)", GUILayout.Height(28)))
                    {
                        TriggerOdinPrank(player);
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
        // ПРАНК: КАРА ОДИНА
        // ==========================================
        private void TriggerOdinPrank(Player target)
        {
            if (target == null) return;

            GameObject? odinPrefab = FindOdinPrefab();
            if (odinPrefab == null)
            {
                SetStatus("Ошибка: Модель Одина не найдена в ресурсах.");
                return;
            }

            // Позиция спавна: 10 метров перед лицом игрока
            Vector3 forward = target.transform.forward;
            forward.y = 0f;
            if (forward.sqrMagnitude < 0.01f) forward = Vector3.forward;
            forward.Normalize();

            Vector3 spawnPos = target.transform.position + forward * 10f;
            if (ZoneSystem.instance != null)
            {
                float groundHeight = ZoneSystem.instance.GetGroundHeight(spawnPos);
                if (groundHeight > 0f) spawnPos.y = groundHeight;
            }

            Quaternion rot = Quaternion.LookRotation((target.transform.position - spawnPos).normalized);

            // Создаем экземпляр Одина
            GameObject odinInstance = UnityEngine.Object.Instantiate(odinPrefab, spawnPos, rot);
            odinInstance.SetActive(true);

            // Удаляем оригинальный скрипт самоуничтожения Одина, чтобы он не исчезал при приближении
            Odin vanillaOdin = odinInstance.GetComponent<Odin>();
            EffectList? despawnEffects = null;
            if (vanillaOdin != null)
            {
                despawnEffects = vanillaOdin.m_despawn;
                UnityEngine.Object.Destroy(vanillaOdin);
            }

            // Навешиваем контроллер кастомного безопасного шоу
            OdinPrankController controller = odinInstance.AddComponent<OdinPrankController>();
            controller.Initialize(target, despawnEffects);

            SetStatus($"⚡ Один явился перед '{target.GetPlayerName()}' на дистанции 10м!");
        }

        private GameObject? FindOdinPrefab()
        {
            // 1. Поиск в ZNetScene
            if (ZNetScene.instance != null)
            {
                var p = ZNetScene.instance.GetPrefab("Odin")
                     ?? ZNetScene.instance.GetPrefab("odin");
                if (p != null) return p;

                if (ZNetScene.instance.m_prefabs != null)
                {
                    foreach (var go in ZNetScene.instance.m_prefabs)
                    {
                        if (go != null)
                        {
                            if (go.GetComponent<Odin>() != null || go.name.Equals("Odin", StringComparison.OrdinalIgnoreCase))
                            {
                                return go;
                            }
                        }
                    }
                }

                if (ZNetScene.instance.m_nonNetViewPrefabs != null)
                {
                    foreach (var go in ZNetScene.instance.m_nonNetViewPrefabs)
                    {
                        if (go != null)
                        {
                            if (go.GetComponent<Odin>() != null || go.name.Equals("Odin", StringComparison.OrdinalIgnoreCase))
                            {
                                return go;
                            }
                        }
                    }
                }
            }

            // 2. Поиск в активных SpawnSystem
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

            // 3. Поиск по всем загруженным объектам Unity в памяти
            try
            {
                var allOdins = Resources.FindObjectsOfTypeAll<Odin>();
                if (allOdins != null && allOdins.Length > 0)
                {
                    return allOdins[0].gameObject;
                }

                var allGos = Resources.FindObjectsOfTypeAll<GameObject>();
                foreach (var go in allGos)
                {
                    if (go != null && (go.GetComponent<Odin>() != null || go.name.Equals("Odin", StringComparison.OrdinalIgnoreCase)))
                    {
                        return go;
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.LogWarning($"Ошибка при поиске Одина в Resources: {ex.Message}");
            }

            return null;
        }
    }

    // =========================================================================
    // КОНТРОЛЛЕР: КАРА ОДИНА
    // =========================================================================
    public class OdinPrankController : MonoBehaviour
    {
        private Player _target = null!;
        private EffectList? _despawnEffects = null;
        private float _stateTimer = 0f;
        private int _phase = 0; // 0: Стоит и смотрит, 1: Молния, 2: Взлет, 3: Исчезновение
        private Vector3 _flightDirection = Vector3.up;
        private float _flightSpeed = 10f;
        private Light _lightningLight = null!;

        public void Initialize(Player target, EffectList? despawnFx)
        {
            _target = target;
            _despawnEffects = despawnFx;

            // Стартовый дым появления
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
                // Фаза 0: Стоит 2 секунды, разворачиваясь лицом к игроку
                if (_target != null)
                {
                    Vector3 lookDir = _target.transform.position - transform.position;
                    lookDir.y = 0f;
                    if (lookDir.sqrMagnitude > 0.01f)
                    {
                        transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(lookDir.normalized), Time.deltaTime * 6f);
                    }
                }

                if (_stateTimer >= 2.0f)
                {
                    _phase = 1;
                    _stateTimer = 0f;
                    ExecuteSafeLightningStrike();
                }
            }
            else if (_phase == 1)
            {
                // Фаза 1: Вспышка света затухает (0.4 сек)
                if (_lightningLight != null)
                {
                    _lightningLight.intensity = Mathf.MoveTowards(_lightningLight.intensity, 0f, Time.deltaTime * 35f);
                }

                if (_stateTimer >= 0.45f)
                {
                    _phase = 2;
                    _stateTimer = 0f;
                    if (_lightningLight != null) Destroy(_lightningLight.gameObject);
                }
            }
            else if (_phase == 2)
            {
                // Фаза 2: Мощный взлет в стратосферу с ускорением!
                _flightSpeed += Time.deltaTime * 50f;
                transform.position += _flightDirection * _flightSpeed * Time.deltaTime;

                // Небольшой наклон силуэта назад в полете
                transform.Rotate(Vector3.right, -12f * Time.deltaTime);

                if (_stateTimer >= 2.5f || transform.position.y > 110f)
                {
                    _phase = 3;
                    // Финальный хлопок дыма в облаках
                    if (_despawnEffects != null)
                    {
                        _despawnEffects.Create(transform.position, transform.rotation);
                    }
                    Destroy(gameObject);
                }
            }
        }

        private void ExecuteSafeLightningStrike()
        {
            // Точка удара молнии: на земле прямо перед игроком (в 2.5 метрах перед его взором)
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

            // 1. Создаем эффектный процедурный 3D-разряд молнии из облаков (55м) прямо в землю!
            GameObject lightningObj = new GameObject("ProceduralLightningBolt");
            ProceduralLightningBolt bolt = lightningObj.AddComponent<ProceduralLightningBolt>();
            bolt.Initialize(strikePos);

            // 2. Ослепительная сине-белая вспышка света (без урона!)
            GameObject lightObj = new GameObject("OdinSafeLightningFlash");
            lightObj.transform.position = strikePos + Vector3.up * 3f;
            _lightningLight = lightObj.AddComponent<Light>();
            _lightningLight.type = LightType.Point;
            _lightningLight.color = new Color(0.75f, 0.92f, 1.0f);
            _lightningLight.intensity = 20f;
            _lightningLight.range = 50f;

            // 3. Сильная тряска экрана у игрока (эффект кинематографичной ударной волны)
            if (GameCamera.instance != null)
            {
                GameCamera.instance.AddShake(strikePos, 18f, 1.3f, true);
            }

            // 4. Громкий звук удара грома
            AudioSource audio = gameObject.AddComponent<AudioSource>();
            audio.spatialBlend = 0.6f;
            audio.minDistance = 6f;
            audio.maxDistance = 140f;
            audio.volume = 1f;

            AudioClip thunderClip = FindThunderClip();
            if (thunderClip != null)
            {
                audio.PlayOneShot(thunderClip);
            }
        }

        private AudioClip FindThunderClip()
        {
            if (ZNetScene.instance == null) return null!;
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
            return null!;
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
        private float _duration = 0.4f; // Длительность видимости разряда
        private float _flickerTimer = 0f;
        private const int SegmentCount = 20;

        public void Initialize(Vector3 groundStrikePos)
        {
            _endPos = groundStrikePos;
            // Исходная точка в небесах: 55-60 метров над землей со случайным смещением
            _startPos = groundStrikePos + Vector3.up * 58f + UnityEngine.Random.insideUnitSphere * 6f;
            _startPos.y = groundStrikePos.y + 58f;

            // Находим или создаем светящийся шейдер
            Shader shader = Shader.Find("Particles/Standard Unlit")
                         ?? Shader.Find("Sprites/Default")
                         ?? Shader.Find("Unlit/Color");
            Material boltMaterial = new Material(shader);

            // Основной ствол молнии
            _mainLine = gameObject.AddComponent<LineRenderer>();
            _mainLine.material = boltMaterial;
            _mainLine.startColor = new Color(0.95f, 0.98f, 1.0f, 1.0f);
            _mainLine.endColor = new Color(0.6f, 0.85f, 1.0f, 1.0f);
            _mainLine.startWidth = 0.6f;
            _mainLine.endWidth = 0.35f;
            _mainLine.positionCount = SegmentCount;

            // 2 боковых ветвления (вилки молнии)
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

            // Генерируем первоначальный зигзаг
            RegenerateBoltGeometry();

            // Создаем искры от удара о землю
            SpawnGroundSparks(groundStrikePos);
        }

        private void Update()
        {
            _timeAlive += Time.deltaTime;
            _flickerTimer += Time.deltaTime;

            // Реалистичный стробоскоп/мерцание молнии каждые 0.05 сек
            if (_flickerTimer >= 0.05f)
            {
                _flickerTimer = 0f;
                RegenerateBoltGeometry();
            }

            // Затухание толщины к концу жизни
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
            // Строим зигзагообразный основной ствол
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

                // Случайный излом в стороны
                float roughness = Mathf.Sin(t * Mathf.PI) * 2.8f;
                float offset1 = UnityEngine.Random.Range(-roughness, roughness);
                float offset2 = UnityEngine.Random.Range(-roughness, roughness);

                mainPoints[i] = basePoint + sideAxis1 * offset1 + sideAxis2 * offset2;
            }

            _mainLine.SetPositions(mainPoints);

            // Строим боковые ветки
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
            // Создаем яркие искры в точке удара о землю
            GameObject sparkObj = new GameObject("GroundSparks");
            sparkObj.transform.position = groundPos + Vector3.up * 0.2f;

            ParticleSystem ps = sparkObj.AddComponent<ParticleSystem>();
            var main = ps.main;
            main.duration = 0.5f;
            main.startLifetime = 0.6f;
            main.startSpeed = 14f;
            main.startSize = 0.15f;
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
