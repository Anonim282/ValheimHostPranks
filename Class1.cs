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
        public const string ModVersion = "1.0.2";

        public static HostPranksPlugin Instance { get; private set; } = null!;

        private ConfigEntry<KeyCode> _toggleKeyConfig = null!;
        private bool _isGuiOpen = false;
        private Rect _windowRect = new Rect(40, 40, 430, 490);
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
                // Открываем только если мы в игре и являемся хостом/сервером
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

            GUI.backgroundColor = new Color(0.12f, 0.12f, 0.14f, 0.95f);
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

            GUILayout.Label("<color=#AAAAAA>Нажмите кнопку пранка напротив нужного игрока:</color>");
            GUILayout.Space(5);

            List<Player> allPlayers = Player.GetAllPlayers();
            if (allPlayers == null || allPlayers.Count == 0)
            {
                GUILayout.Label("<color=#FF6666>Игроки не найдены в мире.</color>");
            }
            else
            {
                _scrollPos = GUILayout.BeginScrollView(_scrollPos, GUILayout.Height(330));

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

                    // Кнопка 1: Комар в ухо
                    if (GUILayout.Button("🦟 Комар в ухо", GUILayout.Height(26)))
                    {
                        TriggerMosquitoPrank(player);
                    }

                    // Кнопка 2: Кара Одина
                    if (GUILayout.Button("⚡ Кара Одина", GUILayout.Height(26)))
                    {
                        TriggerOdinPrank(player);
                    }

                    GUILayout.EndHorizontal();
                    GUILayout.EndVertical();
                    GUILayout.Space(2);
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
        // ПРАНК 1: КОМАР В УХЕ
        // ==========================================
        private void TriggerMosquitoPrank(Player target)
        {
            if (target == null) return;

            GameObject prankObj = new GameObject("Prank_MosquitoEar");
            prankObj.transform.position = target.transform.position + Vector3.up * 1.6f;

            MosquitoEarFollower follower = prankObj.AddComponent<MosquitoEarFollower>();
            follower.TargetPlayer = target;

            SetStatus($"🦟 Комар отправлен жужжать в ухо '{target.GetPlayerName()}'!");
        }

        // ==========================================
        // ПРАНК 2: КАРА ОДИНА
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

            // Позиция спавна: 4.2 метра перед лицом игрока
            Vector3 forward = target.transform.forward;
            forward.y = 0f;
            if (forward.sqrMagnitude < 0.01f) forward = Vector3.forward;
            forward.Normalize();

            Vector3 spawnPos = target.transform.position + forward * 4.2f;
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

            // Навешиваем контроллер кастомного шоу
            OdinPrankController controller = odinInstance.AddComponent<OdinPrankController>();
            controller.Initialize(target, despawnEffects);

            SetStatus($"⚡ Один ({odinPrefab.name}) явился покарать '{target.GetPlayerName()}'!");
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
    // КОНТРОЛЛЕР 1: ЗВУК КОМАРА В УХЕ
    // =========================================================================
    public class MosquitoEarFollower : MonoBehaviour
    {
        public Player TargetPlayer = null!;
        private AudioSource _audioSource = null!;
        private float _timeAlive = 0f;
        private float _duration = 9f; // жужжит 9 секунд
        private float _angle = 0f;
        private float _flyRadius = 0.65f;
        private float _targetHeight = 1.62f;
        private GameObject[]? _gameFlyFxInstances = null;
        private bool _attackTriggered = false;

        private void Start()
        {
            // 1. Пытаемся задействовать оригинальный непрерывный эффект полета комара из игры
            GameObject? mosquitoPrefab = null;
            if (ZNetScene.instance != null)
            {
                mosquitoPrefab = ZNetScene.instance.GetPrefab("Deathsquito")
                              ?? ZNetScene.instance.GetPrefab("deathsquito");
            }

            if (mosquitoPrefab != null)
            {
                Character ch = mosquitoPrefab.GetComponent<Character>();
                if (ch != null && ch.m_flyingContinuousEffect != null)
                {
                    try
                    {
                        _gameFlyFxInstances = ch.m_flyingContinuousEffect.Create(transform.position, transform.rotation, transform);
                    }
                    catch { }
                }
            }

            // 2. Инициализируем 3D AudioSource
            _audioSource = gameObject.AddComponent<AudioSource>();
            _audioSource.spatialBlend = 1f; // Полное 3D позиционирование
            _audioSource.minDistance = 0.4f;
            _audioSource.maxDistance = 8f;
            _audioSource.rolloffMode = AudioRolloffMode.Linear;
            _audioSource.loop = true;
            _audioSource.volume = 0.85f;
            _audioSource.pitch = 1.0f;

            // Если непрерывный эффект из игры не воспроизвел аудио, настраиваем AudioSource
            AudioClip? clip = FindOfficialMosquitoClip(mosquitoPrefab);
            if (clip != null)
            {
                _audioSource.clip = clip;
                _audioSource.Play();
            }
            else if (_gameFlyFxInstances == null || _gameFlyFxInstances.Length == 0)
            {
                // Реалистичный зудящий тембр комара (пилообразный зуммер, без сирен и воя!)
                _audioSource.clip = GenerateRealisticBuzz();
                _audioSource.Play();
            }
        }

        private void Update()
        {
            if (TargetPlayer == null || TargetPlayer.gameObject == null)
            {
                Cleanup();
                Destroy(gameObject);
                return;
            }

            _timeAlive += Time.deltaTime;

            // На 8-й секунде — внезапный бросок к уху (звук укуса/атаки)
            if (_timeAlive >= 7.8f && !_attackTriggered)
            {
                _attackTriggered = true;
                PlayAttackSting();
            }

            if (_timeAlive >= _duration)
            {
                _audioSource.volume = Mathf.MoveTowards(_audioSource.volume, 0f, Time.deltaTime * 3f);
                if (_audioSource.volume <= 0.02f)
                {
                    Cleanup();
                    Destroy(gameObject);
                    return;
                }
            }

            // Быстрое хаотичное кружение вокруг головы
            _angle += Time.deltaTime * 8.5f;
            float pulseRadius = _flyRadius + Mathf.Sin(_timeAlive * 4.5f) * 0.2f;
            float heightOffset = Mathf.Sin(_timeAlive * 3.2f) * 0.15f;

            Vector3 headPos = TargetPlayer.transform.position + Vector3.up * (_targetHeight + heightOffset);
            Vector3 offset = new Vector3(Mathf.Cos(_angle) * pulseRadius, 0f, Mathf.Sin(_angle) * pulseRadius);

            transform.position = headPos + offset;
        }

        private void PlayAttackSting()
        {
            // Небольшой всплеск громкости в ухо перед исчезновением
            _audioSource.pitch = 1.15f;
            _audioSource.volume = 1f;

            // Воспроизводим звук атаки комара если есть
            if (ZNetScene.instance != null)
            {
                GameObject hitFx = ZNetScene.instance.GetPrefab("vfx_blood");
                if (hitFx != null)
                {
                    UnityEngine.Object.Instantiate(hitFx, transform.position, Quaternion.identity);
                }
            }
        }

        private void Cleanup()
        {
            if (_gameFlyFxInstances != null)
            {
                foreach (var inst in _gameFlyFxInstances)
                {
                    if (inst != null) Destroy(inst);
                }
                _gameFlyFxInstances = null;
            }
        }

        private void OnDestroy()
        {
            Cleanup();
        }

        private AudioClip? FindOfficialMosquitoClip(GameObject? mosquitoPrefab)
        {
            if (mosquitoPrefab == null) return null;

            // 1. Ищем в flying continuous effect
            Character ch = mosquitoPrefab.GetComponent<Character>();
            if (ch != null && ch.m_flyingContinuousEffect != null && ch.m_flyingContinuousEffect.m_effectPrefabs != null)
            {
                foreach (var ep in ch.m_flyingContinuousEffect.m_effectPrefabs)
                {
                    if (ep != null && ep.m_prefab != null)
                    {
                        var src = ep.m_prefab.GetComponentInChildren<AudioSource>(true);
                        if (src != null && src.clip != null) return src.clip;
                        var zsfx = ep.m_prefab.GetComponentInChildren<ZSFX>(true);
                        if (zsfx != null && zsfx.m_audioClips != null && zsfx.m_audioClips.Length > 0)
                        {
                            return zsfx.m_audioClips[0];
                        }
                    }
                }
            }

            // 2. Ищем в MonsterAI idle
            BaseAI ai = mosquitoPrefab.GetComponent<BaseAI>();
            if (ai != null && ai.m_idleSound != null && ai.m_idleSound.m_effectPrefabs != null)
            {
                foreach (var ep in ai.m_idleSound.m_effectPrefabs)
                {
                    if (ep != null && ep.m_prefab != null)
                    {
                        var src = ep.m_prefab.GetComponentInChildren<AudioSource>(true);
                        if (src != null && src.clip != null) return src.clip;
                        var zsfx = ep.m_prefab.GetComponentInChildren<ZSFX>(true);
                        if (zsfx != null && zsfx.m_audioClips != null && zsfx.m_audioClips.Length > 0)
                        {
                            return zsfx.m_audioClips[0];
                        }
                    }
                }
            }

            // 3. Ищем во всех дочерних AudioSource
            AudioSource[] sources = mosquitoPrefab.GetComponentsInChildren<AudioSource>(true);
            foreach (var s in sources)
            {
                if (s != null && s.clip != null) return s.clip;
            }

            return null;
        }

        private AudioClip GenerateRealisticBuzz()
        {
            // Синтез настоящего комариного зуда (пилообразная волна 430 Гц с обертонами и шумом крыльев)
            int sampleRate = 44100;
            float freq = 430f;
            int samples = sampleRate / 2; // 0.5 сек циклический буфер
            float[] data = new float[samples];
            System.Random rand = new System.Random(12345);

            for (int i = 0; i < samples; i++)
            {
                float t = (float)i / sampleRate;
                // Пила (sawtooth) дает характерный жужжащий укус звука
                float saw1 = ((t * freq) % 1.0f) * 2f - 1f;
                float saw2 = ((t * freq * 2f) % 1.0f) * 2f - 1f;
                float saw3 = ((t * freq * 3f) % 1.0f) * 2f - 1f;

                float buzz = saw1 * 0.45f + saw2 * 0.25f + saw3 * 0.15f;
                // Легкий шум воздуха от взмахов крыльев
                float wingNoise = (float)(rand.NextDouble() * 0.08 - 0.04);

                data[i] = (buzz + wingNoise) * 0.35f;
            }

            AudioClip clip = AudioClip.Create("MosquitoBuzzSharp", samples, 1, sampleRate, false);
            clip.SetData(data, 0);
            return clip;
        }
    }

    // =========================================================================
    // КОНТРОЛЛЕР 2: КАРА ОДИНА (ПОЯВЛЕНИЕ -> МОЛНИЯ -> ВЗЛЕТ В НЕБЕСА)
    // =========================================================================
    public class OdinPrankController : MonoBehaviour
    {
        private Player _target = null!;
        private EffectList? _despawnEffects = null;
        private float _stateTimer = 0f;
        private int _phase = 0; // 0: Стоит и смотрит, 1: Молния, 2: Взлет, 3: Исчезновение
        private Vector3 _flightDirection = Vector3.up;
        private float _flightSpeed = 8f;
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
                // Фаза 0: Стоит 1.8 секунды, поворачиваясь лицом к игроку
                if (_target != null)
                {
                    Vector3 lookDir = _target.transform.position - transform.position;
                    lookDir.y = 0f;
                    if (lookDir.sqrMagnitude > 0.01f)
                    {
                        transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(lookDir.normalized), Time.deltaTime * 6f);
                    }
                }

                if (_stateTimer >= 1.8f)
                {
                    _phase = 1;
                    _stateTimer = 0f;
                    ExecuteLightningStrike();
                }
            }
            else if (_phase == 1)
            {
                // Фаза 1: Короткая вспышка и пауза после удара молнии (0.4 сек)
                if (_lightningLight != null)
                {
                    _lightningLight.intensity = Mathf.MoveTowards(_lightningLight.intensity, 0f, Time.deltaTime * 20f);
                }

                if (_stateTimer >= 0.4f)
                {
                    _phase = 2;
                    _stateTimer = 0f;
                    if (_lightningLight != null) Destroy(_lightningLight.gameObject);
                }
            }
            else if (_phase == 2)
            {
                // Фаза 2: Взлет в небо! Скорость разгоняется вверх
                _flightSpeed += Time.deltaTime * 45f;
                transform.position += _flightDirection * _flightSpeed * Time.deltaTime;

                // Наклоняем силуэт немного назад в полете
                transform.Rotate(Vector3.right, -15f * Time.deltaTime);

                if (_stateTimer >= 2.8f || transform.position.y > 100f)
                {
                    _phase = 3;
                    // Финальный хлопок дыма в облаках и уничтожение
                    if (_despawnEffects != null)
                    {
                        _despawnEffects.Create(transform.position, transform.rotation);
                    }
                    Destroy(gameObject);
                }
            }
        }

        private void ExecuteLightningStrike()
        {
            Vector3 strikePos = (_target != null) ? _target.transform.position : (transform.position + transform.forward * 2f);

            // 1. Создаем яркую вспышку света
            GameObject lightObj = new GameObject("OdinLightningLight");
            lightObj.transform.position = strikePos + Vector3.up * 2f;
            _lightningLight = lightObj.AddComponent<Light>();
            _lightningLight.color = new Color(0.6f, 0.85f, 1.0f);
            _lightningLight.intensity = 12f;
            _lightningLight.range = 35f;

            // 2. Тряска экрана (shockwave)
            if (GameCamera.instance != null)
            {
                GameCamera.instance.AddShake(strikePos, 16f, 1.2f, true);
            }

            // 3. Эффекты молнии и грома из ZNetScene
            if (ZNetScene.instance != null)
            {
                string[] possibleEffects = new string[] {
                    "vfx_eikthyr_lightning",
                    "vfx_lightning",
                    "lightningAOE",
                    "vfx_eikthyr_stomp",
                    "sfx_eikthyr_attack"
                };

                foreach (string fxName in possibleEffects)
                {
                    GameObject fxPrefab = ZNetScene.instance.GetPrefab(fxName);
                    if (fxPrefab != null)
                    {
                        UnityEngine.Object.Instantiate(fxPrefab, strikePos, Quaternion.identity);
                        break;
                    }
                }
            }

            // 4. Громкий звук удара грома
            AudioSource audio = gameObject.AddComponent<AudioSource>();
            audio.spatialBlend = 0.8f;
            audio.minDistance = 5f;
            audio.maxDistance = 100f;
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
