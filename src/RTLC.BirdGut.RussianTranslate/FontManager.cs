using System;
using System.Collections.Generic;
using BepInEx.Logging;
using UnityEngine;
using UnityEngine.UI;

namespace RTLC.BirdGut.RussianTranslate
{
    /// <summary>
    /// Loads the bundled Cyrillic font and adjusts BirdGut UI.Text layout.
    /// </summary>
    internal sealed class FontManager
    {
        private readonly ManualLogSource _logger;
        private readonly float _lineSpacing;
        private readonly HashSet<int> _configuredTextIds = new HashSet<int>();

        public Font RussianFont { get; private set; }

        public FontManager(AssetBundle bundle, string fontFamiliesCsv, float lineSpacing, ManualLogSource logger)
        {
            _lineSpacing = lineSpacing;
            _logger = logger;
            if (bundle != null)
            {
                Font font = bundle.LoadAsset<Font>("assets/slippysloppy.ttf");
                bool usable = font != null && font.dynamic && font.HasCharacter('Ж') && font.HasCharacter('ё');

                if (usable)
                {
                    font.hideFlags = HideFlags.HideAndDontSave;
                    RussianFont = font;
                    _logger.LogInfo("Loaded Cyrillic Slippysloppy from rtlc.bundle");
                    return;
                }
            }

            _logger.LogWarning("Cyrillic Slippysloppy could not be loaded. Using OS fallback fonts.");
            InitializeFont(fontFamiliesCsv);
        }

        private void InitializeFont(string fontFamiliesCsv)
        {
            var requested = new List<string>();
            if (!string.IsNullOrEmpty(fontFamiliesCsv))
            {
                string[] parts = fontFamiliesCsv.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries);
                for (int i = 0; i < parts.Length; i++)
                {
                    string name = parts[i].Trim();
                    if (!string.IsNullOrEmpty(name))
                    {
                        requested.Add(name);
                    }
                }
            }

            if (requested.Count == 0)
            {
                requested.Add("Segoe Print");
                requested.Add("Ink Free");
                requested.Add("Comic Sans MS");
                requested.Add("Arial");
            }

            var installedSet = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            try
            {
                string[] osFonts = Font.GetOSInstalledFontNames();
                if (osFonts != null)
                {
                    for (int i = 0; i < osFonts.Length; i++)
                    {
                        installedSet.Add(osFonts[i]);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning($"Font.GetOSInstalledFontNames failed: {ex.Message}");
            }

            var available = new List<string>();
            for (int i = 0; i < requested.Count; i++)
            {
                if (installedSet.Count == 0 || installedSet.Contains(requested[i]))
                {
                    available.Add(requested[i]);
                }
            }

            if (available.Count == 0)
            {
                available.Add("Arial");
            }

            try
            {
                RussianFont = Font.CreateDynamicFontFromOSFont(available.ToArray(), 16);
                if (RussianFont != null)
                {
                    RussianFont.hideFlags = HideFlags.HideAndDontSave;
                    _logger.LogInfo($"Created dynamic Russian font chain: {string.Join(", ", available.ToArray())}");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError($"Failed to create dynamic font: {ex}");
            }
        }

        public void ApplyFontAndLayout(Text text)
        {
            if (text == null || RussianFont == null || IsDropdownText(text))
            {
                return;
            }

            if (text.font != RussianFont)
            {
                text.font = RussianFont;
            }

            int id = text.GetInstanceID();
            if (!_configuredTextIds.Add(id))
            {
                return;
            }

            if (text.text == "Hints" || text.text == "Подсказки")
            {
                text.horizontalOverflow = HorizontalWrapMode.Overflow;
                return;
            }

            if (text.text.StartsWith("Created by:\nMicah Boursier", StringComparison.Ordinal) ||
                text.text.StartsWith("Создатель игры:\nMicah Boursier", StringComparison.Ordinal))
            {
                text.alignment = TextAnchor.UpperCenter;
                text.lineSpacing = 0.80f;
                text.resizeTextForBestFit = true;
                text.resizeTextMaxSize = Mathf.Max(1, Mathf.RoundToInt(text.fontSize * 0.86f));
                text.resizeTextMinSize = Mathf.Min(12, text.resizeTextMaxSize);
                text.horizontalOverflow = HorizontalWrapMode.Wrap;
                text.verticalOverflow = VerticalWrapMode.Truncate;
                return;
            }

            string goName = text.gameObject.name;
            string parentName = text.transform.parent != null ? text.transform.parent.name : string.Empty;

            if (goName == "DialogueBox")
            {
                text.lineSpacing = 0.85f;
                text.fontSize = 22;
                text.resizeTextForBestFit = true;
                text.resizeTextMinSize = 14;
                text.resizeTextMaxSize = 22;
                return;
            }

            if (parentName == "DialogueBox")
            {
                text.lineSpacing = 1.0f;
                text.fontSize = 18;
                text.horizontalOverflow = HorizontalWrapMode.Overflow;
                text.verticalOverflow = VerticalWrapMode.Overflow;
                text.resizeTextForBestFit = false;
                return;
            }

            if (text.fontSize < 14 || goName == "GunHint" || parentName == "SetButton")
            {
                // Preserve the scene's font size and allow translated lines to use the full label.
                text.horizontalOverflow = HorizontalWrapMode.Overflow;
                text.verticalOverflow = VerticalWrapMode.Overflow;
            }
            else if (text.rectTransform.rect.height > 120f)
            {
                text.lineSpacing *= _lineSpacing;
            }
        }

        private static bool IsDropdownText(Text text)
        {
            // The resolution caption and item template are cloned by Unity.
            // Preserve their native font, alignment, and canvas scaling.
            return text.GetComponentInParent<Dropdown>() != null;
        }

        public void AdjustTextMesh(Text text, VertexHelper toFill)
        {
            if (text == null || toFill == null || RussianFont == null || text.font != RussianFont || IsDropdownText(text))
            {
                return;
            }

            int count = toFill.currentVertCount;
            if (count < 4)
            {
                return;
            }

            string goName = text.gameObject.name;
            if (goName == "DialogueBox")
            {
                return;
            }

            Rect rect = text.rectTransform.rect;
            float promptNudgeDown = IsContinueOrKeyPrompt(text.text)
                ? Mathf.Clamp(rect.height * 0.08f, 1.5f, 5.0f)
                : 0f;
            if (text.fontSize >= 14 && rect.height > 140f)
            {
                return;
            }

            float minX = float.MaxValue;
            float maxX = float.MinValue;
            float minY = float.MaxValue;
            float maxY = float.MinValue;
            bool hasVisible = false;

            UIVertex v0 = new UIVertex();
            UIVertex v1 = new UIVertex();
            UIVertex v2 = new UIVertex();
            UIVertex v3 = new UIVertex();

            for (int q = 0; q + 3 < count; q += 4)
            {
                toFill.PopulateUIVertex(ref v0, q);
                toFill.PopulateUIVertex(ref v1, q + 1);
                toFill.PopulateUIVertex(ref v2, q + 2);
                toFill.PopulateUIVertex(ref v3, q + 3);

                if (Mathf.Abs(v2.position.x - v0.position.x) < 0.001f && Mathf.Abs(v2.position.y - v0.position.y) < 0.001f)
                {
                    continue;
                }

                hasVisible = true;
                UpdateBounds(v0.position, ref minX, ref maxX, ref minY, ref maxY);
                UpdateBounds(v1.position, ref minX, ref maxX, ref minY, ref maxY);
                UpdateBounds(v2.position, ref minX, ref maxX, ref minY, ref maxY);
                UpdateBounds(v3.position, ref minX, ref maxX, ref minY, ref maxY);
            }

            if (!hasVisible)
            {
                return;
            }

            float glyphCenterX = (minX + maxX) * 0.5f;
            float glyphCenterY = (minY + maxY) * 0.5f;
            float glyphW = maxX - minX;
            UIVertex v = new UIVertex();

            if (text.fontSize < 14)
            {
                const float hintScale = 1.0f;
                float hx = rect.center.x;
                float hy = rect.center.y;
                for (int i = 0; i < count; i++)
                {
                    toFill.PopulateUIVertex(ref v, i);
                    v.position.x = hx + (v.position.x - glyphCenterX) * hintScale;
                    v.position.y = hy + (v.position.y - glyphCenterY) * hintScale - promptNudgeDown;
                    toFill.SetUIVertex(v, i);
                }
                return;
            }

            string parentName = text.transform.parent != null ? text.transform.parent.name : string.Empty;
            if (parentName == "DialogueBox")
            {
                float tabScale = (rect.width > 20f && glyphW > rect.width * 0.92f) ? (rect.width * 0.92f / glyphW) : 1.0f;
                float tx = rect.center.x;
                float ty = rect.center.y - promptNudgeDown;
                for (int i = 0; i < count; i++)
                {
                    toFill.PopulateUIVertex(ref v, i);
                    v.position.x = tx + (v.position.x - glyphCenterX) * tabScale;
                    v.position.y = ty + (v.position.y - glyphCenterY) * tabScale;
                    toFill.SetUIVertex(v, i);
                }
                return;
            }

            if (text.alignment != TextAnchor.MiddleCenter &&
                text.alignment != TextAnchor.MiddleLeft &&
                text.alignment != TextAnchor.MiddleRight)
            {
                return;
            }

            float scale = 1.0f;
            if (rect.width > 20f && glyphW > 0.01f)
            {
                float maxW = (text.alignment == TextAnchor.MiddleCenter) ? (rect.width * 0.94f) : (rect.width * 0.92f);
                if (glyphW > maxW)
                {
                    scale = maxW / glyphW;
                }
            }

            if (string.Equals(text.text, "Нажмите клавишу...", StringComparison.Ordinal))
            {
                // Match the original modal's footprint on the screen.
                scale = Mathf.Min(scale, 0.65f);
            }

            float upwardBias = (rect.height >= 28f) ? Mathf.Min(2.8f, rect.height * 0.06f) : 1.2f;
            float targetY = rect.center.y + upwardBias - promptNudgeDown;

            for (int i = 0; i < count; i++)
            {
                toFill.PopulateUIVertex(ref v, i);
                if (text.alignment == TextAnchor.MiddleCenter)
                {
                    v.position.x = rect.center.x + (v.position.x - glyphCenterX) * scale;
                }
                else if (text.alignment == TextAnchor.MiddleLeft)
                {
                    v.position.x = minX + (v.position.x - minX) * scale;
                }
                else
                {
                    v.position.x = maxX - (maxX - v.position.x) * scale;
                }
                v.position.y = targetY + (v.position.y - glyphCenterY) * scale;
                toFill.SetUIVertex(v, i);
            }
        }

        private static void UpdateBounds(Vector3 p, ref float minX, ref float maxX, ref float minY, ref float maxY)
        {
            if (p.x < minX) minX = p.x;
            if (p.x > maxX) maxX = p.x;
            if (p.y < minY) minY = p.y;
            if (p.y > maxY) maxY = p.y;
        }

        private static bool IsContinueOrKeyPrompt(string value)
        {
            if (string.IsNullOrEmpty(value) || value.IndexOf("нажмите", StringComparison.OrdinalIgnoreCase) < 0)
            {
                return false;
            }

            return value.IndexOf("продолж", StringComparison.OrdinalIgnoreCase) >= 0 ||
                   value.IndexOf("клавиш", StringComparison.OrdinalIgnoreCase) >= 0;
        }
    }
}
