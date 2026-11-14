using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace FishTank
{
    /// <summary>Title screen and in-game HUD, built in code so the scene needs no UI setup.</summary>
    public class Hud : MonoBehaviour
    {
        static readonly Color ButtonColor = new Color(1f, 0.6f, 0.1f, 1f);
        static readonly Color ButtonHoverColor = new Color(1f, 0.78f, 0.3f, 1f);
        static readonly Color DashReadyColor = new Color(0.3f, 0.9f, 1f, 1f);
        static readonly Color DashChargingColor = new Color(0.45f, 0.6f, 0.7f, 0.8f);

        Text scoreText;
        Text livesText;
        Text messageText;
        Text treasureText;
        Text bannerText;
        Coroutine bannerRoutine;
        RectTransform growthFill;

        GameObject hudGroup;
        GameObject titleGroup;
        RectTransform titleText;
        RectTransform newGameButton;
        Image newGameImage;
        float buttonScale = 1f;

        Font font;
        GameObject comboGroup;
        Text comboText;
        RectTransform comboFill;
        int shownCombo = 1;
        float comboPunch;
        RectTransform dashFill;
        Image dashImage;
        RectTransform markerRoot;
        readonly List<Image> markers = new List<Image>();

        public static Hud Create()
        {
            var go = new GameObject("HUD", typeof(Canvas), typeof(CanvasScaler));
            var canvas = go.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = go.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;

            var hud = go.AddComponent<Hud>();
            hud.Build(go.transform);
            return hud;
        }

        void Build(Transform root)
        {
            font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

            // ---- In-game HUD ----
            var hudRoot = MakeStretch(root, "InGame");
            hudGroup = hudRoot.gameObject;
            markerRoot = MakeStretch(hudRoot, "Markers");

            scoreText = MakeText(hudRoot, font, "Score", new Vector2(0f, 1f), new Vector2(40f, -30f),
                new Vector2(500f, 70f), 48, TextAnchor.UpperLeft);
            livesText = MakeText(hudRoot, font, "Lives", new Vector2(1f, 1f), new Vector2(-40f, -30f),
                new Vector2(500f, 70f), 48, TextAnchor.UpperRight);

            treasureText = MakeText(hudRoot, font, "Treasure", new Vector2(0f, 1f), new Vector2(40f, -92f),
                new Vector2(600f, 50f), 34, TextAnchor.UpperLeft);
            treasureText.color = new Color(1f, 0.88f, 0.4f, 1f);

            // Zone discoveries and big finds are announced under the combo counter.
            bannerText = MakeText(hudRoot, font, "Banner", new Vector2(0.5f, 1f), new Vector2(0f, -300f),
                new Vector2(1500f, 220f), 84, TextAnchor.MiddleCenter);
            bannerText.gameObject.SetActive(false);

            // Growth bar: how close the player is to ruling the tank.
            var bar = MakeRect(hudRoot, "Growth", new Vector2(0.5f, 1f), new Vector2(0f, -40f), new Vector2(520f, 28f));
            bar.gameObject.AddComponent<Image>().color = new Color(0f, 0f, 0f, 0.45f);

            var fillGo = new GameObject("Fill", typeof(RectTransform));
            growthFill = fillGo.GetComponent<RectTransform>();
            growthFill.SetParent(bar, false);
            growthFill.anchorMin = Vector2.zero;
            growthFill.anchorMax = new Vector2(0f, 1f);
            growthFill.offsetMin = Vector2.zero;
            growthFill.offsetMax = Vector2.zero;
            fillGo.AddComponent<Image>().color = ButtonColor;

            // Combo multiplier with a draining timer, just under the growth bar.
            var comboRoot = MakeStretch(hudRoot, "Combo");
            comboGroup = comboRoot.gameObject;
            comboText = MakeText(comboRoot, font, "ComboText", new Vector2(0.5f, 1f), new Vector2(0f, -85f),
                new Vector2(600f, 80f), 64, TextAnchor.MiddleCenter);
            comboText.color = new Color(1f, 0.85f, 0.2f, 1f);
            comboFill = MakeBar(comboRoot, "ComboTimer", new Vector2(0.5f, 1f), new Vector2(0f, -172f),
                new Vector2(240f, 10f), new Color(1f, 0.85f, 0.2f, 1f), out _);
            comboGroup.SetActive(false);

            // Dash cooldown, bottom centre.
            var dashLabel = MakeText(hudRoot, font, "DashLabel", new Vector2(0.5f, 0f), new Vector2(0f, 56f),
                new Vector2(300f, 40f), 28, TextAnchor.MiddleCenter);
            dashLabel.text = "BOOST";
            dashFill = MakeBar(hudRoot, "Dash", new Vector2(0.5f, 0f), new Vector2(0f, 36f),
                new Vector2(220f, 14f), DashReadyColor, out dashImage);

            // ---- Title screen (the live tank keeps swimming behind it) ----
            var titleRoot = MakeStretch(root, "Title");
            titleGroup = titleRoot.gameObject;
            var dim = titleGroup.AddComponent<Image>();
            dim.color = new Color(0f, 0.08f, 0.18f, 0.3f);
            dim.raycastTarget = false;

            var title = MakeText(titleRoot, font, "TitleText", new Vector2(0.5f, 0.5f), new Vector2(0f, 300f),
                new Vector2(1700f, 240f), 170, TextAnchor.MiddleCenter);
            title.text = "FISH TANK";
            titleText = title.rectTransform;

            var tagline = MakeText(titleRoot, font, "Tagline", new Vector2(0.5f, 0.5f), new Vector2(0f, 165f),
                new Vector2(1600f, 70f), 44, TextAnchor.MiddleCenter);
            tagline.text = "Eat the small ones. Run from the big ones.";

            newGameButton = MakeRect(titleRoot, "NewGame", new Vector2(0.5f, 0.5f), new Vector2(0f, -250f),
                new Vector2(520f, 130f));
            newGameImage = newGameButton.gameObject.AddComponent<Image>();
            newGameImage.color = ButtonColor;
            newGameImage.raycastTarget = false;
            var label = MakeText(newGameButton, font, "Label", new Vector2(0.5f, 0.5f), Vector2.zero,
                new Vector2(520f, 130f), 64, TextAnchor.MiddleCenter);
            label.text = "NEW GAME";

            var hint = MakeText(titleRoot, font, "Hint", new Vector2(0.5f, 0f), new Vector2(0f, 50f),
                new Vector2(1700f, 60f), 34, TextAnchor.MiddleCenter);
            hint.text = "Swim: your fish follows the mouse (or drag on touch, or WASD)     " +
                        "Boost: hold click / Space";
            hint.color = new Color(1f, 1f, 1f, 0.8f);

            titleGroup.SetActive(false);

            // ---- Centre message (game over, win, errors), drawn on top ----
            messageText = MakeText(root, font, "Message", new Vector2(0.5f, 0.5f), Vector2.zero,
                new Vector2(1600f, 700f), 56, TextAnchor.MiddleCenter);
        }

        public void SetScore(int score) => scoreText.text = "Score " + score;
        public void SetLives(int lives) => livesText.text = "Lives " + Mathf.Max(0, lives);
        public void SetGrowth(float t) => growthFill.anchorMax = new Vector2(Mathf.Clamp01(t), 1f);

        public void SetTreasure(int found, int total)
        {
            treasureText.text = total > 0 ? "Treasure " + found + "/" + total : string.Empty;
        }

        /// <summary>Big announcement that fades in and out: a small line over a large one.</summary>
        public void Banner(string small, string large)
        {
            if (bannerRoutine != null) StopCoroutine(bannerRoutine);
            bannerText.text = "<size=40>" + small + "</size>\n" + large;
            bannerText.gameObject.SetActive(true);
            bannerRoutine = StartCoroutine(AnimateBanner());
        }

        IEnumerator AnimateBanner()
        {
            const float duration = 3.2f;
            var rt = bannerText.rectTransform;
            for (float t = 0f; t < duration; t += Time.unscaledDeltaTime)
            {
                float k = t / duration;
                float alpha = Mathf.Clamp01(k * 8f) * Mathf.Clamp01((1f - k) * 4f);
                bannerText.color = new Color(1f, 1f, 1f, alpha);
                rt.localScale = Vector3.one * Mathf.Lerp(1.25f, 1f, Mathf.Clamp01(k * 6f));
                yield return null;
            }

            bannerText.gameObject.SetActive(false);
            bannerRoutine = null;
        }

        public void SetMessage(string message)
        {
            messageText.text = message ?? string.Empty;
            messageText.gameObject.SetActive(!string.IsNullOrEmpty(message));
        }

        /// <summary>Shows the combo multiplier (hidden at x1) and how much time is left to extend it.</summary>
        public void SetCombo(int multiplier, float timeLeft01)
        {
            bool show = multiplier > 1;
            if (comboGroup.activeSelf != show) comboGroup.SetActive(show);

            if (multiplier != shownCombo)
            {
                if (multiplier > shownCombo) comboPunch = 1f;
                shownCombo = multiplier;
                comboText.text = "x" + multiplier + " COMBO";
            }

            comboFill.anchorMax = new Vector2(Mathf.Clamp01(timeLeft01), 1f);
        }

        /// <summary>Dash cooldown: 0 = just used, 1 = ready.</summary>
        public void SetDash(float ready01)
        {
            dashFill.anchorMax = new Vector2(Mathf.Clamp01(ready01), 1f);
            dashImage.color = ready01 > 0.2f ? DashReadyColor : DashChargingColor;
        }

        /// <summary>Floating text (e.g. "+30") that pops up at a world position and drifts away.</summary>
        public void Popup(Vector3 worldPosition, string message, Color color, float scale)
        {
            var cam = Camera.main;
            if (cam == null) return;

            Vector3 screen = cam.WorldToScreenPoint(worldPosition);
            if (screen.z <= 0f) return;
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(
                    (RectTransform)transform, screen, null, out Vector2 local)) return;

            var text = MakeText(transform, font, "Popup", new Vector2(0.5f, 0.5f), local,
                new Vector2(500f, 90f), 54, TextAnchor.MiddleCenter);
            text.text = message;
            text.color = color;
            StartCoroutine(AnimatePopup(text, local, scale));
        }

        IEnumerator AnimatePopup(Text text, Vector2 start, float scale)
        {
            const float duration = 0.75f;
            var rt = text.rectTransform;
            Color color = text.color;

            for (float t = 0f; t < duration; t += Time.unscaledDeltaTime)
            {
                float k = t / duration;
                float rise = 1f - (1f - k) * (1f - k);
                rt.anchoredPosition = start + Vector2.up * (120f * rise);

                // Pop in big, settle, then fade out.
                float pop = k < 0.15f
                    ? Mathf.Lerp(0.4f, 1.3f, k / 0.15f)
                    : Mathf.Lerp(1.3f, 1f, Mathf.Clamp01((k - 0.15f) / 0.2f));
                rt.localScale = Vector3.one * (scale * pop);

                color.a = 1f - Mathf.Clamp01((k - 0.6f) / 0.4f);
                text.color = color;
                yield return null;
            }

            Destroy(text.gameObject);
        }

        /// <summary>Shows the title screen instead of the in-game HUD, or the other way round.</summary>
        public void ShowTitle(bool show)
        {
            titleGroup.SetActive(show);
            hudGroup.SetActive(!show);
        }

        /// <summary>True on the frame the player clicks / taps NEW GAME or confirms with Enter, Space or a gamepad.</summary>
        public bool NewGamePressed()
        {
            if (!titleGroup.activeSelf) return false;

            var k = Keyboard.current;
            if (k != null && (k.enterKey.wasPressedThisFrame || k.numpadEnterKey.wasPressedThisFrame ||
                              k.spaceKey.wasPressedThisFrame)) return true;

            var g = Gamepad.current;
            if (g != null && (g.buttonSouth.wasPressedThisFrame || g.startButton.wasPressedThisFrame)) return true;

            var p = Pointer.current;
            return p != null && p.press.wasPressedThisFrame && PointerOverNewGame();
        }

        bool PointerOverNewGame()
        {
            var p = Pointer.current;
            return p != null &&
                   RectTransformUtility.RectangleContainsScreenPoint(newGameButton, p.position.ReadValue(), null);
        }

        void Update()
        {
            float t = Time.unscaledTime;
            float dt = Time.unscaledDeltaTime;

            if (comboPunch > 0f && comboText != null)
            {
                comboPunch = Mathf.Max(0f, comboPunch - dt * 5f);
                comboText.rectTransform.localScale = Vector3.one * (1f + 0.5f * comboPunch);
            }

            if (titleGroup == null || !titleGroup.activeSelf) return;

            // Title bobs like it is floating; the button breathes and pops when hovered.
            titleText.anchoredPosition = new Vector2(0f, 300f + Mathf.Sin(t * 1.3f) * 12f);
            titleText.localRotation = Quaternion.Euler(0f, 0f, Mathf.Sin(t * 0.9f) * 1.5f);

            bool hover = PointerOverNewGame();
            float wanted = hover ? 1.12f : 1f + Mathf.Sin(t * 3f) * 0.04f;
            buttonScale = Mathf.Lerp(buttonScale, wanted, 1f - Mathf.Exp(-14f * dt));
            newGameButton.localScale = Vector3.one * buttonScale;
            newGameImage.color = hover ? ButtonHoverColor : ButtonColor;
        }

        // Fish markers. In open 3D water sizes are hard to compare at a distance, so a diamond floats over:
        //   green - a fish you can eat
        //   red   - a fish that can eat you (bigger and brighter when it is close)
        void LateUpdate()
        {
            int used = 0;
            var gm = GameManager.Instance;
            var cam = Camera.main;

            if (gm != null && cam != null && gm.cameraRig != null && gm.State == GameState.Playing &&
                gm.Player != null && gm.Player.IsAlive)
            {
                Fish player = gm.Player;
                var canvasRect = (RectTransform)transform;
                var all = Fish.All;

                for (int i = 0; i < all.Count; i++)
                {
                    Fish f = all[i];
                    if (f == player || !f.IsAlive) continue;

                    bool prey = gm.CanEat(player, f);
                    bool danger = gm.CanEat(f, player);
                    if (!prey && !danger) continue;

                    bool close = (f.Position - player.Position).magnitude < 4f + 2f * f.Size + 2f * player.Size;

                    Vector3 screen = cam.WorldToScreenPoint(
                        f.Position + Vector3.up * (f.Size * (0.2f + f.bodyHalfHeight)));
                    if (screen.z <= 0f || screen.x < 0f || screen.x > Screen.width ||
                        screen.y < 0f || screen.y > Screen.height) continue;
                    if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(
                            canvasRect, screen, null, out Vector2 local)) continue;

                    Image marker = GetMarker(used++);
                    marker.rectTransform.anchoredPosition = local + Vector2.up * 20f;
                    marker.rectTransform.sizeDelta = Vector2.one * (prey ? 11f : close ? 22f : 14f);
                    marker.color = prey
                        ? new Color(0.4f, 1f, 0.5f, close ? 0.9f : 0.5f)
                        : new Color(1f, 0.25f, 0.2f, close ? 0.95f : 0.55f);
                }
            }

            for (int i = used; i < markers.Count; i++)
                if (markers[i].gameObject.activeSelf) markers[i].gameObject.SetActive(false);
        }

        Image GetMarker(int index)
        {
            while (markers.Count <= index)
            {
                var rt = MakeRect(markerRoot, "Marker", new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.one * 16f);
                rt.localRotation = Quaternion.Euler(0f, 0f, 45f);
                var image = rt.gameObject.AddComponent<Image>();
                image.raycastTarget = false;
                markers.Add(image);
            }

            if (!markers[index].gameObject.activeSelf) markers[index].gameObject.SetActive(true);
            return markers[index];
        }

        static RectTransform MakeStretch(Transform parent, string name)
        {
            var go = new GameObject(name, typeof(RectTransform));
            var rt = go.GetComponent<RectTransform>();
            rt.SetParent(parent, false);
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
            return rt;
        }

        /// <summary>Dark backing bar with a coloured fill; returns the fill, sized via its anchorMax.x.</summary>
        static RectTransform MakeBar(Transform parent, string name, Vector2 anchor, Vector2 position, Vector2 size,
            Color color, out Image fillImage)
        {
            var bar = MakeRect(parent, name, anchor, position, size);
            var back = bar.gameObject.AddComponent<Image>();
            back.color = new Color(0f, 0f, 0f, 0.45f);
            back.raycastTarget = false;

            var fillGo = new GameObject("Fill", typeof(RectTransform));
            var fill = fillGo.GetComponent<RectTransform>();
            fill.SetParent(bar, false);
            fill.anchorMin = Vector2.zero;
            fill.anchorMax = Vector2.one;
            fill.offsetMin = Vector2.zero;
            fill.offsetMax = Vector2.zero;
            fillImage = fillGo.AddComponent<Image>();
            fillImage.color = color;
            fillImage.raycastTarget = false;
            return fill;
        }

        static RectTransform MakeRect(Transform parent, string name, Vector2 anchor, Vector2 position, Vector2 size)
        {
            var go = new GameObject(name, typeof(RectTransform));
            var rt = go.GetComponent<RectTransform>();
            rt.SetParent(parent, false);
            rt.anchorMin = rt.anchorMax = rt.pivot = anchor;
            rt.anchoredPosition = position;
            rt.sizeDelta = size;
            return rt;
        }

        static Text MakeText(Transform parent, Font font, string name, Vector2 anchor, Vector2 position,
            Vector2 size, int fontSize, TextAnchor alignment)
        {
            var rt = MakeRect(parent, name, anchor, position, size);
            var text = rt.gameObject.AddComponent<Text>();
            text.font = font;
            text.fontSize = fontSize;
            text.fontStyle = FontStyle.Bold;
            text.alignment = alignment;
            text.color = Color.white;
            text.raycastTarget = false;
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Overflow;

            var outline = rt.gameObject.AddComponent<Outline>();
            outline.effectColor = new Color(0f, 0.1f, 0.2f, 0.8f);
            outline.effectDistance = new Vector2(2f, -2f);
            return text;
        }
    }
}
