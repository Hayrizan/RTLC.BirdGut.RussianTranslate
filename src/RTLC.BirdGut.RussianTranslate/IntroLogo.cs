using System;
using System.Collections.Generic;
using BepInEx.Logging;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace RTLC.BirdGut.RussianTranslate
{
    internal sealed class IntroLogo : MonoBehaviour
    {
        private static readonly string[] OriginalNames =
            { "TitleB", "TitleI", "TitleR", "TitleD", "TitleG", "TitleU", "TitleT" };
        private static readonly string[] LetterNames =
            { "p", "t", "i", "ch", "soft", "e", "n", "u", "t", "r", "o" };

        private readonly Dictionary<string, Sprite> _sprites = new Dictionary<string, Sprite>();
        private SpriteRenderer _originalFlicker;
        private SpriteRenderer _finalLetter;

        internal static void Apply(Scene scene, AssetBundle bundle, ManualLogSource logger)
        {
            if (scene.buildIndex != 3)
                return;

            GameObject title = Array.Find(scene.GetRootGameObjects(), root => root.name == "Title");
            if (title == null || title.GetComponent<IntroLogo>() != null)
                return;

            var original = new SpriteRenderer[OriginalNames.Length];
            for (int i = 0; i < OriginalNames.Length; i++)
            {
                Transform child = title.transform.Find(OriginalNames[i]);
                original[i] = child != null ? child.GetComponent<SpriteRenderer>() : null;
                if (original[i] == null)
                    return;
            }

            IntroLogo logo = title.AddComponent<IntroLogo>();
            try
            {
                foreach (string name in LetterNames)
                    logo.LoadSprite(bundle, name);
                logo.LoadSprite(bundle, "o-red");

                if (logo._sprites["o"].rect.size != logo._sprites["o-red"].rect.size)
                    throw new InvalidOperationException("The two O sprites must have matching dimensions.");

                logo.CreateLetters(original);
                WidenFallOpening(scene);
                logo._originalFlicker = original[1];
                logo._originalFlicker.GetComponent<Animator>().cullingMode = AnimatorCullingMode.AlwaysAnimate;

                // Keep the stock animator as the clock for the O flicker.
                foreach (SpriteRenderer renderer in original)
                {
                    renderer.enabled = false;
                    renderer.GetComponent<Rigidbody2D>().simulated = false;
                }
                logger.LogInfo("Localized intro logo: 11 falling letters.");
            }
            catch (Exception ex)
            {
                logger.LogWarning("Could not localize the intro logo: " + ex.Message);
                Destroy(logo);
            }
        }

        private void LoadSprite(AssetBundle bundle, string name)
        {
            if (_sprites.ContainsKey(name))
                return;

            TextAsset asset = bundle.LoadAsset<TextAsset>("assets/intro-logo/" + name + ".png.bytes");
            if (asset == null)
                throw new InvalidOperationException("Missing intro sprite: " + name);
            byte[] bytes = asset.bytes;
            var texture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            if (!texture.LoadImage(bytes))
            {
                Destroy(texture);
                throw new InvalidOperationException("Invalid intro sprite: " + name);
            }

            texture.wrapMode = TextureWrapMode.Clamp;
            texture.filterMode = FilterMode.Bilinear;
            var rect = new Rect(0, 0, texture.width, texture.height);
            _sprites.Add(name, Sprite.Create(texture, rect, new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect));
        }

        private static void WidenFallOpening(Scene scene)
        {
            GameObject frame = Array.Find(scene.GetRootGameObjects(), root => root.name == "Black");
            if (frame == null)
                return;

            foreach (BoxCollider2D collider in frame.GetComponents<BoxCollider2D>())
            {
                Vector2 offset = collider.offset;
                if (Mathf.Abs(offset.x) < 1f || collider.size.y <= collider.size.x)
                    continue;

                // Shift the side walls outward and leave the floor collider in place.
                offset.x += Mathf.Sign(offset.x) * 1.15f / Mathf.Abs(frame.transform.lossyScale.x);
                collider.offset = offset;
            }
        }

        private void CreateLetters(SpriteRenderer[] originals)
        {
            // Keep the seven original body types in sequence and insert four more.
            int[] templates = { 0, 1, 2, 3, 1, 2, 4, 5, 5, 6, 1 };
            const float preferredHeight = 6.3f;
            const float gap = 0.22f;
            const float wordGap = 0.40f;
            float spacing = gap * (LetterNames.Length - 2) + wordGap;
            float aspectSum = 0f;
            foreach (string name in LetterNames)
            {
                Vector3 size = _sprites[name].bounds.size;
                aspectSum += size.x / size.y;
            }

            // Offset the original side walls to retain clearance for the wider wordmark.
            const float clearWorldWidth = 18.9f;
            float localWidth = clearWorldWidth / Mathf.Abs(transform.lossyScale.x);
            float letterHeight = Mathf.Min(preferredHeight, (localWidth - spacing) / aspectSum);
            float x = -(letterHeight * aspectSum + spacing) * 0.5f;
            for (int i = 0; i < LetterNames.Length; i++)
            {
                SpriteRenderer template = originals[templates[i]];
                Bounds originalBounds = template.sprite.bounds;
                Sprite sprite = _sprites[LetterNames[i]];
                float width = letterHeight * sprite.bounds.size.x / sprite.bounds.size.y;

                GameObject letter = Instantiate(template.gameObject, transform, false);
                letter.name = "RussianIntroLetter" + (i + 1);
                Vector3 position = template.transform.localPosition;
                position.x = x + width * 0.5f - originalBounds.center.x * template.transform.localScale.x;
                position.y = -originalBounds.center.y * template.transform.localScale.y;
                letter.transform.localPosition = position;

                var animator = letter.GetComponent<Animator>();
                if (animator != null)
                    animator.enabled = false;

                // The body keeps its original transform, polygon collider and rotation limit.
                letter.GetComponent<SpriteRenderer>().enabled = false;
                var artwork = new GameObject("LocalizedArtwork");
                artwork.layer = letter.layer;
                artwork.transform.SetParent(letter.transform, false);
                artwork.transform.localPosition = originalBounds.center;
                float artworkScale = letterHeight / sprite.bounds.size.y;
                artwork.transform.localScale = new Vector3(
                    artworkScale / letter.transform.localScale.x,
                    artworkScale / letter.transform.localScale.y, 1f);

                // Resize the original polygon around its sprite center to follow the new artwork.
                var collider = letter.GetComponent<PolygonCollider2D>();
                Vector2 colliderScale = new Vector2(
                    width / (originalBounds.size.x * letter.transform.localScale.x),
                    letterHeight / (originalBounds.size.y * letter.transform.localScale.y));
                Vector2 center = (Vector2)originalBounds.center - collider.offset;
                for (int path = 0; path < collider.pathCount; path++)
                {
                    Vector2[] points = collider.GetPath(path);
                    for (int point = 0; point < points.Length; point++)
                        points[point] = center + Vector2.Scale(points[point] - center, colliderScale);
                    collider.SetPath(path, points);
                }
                letter.GetComponent<Rigidbody2D>().rotation += (i % 2 == 0 ? 1f : -1f) * (0.75f + (i % 3) * 0.25f);
                var renderer = artwork.AddComponent<SpriteRenderer>();
                renderer.sprite = sprite;
                renderer.sharedMaterial = template.sharedMaterial;
                renderer.sortingLayerID = template.sortingLayerID;
                renderer.sortingOrder = template.sortingOrder;
                renderer.color = template.color;

                if (i == LetterNames.Length - 1)
                    _finalLetter = renderer;
                x += width + (i == 5 ? wordGap : gap);
            }
        }

        private void LateUpdate()
        {
            if (_finalLetter != null && _originalFlicker != null && _originalFlicker.sprite != null)
            {
                Sprite sprite = _sprites[_originalFlicker.sprite.name == "TitleIRed" ? "o-red" : "o"];
                if (_finalLetter.sprite != sprite)
                    _finalLetter.sprite = sprite;
            }
        }

        private void OnDestroy()
        {
            foreach (Sprite sprite in _sprites.Values)
            {
                Destroy(sprite.texture);
                Destroy(sprite);
            }
        }
    }
}
