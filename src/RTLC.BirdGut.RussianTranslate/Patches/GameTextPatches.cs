using HarmonyLib;
using UnityEngine;
using UnityEngine.UI;

namespace RTLC.BirdGut.RussianTranslate.Patches
{
    internal static class GameTextPatches
    {
        private static readonly string[] HintPlaceholders = { "x1", "x2", "x3", "x4", "x5", "x6" };
        private static readonly string[] HintPrefKeys = { "KCRight", "KCLeft", "KCJump", "KCDropDown", "KCInteract", "KCPause" };

        private static bool IsActive()
        {
            return Plugin.Instance != null && Plugin.Instance.IsTranslationActive;
        }

        private static void TranslateAndFormatTextComponent(Text text)
        {
            if (text == null || !IsActive())
            {
                return;
            }

            Plugin.Instance.Fonts.ApplyFontAndLayout(text);
            string translated = Plugin.Instance.Catalog.Translate(text.text);
            if (!string.Equals(text.text, translated))
            {
                text.text = translated;
            }
        }

        [HarmonyPatch(typeof(Text), nameof(Text.text), MethodType.Setter)]
        [HarmonyPrefix]
        private static void Text_SetText_Prefix(Text __instance, ref string value)
        {
            if (!IsActive())
            {
                return;
            }

            Plugin.Instance.Fonts.ApplyFontAndLayout(__instance);
            if (!string.IsNullOrEmpty(value))
            {
                value = Plugin.Instance.Catalog.Translate(value);
            }
        }

        [HarmonyPatch(typeof(Text), "OnPopulateMesh", new[] { typeof(VertexHelper) })]
        [HarmonyPostfix]
        private static void Text_OnPopulateMesh_Postfix(Text __instance, VertexHelper toFill)
        {
            if (!IsActive() || __instance == null || toFill == null)
            {
                return;
            }

            Plugin.Instance.Fonts.AdjustTextMesh(__instance, toFill);
        }

        [HarmonyPatch(typeof(TextHint), "Update")]
        [HarmonyPrefix]
        private static void TextHint_Update_Prefix(TextHint __instance)
        {
            if (!IsActive() || __instance == null || __instance.t == null)
            {
                return;
            }

            TranslationCatalog catalog = Plugin.Instance.Catalog;
            FontManager fonts = Plugin.Instance.Fonts;

            for (int i = 0; i < __instance.t.Length; i++)
            {
                Text item = __instance.t[i];
                if (item == null)
                {
                    continue;
                }

                fonts.ApplyFontAndLayout(item);
                string current = item.text;
                if (string.IsNullOrEmpty(current))
                {
                    continue;
                }

                string updated = catalog.Translate(current);
                for (int k = 0; k < HintPlaceholders.Length; k++)
                {
                    string marker = HintPlaceholders[k];
                    if (updated.Contains(marker))
                    {
                        string keyVal = PlayerPrefs.GetString(HintPrefKeys[k]);
                        updated = updated.Replace(marker, catalog.LocalizeKeyName(keyVal));
                    }
                }

                if (!string.Equals(current, updated))
                {
                    item.text = updated;
                }
            }
        }

        [HarmonyPatch(typeof(Dialogue), "Start")]
        [HarmonyPrefix]
        private static void Dialogue_Start_Prefix(Dialogue __instance)
        {
            TranslateDialogueInstance(__instance);
        }

        [HarmonyPatch(typeof(Dialogue), "Next")]
        [HarmonyPrefix]
        private static void Dialogue_Next_Prefix(Dialogue __instance)
        {
            TranslateDialogueInstance(__instance);
        }

        private static void TranslateDialogueInstance(Dialogue dialogue)
        {
            if (!IsActive() || dialogue == null)
            {
                return;
            }

            if (dialogue.DialogueBox != null)
            {
                Plugin.Instance.Fonts.ApplyFontAndLayout(dialogue.DialogueBox);
            }

            if (dialogue.Quotes == null)
            {
                return;
            }

            TranslationCatalog catalog = Plugin.Instance.Catalog;
            for (int i = 0; i < dialogue.Quotes.Count; i++)
            {
                string q = dialogue.Quotes[i];
                if (!string.IsNullOrEmpty(q))
                {
                    string ru = catalog.Translate(q);
                    if (!string.Equals(q, ru))
                    {
                        dialogue.Quotes[i] = ru;
                    }
                }
            }
        }

        [HarmonyPatch(typeof(Health), nameof(Health.PrintMessage))]
        [HarmonyPrefix]
        private static void Health_PrintMessage_Prefix(Health __instance, ref string Message)
        {
            if (!IsActive())
            {
                return;
            }

            if (__instance != null && __instance.DeathMessage != null)
            {
                Plugin.Instance.Fonts.ApplyFontAndLayout(__instance.DeathMessage);
            }

            if (!string.IsNullOrEmpty(Message))
            {
                Message = Plugin.Instance.Catalog.Translate(Message);
            }
        }

        [HarmonyPatch(typeof(LockSceneSelectors), "Awake")]
        [HarmonyPostfix]
        private static void LockSceneSelectors_Awake_Postfix(LockSceneSelectors __instance)
        {
            TranslateChildText(__instance);
        }

        [HarmonyPatch(typeof(LockSceneSelectors), "Start")]
        [HarmonyPostfix]
        private static void LockSceneSelectors_Start_Postfix(LockSceneSelectors __instance)
        {
            TranslateChildText(__instance);
        }

        [HarmonyPatch(typeof(LockSecretSquare), "Awake")]
        [HarmonyPostfix]
        private static void LockSecretSquare_Awake_Postfix(LockSecretSquare __instance)
        {
            TranslateChildText(__instance);
        }

        [HarmonyPatch(typeof(LockSecretSquare), "Start")]
        [HarmonyPostfix]
        private static void LockSecretSquare_Start_Postfix(LockSecretSquare __instance)
        {
            TranslateChildText(__instance);
        }

        private static void TranslateChildText(Component component)
        {
            if (!IsActive() || component == null)
            {
                return;
            }

            Transform t = component.transform.Find("Text");
            if (t != null)
            {
                TranslateAndFormatTextComponent(t.GetComponent<Text>());
            }
        }

        [HarmonyPatch(typeof(InputManagerManager), "Update")]
        [HarmonyPostfix]
        private static void InputManagerManager_Update_Postfix(InputManagerManager __instance)
        {
            TranslateInputLabels(__instance);
        }

        [HarmonyPatch(typeof(InputManagerManager), "OnGUI")]
        [HarmonyPostfix]
        private static void InputManagerManager_OnGUI_Postfix(InputManagerManager __instance)
        {
            TranslateInputLabels(__instance);
        }

        private static void TranslateInputLabels(InputManagerManager mgr)
        {
            if (!IsActive() || mgr == null)
            {
                return;
            }

            TranslateAndFormatTextComponent(mgr.RightText);
            TranslateAndFormatTextComponent(mgr.LeftText);
            TranslateAndFormatTextComponent(mgr.JumpText);
            TranslateAndFormatTextComponent(mgr.DropDownText);
            TranslateAndFormatTextComponent(mgr.InteractText);
            TranslateAndFormatTextComponent(mgr.PauseText);
        }
    }
}
