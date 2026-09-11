using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.TextCore.LowLevel;
using UnityEngine.UI;
using UnityEngine.XR;
using UnityEngine.EventSystems;

namespace TempleCircuit
{
    public sealed class TempleGameHUD : MonoBehaviour
    {
        public Sprite panelSprite;
        public Sprite topHudSprite;
        public Font uiFont;

        Canvas headCanvas;
        Canvas diceCanvas;
        GameObject tileInfoRoot;
        TempleCircuitGame game;
        Font sourceFont;
        TMP_FontAsset font;
        TMP_Text roundText;
        TMP_Text trendText;
        TMP_Text headlineText;
        TMP_Text bodyText;
        TMP_Text hintText;
        readonly List<TMP_Text> playerTexts = new List<TMP_Text>();
        readonly List<Image> playerCards = new List<Image>();
        readonly List<TempleTileInfoBillboard> tileInfoCards = new List<TempleTileInfoBillboard>();
        readonly string[] playerNames = new string[4];
        GameObject playerPropertyHoverRoot;
        Image playerPropertyHoverBackground;
        TMP_Text playerPropertyHoverText;
        int leftHoveredPlayer = -1;
        int rightHoveredPlayer = -1;
        int desktopHoveredPlayer = -1;
        TempleWorldButton primaryButton;
        TempleWorldButton secondaryButton;
        TMP_Text primaryLabel;
        TMP_Text secondaryLabel;
        GameObject chargeGroup;
        RectTransform chargeFill;

        static readonly Color Ink = new Color32(12, 27, 23, 246);
        static readonly Color CardInk = new Color32(21, 43, 35, 246);
        static readonly Color ActiveInk = new Color32(48, 79, 63, 252);
        static readonly Color Cinnabar = new Color32(178, 62, 52, 255);
        static readonly Color Gold = new Color32(220, 181, 91, 255);
        static readonly Color Paper = new Color32(244, 236, 214, 255);
        static readonly Color MutedPaper = new Color32(178, 204, 188, 255);
        static Sprite roundedPanelSprite;

        public void Build(TempleCircuitGame game, Vector3 boardCenter, float boardSize)
        {
            if (diceCanvas != null)
                return;

            this.game = game;

            sourceFont = ResolveSourceFont();

            font = CreateFontInstance(sourceFont, "Head");
            BuildWorldStatusBoard();
            font = CreateFontInstance(sourceFont, "Dice");
            BuildDicePrompt(game, boardCenter, boardSize);
        }

        public void BuildTileInfoCards(TempleCircuitGame game, IReadOnlyList<TempleBoardTile> tiles, Vector3 boardCenter)
        {
            if (game == null || tiles == null || tiles.Count == 0)
                return;
            if (tileInfoRoot != null && tileInfoCards.Count == tiles.Count)
                return;

            if (sourceFont == null)
                sourceFont = ResolveSourceFont();
            font = CreateFontInstance(sourceFont, "Tiles");
            if (tileInfoRoot != null)
                Destroy(tileInfoRoot);
            tileInfoCards.Clear();
            tileInfoRoot = new GameObject("TempleTileInfo_Runtime");
            tileInfoRoot.transform.SetParent(transform, false);

            for (var i = 0; i < tiles.Count; i++)
            {
                var tile = tiles[i];
                if (tile == null)
                    continue;
                var canvasObject = new GameObject($"TempleTileInfo_{i + 1:00}", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(BoxCollider), typeof(TempleTileInfoBillboard));
                canvasObject.transform.SetParent(tileInfoRoot.transform, false);
                var canvas = canvasObject.GetComponent<Canvas>();
                canvas.renderMode = RenderMode.WorldSpace;
                canvas.worldCamera = Camera.main;
                canvas.sortingOrder = 24;
                canvasObject.GetComponent<CanvasScaler>().dynamicPixelsPerUnit = 1f;

                var rect = canvasObject.GetComponent<RectTransform>();
                rect.sizeDelta = new Vector2(284f, 124f);
                var spacing = tile.EstimateSpacing();
                var inheritedScale = Mathf.Max(0.0001f, transform.lossyScale.x);
                rect.localScale = Vector3.one * (spacing * 1.18f / 284f / inheritedScale);
                var aimTarget = canvasObject.GetComponent<BoxCollider>();
                aimTarget.isTrigger = true;
                aimTarget.center = Vector3.zero;
                aimTarget.size = new Vector3(284f, 124f, 12f);

                var tileColor = TilePanelColor(tile.category);
                var frame = CreateRoundedImage("TileInfoGoldFrame", rect, new Color(Gold.r, Gold.g, Gold.b, 0.96f), Vector2.zero, new Vector2(282f, 122f));
                frame.raycastTarget = false;
                var panelColor = Color.Lerp(tileColor, Ink, 0.46f);
                panelColor.a = 0.96f;
                var panel = CreateRoundedImage("TileInfoPanel", frame.rectTransform, panelColor, Vector2.zero, new Vector2(277f, 117f));
                panel.raycastTarget = false;
                AddTilePanelFrame(panel.rectTransform, tileColor);

                // A single centered block prevents the Chinese font's line
                // metrics from clipping the title or the final detail line.
                var content = CreateText("Content", panel.rectTransform, 20, TextAnchor.MiddleCenter, new Color32(255, 250, 232, 255), new Vector2(0f, 1f), new Vector2(244f, 104f));
                content.fontStyle = FontStyles.Bold;
                content.fontWeight = FontWeight.Bold;
                content.outlineColor = new Color32(24, 34, 29, 235);
                content.outlineWidth = 0.20f;
                content.textWrappingMode = TextWrappingModes.Normal;
                content.lineSpacing = -3f;

                var billboard = canvasObject.GetComponent<TempleTileInfoBillboard>();
                billboard.Configure(tile, Camera.main, boardCenter, spacing, content, null, tileColor);
                tileInfoCards.Add(billboard);
            }
            RefreshTileInfoCards(game);
        }

        public void RefreshTileInfoCards(TempleCircuitGame game)
        {
            if (game == null)
                return;
            for (var i = 0; i < tileInfoCards.Count; i++)
            {
                if (tileInfoCards[i] == null)
                    continue;
                tileInfoCards[i].SetContent(game.GetTileInfo(i));
            }
        }

        void BuildWorldStatusBoard()
        {
            var camera = Camera.main;
            if (camera == null)
                return;

            // This compact strip is deliberately the one auxiliary HUD in the
            // experience: it remains centred along the top of the player's view.
            // The board, dice, action panel and four coloured tile panels remain
            // in world space.  Parenting it to the tracked camera keeps its front
            // facing the player without moving or obscuring the tabletop.
            var canvasObject = new GameObject("TempleHUD_TopStatus", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster), typeof(TempleHeadLockedStatusAnchor));
            canvasObject.transform.SetParent(camera.transform, false);
            headCanvas = canvasObject.GetComponent<Canvas>();
            headCanvas.renderMode = RenderMode.WorldSpace;
            headCanvas.worldCamera = camera;
            headCanvas.sortingOrder = 80;
            canvasObject.GetComponent<CanvasScaler>().dynamicPixelsPerUnit = 1f;

            var rect = canvasObject.GetComponent<RectTransform>();
            rect.sizeDelta = new Vector2(1320f, 210f);
            // Deliberately large and high in the view: this is the one small
            // auxiliary HUD, kept clear of the floating board and dice UI.
            rect.localPosition = new Vector3(0f, 0.92f, 1.45f);
            // An identity canvas viewed along the camera's forward axis preserves
            // the artwork's authored left-to-right reading order.  Do not flip
            // this child: a 180-degree yaw mirrors the top status text.
            rect.localRotation = Quaternion.identity;
            rect.localScale = Vector3.one * (1.68f / 1320f);
            canvasObject.GetComponent<TempleHeadLockedStatusAnchor>().Configure(camera, rect.localPosition);

            var background = CreateImage("StatusBackground", rect, topHudSprite == null ? Ink : Color.white, Vector2.zero, new Vector2(1300f, 198f));
            background.sprite = topHudSprite;
            if (topHudSprite != null)
                background.type = Image.Type.Sliced;
            else
                AddOutline(background.gameObject, new Color32(220, 181, 91, 175), new Vector2(2f, -2f));

            var location = CreateText("Location", background.rectTransform, 30, TextAnchor.MiddleLeft, Gold, new Vector2(-415f, 53f), new Vector2(400f, 64f));
            location.text = "香火争渡 · 云上寺院";
            location.fontStyle = FontStyles.Bold;
            roundText = CreateText("Round", background.rectTransform, 26, TextAnchor.MiddleCenter, Paper, new Vector2(0f, 53f), new Vector2(420f, 64f));
            trendText = CreateText("Trend", background.rectTransform, 27, TextAnchor.MiddleRight, Paper, new Vector2(432f, 53f), new Vector2(320f, 64f));

            var colors = new[]
            {
                new Color32(226, 183, 69, 255),
                new Color32(72, 162, 132, 255),
                new Color32(210, 76, 61, 255),
                new Color32(151, 102, 179, 255)
            };
            for (var i = 0; i < 4; i++)
            {
                var card = CreateImage("Player" + i, background.rectTransform, CardInk, new Vector2(-455f + i * 303f, -40f), new Vector2(278f, 82f));
                playerCards.Add(card);
                var cardCollider = card.gameObject.AddComponent<BoxCollider>();
                cardCollider.isTrigger = true;
                cardCollider.center = Vector3.zero;
                cardCollider.size = new Vector3(278f, 82f, 12f);
                var cardHover = card.gameObject.AddComponent<TemplePlayerCardHover>();
                cardHover.Configure(this, i);
                var accent = CreateImage("Accent", card.rectTransform, colors[i], new Vector2(-131f, 0f), new Vector2(7f, 68f));
                accent.raycastTarget = false;
                var text = CreateText("PlayerText", card.rectTransform, 25, TextAnchor.MiddleLeft, Paper, new Vector2(12f, 0f), new Vector2(232f, 80f));
                playerTexts.Add(text);
            }
            BuildPlayerPropertyHover(background.rectTransform);
        }

        void BuildPlayerPropertyHover(RectTransform parent)
        {
            playerPropertyHoverRoot = new GameObject("PlayerPropertyHover", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            var rect = playerPropertyHoverRoot.GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            rect.pivot = new Vector2(0.5f, 1f);
            rect.anchoredPosition = new Vector2(0f, -124f);
            rect.sizeDelta = new Vector2(310f, 104f);
            playerPropertyHoverBackground = playerPropertyHoverRoot.GetComponent<Image>();
            playerPropertyHoverBackground.color = new Color32(12, 27, 23, 246);
            playerPropertyHoverBackground.sprite = GetRoundedPanelSprite();
            playerPropertyHoverBackground.type = Image.Type.Sliced;
            playerPropertyHoverBackground.raycastTarget = false;
            AddOutline(playerPropertyHoverRoot, new Color32(220, 181, 91, 185), new Vector2(1.5f, -1.5f));

            playerPropertyHoverText = CreateText("PropertyText", rect, 22, TextAnchor.MiddleLeft, Paper, Vector2.zero, new Vector2(286f, 92f));
            playerPropertyHoverText.fontStyle = FontStyles.Bold;
            playerPropertyHoverText.fontWeight = FontWeight.Bold;
            playerPropertyHoverText.lineSpacing = 2f;
            playerPropertyHoverText.alignment = TextAlignmentOptions.TopLeft;
            playerPropertyHoverText.textWrappingMode = TextWrappingModes.NoWrap;
            playerPropertyHoverText.overflowMode = TextOverflowModes.Overflow;
            playerPropertyHoverRoot.SetActive(false);
        }

        Bounds CalculateWorldBoardBounds()
        {
            var tiles = FindObjectsByType<TempleBoardTile>(FindObjectsInactive.Exclude);
            if (tiles.Length == 0)
                return new Bounds(transform.position, Vector3.one);
            var bounds = new Bounds(tiles[0].transform.position, Vector3.zero);
            for (var i = 1; i < tiles.Length; i++)
                bounds.Encapsulate(tiles[i].transform.position);
            return bounds;
        }

        void BuildDicePrompt(TempleCircuitGame game, Vector3 boardCenter, float boardSize)
        {
            var canvasObject = new GameObject("TempleHUD_DiceSpatial", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster), typeof(TempleDicePromptAnchor));
            canvasObject.transform.SetParent(transform, true);
            diceCanvas = canvasObject.GetComponent<Canvas>();
            diceCanvas.renderMode = RenderMode.WorldSpace;
            diceCanvas.worldCamera = Camera.main;
            diceCanvas.sortingOrder = 40;
            canvasObject.GetComponent<CanvasScaler>().dynamicPixelsPerUnit = 1f;

            var canvasRect = canvasObject.GetComponent<RectTransform>();
            canvasRect.sizeDelta = new Vector2(700f, 350f);
            var inheritedScale = Mathf.Max(0.0001f, transform.lossyScale.x);
            // Increase only the physical size of the dice action surface. Its
            // anchor remains beside the central die, so the board composition
            // and button proportions are unchanged.
            var scale = Mathf.Max(0.0001f, boardSize * 0.36f / 700f);
            canvasRect.localScale = Vector3.one * (scale / inheritedScale);
            var promptAnchor = canvasObject.GetComponent<TempleDicePromptAnchor>();
            promptAnchor.Configure(game != null && game.dice != null ? game.dice.transform : null, Camera.main, boardCenter, boardSize);

            var panel = CreateImage("DiceActionPanel", canvasRect, panelSprite == null ? Ink : new Color(1f, 1f, 1f, 0.98f), Vector2.zero, new Vector2(660f, 320f));
            panel.sprite = panelSprite;
            panel.preserveAspect = false;
            if (panelSprite == null)
                AddOutline(panel.gameObject, new Color32(220, 181, 91, 190), new Vector2(2f, -2f));

            headlineText = CreateText("Headline", panel.rectTransform, 40, TextAnchor.MiddleLeft, Gold, new Vector2(0f, 86f), new Vector2(530f, 62f));
            headlineText.fontStyle = FontStyles.Bold;
            bodyText = CreateText("Body", panel.rectTransform, 26, TextAnchor.UpperLeft, Paper, new Vector2(0f, 24f), new Vector2(530f, 82f));
            bodyText.textWrappingMode = TextWrappingModes.Normal;
            bodyText.overflowMode = TextOverflowModes.Truncate;
            bodyText.lineSpacing = 3f;
            hintText = CreateText("Hint", panel.rectTransform, 22, TextAnchor.MiddleLeft, Paper, new Vector2(0f, -42f), new Vector2(530f, 42f));

            chargeGroup = new GameObject("DiceCharge", typeof(RectTransform));
            var chargeRect = chargeGroup.GetComponent<RectTransform>();
            chargeRect.SetParent(panel.rectTransform, false);
            chargeRect.anchoredPosition = new Vector2(0f, -61f);
            chargeRect.sizeDelta = new Vector2(470f, 14f);
            var track = CreateImage("Track", chargeRect, new Color32(33, 66, 53, 245), Vector2.zero, new Vector2(470f, 14f));
            var fill = CreateImage("Fill", track.rectTransform, Cinnabar, new Vector2(-229f, 0f), new Vector2(0f, 10f));
            chargeFill = fill.rectTransform;
            chargeFill.pivot = new Vector2(0f, 0.5f);

            primaryButton = CreateButton(game, panel.rectTransform, "Primary", new Vector2(-138f, -104f), new Vector2(250f, 56f), Cinnabar, out primaryLabel);
            secondaryButton = CreateButton(game, panel.rectTransform, "Secondary", new Vector2(138f, -104f), new Vector2(250f, 56f), new Color32(48, 85, 72, 255), out secondaryLabel);
            SetChargeVisible(false);
            HideButtons();
        }

        public void RefreshPlayers(IReadOnlyList<TemplePlayerState> players, int round, int maxRounds, string trend, int activePlayer, int fameToWin)
        {
            if (roundText != null)
                roundText.text = $"巡游 {Mathf.Min(round, maxRounds)} / {maxRounds} · {fameToWin}功德圆满";
            if (trendText != null)
                trendText.text = $"香火流向 · {trend}";
            for (var i = 0; i < playerTexts.Count && i < players.Count; i++)
            {
                var player = players[i];
                playerNames[i] = player.name;
                var status = player.bankrupt ? "已离寺" : $"{player.cash} 金 · {player.fame} 功德";
                var displayName = i == activePlayer ? $"<b>本轮 · {player.name}</b>" : player.name;
                playerTexts[i].text = $"{displayName}\n{status}";
                playerTexts[i].color = player.bankrupt ? new Color32(134, 139, 132, 255) : Paper;
                playerCards[i].color = i == activePlayer ? ActiveInk : CardInk;
            }
            RefreshPlayerPropertyHover();
        }

        /// <summary>Called by either controller ray when it rests on a player card.</summary>
        public void SetPlayerCardHover(int playerId, XRNode hand, bool hovered)
        {
            if (hand == XRNode.LeftHand)
                leftHoveredPlayer = hovered ? playerId : leftHoveredPlayer == playerId ? -1 : leftHoveredPlayer;
            else if (hand == XRNode.RightHand)
                rightHoveredPlayer = hovered ? playerId : rightHoveredPlayer == playerId ? -1 : rightHoveredPlayer;

            RefreshPlayerPropertyHover();
        }

        public void SetDesktopPlayerCardHover(int playerId, bool hovered)
        {
            desktopHoveredPlayer = hovered ? playerId : desktopHoveredPlayer == playerId ? -1 : desktopHoveredPlayer;
            RefreshPlayerPropertyHover();
        }

        void RefreshPlayerPropertyHover()
        {
            var active = leftHoveredPlayer >= 0 ? leftHoveredPlayer : rightHoveredPlayer >= 0 ? rightHoveredPlayer : desktopHoveredPlayer;
            if (active < 0 || game == null || playerPropertyHoverRoot == null)
            {
                playerPropertyHoverRoot?.SetActive(false);
                return;
            }

            var summary = game.GetOwnedPropertySummary(active);
            var name = active < playerNames.Length && !string.IsNullOrEmpty(playerNames[active]) ? playerNames[active] : $"玩家 {active + 1}";
            var entries = summary.Split('\n');
            var twoColumns = entries.Length > 5;
            var rows = twoColumns ? (entries.Length + 1) / 2 : entries.Length;
            var content = new System.Text.StringBuilder($"<b>{name} · 已占领房产</b>\n");
            for (var row = 0; row < rows; row++)
            {
                content.Append(entries[row]);
                if (twoColumns && row + rows < entries.Length)
                    content.Append("<pos=292>").Append(entries[row + rows]);
                if (row + 1 < rows)
                    content.Append('\n');
            }
            playerPropertyHoverText.text = content.ToString();
            var width = twoColumns ? 600f : 310f;
            var preferredHeight = playerPropertyHoverText.GetPreferredValues(playerPropertyHoverText.text, width - 24f, Mathf.Infinity).y;
            var height = Mathf.Max(88f, preferredHeight + 28f);
            var hoverRect = playerPropertyHoverRoot.GetComponent<RectTransform>();
            hoverRect.sizeDelta = new Vector2(width, height);
            playerPropertyHoverText.rectTransform.sizeDelta = new Vector2(width - 24f, height - 24f);
            var targetCard = active < playerCards.Count ? playerCards[active] : null;
            if (targetCard != null)
            {
                var target = targetCard.rectTransform.anchoredPosition;
                hoverRect.anchoredPosition = new Vector2(Mathf.Clamp(target.x, -638f + width * 0.5f, 638f - width * 0.5f), target.y - 49f);
            }
            playerPropertyHoverRoot.SetActive(true);
        }

        public void ShowRoll(string playerName, bool reroll)
        {
            headlineText.text = reroll ? "改骰签已启用" : $"{playerName}的回合";
            bodyText.text = "按住中央骰子，积蓄香火。";
            hintText.text = "松手落骰";
            SetChargeVisible(false);
            HideButtons();
        }

        public void ShowDiceCharge(float strength)
        {
            headlineText.text = "香火正在汇聚";
            bodyText.text = $"蓄力 {Mathf.RoundToInt(strength * 100f)}%";
            hintText.text = "松手落骰";
            SetChargeVisible(true);
            if (chargeFill != null)
                chargeFill.sizeDelta = new Vector2(458f * Mathf.Clamp01(strength), 10f);
        }

        public void ShowRolling()
        {
            headlineText.text = "骰子落定中";
            bodyText.text = "香火点数即将揭晓。";
            hintText.text = string.Empty;
            SetChargeVisible(false);
            HideButtons();
        }

        public void ShowChoices(string title, string body, string primary, string primaryAction, string secondary, string secondaryAction, bool primaryEnabled = true)
        {
            headlineText.text = title;
            bodyText.text = FormatChoiceBody(body);
            hintText.text = "请选择";
            SetChargeVisible(false);
            ConfigureButton(primaryButton, primaryLabel, primary, primaryAction, primaryEnabled);
            ConfigureButton(secondaryButton, secondaryLabel, secondary, secondaryAction, true);
        }

        static string FormatChoiceBody(string body)
        {
            if (string.IsNullOrEmpty(body) || !body.StartsWith("前进 ", System.StringComparison.Ordinal))
                return body;

            var clauseEnd = body.IndexOf('，');
            if (clauseEnd <= 0 || clauseEnd >= body.Length - 1)
                return body;
            var advance = body.Substring(0, clauseEnd);
            var remainder = body.Substring(clauseEnd + 1);
            return $"<size=34><b>{advance}</b></size>\n<size=22>{remainder}</size>";
        }

        public void ShowResult(string title, string body)
        {
            headlineText.text = title;
            bodyText.text = body;
            hintText.text = string.Empty;
            SetChargeVisible(false);
            HideButtons();
        }

        public void ShowWinner(string winner, string detail)
        {
            ShowChoices($"圆满 · {winner}获胜", detail, "再开一局", "restart", "留在棋盘", "dismiss-win");
        }

        void SetChargeVisible(bool visible)
        {
            if (chargeGroup != null)
                chargeGroup.SetActive(visible);
        }

        void ConfigureButton(TempleWorldButton button, TMP_Text label, string text, string action, bool enabled)
        {
            button.gameObject.SetActive(true);
            button.actionId = action;
            button.SetInteractable(enabled);
            label.text = text;
            label.color = enabled ? Paper : new Color32(130, 136, 129, 255);
        }

        void HideButtons()
        {
            if (primaryButton != null)
                primaryButton.gameObject.SetActive(false);
            if (secondaryButton != null)
                secondaryButton.gameObject.SetActive(false);
        }

        TempleWorldButton CreateButton(TempleCircuitGame game, RectTransform parent, string name, Vector2 position, Vector2 size, Color color, out TMP_Text label)
        {
            var image = CreateImage(name, parent, color, position, size);
            var button = image.gameObject.AddComponent<Button>();
            var worldButton = image.gameObject.AddComponent<TempleWorldButton>();
            worldButton.game = game;
            var collider = image.gameObject.AddComponent<BoxCollider>();
            collider.size = new Vector3(size.x, size.y, 12f);
            label = CreateText("Label", image.rectTransform, 26, TextAnchor.MiddleCenter, Paper, Vector2.zero, size - new Vector2(20f, 4f));
            return worldButton;
        }

        static Image CreateImage(string name, RectTransform parent, Color color, Vector2 position, Vector2 size)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            var rect = go.GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
            var image = go.GetComponent<Image>();
            image.color = color;
            return image;
        }

        static Image CreateRoundedImage(string name, RectTransform parent, Color color, Vector2 position, Vector2 size)
        {
            var image = CreateImage(name, parent, color, position, size);
            image.sprite = GetRoundedPanelSprite();
            image.type = Image.Type.Sliced;
            return image;
        }

        static Sprite GetRoundedPanelSprite()
        {
            if (roundedPanelSprite != null)
                return roundedPanelSprite;

            const int size = 64;
            const int radius = 16;
            var pixels = new Color32[size * size];
            for (var y = 0; y < size; y++)
            {
                for (var x = 0; x < size; x++)
                {
                    var distanceX = Mathf.Max(radius - x, x - (size - radius - 1), 0);
                    var distanceY = Mathf.Max(radius - y, y - (size - radius - 1), 0);
                    var cornerDistance = Mathf.Sqrt(distanceX * distanceX + distanceY * distanceY);
                    var alpha = Mathf.Clamp01(radius - cornerDistance + 0.75f);
                    pixels[y * size + x] = new Color(1f, 1f, 1f, alpha);
                }
            }

            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false, true)
            {
                name = "Temple Rounded UI Panel",
                hideFlags = HideFlags.DontSave,
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp
            };
            texture.SetPixels32(pixels);
            texture.Apply(false, true);
            roundedPanelSprite = Sprite.Create(texture, new Rect(0f, 0f, size, size), new Vector2(0.5f, 0.5f), 64f, 0u, SpriteMeshType.FullRect, new Vector4(radius, radius, radius, radius));
            roundedPanelSprite.name = "Temple Rounded UI Panel Sprite";
            return roundedPanelSprite;
        }

        TMP_Text CreateText(string name, RectTransform parent, int size, TextAnchor anchor, Color color, Vector2 position, Vector2 dimensions)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
            var rect = go.GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            rect.anchoredPosition = position;
            rect.sizeDelta = dimensions;
            var text = go.GetComponent<TextMeshProUGUI>();
            text.font = font;
            text.fontSize = size;
            text.alignment = ToTmpAlignment(anchor);
            text.color = color;
            text.richText = true;
            text.raycastTarget = false;
            text.fontStyle = FontStyles.Bold;
            text.textWrappingMode = TextWrappingModes.NoWrap;
            text.overflowMode = TextOverflowModes.Truncate;
            text.lineSpacing = 0f;
            var shadow = go.AddComponent<Shadow>();
            shadow.effectColor = new Color32(5, 12, 10, 230);
            shadow.effectDistance = new Vector2(1.5f, -1.5f);
            return text;
        }

        static TextAlignmentOptions ToTmpAlignment(TextAnchor anchor)
        {
            switch (anchor)
            {
                case TextAnchor.UpperLeft: return TextAlignmentOptions.TopLeft;
                case TextAnchor.UpperCenter: return TextAlignmentOptions.Top;
                case TextAnchor.UpperRight: return TextAlignmentOptions.TopRight;
                case TextAnchor.MiddleLeft: return TextAlignmentOptions.Left;
                case TextAnchor.MiddleRight: return TextAlignmentOptions.Right;
                case TextAnchor.LowerLeft: return TextAlignmentOptions.BottomLeft;
                case TextAnchor.LowerCenter: return TextAlignmentOptions.Bottom;
                case TextAnchor.LowerRight: return TextAlignmentOptions.BottomRight;
                default: return TextAlignmentOptions.Center;
            }
        }

        static void AddOutline(GameObject go, Color color, Vector2 distance)
        {
            var outline = go.AddComponent<Outline>();
            outline.effectColor = color;
            outline.effectDistance = distance;
        }

        static void AddTilePanelFrame(RectTransform parent, Color tileColor)
        {
            var topLine = CreateImage("GoldTopStroke", parent, new Color(Gold.r, Gold.g, Gold.b, 0.55f), new Vector2(0f, 47f), new Vector2(190f, 1.5f));
            topLine.raycastTarget = false;
            var bottomLine = CreateImage("GoldBottomStroke", parent, new Color(Gold.r, Gold.g, Gold.b, 0.55f), new Vector2(0f, -47f), new Vector2(190f, 1.5f));
            bottomLine.raycastTarget = false;
            var sideTint = CreateImage("ColorWash", parent, new Color(tileColor.r, tileColor.g, tileColor.b, 0.16f), Vector2.zero, new Vector2(244f, 92f));
            sideTint.raycastTarget = false;

            CreateTileEndCap(parent, "LeftCloudCap", new Vector2(-128f, 0f), true, tileColor);
            CreateTileEndCap(parent, "RightCloudCap", new Vector2(128f, 0f), false, tileColor);
            CreateCloudOrnament(parent, "CloudLowerLeft", new Vector2(-96f, -38f), false, tileColor);
            CreateCloudOrnament(parent, "CloudUpperRight", new Vector2(96f, 38f), true, tileColor);
        }

        static void CreateTileEndCap(RectTransform parent, string name, Vector2 position, bool left, Color tileColor)
        {
            var cap = CreateImage(name, parent, new Color(tileColor.r, tileColor.g, tileColor.b, 0.78f), position, new Vector2(24f, 66f));
            cap.raycastTarget = false;
            var capOutline = cap.gameObject.AddComponent<Outline>();
            capOutline.effectColor = new Color(Gold.r, Gold.g, Gold.b, 0.9f);
            capOutline.effectDistance = new Vector2(left ? 1f : -1f, -1f);

            var curl = CreateImage("CloudCurl", cap.rectTransform, new Color(Gold.r, Gold.g, Gold.b, 0.72f), new Vector2(left ? 8f : -8f, 0f), new Vector2(12f, 32f));
            curl.raycastTarget = false;
        }

        static void CreateCloudOrnament(RectTransform parent, string name, Vector2 position, bool mirror, Color tileColor)
        {
            var root = new GameObject(name, typeof(RectTransform));
            var rect = root.GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            rect.anchoredPosition = position;
            rect.localScale = new Vector3(mirror ? -1f : 1f, 1f, 1f);

            var curl = CreateImage("CloudCurl", rect, new Color(Gold.r, Gold.g, Gold.b, 0.8f), Vector2.zero, new Vector2(25f, 4f));
            curl.raycastTarget = false;
            var puffOne = CreateImage("CloudPuffOne", rect, new Color(tileColor.r, tileColor.g, tileColor.b, 0.62f), new Vector2(-7f, 2f), new Vector2(12f, 12f));
            puffOne.raycastTarget = false;
            var puffTwo = CreateImage("CloudPuffTwo", rect, new Color(tileColor.r, tileColor.g, tileColor.b, 0.62f), new Vector2(6f, 5f), new Vector2(15f, 15f));
            puffTwo.raycastTarget = false;
            var puffThree = CreateImage("CloudPuffThree", rect, new Color(tileColor.r, tileColor.g, tileColor.b, 0.62f), new Vector2(18f, 2f), new Vector2(10f, 10f));
            puffThree.raycastTarget = false;
        }

        static Font CreateFallbackFont()
        {
            var candidates = new[] { "Noto Sans CJK SC", "Droid Sans Fallback", "Arial" };
            return Font.CreateDynamicFontFromOSFont(candidates, 48);
        }

        Font ResolveSourceFont()
        {
            var resolved = uiFont != null ? uiFont : Resources.Load<Font>("Fonts/NotoSansSC-Variable");
            return resolved != null ? resolved : CreateFallbackFont();
        }

        static TMP_FontAsset CreateFontInstance(Font source, string role)
        {
            if (source == null)
                return null;
            // A denser signed-distance atlas keeps Chinese strokes separated
            // after the world-space tile canvases are minified by the XR eye
            // target. Limit the 2048 atlas to tile labels; the already-large
            // top and dice panels keep their lighter 1024 atlases.
            var highResolution = string.Equals(role, "Tiles", System.StringComparison.Ordinal);
            var asset = TMP_FontAsset.CreateFontAsset(
                source,
                highResolution ? 96 : 64,
                highResolution ? 9 : 6,
                GlyphRenderMode.SDFAA,
                highResolution ? 2048 : 1024,
                highResolution ? 2048 : 1024,
                AtlasPopulationMode.Dynamic,
                true);
            if (asset != null)
                asset.name = source.name + "_" + role;
            return asset;
        }

        static Color TilePanelColor(TempleTileCategory category)
        {
            switch (category)
            {
                case TempleTileCategory.Offering: return new Color32(226, 183, 69, 255);
                // The source board assets serialize folder 44 (purple) as
                // Meditation and folder 42 (green) as Relic. Match the panel
                // to the brick's authored colour instead of enum declaration
                // order so every card and physical tile agree in the scene.
                case TempleTileCategory.Meditation: return new Color32(151, 102, 179, 255);
                case TempleTileCategory.Incense: return new Color32(210, 76, 61, 255);
                default: return new Color32(72, 162, 132, 255);
            }
        }
    }

    public sealed class TempleDicePromptAnchor : MonoBehaviour
    {
        public Camera targetCamera;
        public Transform diceTarget;
        public float boardSize = 1f;
        // Keep the action card visibly to the player's right of the die,
        // leaving the central shrine and die silhouette unobstructed.
        public float sideRatio = 0.24f;
        public float forwardRatio = 0.10f;
        public float verticalRatio = 0.11f;
        public float followSpeed = 8f;

        Vector3 diceTop;
        bool configured;
        Transform cachedRendererTarget;
        Renderer[] diceRenderers = System.Array.Empty<Renderer>();

        public void Configure(Transform dice, Camera camera, Vector3 fallbackCenter, float size)
        {
            diceTarget = dice;
            targetCamera = camera;
            boardSize = Mathf.Max(0.01f, size);
            CacheDiceRenderers();
            diceTop = diceTarget == null ? fallbackCenter : CalculateTop();
            configured = true;
            UpdatePose(true);
        }

        void LateUpdate()
        {
            if (targetCamera == null)
                targetCamera = Camera.main;
            if (!configured || targetCamera == null)
                return;
            UpdatePose(false);
        }

        void UpdatePose(bool immediate)
        {
            // The dice is re-grounded after leaving the title stage and moves
            // during the roll.  Re-reading its visual top each frame keeps this
            // action panel beside the central dice rather than at a stale point
            // under the scene.
            if (diceTarget != null)
                diceTop = CalculateTop();
            var toCamera = targetCamera.transform.position - diceTop;
            toCamera.y = 0f;
            if (toCamera.sqrMagnitude < 0.001f)
                toCamera = Vector3.back;
            toCamera.Normalize();
            var cameraRight = Vector3.Cross(toCamera, Vector3.up).normalized;
            var desiredPosition = diceTop + cameraRight * (boardSize * sideRatio) +
                toCamera * (boardSize * forwardRatio) + Vector3.up * (boardSize * verticalRatio);
            var blend = immediate ? 1f : 1f - Mathf.Exp(-followSpeed * Time.unscaledDeltaTime);
            transform.position = Vector3.Lerp(transform.position, desiredPosition, blend);

            transform.rotation = targetCamera.transform.rotation;
        }

        void CacheDiceRenderers()
        {
            cachedRendererTarget = diceTarget;
            diceRenderers = diceTarget == null
                ? System.Array.Empty<Renderer>()
                : diceTarget.GetComponentsInChildren<Renderer>();
        }

        Vector3 CalculateTop()
        {
            if (diceTarget != cachedRendererTarget)
                CacheDiceRenderers();
            if (diceTarget == null)
                return diceTop;

            var found = false;
            var bounds = default(Bounds);
            foreach (var renderer in diceRenderers)
            {
                if (renderer == null)
                    continue;
                if (!found)
                {
                    bounds = renderer.bounds;
                    found = true;
                }
                else
                    bounds.Encapsulate(renderer.bounds);
            }
            if (!found)
                return diceTarget.position;
            return new Vector3(bounds.center.x, bounds.max.y, bounds.center.z);
        }
    }

    /// <summary>Trigger-only target attached to a top status player card.</summary>
    public sealed class TemplePlayerCardHover : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
    {
        TempleGameHUD hud;
        int playerId;

        public void Configure(TempleGameHUD owner, int id)
        {
            hud = owner;
            playerId = id;
        }

        public void SetRayHovered(XRNode hand, bool hovered)
        {
            hud?.SetPlayerCardHover(playerId, hand, hovered);
        }

        public void OnPointerEnter(PointerEventData eventData) => hud?.SetDesktopPlayerCardHover(playerId, true);
        public void OnPointerExit(PointerEventData eventData) => hud?.SetDesktopPlayerCardHover(playerId, false);

        void OnDisable()
        {
            hud?.SetPlayerCardHover(playerId, XRNode.LeftHand, false);
            hud?.SetPlayerCardHover(playerId, XRNode.RightHand, false);
            hud?.SetDesktopPlayerCardHover(playerId, false);
        }
    }

    /// <summary>
    /// Keeps the main status board in the tabletop world at a high authored anchor while
    /// turning its front toward the viewer. Position is intentionally never camera-relative.
    /// </summary>
    public sealed class TempleWorldStatusAnchor : MonoBehaviour
    {
        TempleXRTabletopRig rig;
        Camera targetCamera;
        Vector3 worldAnchor;

        public void Configure(TempleXRTabletopRig tabletopRig, Camera camera, Vector3 anchor)
        {
            rig = tabletopRig;
            targetCamera = camera;
            worldAnchor = anchor;
            ApplyPose();
        }

        void LateUpdate()
        {
            if (targetCamera == null)
                targetCamera = Camera.main;
            if (rig != null)
                worldAnchor = rig.GetWorldTablePoint(rig.statusBoardOffsetFromTabletop);
            ApplyPose();
        }

        void ApplyPose()
        {
            if (targetCamera == null)
                return;
            transform.position = worldAnchor;
            var towardCamera = targetCamera.transform.position - worldAnchor;
            if (towardCamera.sqrMagnitude > 0.001f)
                transform.rotation = Quaternion.LookRotation(towardCamera.normalized, Vector3.up);
        }
    }

    /// <summary>
    /// Keeps the intentionally small status strip fixed at the top centre of the
    /// player's view.  All primary interaction surfaces remain world-space.
    /// </summary>
    public sealed class TempleHeadLockedStatusAnchor : MonoBehaviour
    {
        Camera targetCamera;
        Vector3 localAnchor;

        public void Configure(Camera camera, Vector3 anchor)
        {
            targetCamera = camera;
            localAnchor = anchor;
            ApplyPose();
        }

        void LateUpdate()
        {
            if (targetCamera == null)
                targetCamera = Camera.main;
            ApplyPose();
        }

        void ApplyPose()
        {
            if (targetCamera == null)
                return;
            if (transform.parent != targetCamera.transform)
                transform.SetParent(targetCamera.transform, false);
            transform.localPosition = localAnchor;
            transform.localRotation = Quaternion.identity;
        }
    }

    public sealed class TempleTileInfoBillboard : MonoBehaviour
    {
        TempleBoardTile tile;
        Camera targetCamera;
        Vector3 boardCenter;
        float spacing;
        TMP_Text contentText;
        Image accentImage;
        Color tileColor;
        Canvas tileCanvas;
        int defaultSortingOrder;
        bool leftRayHovered;
        bool rightRayHovered;
        Collider aimCollider;
        TempleStartScreen startScreen;

        public void Configure(TempleBoardTile targetTile, Camera camera, Vector3 center, float tileSpacing, TMP_Text content, Image accent, Color color)
        {
            tile = targetTile;
            targetCamera = camera;
            boardCenter = center;
            spacing = Mathf.Max(0.1f, tileSpacing);
            contentText = content;
            accentImage = accent;
            tileColor = color;
            tileCanvas = GetComponent<Canvas>();
            if (tileCanvas != null)
            {
                tileCanvas.overrideSorting = true;
                defaultSortingOrder = tileCanvas.sortingOrder;
            }
            aimCollider = GetComponent<Collider>();
            startScreen = FindAnyObjectByType<TempleStartScreen>();
            if (aimCollider != null && startScreen != null && !startScreen.GameStarted)
                aimCollider.enabled = false;
            UpdatePose();
        }

        public void SetRayHovered(XRNode hand, bool hovered)
        {
            if (hand == XRNode.LeftHand)
                leftRayHovered = hovered;
            else if (hand == XRNode.RightHand)
                rightRayHovered = hovered;

            if (tileCanvas != null)
                tileCanvas.sortingOrder = leftRayHovered || rightRayHovered ? 240 : defaultSortingOrder;
        }

        public void SetContent(TempleTileInfoContent content)
        {
            if (contentText != null)
                contentText.text = $"<size=32><b>{content.title}</b></size>\n<size=21><b>{content.detail}</b></size>";
            if (accentImage != null)
                accentImage.color = tileColor;
        }

        void LateUpdate()
        {
            if (targetCamera == null)
                targetCamera = Camera.main;
            if (aimCollider != null && !aimCollider.enabled && (startScreen == null || startScreen.GameStarted))
                aimCollider.enabled = true;
            UpdatePose();
        }

        void UpdatePose()
        {
            if (tile == null || targetCamera == null)
                return;

            var tileCenter = tile.transform.position;
            var tileTop = tile.transform.position.y;
            var radialExtent = spacing * 0.5f;
            var hasBounds = tile.TryGetSurfaceBounds(out var bounds);
            if (hasBounds)
            {
                tileCenter = bounds.center;
                tileTop = bounds.max.y;
            }

            var outward = tileCenter - boardCenter;
            outward.y = 0f;
            if (outward.sqrMagnitude < 0.001f)
                outward = Vector3.forward;
            outward.Normalize();
            if (hasBounds)
                radialExtent = Mathf.Abs(outward.x) * bounds.extents.x + Mathf.Abs(outward.z) * bounds.extents.z;

            var tangent = Vector3.Cross(Vector3.up, outward).normalized;
            // Alternate neighbouring cards along the path and split them into
            // two shallow height bands. This keeps the ring ordered while
            // preventing the wide XR labels from stacking over each other.
            var lateralBand = tile.index % 3 - 1;
            var heightBand = tile.index % 2;
            transform.position = new Vector3(tileCenter.x, tileTop + spacing * (0.38f + heightBand * 0.16f), tileCenter.z)
                + outward * (radialExtent + spacing * (0.24f + heightBand * 0.10f))
                + tangent * (spacing * lateralBand * 0.20f);
            var direction = targetCamera.transform.position - transform.position;
            if (direction.sqrMagnitude > 0.001f)
                transform.rotation = Quaternion.LookRotation(-direction.normalized, targetCamera.transform.up);
        }
    }
}
