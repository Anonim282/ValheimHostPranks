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
        public const string ModVersion = "1.0.3";

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
                    if (GUILayout.Button("⚡ Кара Одина (10м, 0 урона)", GUILayout.Height(28)))
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

            // Позиция спавна: 10 метров перед лицом игрока (красивая панорамная дистанция)
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

            SetStatus($"⚡ Один ({odinPrefab.name}) явился перед '{target.GetPlayerName()}' на дистанции 10м!");
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
    // КОНТРОЛЛЕР: КАРА ОДИНА (ПОЯВЛЕНИЕ 10м -> МОЛНИЯ 0 УРОНА -> ВЗЛЕТ В НЕБЕСА)
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
                // Фаза 1: Ослепительная вспышка света быстро затухает (0.35 сек)
                if (_lightningLight != null)
                {
                    _lightningLight.intensity = Mathf.MoveTowards(_lightningLight.intensity, 0f, Time.deltaTime * 25f);
                }

                if (_stateTimer >= 0.35f)
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
            // Точка удара молнии: на земле между Одином и игроком (примерно в 3 метрах перед игроком)
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

            // 1. Ослепительная сине-белая вспышка света (чистый свет, без коллайдеров и урона!)
            GameObject lightObj = new GameObject("OdinSafeLightningFlash");
            lightObj.transform.position = strikePos + Vector3.up * 3f;
            _lightningLight = lightObj.AddComponent<Light>();
            _lightningLight.type = LightType.Point;
            _lightningLight.color = new Color(0.7f, 0.9f, 1.0f);
            _lightningLight.intensity = 15f;
            _lightningLight.range = 45f;

            // 2. Сильная тряска экрана у игрока (эффект кинематографичной ударной волны)
            if (GameCamera.instance != null)
            {
                GameCamera.instance.AddShake(strikePos, 16f, 1.4f, true);
            }

            // 3. Безопасные визуальные эффекты (строго БЕЗ урона!)
            if (ZNetScene.instance != null)
            {
                // Исключены любые AOE префабы! Только чистые визуальные эффекты
                string[] safeVisuals = new string[] {
                    "vfx_eikthyr_lightning",
                    "vfx_lightning",
                    "vfx_eikthyr_stomp"
                };

                foreach (string fxName in safeVisuals)
                {
                    GameObject fxPrefab = ZNetScene.instance.GetPrefab(fxName);
                    if (fxPrefab != null)
                    {
                        GameObject spawned = UnityEngine.Object.Instantiate(fxPrefab, strikePos, Quaternion.identity);

                        // КРИТИЧЕСКИЙ ЗАЩИТНЫЙ ФИЛЬТР: Принудительно удаляем любые боевые компоненты
                        Aoe[] dangerousAoes = spawned.GetComponentsInChildren<Aoe>(true);
                        foreach (var aoe in dangerousAoes)
                        {
                            if (aoe != null)
                            {
                                aoe.m_damage = default; // Обнуляем весь урон
                                UnityEngine.Object.Destroy(aoe);
                            }
                        }

                        // Удаляем любые коллайдеры урона
                        Collider[] colliders = spawned.GetComponentsInChildren<Collider>(true);
                        foreach (var col in colliders)
                        {
                            if (col != null) UnityEngine.Object.Destroy(col);
                        }

                        break;
                    }
                }
            }

            // 4. Громкий звук удара грома
            AudioSource audio = gameObject.AddComponent<AudioSource>();
            audio.spatialBlend = 0.7f;
            audio.minDistance = 6f;
            audio.maxDistance = 120f;
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
}
