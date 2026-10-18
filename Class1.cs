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
        public const string ModVersion = "1.0.1";

        public static HostPranksPlugin Instance { get; private set; } = null!;

        private ConfigEntry<KeyCode> _toggleKeyConfig = null!;
        private bool _isGuiOpen = false;
        private Rect _windowRect = new Rect(40, 40, 420, 480);
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
            GUI.DragWindow(new Rect(0, 0, 380, 25));

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
                _scrollPos = GUILayout.BeginScrollView(_scrollPos, GUILayout.Height(320));

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

            GUILayout.Space(8);
            GUILayout.Label($"<color=#E0E0E0>Статус:</color> <color=#FFFF66>{_statusMessage}</color>");
        }

        public void SetStatus(string message)
        {
            _statusMessage = message;
            _statusTimer = 4.5f;
            Logger.LogInfo(message);
        }

        // ==========================================
        // ПРАНК 1: КОМАР В УХЕ
        // ==========================================
        private void TriggerMosquitoPrank(Player target)
        {
            if (target == null) return;

            // Создаем невидимый звуковой эмиттер, кружащийся вокруг головы игрока
            GameObject prankObj = new GameObject("Prank_MosquitoEar");
            prankObj.transform.position = target.transform.position + Vector3.up * 1.6f;

            MosquitoEarFollower follower = prankObj.AddComponent<MosquitoEarFollower>();
            follower.TargetPlayer = target;

            SetStatus($"🦟 Комар отправлен жужжать в ухо игроку '{target.GetPlayerName()}'!");
        }

        // ==========================================
        // ПРАНК 2: КАРА ОДИНА
        // ==========================================
        private void TriggerOdinPrank(Player target)
        {
            if (target == null) return;

            if (ZNetScene.instance == null)
            {
                SetStatus("Ошибка: ZNetScene недоступен.");
                return;
            }

            GameObject odinPrefab = ZNetScene.instance.GetPrefab("Odin");
            if (odinPrefab == null)
            {
                SetStatus("Ошибка: Префаб 'Odin' не найден в игре.");
                return;
            }

            // Позиция спавна: 4.5 метра перед лицом игрока
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

            // Спавним Одина
            GameObject odinInstance = UnityEngine.Object.Instantiate(odinPrefab, spawnPos, rot);

            // Удаляем оригинальный скрипт самоуничтожения Одина, чтобы он не исчезал вблизи
            Odin vanillaOdin = odinInstance.GetComponent<Odin>();
            EffectList? despawnEffects = null;
            if (vanillaOdin != null)
            {
                despawnEffects = vanillaOdin.m_despawn;
                UnityEngine.Object.Destroy(vanillaOdin);
            }

            // Навешиваем наш контроллер шоу
            OdinPrankController controller = odinInstance.AddComponent<OdinPrankController>();
            controller.Initialize(target, despawnEffects);

            SetStatus($"⚡ Один явился покарать игрока '{target.GetPlayerName()}'!");
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
        private float _duration = 10f; // жужжит 10 секунд
        private float _angle = 0f;
        private float _flyRadius = 0.7f;
        private float _targetHeight = 1.65f;

        private void Start()
        {
            _audioSource = gameObject.AddComponent<AudioSource>();
            _audioSource.spatialBlend = 1f; // Полный 3D-звук
            _audioSource.minDistance = 0.5f;
            _audioSource.maxDistance = 12f;
            _audioSource.rolloffMode = AudioRolloffMode.Linear;
            _audioSource.loop = true;
            _audioSource.volume = 0.9f;
            _audioSource.pitch = 1.0f;

            // Ищем звук комара из префаба Deathsquito
            AudioClip clip = FindMosquitoClip();
            if (clip != null)
            {
                _audioSource.clip = clip;
                _audioSource.Play();
            }
            else
            {
                // Синтезируем высокочастотный звук жужжания если клип из игры не достался
                _audioSource.clip = GenerateBuzzClip();
                _audioSource.Play();
            }
        }

        private void Update()
        {
            if (TargetPlayer == null || TargetPlayer.gameObject == null)
            {
                Destroy(gameObject);
                return;
            }

            _timeAlive += Time.deltaTime;
            if (_timeAlive >= _duration)
            {
                // Затухание и исчезновение
                _audioSource.volume = Mathf.MoveTowards(_audioSource.volume, 0f, Time.deltaTime * 2f);
                if (_audioSource.volume <= 0.01f)
                {
                    Destroy(gameObject);
                    return;
                }
            }

            // Быстрое хаотичное кружение вокруг головы
            _angle += Time.deltaTime * 7.5f;
            float pulseRadius = _flyRadius + Mathf.Sin(_timeAlive * 5f) * 0.25f;
            float heightOffset = Mathf.Sin(_timeAlive * 3.5f) * 0.2f;

            Vector3 headPos = TargetPlayer.transform.position + Vector3.up * (_targetHeight + heightOffset);
            Vector3 offset = new Vector3(Mathf.Cos(_angle) * pulseRadius, 0f, Mathf.Sin(_angle) * pulseRadius);

            transform.position = headPos + offset;

            // Модуляция высоты звука (эффект Доплера при сближении с ухом)
            _audioSource.pitch = 1.0f + Mathf.Sin(_timeAlive * 6f) * 0.15f;
        }

        private AudioClip FindMosquitoClip()
        {
            if (ZNetScene.instance == null) return null!;
            GameObject mosquitoPrefab = ZNetScene.instance.GetPrefab("Deathsquito");
            if (mosquitoPrefab == null) return null!;

            // Проверяем AudioSource на префабе
            AudioSource[] sources = mosquitoPrefab.GetComponentsInChildren<AudioSource>(true);
            foreach (var src in sources)
            {
                if (src != null && src.clip != null) return src.clip;
            }

            // Проверяем EffectList в BaseAI / MonsterAI
            BaseAI ai = mosquitoPrefab.GetComponent<BaseAI>();
            if (ai != null && ai.m_idleSound != null && ai.m_idleSound.m_effectPrefabs != null)
            {
                foreach (var fx in ai.m_idleSound.m_effectPrefabs)
                {
                    if (fx.m_prefab != null)
                    {
                        var fxAudio = fx.m_prefab.GetComponentInChildren<AudioSource>(true);
                        if (fxAudio != null && fxAudio.clip != null) return fxAudio.clip;
                        var zsfx = fx.m_prefab.GetComponentInChildren<ZSFX>(true);
                        if (zsfx != null && zsfx.m_audioClips != null && zsfx.m_audioClips.Length > 0)
                        {
                            return zsfx.m_audioClips[0];
                        }
                    }
                }
            }

            return null!;
        }

        private AudioClip GenerateBuzzClip()
        {
            // Процедурный зуммер на 450-500 Гц (частота взмахов крыльев комара)
            int sampleRate = 44100;
            float freq = 480f;
            int samples = sampleRate / 2; // 0.5 сек циклический
            float[] data = new float[samples];
            for (int i = 0; i < samples; i++)
            {
                float t = (float)i / sampleRate;
                // Основной тон + обертоны для характерного писклявого тембра
                float sample = Mathf.Sin(2 * Mathf.PI * freq * t) * 0.6f
                             + Mathf.Sin(2 * Mathf.PI * freq * 2f * t) * 0.25f
                             + Mathf.Sin(2 * Mathf.PI * freq * 3f * t) * 0.15f;
                data[i] = sample * 0.4f;
            }

            AudioClip clip = AudioClip.Create("ProceduralMosquitoBuzz", samples, 1, sampleRate, false);
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
                    _lightningLight.intensity = Mathf.MoveTowards(_lightningLight.intensity, 0f, Time.deltaTime * 18f);
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
                // Фаза 2: Взлет в небо! Скорость разгоняется вверх
                _flightSpeed += Time.deltaTime * 45f;
                transform.position += _flightDirection * _flightSpeed * Time.deltaTime;

                // Наклоняем Одина немного назад/вверх в полете
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
            _lightningLight.intensity = 10f;
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

            // Пробуем найти thunder/lightning clip
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
