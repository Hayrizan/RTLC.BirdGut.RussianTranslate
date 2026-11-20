using System;
using System.IO;
using BepInEx;
using BepInEx.Configuration;
using HarmonyLib;
using RTLC.BirdGut.RussianTranslate.Patches;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace RTLC.BirdGut.RussianTranslate
{
    [BepInPlugin(PluginGuid, PluginName, PluginVersion)]
    public sealed class Plugin : BaseUnityPlugin
    {
        public const string PluginGuid = "com.rtlc.birdgut.russiantranslate";
        public const string PluginName = "RTLC.BirdGut.RussianTranslate";
        public const string PluginVersion = "1.0.0";

        internal static Plugin Instance { get; private set; }

        internal TranslationCatalog Catalog { get; private set; }
        internal FontManager Fonts { get; private set; }
        internal TextureReplacer Textures { get; private set; }

        private ConfigEntry<bool> _enabledConfig;
        private ConfigEntry<bool> _enableTexturesConfig;
        private ConfigEntry<string> _fontFamiliesConfig;
        private ConfigEntry<float> _lineSpacingConfig;
        private ConfigEntry<bool> _dumpUntranslatedConfig;

        private Harmony _harmony;
        private AssetBundle _resources;
        private bool _initialized;

        internal bool IsTranslationActive => _initialized && _enabledConfig.Value;

        private void Awake()
        {
            Instance = this;

            _enabledConfig = Config.Bind("General", "Enabled", true, "Enable Russian translation plugin.");
            _enableTexturesConfig = Config.Bind("General", "EnableTextureTranslation", true, "Replace English in-game textures and signs with Russian versions.");
            _fontFamiliesConfig = Config.Bind("Typography", "FontFamilies", "Segoe Print, Ink Free, Comic Sans MS, Arial", "Comma-separated OS font family fallback chain.");
            _lineSpacingConfig = Config.Bind("Typography", "LineSpacing", 0.88f, "Line spacing multiplier for UI.Text components.");
            _dumpUntranslatedConfig = Config.Bind("Debug", "DumpUntranslated", false, "Log untranslated non-empty strings to BepInEx log.");

            if (!_enabledConfig.Value)
            {
                Logger.LogInfo("Plugin disabled via configuration.");
                return;
            }

            string pluginDir = Path.GetDirectoryName(Info.Location) ?? Paths.PluginPath;
            string bundlePath = Path.Combine(pluginDir, "rtlc.bundle");
            _resources = File.Exists(bundlePath) ? AssetBundle.LoadFromFile(bundlePath) : null;
            if (_resources == null)
            {
                Logger.LogError("Could not load " + bundlePath);
                return;
            }

            TextAsset translations = _resources.LoadAsset<TextAsset>("assets/translations.json.bytes");
            TextAsset metadata = _resources.LoadAsset<TextAsset>("assets/textures_meta.json.bytes");
            Catalog = new TranslationCatalog(translations != null ? translations.text : null, Logger, _dumpUntranslatedConfig.Value);
            Fonts = new FontManager(_resources, _fontFamiliesConfig.Value, _lineSpacingConfig.Value, Logger);
            Textures = new TextureReplacer(_resources, metadata != null ? metadata.text : null, Logger);

            _harmony = new Harmony(PluginGuid);
            _harmony.PatchAll(typeof(GameTextPatches));

            _initialized = true;

            SceneManager.sceneLoaded += OnSceneLoaded;

            Logger.LogInfo($"Initialized {PluginName} v{PluginVersion}: {Catalog.Count} text entries, {Textures.LoadedTextureCount} PNG textures, Harmony patches applied.");
            Scene active = SceneManager.GetActiveScene();
            ApplyToActiveScene(string.IsNullOrEmpty(active.name) ? "Awake" : active.name);
        }

        private void Start()
        {
            if (IsTranslationActive)
            {
                ApplyToActiveScene("Start");
            }
        }

        private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            if (IsTranslationActive)
            {
                ApplyToActiveScene(scene.name);
                if (_enableTexturesConfig.Value)
                {
                    IntroLogo.Apply(scene, _resources, Logger);
                    Textures.AlignStomachSignLetter(scene, _resources);
                }
            }
        }

        private void LateUpdate()
        {
            if (IsTranslationActive && _enableTexturesConfig.Value && Textures != null)
            {
                Textures.UpdateAnimatedSpriteRenderers();
            }
        }

        private void OnDestroy()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
            if (_resources != null) _resources.Unload(false);
            if (_harmony != null)
            {
                _harmony.UnpatchSelf();
            }
        }

        private void ApplyToActiveScene(string sceneLabel)
        {
            try
            {
                int translatedTexts = 0;
                Text[] allTexts = Resources.FindObjectsOfTypeAll<Text>();
                for (int i = 0; i < allTexts.Length; i++)
                {
                    Text t = allTexts[i];
                    if (t == null)
                    {
                        continue;
                    }

                    Fonts.ApplyFontAndLayout(t);
                    string raw = t.text;
                    if (!string.IsNullOrEmpty(raw))
                    {
                        string ru = Catalog.Translate(raw);
                        if (!string.Equals(raw, ru))
                        {
                            t.text = ru;
                            translatedTexts++;
                        }
                    }
                }

                if (_enableTexturesConfig.Value)
                {
                    Textures.ApplyToLoadedObjects();
                }

                Logger.LogInfo($"Applied Russian localization to scene '{sceneLabel}' (processed {allTexts.Length} UI.Text objects, updated {translatedTexts}, patched {Textures.PatchedTextureCount} textures, {Textures.ReplacedSpriteCount} sprites).");
            }
            catch (Exception ex)
            {
                Logger.LogError($"Error applying localization in scene '{sceneLabel}': {ex}");
            }
        }
    }
}
