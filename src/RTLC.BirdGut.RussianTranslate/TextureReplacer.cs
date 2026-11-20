using System;
using System.Collections.Generic;
using System.IO;
using BepInEx.Logging;
using UnityEngine;
using UnityEngine.UI;

namespace RTLC.BirdGut.RussianTranslate
{
    internal sealed class TextureMetaEntry
    {
        public string name = string.Empty;
        public float pivotX = 0.5f;
        public float pivotY = 0.5f;
        public float pixelsPerUnit = 100f;
        public float borderX = 0f;
        public float borderY = 0f;
        public float borderZ = 0f;
        public float borderW = 0f;
    }

    /// <summary>
    /// Replaces English textures and tight-mesh sprites with full-rect Russian equivalents.
    /// </summary>
    internal sealed class TextureReplacer
    {
        private readonly ManualLogSource _logger;
        private readonly Dictionary<string, byte[]> _pngByName = new Dictionary<string, byte[]>(StringComparer.Ordinal);
        private readonly Dictionary<string, TextureMetaEntry> _metaByName = new Dictionary<string, TextureMetaEntry>(StringComparer.Ordinal);
        private readonly Dictionary<int, bool> _patchedTexIds = new Dictionary<int, bool>();
        private readonly Dictionary<int, Sprite> _spriteByOrigId = new Dictionary<int, Sprite>();
        private readonly Dictionary<string, Sprite> _spriteByName = new Dictionary<string, Sprite>(StringComparer.Ordinal);
        private readonly HashSet<int> _createdSpriteIds = new HashSet<int>();
        private SpriteRenderer[] _sceneRenderers = new SpriteRenderer[0];

        public int LoadedTextureCount => _pngByName.Count;
        public int PatchedTextureCount => _patchedTexIds.Count;
        public int ReplacedSpriteCount => _createdSpriteIds.Count;

        public TextureReplacer(AssetBundle bundle, string metadata, ManualLogSource logger)
        {
            _logger = logger;
            LoadMetadata(metadata);
            LoadPngAssets(bundle);
        }

        private void LoadMetadata(string json)
        {
            if (string.IsNullOrEmpty(json))
            {
                _logger.LogWarning("Texture metadata missing from rtlc.bundle.");
                return;
            }

            try
            {
                var root = JsonReader.Parse(json) as Dictionary<string, object>;
                if (root == null || !root.TryGetValue("textures", out object texObj) || !(texObj is List<object> texList))
                {
                    _logger.LogWarning($"Failed to parse textures list in rtlc.bundle.");
                    return;
                }

                for (int i = 0; i < texList.Count; i++)
                {
                    if (!(texList[i] is Dictionary<string, object> item))
                    {
                        continue;
                    }

                    string name = JsonReader.GetString(item, "name", null);
                    if (string.IsNullOrEmpty(name))
                    {
                        continue;
                    }

                    _metaByName[name] = new TextureMetaEntry
                    {
                        name = name,
                        pivotX = JsonReader.GetFloat(item, "pivotX", 0.5f),
                        pivotY = JsonReader.GetFloat(item, "pivotY", 0.5f),
                        pixelsPerUnit = JsonReader.GetFloat(item, "pixelsPerUnit", 100f),
                        borderX = JsonReader.GetFloat(item, "borderX", 0f),
                        borderY = JsonReader.GetFloat(item, "borderY", 0f),
                        borderZ = JsonReader.GetFloat(item, "borderZ", 0f),
                        borderW = JsonReader.GetFloat(item, "borderW", 0f),
                    };
                }

                _logger.LogInfo($"Loaded {_metaByName.Count} sprite metadata entries.");
            }
            catch (Exception ex)
            {
                _logger.LogError($"Failed to read textures_meta.json: {ex}");
            }
        }

        private void LoadPngAssets(AssetBundle bundle)
        {
            foreach (string address in bundle.GetAllAssetNames())
            {
                if (!address.StartsWith("assets/textures/", StringComparison.Ordinal) ||
                    !address.EndsWith(".png.bytes", StringComparison.Ordinal))
                    continue;

                TextAsset asset = bundle.LoadAsset<TextAsset>(address);
                if (asset != null)
                    _pngByName[Path.GetFileNameWithoutExtension(asset.name)] = asset.bytes;
            }
        }

        private Sprite _stomachLetterSprite;

        private void ReplaceStomachLetterSprite(SpriteRenderer renderer, AssetBundle bundle)
        {
            if (_stomachLetterSprite == null)
            {
                TextAsset asset = bundle.LoadAsset<TextAsset>("assets/stomach-letter.png.bytes");
                if (asset == null)
                    return;

                byte[] bytes = asset.bytes;
                var texture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
                if (!texture.LoadImage(bytes, false) || texture.width != 600 || texture.height != 800)
                {
                    UnityEngine.Object.Destroy(texture);
                    _logger.LogWarning("Invalid Russian stomach letter texture.");
                    return;
                }

                texture.wrapMode = TextureWrapMode.Clamp;
                _stomachLetterSprite = Sprite.Create(texture, new Rect(0f, 0f, 600f, 800f),
                    new Vector2(0.5f, 0.5f), renderer.sprite.pixelsPerUnit, 0u, SpriteMeshType.FullRect);
                _stomachLetterSprite.name = "RussianStomachO";
            }

            renderer.sprite = _stomachLetterSprite;
        }

        internal void AlignStomachSignLetter(UnityEngine.SceneManagement.Scene scene, AssetBundle bundle)
        {
            if (scene.buildIndex != 4)
                return;

            GameObject sign = null;
            GameObject letter = null;
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                if (root.name == "Stomach Sign") sign = root;
                if (root.name == "o") letter = root;
            }

            if (sign == null || letter == null)
                return;

            Sprite signSprite = sign.GetComponent<SpriteRenderer>().sprite;
            var body = letter.GetComponent<Rigidbody2D>();
            var renderer = letter.GetComponent<SpriteRenderer>();
            if (signSprite == null || !_patchedTexIds.ContainsKey(signSprite.texture.GetInstanceID()) ||
                body == null || renderer == null || renderer.sprite == null || renderer.sprite.name != "o")
                return;

            // Bounds of the striped O on the 2048 x 683 Russian sign, measured from the top left.
            Rect opening = new Rect(1402f, 180f, 269f, 414f);
            Vector2 pixel = new Vector2(
                opening.center.x / 2048f * signSprite.rect.width,
                (1f - opening.center.y / 683f) * signSprite.rect.height);
            Vector2 local = (pixel - signSprite.pivot) / signSprite.pixelsPerUnit;
            Vector3 target = sign.transform.TransformPoint(local);

            // The original sprite has a 526 x 734 visible area. Scale its existing collider with it.
            Vector3 worldScale = sign.transform.lossyScale;
            float letterPpu = renderer.sprite.pixelsPerUnit;
            letter.transform.localScale = new Vector3(
                opening.width / 2048f * signSprite.rect.width / signSprite.pixelsPerUnit * worldScale.x * letterPpu / 526f,
                opening.height / 683f * signSprite.rect.height / signSprite.pixelsPerUnit * worldScale.y * letterPpu / 734f,
                letter.transform.localScale.z);
            letter.transform.position = new Vector3(target.x, target.y, letter.transform.position.z);
            body.position = target;
            ReplaceStomachLetterSprite(renderer, bundle);
            _logger.LogInfo("Aligned the falling O with the Russian stomach sign.");
        }

        public void ApplyToLoadedObjects()
        {
            if (_pngByName.Count == 0)
            {
                return;
            }

            PatchTextures();
            BuildReplacementSprites();
            PatchUiImages();
            RefreshSceneSpriteRenderers();
        }

        private void PatchTextures()
        {
            Texture2D[] allTex = Resources.FindObjectsOfTypeAll<Texture2D>();
            for (int i = 0; i < allTex.Length; i++)
            {
                Texture2D tex = allTex[i];
                if (tex == null || string.IsNullOrEmpty(tex.name))
                {
                    continue;
                }

                int texId = tex.GetInstanceID();
                if (_patchedTexIds.ContainsKey(texId))
                {
                    continue;
                }

                if (!_pngByName.TryGetValue(tex.name, out byte[] bytes))
                {
                    continue;
                }

                try
                {
                    if (tex.LoadImage(bytes, false))
                    {
                        tex.wrapMode = TextureWrapMode.Clamp;
                        _patchedTexIds[texId] = true;
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError($"LoadImage failed for Texture2D '{tex.name}': {ex}");
                }
            }
        }

        private void BuildReplacementSprites()
        {
            Sprite[] allSprites = Resources.FindObjectsOfTypeAll<Sprite>();
            for (int i = 0; i < allSprites.Length; i++)
            {
                Sprite sp = allSprites[i];
                if (sp == null)
                {
                    continue;
                }

                int spId = sp.GetInstanceID();
                if (_createdSpriteIds.Contains(spId) || _spriteByOrigId.ContainsKey(spId))
                {
                    continue;
                }

                Texture2D tex = sp.texture;
                if (tex == null || string.IsNullOrEmpty(tex.name) || !_pngByName.ContainsKey(tex.name))
                {
                    continue;
                }

                Sprite replacement = GetOrCreateReplacementSprite(sp);
                if (replacement != null)
                {
                    _spriteByOrigId[spId] = replacement;
                    if (!string.IsNullOrEmpty(sp.name) && !_spriteByName.ContainsKey(sp.name))
                    {
                        _spriteByName[sp.name] = replacement;
                    }
                }
            }
        }

        private Sprite GetOrCreateReplacementSprite(Sprite orig)
        {
            Texture2D tex = orig.texture;
            Vector2 pivot = new Vector2(0.5f, 0.5f);
            float ppu = orig.pixelsPerUnit > 0f ? orig.pixelsPerUnit : 100f;
            Vector4 border = orig.border;

            if (_metaByName.TryGetValue(tex.name, out TextureMetaEntry meta))
            {
                pivot = new Vector2(meta.pivotX, meta.pivotY);
                if (meta.pixelsPerUnit > 0f)
                {
                    ppu = meta.pixelsPerUnit;
                }
                border = new Vector4(meta.borderX, meta.borderY, meta.borderZ, meta.borderW);
            }

            Rect fullRect = new Rect(0f, 0f, tex.width, tex.height);
            Sprite created = Sprite.Create(tex, fullRect, pivot, ppu, 0u, SpriteMeshType.FullRect, border);
            created.name = orig.name;
            created.hideFlags = HideFlags.HideAndDontSave;
            _createdSpriteIds.Add(created.GetInstanceID());
            return created;
        }

        private Sprite ResolveReplacement(Sprite current)
        {
            if (current == null)
            {
                return null;
            }

            int id = current.GetInstanceID();
            if (_createdSpriteIds.Contains(id))
            {
                return null;
            }

            if (_spriteByOrigId.TryGetValue(id, out Sprite byId))
            {
                return byId;
            }

            if (!string.IsNullOrEmpty(current.name) && _spriteByName.TryGetValue(current.name, out Sprite byName))
            {
                _spriteByOrigId[id] = byName;
                return byName;
            }

            if (current.texture != null && !string.IsNullOrEmpty(current.texture.name) && _pngByName.ContainsKey(current.texture.name))
            {
                Sprite created = GetOrCreateReplacementSprite(current);
                _spriteByOrigId[id] = created;
                if (!string.IsNullOrEmpty(current.name))
                {
                    _spriteByName[current.name] = created;
                }
                return created;
            }

            return null;
        }

        private void PatchUiImages()
        {
            Image[] images = Resources.FindObjectsOfTypeAll<Image>();
            for (int i = 0; i < images.Length; i++)
            {
                Image img = images[i];
                if (img == null)
                {
                    continue;
                }

                Sprite repl = ResolveReplacement(img.sprite);
                if (repl != null)
                {
                    img.sprite = repl;
                }

                Sprite ovrRepl = ResolveReplacement(img.overrideSprite);
                if (ovrRepl != null)
                {
                    img.overrideSprite = ovrRepl;
                }
            }

            Selectable[] selectables = Resources.FindObjectsOfTypeAll<Selectable>();
            for (int i = 0; i < selectables.Length; i++)
            {
                Selectable sel = selectables[i];
                if (sel == null)
                {
                    continue;
                }

                SpriteState state = sel.spriteState;
                bool changed = false;

                Sprite hi = ResolveReplacement(state.highlightedSprite);
                if (hi != null)
                {
                    state.highlightedSprite = hi;
                    changed = true;
                }

                Sprite pr = ResolveReplacement(state.pressedSprite);
                if (pr != null)
                {
                    state.pressedSprite = pr;
                    changed = true;
                }

                Sprite dis = ResolveReplacement(state.disabledSprite);
                if (dis != null)
                {
                    state.disabledSprite = dis;
                    changed = true;
                }

                if (changed)
                {
                    sel.spriteState = state;
                }
            }
        }

        public void RefreshSceneSpriteRenderers()
        {
            _sceneRenderers = Resources.FindObjectsOfTypeAll<SpriteRenderer>();
            UpdateAnimatedSpriteRenderers();
        }

        public void UpdateAnimatedSpriteRenderers()
        {
            if (_sceneRenderers == null || _sceneRenderers.Length == 0 || _pngByName.Count == 0)
            {
                return;
            }

            for (int i = 0; i < _sceneRenderers.Length; i++)
            {
                SpriteRenderer sr = _sceneRenderers[i];
                if (sr == null)
                {
                    continue;
                }

                Sprite repl = ResolveReplacement(sr.sprite);
                if (repl != null)
                {
                    sr.sprite = repl;
                }
            }
        }
    }
}
