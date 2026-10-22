using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace TempleCircuit
{
    [Serializable]
    public sealed class TemplePlayerState
    {
        public int id;
        public string name;
        public bool human;
        public int cash;
        public int fame;
        public int position;
        public int rerolls;
        public bool bankrupt;
        public readonly List<int> properties = new List<int>();
        public readonly List<TempleTileCategory> completedSets = new List<TempleTileCategory>();
    }

    public struct TempleTileInfoContent
    {
        public string title;
        public string detail;
        public Color accent;

        public TempleTileInfoContent(string title, string detail, Color accent)
        {
            this.title = title;
            this.detail = detail;
            this.accent = accent;
        }
    }

    public sealed class TempleCircuitGame : MonoBehaviour
    {
        enum TileType { Start, Property, Chance, Festival, Tax, Bonus, Gate }
        enum Phase { Starting, AwaitRoll, HoldingDice, Rolling, DecideRoll, Moving, DecideProperty, DecideUpgrade, DecideFestival, DecideChance, Resolving, Finished }

        sealed class TileState
        {
            public string name;
            public TileType type;
            public TempleTileCategory category;
            public int price;
            public int rent;
            public int amount;
            public int owner = -1;
            public int level;
        }

        struct TileBlueprint
        {
            public readonly string name;
            public readonly TileType type;
            public readonly int price;
            public readonly int rent;
            public readonly int amount;

            public TileBlueprint(string name, TileType type, int price = 0, int rent = 0, int amount = 0)
            {
                this.name = name;
                this.type = type;
                this.price = price;
                this.rent = rent;
                this.amount = amount;
            }
        }

        public TempleBoardTile[] pathTiles;
        public TemplePawnFollower[] pawnFollowers;
        public TempleDiceInteractor dice;
        public TempleGameHUD hud;
        public TempleAudioManager audioManager;
        public GameObject[] ownershipFlagPrefabs;
        public Renderer[] ownershipWalls;
        [Min(1)] public int maxRounds = 16;
        [Min(1)] public int fameToWin = 8;
        [Min(0.05f)] public float moveStepSeconds = 0.34f;

        readonly List<TemplePlayerState> players = new List<TemplePlayerState>();
        readonly List<TileState> tiles = new List<TileState>();
        readonly List<GameObject> ownershipMarkers = new List<GameObject>();

        Phase phase;
        int activePlayer;
        int round;
        int pendingRoll;
        int decisionTileIndex = -1;
        bool rerolling;
        bool finished;
        TempleTileCategory trend;

        // EN: 你→You, 阿满→Ah Man, 小岚→Xiao Lan, 七仔→Qi Zai
        static readonly string[] PlayerNames = { "You", "Ah Man", "Xiao Lan", "Qi Zai" };
        static readonly Color[] PlayerColors =
        {
            new Color32(226, 183, 69, 255),
            new Color32(72, 162, 132, 255),
            new Color32(210, 76, 61, 255),
            new Color32(151, 102, 179, 255)
        };

        // EN tile names per coordinator table
        static readonly TileBlueprint[] Blueprints =
        {
            new TileBlueprint("Temple Gate Bell", TileType.Start),
            new TileBlueprint("Zen Stone Path", TileType.Property, 80, 18),
            new TileBlueprint("Pilgrims' Corridor", TileType.Property, 80, 18),
            new TileBlueprint("Ritual Vault", TileType.Property, 80, 18),
            new TileBlueprint("Lotus Lantern Pavilion", TileType.Property, 80, 18),
            new TileBlueprint("Prayer Slip", TileType.Chance),
            new TileBlueprint("Vegetarian Refectory", TileType.Property, 100, 22),
            new TileBlueprint("Merit Red Envelope", TileType.Bonus, amount: 40),
            new TileBlueprint("Lamp Offering Tower", TileType.Property, 100, 22),
            new TileBlueprint("Sutra Cloister", TileType.Property, 100, 22),
            new TileBlueprint("Incense Fund Fee", TileType.Tax, amount: 35),
            new TileBlueprint("Relic Pavilion", TileType.Property, 100, 22),
            new TileBlueprint("Prayer Slip", TileType.Chance),
            new TileBlueprint("Morning Bell Ceremony", TileType.Festival),
            new TileBlueprint("Prayer Incense Altar", TileType.Property, 120, 26),
            new TileBlueprint("Dedication Gate", TileType.Gate),
            new TileBlueprint("Wooden Fish Lodge", TileType.Property, 130, 28),
            new TileBlueprint("Meditation Hall", TileType.Property, 130, 28),
            new TileBlueprint("Prayer Slip", TileType.Chance),
            new TileBlueprint("Sutra Library", TileType.Property, 130, 28),
            new TileBlueprint("Collect Merit", TileType.Bonus, amount: 40),
            new TileBlueprint("Grand Ceremony", TileType.Festival),
            new TileBlueprint("Lamp Oil Fee", TileType.Tax, amount: 45),
            new TileBlueprint("Prayer Slip", TileType.Chance),
            new TileBlueprint("Dedication Gate", TileType.Gate),
            new TileBlueprint("Rejoicing in Merit", TileType.Bonus, amount: 45),
            new TileBlueprint("Lantern Ceremony", TileType.Festival),
            new TileBlueprint("Repair Fee", TileType.Tax, amount: 50),
            new TileBlueprint("Prayer Slip", TileType.Chance),
            new TileBlueprint("Merit of Giving", TileType.Bonus, amount: 50),
            new TileBlueprint("Water and Land Ceremony", TileType.Festival),
            new TileBlueprint("Prayer Slip", TileType.Chance)
        };

        public bool CanHoldDice => !finished && phase == Phase.AwaitRoll && players.Count > activePlayer && players[activePlayer].human;
        public IReadOnlyList<TemplePlayerState> Players => players;

        void Start()
        {
            pathTiles = pathTiles == null ? Array.Empty<TempleBoardTile>() : pathTiles.Where(t => t != null).OrderBy(t => t.index).ToArray();
            if (pathTiles.Length != Blueprints.Length)
            {
                Debug.LogError($"TempleCircuitGame requires exactly {Blueprints.Length} path tiles; found {pathTiles.Length}.", this);
                enabled = false;
                return;
            }

            if (hud == null)
                hud = GetComponent<TempleGameHUD>();
            if (audioManager == null)
                audioManager = GetComponent<TempleAudioManager>();
            if (dice != null)
            {
                dice.game = this;
                dice.SnapToSupportSurface();
                dice.CaptureRestPose();
            }
            var bounds = CalculateBoardBounds();
            hud.Build(this, bounds.center, Mathf.Max(bounds.size.x, bounds.size.z));
            ResetGame();
        }

        public void ResetGame()
        {
            StopAllCoroutines();
            dice?.CancelHold();
            ClearOwnershipMarkers();
            players.Clear();
            tiles.Clear();
            for (var i = 0; i < 4; i++)
            {
                players.Add(new TemplePlayerState
                {
                    id = i,
                    name = PlayerNames[i],
                    human = i == 0,
                    cash = 360,
                    fame = 0,
                    position = 0,
                    rerolls = i == 0 ? 2 : 1
                });
            }

            for (var i = 0; i < Blueprints.Length; i++)
            {
                var source = Blueprints[i];
                tiles.Add(new TileState
                {
                    name = source.name,
                    type = source.type,
                    category = pathTiles[i].category,
                    price = source.price,
                    rent = source.rent,
                    amount = source.amount
                });
            }

            activePlayer = 0;
            round = 1;
            pendingRoll = 0;
            decisionTileIndex = -1;
            rerolling = false;
            finished = false;
            trend = TempleTileCategory.Incense;
            phase = Phase.Starting;
            hud.BuildTileInfoCards(this, pathTiles, CalculateBoardBounds().center);
            hud.RefreshTileInfoCards(this);
            PlaceAllPawns();
            RefreshHud();
            // EN: 晨钟一响 / 四位行脚者同时入寺，香火巡游开始。
            hud.ShowResult("The Bell Rings", "Four pilgrims enter the temple together. The incense circuit begins.");
            StartCoroutine(BeginTurnAfter(0.8f));
        }

        IEnumerator BeginTurnAfter(float delay)
        {
            yield return new WaitForSeconds(delay);
            BeginTurn();
        }

        void BeginTurn()
        {
            if (finished)
                return;
            var player = players[activePlayer];
            if (player.bankrupt)
            {
                AdvanceTurn();
                return;
            }

            RefreshHud();
            if (player.human)
            {
                phase = Phase.AwaitRoll;
                hud.ShowRoll(player.name, rerolling);
            }
            else
            {
                phase = Phase.Rolling;
                StartCoroutine(BotTurnRoutine(player));
            }
        }

        public void NotifyDiceHoldStarted()
        {
            if (!CanHoldDice)
                return;
            phase = Phase.HoldingDice;
            hud.ShowDiceCharge(0f);
        }

        public void ReportDiceCharge(float strength)
        {
            if (phase == Phase.HoldingDice)
                hud.ShowDiceCharge(strength);
        }

        public void OnDiceHoldReleased(float holdSeconds)
        {
            if (phase != Phase.HoldingDice)
                return;
            phase = Phase.Rolling;
            hud.ShowRolling();
            var motionSeed = Mathf.RoundToInt(holdSeconds * 997f) + UnityEngine.Random.Range(0, 9973);
            var value = 1 + Mathf.Abs(motionSeed % 6);
            var power = Mathf.InverseLerp(0.45f, 2.2f, holdSeconds);
            dice.Roll(value, power, OnHumanDiceSettled);
        }

        void OnHumanDiceSettled(int value)
        {
            pendingRoll = value;
            var player = players[activePlayer];
            if (!rerolling && player.rerolls > 0)
            {
                phase = Phase.DecideRoll;
                // EN: body starts with "Move " so FormatChoiceBody will bold the first clause
                hud.ShowChoices($"Dice Settled: {value}", $"Move {value}, or use a Re-roll Slip to roll again.", "Accept Roll", "accept-roll", $"Re-roll Slip · {player.rerolls} left", "reroll");
                return;
            }
            rerolling = false;
            StartCoroutine(MoveAndResolveRoutine(player, value));
        }

        IEnumerator BotTurnRoutine(TemplePlayerState player)
        {
            // EN: {player.name}正在观望香火 / 对手正在盘算下一座院落。
            hud.ShowResult($"{player.name} Reads the Incense", "Your opponent is planning their next move.");
            yield return new WaitForSeconds(0.75f);
            var value = UnityEngine.Random.Range(1, 7);
            var done = false;
            dice.Roll(value, 0.55f, rolled => { value = rolled; done = true; });
            yield return new WaitUntil(() => done);
            if (ShouldBotReroll(player, value))
            {
                player.rerolls--;
                // EN: {player.name}使用改骰签 / 嫌 {value} 步不合心意，重新投掷。
                hud.ShowResult($"{player.name} Uses a Re-roll Slip", $"Discarded roll of {value}, rolling again.");
                yield return new WaitForSeconds(0.45f);
                done = false;
                value = UnityEngine.Random.Range(1, 7);
                dice.Roll(value, 0.65f, rolled => { value = rolled; done = true; });
                yield return new WaitUntil(() => done);
            }
            yield return MoveAndResolveRoutine(player, value);
        }

        public void HandleAction(string action)
        {
            if (string.IsNullOrEmpty(action))
                return;
            if (action == "restart")
            {
                ResetGame();
                return;
            }
            if (action == "dismiss-win")
            {
                // EN: 本局已圆满 / 可以继续环绕棋盘查看各座院落。
                hud.ShowResult("The Board Is Done", "You may continue to view the properties.");
                return;
            }
            if (finished || players.Count == 0 || !players[activePlayer].human)
                return;

            switch (action)
            {
                case "accept-roll" when phase == Phase.DecideRoll:
                    rerolling = false;
                    StartCoroutine(MoveAndResolveRoutine(players[activePlayer], pendingRoll));
                    break;
                case "reroll" when phase == Phase.DecideRoll && players[activePlayer].rerolls > 0:
                    players[activePlayer].rerolls--;
                    rerolling = true;
                    phase = Phase.AwaitRoll;
                    RefreshHud();
                    hud.ShowRoll(players[activePlayer].name, true);
                    break;
                case "buy" when phase == Phase.DecideProperty:
                    Purchase(players[activePlayer], decisionTileIndex);
                    // EN: 新院落入册 / 「{name}」现在归你。
                    hud.ShowResult("Property Claimed", $"\"{tiles[decisionTileIndex].name}\" is now yours.");
                    StartCoroutine(FinishTurnRoutine());
                    break;
                case "skip-buy" when phase == Phase.DecideProperty:
                    // EN: 现金留在手中 / 院落仍属寺院，其他人之后仍可修下。
                    hud.ShowResult("Kept Your Gold", "The property remains available for others.");
                    StartCoroutine(FinishTurnRoutine());
                    break;
                case "upgrade" when phase == Phase.DecideUpgrade:
                    Upgrade(players[activePlayer], decisionTileIndex);
                    // EN: 檐角又添了一层 / 「{name}」修缮完成。
                    hud.ShowResult("Upgrade Complete", $"\"{tiles[decisionTileIndex].name}\" has been upgraded.");
                    StartCoroutine(FinishTurnRoutine());
                    break;
                case "skip-upgrade" when phase == Phase.DecideUpgrade:
                    // EN: 今日先守住现金 / 院落维持现状。
                    hud.ShowResult("Kept Your Gold", "The property stays as-is.");
                    StartCoroutine(FinishTurnRoutine());
                    break;
                case "festival-bold" when phase == Phase.DecideFestival:
                    ApplyFestival(players[activePlayer], true);
                    StartCoroutine(FinishTurnRoutine());
                    break;
                case "festival-safe" when phase == Phase.DecideFestival:
                    ApplyFestival(players[activePlayer], false);
                    StartCoroutine(FinishTurnRoutine());
                    break;
                case "chance-known" when phase == Phase.DecideChance && players[activePlayer].cash >= 35:
                    ResolveKnownChance(players[activePlayer]);
                    StartCoroutine(FinishTurnRoutine());
                    break;
                case "chance-draw" when phase == Phase.DecideChance:
                    StartCoroutine(ResolveRandomChanceRoutine(players[activePlayer]));
                    break;
            }
        }

        IEnumerator MoveAndResolveRoutine(TemplePlayerState player, int steps)
        {
            phase = Phase.Moving;
            pendingRoll = 0;
            // EN: {player.name}前进 {steps} 格 / 棋子正沿寺院外侧彩砖行走。
            hud.ShowResult($"{player.name} Moves {steps} Space{(steps == 1 ? "" : "s")}", "Moving along the temple circuit.");
            for (var step = 0; step < steps; step++)
            {
                var next = (player.position + 1) % pathTiles.Length;
                if (next == 0)
                    player.cash += 60;
                yield return MovePawnOneStep(player, next);
                player.position = next;
                RefreshHud();
            }
            yield return new WaitForSeconds(0.35f);
            yield return ResolveTileRoutine(player);
        }

        IEnumerator MovePawnOneStep(TemplePlayerState player, int next)
        {
            var follower = pawnFollowers[player.id];
            audioManager?.PlayPawnStep();
            follower.follow = false;
            var start = follower.transform.position;
            var duration = Mathf.Max(0.05f, moveStepSeconds);
            for (var elapsed = 0f; elapsed < duration; elapsed += Time.deltaTime)
            {
                var p = Mathf.Clamp01(elapsed / duration);
                var target = pathTiles[next].GetStandingPoint(player.id);
                var position = Vector3.Lerp(start, target, Mathf.SmoothStep(0f, 1f, p));
                position.y += Mathf.Sin(p * Mathf.PI) * pathTiles[next].EstimateSpacing() * 0.22f;
                follower.transform.position = position;
                follower.SetJumpStretch(p);
                yield return null;
            }
            follower.ResetJumpStretch();
            follower.tile = pathTiles[next];
            follower.follow = true;
        }

        IEnumerator ResolveTileRoutine(TemplePlayerState player)
        {
            phase = Phase.Resolving;
            var tile = tiles[player.position];
            decisionTileIndex = player.position;
            // EN: 停在「{tile.name}」
            hud.ShowResult($"Landed on {tile.name}", DescribeTile(tile));
            yield return new WaitForSeconds(0.6f);

            switch (tile.type)
            {
                case TileType.Property:
                    yield return ResolvePropertyRoutine(player, player.position);
                    break;
                case TileType.Chance:
                    if (player.human)
                    {
                        phase = Phase.DecideChance;
                        // EN: 山门递来两支签 / 明签稳定获取功德；免费抽签则会当场结算随机结果。
                        // EN: 35 金买明签 → Buy Revealed Slip · 35G
                        // EN: 免费抽祈愿签 → Draw a Free Prayer Slip
                        hud.ShowChoices("Two Slips at the Gate", "A Revealed Slip gives steady merit; a free draw resolves a random outcome on the spot.", "Buy Revealed Slip · 35G", "chance-known", "Draw a Free Prayer Slip", "chance-draw", player.cash >= 35);
                    }
                    else if (player.cash >= 120 && UnityEngine.Random.value < 0.65f)
                    {
                        ResolveKnownChance(player);
                        yield return FinishTurnRoutine();
                    }
                    else
                        yield return ResolveRandomChanceRoutine(player);
                    break;
                case TileType.Festival:
                    if (player.cash < 30)
                    {
                        player.cash += 20;
                        // EN: 没主坛，也没空手 / 协助收场，获得 20 金。
                        hud.ShowResult("Can't Lead, But Not Empty-Handed", "Assisted and received 20G.");
                        yield return FinishTurnRoutine();
                    }
                    else if (player.human)
                    {
                        phase = Phase.DecideFestival;
                        // EN: 法会开坛，要不要搏一把？ / 冒险有 65% 概率获得 80 金与 2 功德；稳妥则固定获得 30 金。
                        // EN: 押 30 金主坛 → Stake 30G to Lead
                        // EN: 协助法会得 30 金 → Assist for 30G
                        hud.ShowChoices("The Ceremony Begins — Take the Gamble?", "Bold: 65% chance of 80G and 2 Merit; Safe: guaranteed 30G.", "Stake 30G to Lead", "festival-bold", "Assist for 30G", "festival-safe");
                    }
                    else
                    {
                        ApplyFestival(player, player.cash >= 90 && UnityEngine.Random.value < 0.62f);
                        yield return FinishTurnRoutine();
                    }
                    break;
                case TileType.Tax:
                    ChargeBank(player, tile.amount);
                    // EN: 公账递到院前 / 支付 {n} 金。
                    hud.ShowResult("Temple Fund Collected", $"Paid {Mathf.Min(tile.amount, player.cash + tile.amount)}G.");
                    if (!finished)
                        yield return FinishTurnRoutine();
                    break;
                case TileType.Bonus:
                    player.cash += tile.amount;
                    RefreshHud();
                    // EN: 收到一封功德帖 / 现金增加 {n} 金。
                    hud.ShowResult("Merit Offering Received", $"Cash increased by {tile.amount}G.");
                    yield return FinishTurnRoutine();
                    break;
                case TileType.Gate:
                    player.cash += 35;
                    player.rerolls++;
                    RefreshHud();
                    // EN: 回向门把好运交到手里 / 35 金入袋，再添一张改骰签。
                    hud.ShowResult("The Dedication Gate Turns Luck Your Way", "35G gained, plus one Re-roll Slip.");
                    yield return FinishTurnRoutine();
                    break;
                default:
                    // EN: 山门钟声回荡 / 继续下一位行脚者的回合。
                    hud.ShowResult("Temple Gate Bell Rings", "On to the next pilgrim's turn.");
                    yield return FinishTurnRoutine();
                    break;
            }
        }

        IEnumerator ResolvePropertyRoutine(TemplePlayerState player, int tileIndex)
        {
            var tile = tiles[tileIndex];
            if (tile.owner < 0)
            {
                if (player.cash < tile.price)
                {
                    // EN: 院落擦肩而过 / 修下「{name}」需要 {price} 金。
                    hud.ShowResult("Short on Gold", $"Needs {tile.price}G to acquire {tile.name}.");
                    yield return FinishTurnRoutine();
                }
                else if (player.human)
                {
                    phase = Phase.DecideProperty;
                    // EN: 把「{name}」收入功德簿？ / {price} 金修下，基础收益 {rent} 金；当前香火流向加成会翻倍。
                    // EN: 收入 · {price} 金 → Acquire · {price}G
                    // EN: 留住现金 → Skip
                    hud.ShowChoices($"Acquire {tile.name}?", $"{tile.price}G to acquire. Base rent: {tile.rent}G; current incense bonus doubles it.", $"Acquire · {tile.price}G", "buy", "Skip", "skip-buy");
                }
                else
                {
                    if (ShouldBotBuy(player, tile))
                        Purchase(player, tileIndex);
                    yield return FinishTurnRoutine();
                }
                yield break;
            }

            if (tile.owner == player.id)
            {
                var cost = UpgradeCost(tile);
                if (tile.level >= 2 || player.cash < cost)
                {
                    // EN: 回到自己的院落 / 这座院落已经修缮到满级。/ 修缮需要 {cost} 金。
                    hud.ShowResult($"Back to {Poss(player.name)} Property", tile.level >= 2 ? "This property is fully upgraded." : $"Upgrade costs {cost}G.");
                    yield return FinishTurnRoutine();
                }
                else if (player.human)
                {
                    phase = Phase.DecideUpgrade;
                    // EN: 给「{name}」添一层檐角？ / 花 {cost} 金修缮，收益将从 {rent} 金升至 {newRent} 金。
                    // EN: 修缮 · {cost} 金 → Upgrade · {cost}G
                    // EN: 暂不修缮 → Skip
                    hud.ShowChoices($"Upgrade {tile.name}?", $"Spend {cost}G. Rent rises from {CalculateRent(tile)}G to {CalculateRent(tile, tile.level + 1)}G.", $"Upgrade · {cost}G", "upgrade", "Skip", "skip-upgrade");
                }
                else
                {
                    if (ShouldBotUpgrade(player, tile, cost))
                        Upgrade(player, tileIndex);
                    yield return FinishTurnRoutine();
                }
                yield break;
            }

            var owner = players[tile.owner];
            var rent = CalculateRent(tile);
            var paid = Mathf.Min(player.cash, rent);
            player.cash -= paid;
            owner.cash += paid;
            // EN: ，本轮香火加成已翻倍 → , incense bonus doubled this round
            var doubled = tile.category == trend ? ", incense bonus doubled this round" : string.Empty;
            // EN: 香客走进别人的院落 / 向 {owner.name} 支付 {paid} 金{doubled}。
            // EN: if owner is "You" (player 0) use "you" (lowercase object pronoun)
            var ownerRef = owner.name == "You" ? "you" : owner.name;
            hud.ShowResult("Visiting Another's Property", $"Paid {paid}G to {ownerRef}{doubled}.");
            if (paid < rent)
                Bankrupt(player);
            RefreshHud();
            if (!finished)
                yield return FinishTurnRoutine();
        }

        void Purchase(TemplePlayerState player, int tileIndex)
        {
            var tile = tiles[tileIndex];
            player.cash -= tile.price;
            tile.owner = player.id;
            player.properties.Add(tileIndex);
            audioManager?.PlayClaim();
            UpdateOwnershipMarker(tileIndex);
            CheckCompletedSet(player, tile.category);
            RefreshHud();
        }

        void Upgrade(TemplePlayerState player, int tileIndex)
        {
            var tile = tiles[tileIndex];
            player.cash -= UpgradeCost(tile);
            tile.level++;
            audioManager?.PlayClaim();
            UpdateOwnershipMarker(tileIndex);
            RefreshHud();
        }

        void ApplyFestival(TemplePlayerState player, bool bold)
        {
            if (!bold)
            {
                player.cash += 30;
                // EN: 稳妥也是本事 / 忙完一轮，30 金落袋。
                hud.ShowResult("Safe and Steady", "A solid 30G for your effort.");
            }
            else
            {
                player.cash -= 30;
                if (UnityEngine.Random.value < 0.65f)
                {
                    player.cash += 80;
                    player.fame += 2;
                    // EN: 满寺香客都记住了你的法号 / 净赚 50 金，功德 +2。
                    hud.ShowResult("The Temple Remembers Your Name", "Net 50G gained. Merit +2.");
                }
                else
                    // EN: 香火暂歇 / 法会遇上冷场，损失 30 金押金。
                    hud.ShowResult("The Incense Fades", "The ceremony fell flat. Lost your 30G stake.");
            }
            RefreshHud();
        }

        void ResolveKnownChance(TemplePlayerState player)
        {
            player.cash -= 35;
            player.fame++;
            player.rerolls++;
            RefreshHud();
            // EN: 这次不把修行交给运气 / 功德 +1，改骰签 +1。
            hud.ShowResult("Practice Without Relying on Luck", "Merit +1, Re-roll Slip +1.");
        }

        IEnumerator ResolveRandomChanceRoutine(TemplePlayerState player)
        {
            phase = Phase.Resolving;
            var eventIndex = UnityEngine.Random.Range(0, 8);
            string result;
            switch (eventIndex)
            {
                case 0:
                    var count = player.properties.Count(index => tiles[index].category == trend);
                    var gain = 20 + count * 20;
                    player.cash += gain;
                    // EN: 香客循着本轮香火而来，获得 {gain} 金。
                    result = $"Pilgrims follow the incense. Gained {gain}G.";
                    break;
                case 1:
                    ChargeBank(player, 25);
                    // EN: 一场短雨打湿经幡，损失 25 金。
                    result = "A brief rain wets the prayer flags. Lost 25G.";
                    break;
                case 2:
                    player.fame++;
                    player.cash += 20;
                    // EN: 法号传到山下，获得 20 金与 1 功德。
                    result = "Your dharma name spreads down the mountain. Gained 20G and 1 Merit.";
                    break;
                case 3:
                    player.rerolls++;
                    // EN: 拾到一张未曾启用的改骰签。
                    result = "Found an unused Re-roll Slip.";
                    break;
                case 4:
                    var total = 0;
                    foreach (var other in players)
                    {
                        if (other.id == player.id || other.bankrupt || other.cash < 10)
                            continue;
                        other.cash -= 10;
                        total += 10;
                    }
                    player.cash += total;
                    // EN: 邻院合掌送来 {total} 金香火。
                    result = $"Neighboring halls send {total}G in offerings.";
                    break;
                case 5:
                    var fee = player.properties.Count * 12;
                    ChargeBank(player, fee);
                    // EN: 寺院巡检收走 {fee} 金。/ 你还没有院落，顺利通过巡检。
                    result = fee > 0 ? $"Temple inspectors collected {fee}G." : "No properties owned — inspection passed.";
                    break;
                case 6:
                    if (UnityEngine.Random.value < 0.5f)
                    {
                        player.fame += 2;
                        // EN: 即兴说法大获成功，功德 +2。
                        result = "Impromptu sermon was a hit. Merit +2.";
                    }
                    else
                    {
                        ChargeBank(player, 30);
                        // EN: 即兴说法冷场，临时法坛花掉 30 金。
                        result = "The sermon fell flat. Spent 30G on the impromptu altar.";
                    }
                    break;
                default:
                    var back = (player.position - 2 + pathTiles.Length) % pathTiles.Length;
                    yield return MovePawnOneStep(player, (player.position - 1 + pathTiles.Length) % pathTiles.Length);
                    yield return MovePawnOneStep(player, back);
                    player.position = back;
                    // EN: 回向小径把你带回两格，落地不触发事件。
                    result = "The winding path carries you back two spaces. No effect on landing.";
                    break;
            }
            RefreshHud();
            // EN: 香客递来一张祈愿签
            hud.ShowResult("A Prayer Slip is Drawn", result);
            if (!finished)
                yield return FinishTurnRoutine();
        }

        IEnumerator FinishTurnRoutine()
        {
            phase = Phase.Resolving;
            RefreshHud();
            var fameWinner = players.FirstOrDefault(p => !p.bankrupt && p.fame >= fameToWin);
            if (fameWinner != null)
            {
                // EN: 功德达到 {fameToWin}，提前圆满。
                EndGame(fameWinner, $"Reached {fameToWin} Merit. Victory!");
                yield break;
            }
            yield return new WaitForSeconds(1.05f);
            AdvanceTurn();
        }

        void AdvanceTurn()
        {
            if (finished)
                return;
            activePlayer = (activePlayer + 1) % players.Count;
            rerolling = false;
            if (activePlayer == 0)
            {
                round++;
                if (round > maxRounds)
                {
                    var winner = players.Where(p => !p.bankrupt).OrderByDescending(PlayerScore).First();
                    // EN: {maxRounds} 轮巡游结束，按现金、院落价值与功德结算。
                    EndGame(winner, $"{maxRounds} rounds complete. Final score: cash, property value, and merit.");
                    return;
                }
                var choices = Enum.GetValues(typeof(TempleTileCategory)).Cast<TempleTileCategory>().Where(c => c != trend).ToArray();
                trend = choices[UnityEngine.Random.Range(0, choices.Length)];
            }
            BeginTurn();
        }

        void EndGame(TemplePlayerState winner, string reason)
        {
            finished = true;
            phase = Phase.Finished;
            RefreshHud();
            hud.ShowWinner(winner.name, reason + $"  Final score: {PlayerScore(winner)}.");
        }

        int PlayerScore(TemplePlayerState player)
        {
            if (player.bankrupt)
                return 0;
            var propertyValue = player.properties.Sum(i => tiles[i].price + tiles[i].level * UpgradeCost(tiles[i]));
            return player.cash + propertyValue + player.fame * 70;
        }

        bool ShouldBotReroll(TemplePlayerState player, int value)
        {
            if (player.rerolls <= 0)
                return false;
            var target = tiles[(player.position + value) % tiles.Count];
            return (value <= 2 && round >= 5) || (target.type == TileType.Tax && UnityEngine.Random.value > 0.22f);
        }

        bool ShouldBotBuy(TemplePlayerState player, TileState tile)
        {
            var ownedInCategory = player.properties.Count(i => tiles[i].category == tile.category);
            var reserve = 55 + Mathf.Max(0, maxRounds - round) * 2;
            return player.cash >= tile.price && (player.cash - tile.price >= reserve || ownedInCategory >= 2 || tile.category == trend);
        }

        bool ShouldBotUpgrade(TemplePlayerState player, TileState tile, int cost)
        {
            return tile.level < 2 && player.cash - cost >= 45 && (tile.category == trend || player.completedSets.Contains(tile.category) || round >= maxRounds - 4);
        }

        int CalculateRent(TileState tile, int level = -1)
        {
            if (level < 0)
                level = tile.level;
            return tile.rent * (level + 1) * (tile.category == trend ? 2 : 1);
        }

        static int UpgradeCost(TileState tile) => Mathf.RoundToInt((tile.price * 0.5f + tile.level * 15f) / 5f) * 5;

        /// <summary>
        /// Returns the possessive determiner for a player name.
        /// "You" → "Your"; all others → "{name}'s".
        /// </summary>
        static string Poss(string name) => name == "You" ? "Your" : name + "'s";

        void CheckCompletedSet(TemplePlayerState player, TempleTileCategory category)
        {
            var categoryTiles = tiles.Select((tile, index) => new { tile, index }).Where(x => x.tile.type == TileType.Property && x.tile.category == category).ToArray();
            if (categoryTiles.Length == 0 || player.completedSets.Contains(category) || categoryTiles.Any(x => x.tile.owner != player.id))
                return;
            player.completedSets.Add(category);
            player.fame += 2;
            // EN: 集齐「{CategoryName(category)}」/ 功德 +2。 → Set Complete: X / Merit +2.
            hud.ShowResult($"Set Complete: {CategoryName(category)}", "Merit +2.");
        }

        void ChargeBank(TemplePlayerState player, int amount)
        {
            if (amount <= 0)
                return;
            var paid = Mathf.Min(player.cash, amount);
            player.cash -= paid;
            if (paid < amount)
                Bankrupt(player);
            RefreshHud();
        }

        void Bankrupt(TemplePlayerState player)
        {
            player.bankrupt = true;
            player.cash = 0;
            foreach (var index in player.properties.ToArray())
            {
                tiles[index].owner = -1;
                tiles[index].level = 0;
                UpdateOwnershipMarker(index);
            }
            player.properties.Clear();
            var alive = players.Where(p => !p.bankrupt).ToArray();
            if (alive.Length == 1)
                // EN: 其余行脚者都已离寺，最后守住香火的人获胜。
                EndGame(alive[0], "All other pilgrims have left the temple. Last one standing wins.");
        }

        void UpdateOwnershipMarker(int tileIndex)
        {
            var markerName = $"OwnerMarker_{tileIndex:00}";
            var existing = ownershipMarkers.Find(item => item != null && item.name == markerName);
            if (existing != null)
            {
                ownershipMarkers.Remove(existing);
                existing.SetActive(false);
                Destroy(existing);
            }
            var tile = tiles[tileIndex];
            if (tile.owner < 0)
            {
                hud?.RefreshTileInfoCards(this);
                return;
            }

            var marker = new GameObject(markerName);
            var spacing = pathTiles[tileIndex].EstimateSpacing();
            if (pathTiles[tileIndex].TryGetSurfaceBounds(out var footprint))
                spacing = Mathf.Max(0.001f, Mathf.Min(footprint.size.x, footprint.size.z));
            var boardCenter = CalculateBoardBounds().center;
            var tileComponent = pathTiles[tileIndex];
            marker.transform.SetParent(transform, true);
            var flagAnchor = marker.AddComponent<TempleOwnershipFlagAnchor>();
            flagAnchor.Configure(tileComponent, boardCenter, spacing, ownershipWalls);

            var flagPrefab = ownershipFlagPrefabs != null && tile.owner < ownershipFlagPrefabs.Length ? ownershipFlagPrefabs[tile.owner] : null;
            if (flagPrefab != null)
            {
                var flag = Instantiate(flagPrefab, marker.transform);
                flag.name = $"OwnerFlag_Player_{tile.owner + 1}";
                flag.transform.localPosition = Vector3.zero;
                flag.transform.localRotation = Quaternion.Euler(-90f, 0f, 0f);
                flag.transform.localScale = Vector3.one;
                if (TryGetRendererBounds(flag, out var bounds) && bounds.size.y > 0.0001f)
                {
                    var targetHeight = spacing * (0.52f + tile.level * 0.055f);
                    flag.transform.localScale *= targetHeight / bounds.size.y;
                    if (TryGetRendererBounds(flag, out bounds))
                        flag.transform.position += Vector3.up * (marker.transform.position.y - bounds.min.y);
                }
                foreach (var collider in flag.GetComponentsInChildren<Collider>())
                    Destroy(collider);
                flagAnchor.SetFlagModel(flag.transform);
            }
            ownershipMarkers.Add(marker);
            hud?.RefreshTileInfoCards(this);
        }

        void ClearOwnershipMarkers()
        {
            foreach (var marker in ownershipMarkers)
                if (marker != null)
                    Destroy(marker);
            ownershipMarkers.Clear();
        }

        void PlaceAllPawns()
        {
            if (pawnFollowers == null)
                return;
            for (var i = 0; i < pawnFollowers.Length && i < players.Count; i++)
            {
                pawnFollowers[i].playerIndex = i;
                pawnFollowers[i].tile = pathTiles[0];
                pawnFollowers[i].follow = true;
                pawnFollowers[i].ResetJumpStretch();
                pawnFollowers[i].transform.position = pathTiles[0].GetStandingPoint(i);
            }
        }

        void RefreshHud()
        {
            if (hud != null && players.Count > 0)
                hud.RefreshPlayers(players, round, maxRounds, CategoryName(trend), activePlayer, fameToWin);
        }

        string DescribeTile(TileState tile)
        {
            switch (tile.type)
            {
                // EN: 可用 {price} 金修下的{category}院落。→ A {category} property available for {price}G.
                // EN: 当前归 {name} 所有，{level} 级。→ Currently owned by {name}. Level {level}.
                case TileType.Property: return tile.owner < 0 ? $"A {CategoryName(tile.category)} property available for {tile.price}G." : $"Currently owned by {players[tile.owner].name}. Level {tile.level}.";
                // EN: 一支祈愿签正在等待揭晓。
                case TileType.Chance: return "A prayer slip awaits.";
                // EN: 法会会在稳妥与冒险之间放大选择。
                case TileType.Festival: return "The ceremony amplifies your choice: safe or bold.";
                // EN: 需向寺院公账支付 {amount} 金。
                case TileType.Tax: return $"Pay {tile.amount}G to the temple fund.";
                // EN: 可获得 {amount} 金功德帖。
                case TileType.Bonus: return $"Receive a merit offering of {tile.amount}G.";
                // EN: 获得 35 金与一张改骰签。
                case TileType.Gate: return "Gain 35G and one Re-roll Slip.";
                // EN: 绕寺一周时会获得 60 金。
                default: return "Pass this point to receive 60G.";
            }
        }

        /// <summary>
        /// Returns a compact, stable summary for the player-name hover card.
        /// The UI can query this without reaching into the private tile state;
        /// malformed or stale indices are ignored instead of throwing.
        /// </summary>
        public string GetOwnedPropertySummary(int playerId)
        {
            if (playerId < 0 || playerId >= players.Count)
                return "No Properties Owned";

            var owned = players[playerId].properties;
            if (owned == null || owned.Count == 0)
                return "No Properties Owned";

            var lines = new List<string>(owned.Count);
            for (var i = 0; i < owned.Count; i++)
            {
                var tileIndex = owned[i];
                if (tileIndex < 0 || tileIndex >= tiles.Count)
                    continue;

                var tile = tiles[tileIndex];
                if (tile == null || tile.owner != playerId || string.IsNullOrEmpty(tile.name))
                    continue;

                // EN: {tile.name} · {level + 1}级 → {tile.name} · Lv.{level + 1}
                lines.Add($"{tile.name} · Lv.{tile.level + 1}");
            }

            return lines.Count == 0 ? "No Properties Owned" : string.Join("\n", lines);
        }

        public TempleTileInfoContent GetTileInfo(int index)
        {
            if (index < 0 || index >= tiles.Count)
                // EN: 未知格 → Unknown
                return new TempleTileInfoContent("Unknown", "", new Color32(220, 181, 91, 255));

            var tile = tiles[index];
            var accent = tile.owner >= 0 && tile.owner < PlayerColors.Length ? PlayerColors[tile.owner] : TileAccent(tile);
            switch (tile.type)
            {
                case TileType.Property:
                    // EN: 可购修行院落\n{price}金 · 租{rent} → Available\n{price}G · Rent {rent}G
                    // EN: {name}的院落\n{level+1}级 · 租{rent} → {name}'s Property\nLevel {level+1} · Rent {rent}G
                    var detail = tile.owner < 0
                        ? $"Available\n{tile.price}G · Rent {tile.rent}G"
                        : $"{Poss(players[tile.owner].name)} Property\nLevel {tile.level + 1} · Rent {CalculateRent(tile)}G";
                    return new TempleTileInfoContent(CompactTileName(tile.name), detail, accent);
                case TileType.Chance:
                    // EN: 祈签 → Slip; 抽取随机祈愿\n福祸皆可能 → Random prayer\nGood or ill fate
                    return new TempleTileInfoContent("Slip", "Random prayer\nGood or ill fate", accent);
                case TileType.Festival:
                    // EN: 法会收益抉择\n稳取或冒险 → Ceremony choice\nRisk or safety
                    return new TempleTileInfoContent(CompactTileName(tile.name), "Ceremony choice\nRisk or safety", accent);
                case TileType.Tax:
                    // EN: 寺院公共支出\n支付{amount}金 → Temple fund\nPay {amount}G
                    return new TempleTileInfoContent(CompactTileName(tile.name), $"Temple fund\nPay {tile.amount}G", accent);
                case TileType.Bonus:
                    // EN: 领取功德奖励\n获得{amount}金 → Merit reward\nGain {amount}G
                    return new TempleTileInfoContent(CompactTileName(tile.name), $"Merit reward\nGain {tile.amount}G", accent);
                case TileType.Gate:
                    // EN: 回向 → Dedicate; 回向转运奖励\n35金 · 改骰签 → Luck reward\n35G · Re-roll Slip
                    return new TempleTileInfoContent("Dedicate", "Luck reward\n35G · Re-roll Slip", accent);
                default:
                    // EN: 起点 → Start; 巡游起始位置\n过圈获得60金 → Circuit start\nPass: 60G
                    return new TempleTileInfoContent("Start", "Circuit start\nPass: 60G", accent);
            }
        }

        static string CompactTileName(string name)
        {
            // EN compact names per coordinator table (长名 / 短名)
            switch (name)
            {
                case "Zen Stone Path": return "Stone Path";
                case "Pilgrims' Corridor": return "Corridor";
                case "Ritual Vault": return "Ritual";
                case "Lotus Lantern Pavilion": return "Lotus Lamps";
                case "Vegetarian Refectory": return "Refectory";
                case "Merit Red Envelope": return "Envelope";
                case "Lamp Offering Tower": return "Lamp Tower";
                case "Sutra Cloister": return "Cloister";
                case "Incense Fund Fee": return "Incense Fee";
                case "Relic Pavilion": return "Relics";
                case "Morning Bell Ceremony": return "Morning Bell";
                case "Prayer Incense Altar": return "Altar";
                case "Wooden Fish Lodge": return "Wooden Fish";
                case "Meditation Hall": return "Meditation";
                case "Sutra Library": return "Library";
                case "Collect Merit": return "Collect";
                case "Grand Ceremony": return "Grand";
                case "Lamp Oil Fee": return "Oil Fee";
                case "Rejoicing in Merit": return "Rejoice";
                case "Lantern Ceremony": return "Lantern Rite";
                case "Repair Fee": return "Repairs";
                case "Merit of Giving": return "Giving";
                case "Water and Land Ceremony": return "Water & Land";
                default: return name.Length > 2 ? name : name;
            }
        }

        static Color TileAccent(TileState tile)
        {
            switch (tile.type)
            {
                case TileType.Chance: return new Color32(196, 80, 64, 255);
                case TileType.Festival: return new Color32(220, 181, 91, 255);
                case TileType.Tax: return new Color32(172, 67, 57, 255);
                case TileType.Bonus: return new Color32(72, 162, 132, 255);
                case TileType.Gate: return new Color32(151, 102, 179, 255);
                case TileType.Start: return new Color32(226, 183, 69, 255);
            }
            switch (tile.category)
            {
                case TempleTileCategory.Offering: return new Color32(214, 166, 71, 255);
                case TempleTileCategory.Meditation: return new Color32(87, 151, 123, 255);
                case TempleTileCategory.Incense: return new Color32(196, 88, 66, 255);
                default: return new Color32(116, 132, 174, 255);
            }
        }

        static string CategoryName(TempleTileCategory category)
        {
            // EN: 供灯阁→Lamp Pavilion, 禅修院→Meditation Court, 香火院→Incense Court, 法器阁→Ritual Pavilion
            switch (category)
            {
                case TempleTileCategory.Offering: return "Lamp Pavilion";
                case TempleTileCategory.Meditation: return "Meditation Court";
                case TempleTileCategory.Incense: return "Incense Court";
                default: return "Ritual Pavilion";
            }
        }

        Bounds CalculateBoardBounds()
        {
            var bounds = new Bounds(pathTiles[0].transform.position, Vector3.zero);
            foreach (var tile in pathTiles)
            {
                var renderers = tile.GetComponentsInChildren<Renderer>();
                foreach (var renderer in renderers)
                    bounds.Encapsulate(renderer.bounds);
            }
            return bounds;
        }

        static bool TryGetRendererBounds(GameObject root, out Bounds bounds)
        {
            var renderers = root.GetComponentsInChildren<Renderer>();
            if (renderers.Length == 0)
            {
                bounds = default;
                return false;
            }
            bounds = renderers[0].bounds;
            for (var i = 1; i < renderers.Length; i++)
                bounds.Encapsulate(renderers[i].bounds);
            return true;
        }
    }
}
