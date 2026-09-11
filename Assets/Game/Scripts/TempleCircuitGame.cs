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

        static readonly string[] PlayerNames = { "你", "阿满", "小岚", "七仔" };
        static readonly Color[] PlayerColors =
        {
            new Color32(226, 183, 69, 255),
            new Color32(72, 162, 132, 255),
            new Color32(210, 76, 61, 255),
            new Color32(151, 102, 179, 255)
        };

        static readonly TileBlueprint[] Blueprints =
        {
            new TileBlueprint("山门鸣钟", TileType.Start),
            new TileBlueprint("禅心石径", TileType.Property, 80, 18),
            new TileBlueprint("香客长廊", TileType.Property, 80, 18),
            new TileBlueprint("法器库", TileType.Property, 80, 18),
            new TileBlueprint("莲花灯阁", TileType.Property, 80, 18),
            new TileBlueprint("祈愿签", TileType.Chance),
            new TileBlueprint("素斋院", TileType.Property, 100, 22),
            new TileBlueprint("功德红包", TileType.Bonus, amount: 40),
            new TileBlueprint("供灯塔", TileType.Property, 100, 22),
            new TileBlueprint("经藏回廊", TileType.Property, 100, 22),
            new TileBlueprint("香火公费", TileType.Tax, amount: 35),
            new TileBlueprint("舍利阁", TileType.Property, 100, 22),
            new TileBlueprint("祈愿签", TileType.Chance),
            new TileBlueprint("晨钟法会", TileType.Festival),
            new TileBlueprint("祈愿香台", TileType.Property, 120, 26),
            new TileBlueprint("回向门", TileType.Gate),
            new TileBlueprint("木鱼舍", TileType.Property, 130, 28),
            new TileBlueprint("禅修堂", TileType.Property, 130, 28),
            new TileBlueprint("祈愿签", TileType.Chance),
            new TileBlueprint("藏经楼", TileType.Property, 130, 28),
            new TileBlueprint("收功德", TileType.Bonus, amount: 40),
            new TileBlueprint("大法会", TileType.Festival),
            new TileBlueprint("香油公费", TileType.Tax, amount: 45),
            new TileBlueprint("祈愿签", TileType.Chance),
            new TileBlueprint("回向门", TileType.Gate),
            new TileBlueprint("随喜功德", TileType.Bonus, amount: 45),
            new TileBlueprint("灯火法会", TileType.Festival),
            new TileBlueprint("修缮公费", TileType.Tax, amount: 50),
            new TileBlueprint("祈愿签", TileType.Chance),
            new TileBlueprint("布施功德", TileType.Bonus, amount: 50),
            new TileBlueprint("水陆法会", TileType.Festival),
            new TileBlueprint("祈愿签", TileType.Chance)
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
                // The title stage hides gameplay renderers until START.  Snap only
                // after they are restored so the dice sits on the central temple,
                // not at the temporary title-stage height beneath the board.
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
            hud.ShowResult("晨钟一响", "四位行脚者同时入寺，香火巡游开始。");
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
                hud.ShowChoices($"骰子落定：{value} 点", $"前进 {value} 格，或消耗一张改骰签重新投掷。", "按此点数行走", "accept-roll", $"改骰签 · 剩 {player.rerolls} 张", "reroll");
                return;
            }
            rerolling = false;
            StartCoroutine(MoveAndResolveRoutine(player, value));
        }

        IEnumerator BotTurnRoutine(TemplePlayerState player)
        {
            hud.ShowResult($"{player.name}正在观望香火", "对手正在盘算下一座院落。");
            yield return new WaitForSeconds(0.75f);
            var value = UnityEngine.Random.Range(1, 7);
            var done = false;
            dice.Roll(value, 0.55f, rolled => { value = rolled; done = true; });
            yield return new WaitUntil(() => done);
            if (ShouldBotReroll(player, value))
            {
                player.rerolls--;
                hud.ShowResult($"{player.name}使用改骰签", $"嫌 {value} 步不合心意，重新投掷。");
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
                hud.ShowResult("本局已圆满", "可以继续环绕棋盘查看各座院落。");
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
                    hud.ShowResult("新院落入册", $"「{tiles[decisionTileIndex].name}」现在归你。");
                    StartCoroutine(FinishTurnRoutine());
                    break;
                case "skip-buy" when phase == Phase.DecideProperty:
                    hud.ShowResult("现金留在手中", "院落仍属寺院，其他人之后仍可修下。");
                    StartCoroutine(FinishTurnRoutine());
                    break;
                case "upgrade" when phase == Phase.DecideUpgrade:
                    Upgrade(players[activePlayer], decisionTileIndex);
                    hud.ShowResult("檐角又添了一层", $"「{tiles[decisionTileIndex].name}」修缮完成。");
                    StartCoroutine(FinishTurnRoutine());
                    break;
                case "skip-upgrade" when phase == Phase.DecideUpgrade:
                    hud.ShowResult("今日先守住现金", "院落维持现状。");
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
            hud.ShowResult($"{player.name}前进 {steps} 格", "棋子正沿寺院外侧彩砖行走。");
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
            hud.ShowResult($"停在「{tile.name}」", DescribeTile(tile));
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
                        hud.ShowChoices("山门递来两支签", "明签稳定获取功德；免费抽签则会当场结算随机结果。", "35 金买明签", "chance-known", "免费抽祈愿签", "chance-draw", player.cash >= 35);
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
                        hud.ShowResult("没主坛，也没空手", "协助收场，获得 20 金。");
                        yield return FinishTurnRoutine();
                    }
                    else if (player.human)
                    {
                        phase = Phase.DecideFestival;
                        hud.ShowChoices("法会开坛，要不要搏一把？", "冒险有 65% 概率获得 80 金与 2 功德；稳妥则固定获得 30 金。", "押 30 金主坛", "festival-bold", "协助法会得 30 金", "festival-safe");
                    }
                    else
                    {
                        ApplyFestival(player, player.cash >= 90 && UnityEngine.Random.value < 0.62f);
                        yield return FinishTurnRoutine();
                    }
                    break;
                case TileType.Tax:
                    ChargeBank(player, tile.amount);
                    hud.ShowResult("公账递到院前", $"支付 {Mathf.Min(tile.amount, player.cash + tile.amount)} 金。");
                    if (!finished)
                        yield return FinishTurnRoutine();
                    break;
                case TileType.Bonus:
                    player.cash += tile.amount;
                    RefreshHud();
                    hud.ShowResult("收到一封功德帖", $"现金增加 {tile.amount} 金。");
                    yield return FinishTurnRoutine();
                    break;
                case TileType.Gate:
                    player.cash += 35;
                    player.rerolls++;
                    RefreshHud();
                    hud.ShowResult("回向门把好运交到手里", "35 金入袋，再添一张改骰签。");
                    yield return FinishTurnRoutine();
                    break;
                default:
                    hud.ShowResult("山门钟声回荡", "继续下一位行脚者的回合。");
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
                    hud.ShowResult("院落擦肩而过", $"修下「{tile.name}」需要 {tile.price} 金。");
                    yield return FinishTurnRoutine();
                }
                else if (player.human)
                {
                    phase = Phase.DecideProperty;
                    hud.ShowChoices($"把「{tile.name}」收入功德簿？", $"{tile.price} 金修下，基础收益 {tile.rent} 金；当前香火流向加成会翻倍。", $"收入 · {tile.price} 金", "buy", "留住现金", "skip-buy");
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
                    hud.ShowResult("回到自己的院落", tile.level >= 2 ? "这座院落已经修缮到满级。" : $"修缮需要 {cost} 金。");
                    yield return FinishTurnRoutine();
                }
                else if (player.human)
                {
                    phase = Phase.DecideUpgrade;
                    hud.ShowChoices($"给「{tile.name}」添一层檐角？", $"花 {cost} 金修缮，收益将从 {CalculateRent(tile)} 金升至 {CalculateRent(tile, tile.level + 1)} 金。", $"修缮 · {cost} 金", "upgrade", "暂不修缮", "skip-upgrade");
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
            var doubled = tile.category == trend ? "，本轮香火加成已翻倍" : string.Empty;
            hud.ShowResult("香客走进别人的院落", $"向 {owner.name} 支付 {paid} 金{doubled}。");
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
                hud.ShowResult("稳妥也是本事", "忙完一轮，30 金落袋。");
            }
            else
            {
                player.cash -= 30;
                if (UnityEngine.Random.value < 0.65f)
                {
                    player.cash += 80;
                    player.fame += 2;
                    hud.ShowResult("满寺香客都记住了你的法号", "净赚 50 金，功德 +2。");
                }
                else
                    hud.ShowResult("香火暂歇", "法会遇上冷场，损失 30 金押金。");
            }
            RefreshHud();
        }

        void ResolveKnownChance(TemplePlayerState player)
        {
            player.cash -= 35;
            player.fame++;
            player.rerolls++;
            RefreshHud();
            hud.ShowResult("这次不把修行交给运气", "功德 +1，改骰签 +1。");
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
                    result = $"香客循着本轮香火而来，获得 {gain} 金。";
                    break;
                case 1:
                    ChargeBank(player, 25);
                    result = "一场短雨打湿经幡，损失 25 金。";
                    break;
                case 2:
                    player.fame++;
                    player.cash += 20;
                    result = "法号传到山下，获得 20 金与 1 功德。";
                    break;
                case 3:
                    player.rerolls++;
                    result = "拾到一张未曾启用的改骰签。";
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
                    result = $"邻院合掌送来 {total} 金香火。";
                    break;
                case 5:
                    var fee = player.properties.Count * 12;
                    ChargeBank(player, fee);
                    result = fee > 0 ? $"寺院巡检收走 {fee} 金。" : "你还没有院落，顺利通过巡检。";
                    break;
                case 6:
                    if (UnityEngine.Random.value < 0.5f)
                    {
                        player.fame += 2;
                        result = "即兴说法大获成功，功德 +2。";
                    }
                    else
                    {
                        ChargeBank(player, 30);
                        result = "即兴说法冷场，临时法坛花掉 30 金。";
                    }
                    break;
                default:
                    var back = (player.position - 2 + pathTiles.Length) % pathTiles.Length;
                    yield return MovePawnOneStep(player, (player.position - 1 + pathTiles.Length) % pathTiles.Length);
                    yield return MovePawnOneStep(player, back);
                    player.position = back;
                    result = "回向小径把你带回两格，落地不触发事件。";
                    break;
            }
            RefreshHud();
            hud.ShowResult("香客递来一张祈愿签", result);
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
                EndGame(fameWinner, $"功德达到 {fameToWin}，提前圆满。");
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
                    EndGame(winner, $"{maxRounds} 轮巡游结束，按现金、院落价值与功德结算。");
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
            hud.ShowWinner(winner.name, reason + $"  最终评分 {PlayerScore(winner)}。");
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

        void CheckCompletedSet(TemplePlayerState player, TempleTileCategory category)
        {
            var categoryTiles = tiles.Select((tile, index) => new { tile, index }).Where(x => x.tile.type == TileType.Property && x.tile.category == category).ToArray();
            if (categoryTiles.Length == 0 || player.completedSets.Contains(category) || categoryTiles.Any(x => x.tile.owner != player.id))
                return;
            player.completedSets.Add(category);
            player.fame += 2;
            hud.ShowResult($"集齐「{CategoryName(category)}」", "功德 +2。");
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
                EndGame(alive[0], "其余行脚者都已离寺，最后守住香火的人获胜。");
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
            // The general UI spacing has a 0.2-unit minimum. Flags must instead
            // fit the actual stone footprint after conversion to XR metres.
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
                case TileType.Property: return tile.owner < 0 ? $"可用 {tile.price} 金修下的{CategoryName(tile.category)}院落。" : $"当前归 {players[tile.owner].name} 所有，{tile.level} 级。";
                case TileType.Chance: return "一支祈愿签正在等待揭晓。";
                case TileType.Festival: return "法会会在稳妥与冒险之间放大选择。";
                case TileType.Tax: return $"需向寺院公账支付 {tile.amount} 金。";
                case TileType.Bonus: return $"可获得 {tile.amount} 金功德帖。";
                case TileType.Gate: return "获得 35 金与一张改骰签。";
                default: return "绕寺一周时会获得 60 金。";
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
                return "暂无已占领房产";

            var owned = players[playerId].properties;
            if (owned == null || owned.Count == 0)
                return "暂无已占领房产";

            var lines = new List<string>(owned.Count);
            for (var i = 0; i < owned.Count; i++)
            {
                var tileIndex = owned[i];
                if (tileIndex < 0 || tileIndex >= tiles.Count)
                    continue;

                var tile = tiles[tileIndex];
                if (tile == null || tile.owner != playerId || string.IsNullOrEmpty(tile.name))
                    continue;

                lines.Add($"{tile.name} · {tile.level + 1}级");
            }

            return lines.Count == 0 ? "暂无已占领房产" : string.Join("\n", lines);
        }

        public TempleTileInfoContent GetTileInfo(int index)
        {
            if (index < 0 || index >= tiles.Count)
                return new TempleTileInfoContent("未知格", "", new Color32(220, 181, 91, 255));

            var tile = tiles[index];
            var accent = tile.owner >= 0 && tile.owner < PlayerColors.Length ? PlayerColors[tile.owner] : TileAccent(tile);
            switch (tile.type)
            {
                case TileType.Property:
                    var detail = tile.owner < 0
                        ? $"可购修行院落\n{tile.price}金 · 租{tile.rent}"
                        : $"{players[tile.owner].name}的院落\n{tile.level + 1}级 · 租{CalculateRent(tile)}";
                    return new TempleTileInfoContent(CompactTileName(tile.name), detail, accent);
                case TileType.Chance:
                    return new TempleTileInfoContent("祈签", "抽取随机祈愿\n福祸皆可能", accent);
                case TileType.Festival:
                    return new TempleTileInfoContent(CompactTileName(tile.name), "法会收益抉择\n稳取或冒险", accent);
                case TileType.Tax:
                    return new TempleTileInfoContent(CompactTileName(tile.name), $"寺院公共支出\n支付{tile.amount}金", accent);
                case TileType.Bonus:
                    return new TempleTileInfoContent(CompactTileName(tile.name), $"领取功德奖励\n获得{tile.amount}金", accent);
                case TileType.Gate:
                    return new TempleTileInfoContent("回向", "回向转运奖励\n35金 · 改骰签", accent);
                default:
                    return new TempleTileInfoContent("起点", "巡游起始位置\n过圈获得60金", accent);
            }
        }

        static string CompactTileName(string name)
        {
            switch (name)
            {
                case "禅心石径": return "石径";
                case "香客长廊": return "长廊";
                case "法器库": return "法器";
                case "莲花灯阁": return "灯阁";
                case "素斋院": return "素斋";
                case "功德红包": return "功德";
                case "供灯塔": return "供灯";
                case "经藏回廊": return "经藏";
                case "香火公费": return "香费";
                case "舍利阁": return "舍利";
                case "晨钟法会": return "晨钟";
                case "祈愿香台": return "香台";
                case "木鱼舍": return "木鱼";
                case "禅修堂": return "禅修";
                case "藏经楼": return "藏经";
                case "收功德": return "收功";
                case "大法会": return "法会";
                case "香油公费": return "香油";
                case "随喜功德": return "随喜";
                case "灯火法会": return "灯火";
                case "修缮公费": return "修缮";
                case "布施功德": return "布施";
                case "水陆法会": return "水陆";
                default: return name.Length > 2 ? name.Substring(name.Length - 2) : name;
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
            switch (category)
            {
                case TempleTileCategory.Offering: return "供灯阁";
                case TempleTileCategory.Meditation: return "禅修院";
                case TempleTileCategory.Incense: return "香火院";
                default: return "法器阁";
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
