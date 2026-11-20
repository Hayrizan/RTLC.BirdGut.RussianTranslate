using System;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;
using BepInEx.Logging;

namespace RTLC.BirdGut.RussianTranslate
{
    /// <summary>
    /// Loads and serves Russian translations and localized key names.
    /// </summary>
    internal sealed class TranslationCatalog
    {
        private const string ClickToChangeSuffix = " (Click to Change)";
        private const string ClickToChangeRu = "  (сменить)";

        private static readonly Regex ResolutionRegex = new Regex(@"^\s*\d+\s*x\s*\d+\s*$");
        private static readonly Regex CyrillicRegex = new Regex(@"[\u0400-\u04FF]");

        private readonly Dictionary<string, string> _exactMap = new Dictionary<string, string>(StringComparer.Ordinal);
        private readonly Dictionary<string, string> _trimmedMap = new Dictionary<string, string>(StringComparer.Ordinal);
        private readonly Dictionary<string, string> _keyNames = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        private readonly HashSet<string> _loggedUntranslated = new HashSet<string>(StringComparer.Ordinal);
        private readonly ManualLogSource _logger;
        private readonly bool _dumpUntranslated;

        public int Count => _exactMap.Count;

        public TranslationCatalog(string json, ManualLogSource logger, bool dumpUntranslated)
        {
            _logger = logger;
            _dumpUntranslated = dumpUntranslated;
            InitKeyNames();
            LoadFallbackDefaults();
            LoadFromBundle(json);
        }

        private void InitKeyNames()
        {
            _keyNames["Space"] = "Space";
            _keyNames["Escape"] = "Esc";
            _keyNames["Return"] = "Enter";
            _keyNames["KeypadEnter"] = "Num Enter";
            _keyNames["UpArrow"] = "Стрелка вверх";
            _keyNames["DownArrow"] = "Стрелка вниз";
            _keyNames["LeftArrow"] = "Стрелка влево";
            _keyNames["RightArrow"] = "Стрелка вправо";
            _keyNames["LeftShift"] = "Л. Shift";
            _keyNames["RightShift"] = "Пр. Shift";
            _keyNames["LeftControl"] = "Л. Ctrl";
            _keyNames["RightControl"] = "Пр. Ctrl";
            _keyNames["LeftAlt"] = "Л. Alt";
            _keyNames["RightAlt"] = "Пр. Alt";
            _keyNames["Backspace"] = "Backspace";
            _keyNames["Tab"] = "Tab";
            _keyNames["Delete"] = "Del";
            _keyNames["CapsLock"] = "Caps Lock";
            _keyNames["Mouse0"] = "Lmb";
            _keyNames["Mouse1"] = "Rmb";
            _keyNames["Mouse2"] = "Mmb";
            for (int i = 0; i <= 9; i++)
            {
                _keyNames["Alpha" + i] = i.ToString();
                _keyNames["Keypad" + i] = "Num " + i;
            }
        }

        private void LoadFallbackDefaults()
        {
            AddMapping("Start New Game", "Новая игра");
            AddMapping("Load", "Загрузить");
            AddMapping("Select Level", "Выбор уровня");
            AddMapping("Choose an Organ", "Выберите орган");
            AddMapping("Options", "Настройки");
            AddMapping("Secrets", "Секреты");
            AddMapping("Credits", "Авторы");
            AddMapping("Quit Game", "Выход");
            AddMapping("Cancel", "Отмена");
            AddMapping("Done", "Готово");
            AddMapping("Controls", "Управление");
            AddMapping("Locked", "Закрыто");
            AddMapping("Visit the Nine", "Посетить Девятерых");
            AddMapping("Recieve the Blessing", "Принять благословение");
            AddMapping("You Died", "Вы погибли");
            AddMapping("You Died.", "Вы погибли.");
            AddMapping("Retry Level", "Повторить уровень");
            AddMapping("Save & Quit", "Сохранить и выйти");
        }

        private void LoadFromBundle(string json)
        {
            if (string.IsNullOrEmpty(json))
            {
                _logger.LogWarning($"Translations missing from rtlc.bundle. Using built-in fallback entries.");
                return;
            }

            try
            {

                var root = JsonReader.Parse(json) as Dictionary<string, object>;
                if (root == null || !root.TryGetValue("entries", out object entriesObj) || !(entriesObj is List<object> entriesList))
                {
                    _logger.LogWarning($"Failed to parse entries from rtlc.bundle.");
                    return;
                }

                for (int i = 0; i < entriesList.Count; i++)
                {
                    if (!(entriesList[i] is Dictionary<string, object> entry))
                    {
                        continue;
                    }

                    string key = JsonReader.GetString(entry, "key", null);
                    string val = JsonReader.GetString(entry, "value", null);
                    if (!string.IsNullOrEmpty(key) && val != null)
                    {
                        AddMapping(key, val);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error loading translations from rtlc.bundle: {ex}");
            }
        }

        private void AddMapping(string key, string value)
        {
            string normKey = NormalizeNewlines(key);
            string normVal = NormalizeNewlines(value);
            _exactMap[normKey] = normVal;

            string trimmedKey = normKey.Trim();
            if (!string.IsNullOrEmpty(trimmedKey) && !_trimmedMap.ContainsKey(trimmedKey))
            {
                _trimmedMap[trimmedKey] = normVal.Trim();
            }
        }

        public string LocalizeKeyName(string rawKeyCode)
        {
            if (string.IsNullOrEmpty(rawKeyCode))
            {
                return string.Empty;
            }

            string trimmed = rawKeyCode.Trim();
            if (_keyNames.TryGetValue(trimmed, out string localized))
            {
                return localized;
            }

            return trimmed;
        }

        public string Translate(string input)
        {
            if (string.IsNullOrEmpty(input))
            {
                return input;
            }

            // Never modify resolution strings ("1920 x 1080") parsed by ResolutionSetter.
            if (ResolutionRegex.IsMatch(input))
            {
                return input;
            }

            // Skip strings that already contain Cyrillic.
            if (CyrillicRegex.IsMatch(input))
            {
                return input;
            }

            string norm = NormalizeNewlines(input);

            if (norm.EndsWith(ClickToChangeSuffix, StringComparison.Ordinal))
            {
                string keyPart = norm.Substring(0, norm.Length - ClickToChangeSuffix.Length);
                return LocalizeKeyName(keyPart) + ClickToChangeRu;
            }

            if (_exactMap.TryGetValue(norm, out string exact))
            {
                return exact;
            }

            string trimmed = norm.Trim();
            if (!string.IsNullOrEmpty(trimmed) && _trimmedMap.TryGetValue(trimmed, out string trimmedVal))
            {
                int start = norm.IndexOf(trimmed, StringComparison.Ordinal);
                string prefix = start > 0 ? norm.Substring(0, start) : string.Empty;
                int tailStart = start + trimmed.Length;
                string suffix = tailStart < norm.Length ? norm.Substring(tailStart) : string.Empty;
                return prefix + trimmedVal + suffix;
            }

            if (_dumpUntranslated && trimmed.Length > 0 && _loggedUntranslated.Add(norm))
            {
                _logger.LogInfo("[Untranslated] " + norm.Replace("\n", "\\n"));
            }

            return input;
        }

        private static string NormalizeNewlines(string text)
        {
            return text.IndexOf('\r') >= 0 ? text.Replace("\r\n", "\n").Replace("\r", "\n") : text;
        }
    }
}
