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

        // EN: player display names translated by meaning
        // You = human player, Aman = 阿满 (content/peaceful), Mist = 小岚 (little mountain mist), Seven = 七仔 (seven kid)
        static readonly string[] PlayerNames = { "You", "Aman", "Mist", "Seven" };
        static readonly Color[] PlayerColors =
        {
            new Color32(226, 183, 69, 255),
            new Color32(72, 162, 132, 255),
            new Color32(210, 76, 61, 255),
            new Color32(151, 102, 179, 255)
        };

        static readonly TileBlueprint[] Blueprints =
        {
            new TileBlueprint("Bell Gate", TileType.Start),
            new TileBlueprint("Zen Stone Path", TileType.Property, 80, 18),
            new TileBlueprint("Pilgrim Arcade", TileType.Property, 80, 18),
            new TileBlueprint("Sacred Relics Hall", TileType.Property, 80, 18),
            new TileBlueprint("Lotus Lamp Tower", TileType.Property, 80, 18),
            new TileBlueprint("Wish Lot", TileType.Chance),
            new TileBlueprint("Vegetarian Court", TileType.Property, 100, 22),
            new TileBlueprint("Merit Blessing", TileType.Bonus, amount: 40),
            new TileBlueprint("Offering Lighthouse", TileType.Property, 100, 22),
            new TileBlueprint("Scripture Gallery", TileType.Property, 100, 22),
            new TileBlueprint("Incense Levy", TileType.Tax, amount: 35),
            new TileBlueprint("Relic Pavilion", TileType.Property, 100, 22),
            new TileBlueprint("Wish Lot", TileType.Chance),
            new TileBlueprint("Morning Bell Ceremony", TileType.Festival),
            new TileBlueprint("Incense Altar", TileType.Property, 120, 26),
            new TileBlueprint("Dedication Gate", TileType.Gate),
            new TileBlueprint("Wooden Fish Cottage", TileType.Property, 130, 28),
            new TileBlueprint("Meditation Hall", TileType.Property, 130, 28),
            new TileBlueprint("Wish Lot", TileType.Chance),
            new TileBlueprint("Canon Tower", TileType.Property, 130, 28),
            new TileBlueprint("Merit Collection", TileType.Bonus, amount: 40),
            new TileBlueprint("Grand Ceremony", TileType.Festival),
            new TileBlueprint("Lamp Oil Levy", TileType.Tax, amount: 45),
            new TileBlueprint("Wish Lot", TileType.Chance),
            new TileBlueprint("Dedication Gate", TileType.Gate),
            new TileBlueprint("Sympathetic Merit", TileType.Bonus, amount: 45),
            new TileBlueprint("Lantern Ceremony", TileType.Festival),
            new TileBlueprint("Repair Levy", TileType.Tax, amount: 50),
            new TileBlueprint("Wish Lot", TileType.Chance),
            new TileBlueprint("Alms Merit", TileType.Bonus, amount: 50),
            new TileBlueprint("Water-Land Ceremony", TileType.Festival),
            new TileBlueprint("Wish Lot", TileType.Chance)
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
            hud.ShowResult("The Bell Rings", "Four pilgrims enter the temple together. The Incense Circuit begins.");
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
                hud.ShowChoices($"Dice Settled: {value}", $"Move {value} spaces, or spend a Re-roll Slip to roll again.", $"Move {value}", "accept-roll", $"Re-roll · {player.rerolls} Left", "reroll");
                return;
            }
            rerolling = false;
            StartCoroutine(MoveAndResolveRoutine(player, value));
        }

        IEnumerator BotTurnRoutine(TemplePlayerState player)
        {
            hud.ShowResult($"{player.name} Studies the Incense", "Your opponent plans their next move.");
            yield return new WaitForSeconds(0.75f);
            var value = UnityEngine.Random.Range(1, 7);
            var done = false;
            dice.Roll(value, 0.55f, rolled => { value = rolled; done = true; });
            yield return new WaitUntil(() => done);
            if (ShouldBotReroll(player, value))
            {
                player.rerolls--;
                hud.ShowResult($"{player.name} Uses a Re-roll Slip", $"Not happy with {value} spaces. Rolling again.");
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
                hud.ShowResult("This Game Is Complete", "Feel free to explore the board and view each courtyard.");
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
                    hud.ShowResult("Courtyard Registered", $"{tiles[decisionTileIndex].name} now belongs to you.");
                    StartCoroutine(FinishTurnRoutine());
                    break;
                case "skip-buy" when phase == Phase.DecideProperty:
                    hud.ShowResult("Keeping Your Cash", "The courtyard remains temple property. Others may still acquire it.");
                    StartCoroutine(FinishTurnRoutine());
                    break;
                case "upgrade" when phase == Phase.DecideUpgrade:
                    Upgrade(players[activePlayer], decisionTileIndex);
                    hud.ShowResult("Another Eave Added", $"{tiles[decisionTileIndex].name} has been upgraded.");
                    StartCoroutine(FinishTurnRoutine());
                    break;
                case "skip-upgrade" when phase == Phase.DecideUpgrade:
                    hud.ShowResult("Preserving Cash for Now", "The courtyard remains as is.");
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
            hud.ShowResult($"{player.name} Moves {steps} Spaces", "The pawn follows the colored tiles around the temple.");
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
                        hud.ShowChoices("The Gate Offers Two Lots", "A known lot gives steady Merit; a free draw resolves a random result.", "Buy Known Lot · 35G", "chance-known", "Draw Free Wish Lot", "chance-draw", player.cash >= 35);
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
                        hud.ShowResult("No Presiding, But Not Empty-Handed", "You helped wrap up the ceremony. Gain 20 Gold.");
                        yield return FinishTurnRoutine();
                    }
                    else if (player.human)
                    {
                        phase = Phase.DecideFestival;
                        hud.ShowChoices("A Ceremony Opens — Dare You Preside?", "Bold: 65% chance for 80 Gold & +2 Merit. Safe: guaranteed 30 Gold.", "Preside · Risk 30 Gold", "festival-bold", "Assist · Gain 30 Gold", "festival-safe");
                    }
                    else
                    {
                        ApplyFestival(player, player.cash >= 90 && UnityEngine.Random.value < 0.62f);
                        yield return FinishTurnRoutine();
                    }
                    break;
                case TileType.Tax:
                    ChargeBank(player, tile.amount);
                    hud.ShowResult("The Temple Ledger Arrives", $"Pay {Mathf.Min(tile.amount, player.cash + tile.amount)} Gold.");
                    if (!finished)
                        yield return FinishTurnRoutine();
                    break;
                case TileType.Bonus:
                    player.cash += tile.amount;
                    RefreshHud();
                    hud.ShowResult("A Merit Scroll Arrives", $"You receive {tile.amount} Gold.");
                    yield return FinishTurnRoutine();
                    break;
                case TileType.Gate:
                    player.cash += 35;
                    player.rerolls++;
                    RefreshHud();
                    hud.ShowResult("The Dedication Gate Bestows Fortune", "Gain 35 Gold and one Re-roll Slip.");
                    yield return FinishTurnRoutine();
                    break;
                default:
                    hud.ShowResult("The Bell Echoes at the Gate", "Next pilgrim's turn begins.");
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
                    hud.ShowResult("Courtyard Passes By", $"Acquiring {tile.name} costs {tile.price} Gold.");
                    yield return FinishTurnRoutine();
                }
                else if (player.human)
                {
                    phase = Phase.DecideProperty;
                    hud.ShowChoices($"Add {tile.name} to the Merit Register?", $"Cost: {tile.price} Gold. Base rent: {tile.rent} Gold. Current incense bonus doubles rent.", $"Acquire · {tile.price}G", "buy", "Keep Cash", "skip-buy");
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
                    hud.ShowResult("Back to Your Own Courtyard", tile.level >= 2 ? "This courtyard is already fully upgraded." : $"Upgrading costs {cost} Gold.");
                    yield return FinishTurnRoutine();
                }
                else if (player.human)
                {
                    phase = Phase.DecideUpgrade;
                    hud.ShowChoices($"Add Another Eave to {tile.name}?", $"Spend {cost} Gold to upgrade. Rent rises from {CalculateRent(tile)} to {CalculateRent(tile, tile.level + 1)} Gold.", $"Upgrade · {cost}G", "upgrade", "Not Now", "skip-upgrade");
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
            var doubled = tile.category == trend ? " (incense bonus doubled)" : string.Empty;
            hud.ShowResult("A Pilgrim Enters Another's Courtyard", $"You pay {owner.name} {paid} Gold{doubled}.");
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
                hud.ShowResult("Steady Is Skilled Too", "You helped with the ceremony. 30 Gold secured.");
            }
            else
            {
                player.cash -= 30;
                if (UnityEngine.Random.value < 0.65f)
                {
                    player.cash += 80;
                    player.fame += 2;
                    hud.ShowResult("All Temple Pilgrims Remember Your Name", "Net gain: 50 Gold. +2 Merit.");
                }
                else
                    hud.ShowResult("The Incense Dims", "The ceremony fell flat. Lost 30 Gold deposit.");
            }
            RefreshHud();
        }

        void ResolveKnownChance(TemplePlayerState player)
        {
            player.cash -= 35;
            player.fame++;
            player.rerolls++;
            RefreshHud();
            hud.ShowResult("Leaving Nothing to Chance", "+1 Merit. +1 Re-roll Slip.");
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
                    result = $"Pilgrims follow the incense trend. Gain {gain} Gold.";
                    break;
                case 1:
                    ChargeBank(player, 25);
                    result = "A brief rain dampens the prayer flags. Lose 25 Gold.";
                    break;
                case 2:
                    player.fame++;
                    player.cash += 20;
                    result = "Your name echoes down the mountain. Gain 20 Gold and +1 Merit.";
                    break;
                case 3:
                    player.rerolls++;
                    result = "You find an unused Re-roll Slip.";
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
                    result = $"Neighboring courtyards offer {total} Gold in incense tribute.";
                    break;
                case 5:
                    var fee = player.properties.Count * 12;
                    ChargeBank(player, fee);
                    result = fee > 0 ? $"The temple inspector collects {fee} Gold." : "No courtyards yet. You pass inspection without paying.";
                    break;
                case 6:
                    if (UnityEngine.Random.value < 0.5f)
                    {
                        player.fame += 2;
                        result = "Your impromptu teaching is a great success. +2 Merit.";
                    }
                    else
                    {
                        ChargeBank(player, 30);
                        result = "Your impromptu teaching falls flat. The altar costs 30 Gold.";
                    }
                    break;
                default:
                    var back = (player.position - 2 + pathTiles.Length) % pathTiles.Length;
                    yield return MovePawnOneStep(player, (player.position - 1 + pathTiles.Length) % pathTiles.Length);
                    yield return MovePawnOneStep(player, back);
                    player.position = back;
                    result = "The Dedication Path sends you back two spaces. No event on landing.";
                    break;
            }
            RefreshHud();
            hud.ShowResult("A Pilgrim Presents a Wish Lot", result);
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
                EndGame(fameWinner, $"Merit reached {fameToWin}. An early completion!");
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
                    EndGame(winner, $"{maxRounds} rounds complete. Scored by Gold, courtyard value, and Merit.");
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

        void CheckCompletedSet(TemplePlayerState player, TempleTileCategory category)
        {
            var categoryTiles = tiles.Select((tile, index) => new { tile, index }).Where(x => x.tile.type == TileType.Property && x.tile.category == category).ToArray();
            if (categoryTiles.Length == 0 || player.completedSets.Contains(category) || categoryTiles.Any(x => x.tile.owner != player.id))
                return;
            player.completedSets.Add(category);
            player.fame += 2;
            hud.ShowResult($"Completed the {CategoryName(category)} Set!", "+2 Merit.");
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
                EndGame(alive[0], "All other pilgrims have left the temple. The last one standing wins.");
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
                case TileType.Property: return tile.owner < 0 ? $"A {CategoryName(tile.category)} courtyard available for {tile.price} Gold." : $"Currently owned by {players[tile.owner].name}. Level {tile.level}.";
                case TileType.Chance: return "A Wish Lot awaits.";
                case TileType.Festival: return "The Ceremony amplifies your choice between safety and risk.";
                case TileType.Tax: return $"Pay {tile.amount} Gold to the temple fund.";
                case TileType.Bonus: return $"Receive a Merit scroll worth {tile.amount} Gold.";
                case TileType.Gate: return "Gain 35 Gold and one Re-roll Slip.";
                default: return "Gain 60 Gold for each completed lap.";
            }
        }

        /// <summary>
        /// Returns a compact, stable summary for the player-name hover card.
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

                lines.Add($"{tile.name} · Lv.{tile.level + 1}");
            }

            return lines.Count == 0 ? "No Properties Owned" : string.Join("\n", lines);
        }

        public TempleTileInfoContent GetTileInfo(int index)
        {
            if (index < 0 || index >= tiles.Count)
                return new TempleTileInfoContent("Unknown", "", new Color32(220, 181, 91, 255));

            var tile = tiles[index];
            var accent = tile.owner >= 0 && tile.owner < PlayerColors.Length ? PlayerColors[tile.owner] : TileAccent(tile);
            switch (tile.type)
            {
                case TileType.Property:
                    var detail = tile.owner < 0
                        ? $"Available Courtyard\n{tile.price}G · Rent {tile.rent}"
                        : $"{players[tile.owner].name}'s Courtyard\nLv.{tile.level + 1} · Rent {CalculateRent(tile)}";
                    return new TempleTileInfoContent(CompactTileName(tile.name), detail, accent);
                case TileType.Chance:
                    return new TempleTileInfoContent("Lot", "Random Wish Lot\nBoon or Bane", accent);
                case TileType.Festival:
                    return new TempleTileInfoContent(CompactTileName(tile.name), "Ceremony Choice\nSafe or Bold", accent);
                case TileType.Tax:
                    return new TempleTileInfoContent(CompactTileName(tile.name), $"Temple Common Fund\nPay {tile.amount}G", accent);
                case TileType.Bonus:
                    return new TempleTileInfoContent(CompactTileName(tile.name), $"Merit Reward\nGain {tile.amount}G", accent);
                case TileType.Gate:
                    return new TempleTileInfoContent("Dedicate", "Dedication Reward\n35G · Re-roll Slip", accent);
                default:
                    return new TempleTileInfoContent("Start", "Circuit Start\nGain 60G per lap", accent);
            }
        }

        static string CompactTileName(string name)
        {
            switch (name)
            {
                case "Zen Stone Path": return "Zen Path";
                case "Pilgrim Arcade": return "Arcade";
                case "Sacred Relics Hall": return "Relics";
                case "Lotus Lamp Tower": return "Lotus";
                case "Vegetarian Court": return "Veggie";
                case "Merit Blessing": return "Merit";
                case "Offering Lighthouse": return "Offering";
                case "Scripture Gallery": return "Canon";
                case "Incense Levy": return "Inc. Levy";
                case "Relic Pavilion": return "Relic";
                case "Morning Bell Ceremony": return "Bell";
                case "Incense Altar": return "Altar";
                case "Wooden Fish Cottage": return "WoodFish";
                case "Meditation Hall": return "Retreat";
                case "Canon Tower": return "Vault";
                case "Merit Collection": return "Collect";
                case "Grand Ceremony": return "Grand";
                case "Lamp Oil Levy": return "Oil Levy";
                case "Sympathetic Merit": return "Alms";
                case "Lantern Ceremony": return "Lantern";
                case "Repair Levy": return "Repair";
                case "Alms Merit": return "Dana";
                case "Water-Land Ceremony": return "Water";
                default: return name.Length > 6 ? name.Substring(0, 6) : name;
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

        // EN: category display names (translated from Chinese)
        // 供灯阁 = Lamp Pavilion Wing, 禅修院 = Meditation Wing, 香火院 = Incense Wing, 法器阁 = Relic Wing
        static string CategoryName(TempleTileCategory category)
        {
            switch (category)
            {
                case TempleTileCategory.Offering: return "Lamp Wing";
                case TempleTileCategory.Meditation: return "Zen Wing";
                case TempleTileCategory.Incense: return "Incense Wing";
                default: return "Relic Wing";
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
