using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

namespace FishTank
{
    public enum GameState { Ready, Playing, GameOver, Won }

    /// <summary>
    /// Game rules: who eats whom, score, growth, lives, win / lose and restart.
    /// </summary>
    public class GameManager : MonoBehaviour
    {
        public static GameManager Instance { get; private set; }

        public FishDatabase database;
        public CameraRig cameraRig;

        [Header("Rules")]
        public float startSize = 0.8f;
        public float winSize = 8f;
        [Tooltip("A fish must be this many times longer than another to eat it.")]
        public float eatMargin = 1.15f;
        [Tooltip("Player growth per unit of prey size eaten.")]
        public float growthPerPreySize = 0.07f;
        [Tooltip("Seconds you have to eat the next fish to keep the combo going.")]
        public float comboWindow = 2.5f;
        public int maxCombo = 8;
        public int startLives = 3;
        public float respawnDelay = 1.2f;
        public float respawnInvulnerability = 2.5f;

        [Header("Exploring")]
        [Tooltip("Score for swimming into a zone for the first time.")]
        public int discoveryPoints = 100;

        public GameState State { get; private set; } = GameState.Ready;
        public Fish Player { get; private set; }
        public int Score { get; private set; }
        public int Lives { get; private set; }
        public bool PlayerInvulnerable => Time.time < invulnerableUntil;
        public float PlayerMaxSpeed => playerControl != null ? playerControl.MaxSpeed : 5f;
        public bool PlayerDashing => playerControl != null && playerControl.IsBoosting;

        /// <summary>Time.time at which the current game left the title screen.</summary>
        public float StartedAt { get; private set; }
        /// <summary>Score multiplier from eating fish in quick succession (1 = no combo).</summary>
        public int ComboMultiplier => Time.time < comboExpiresAt ? Mathf.Min(comboCount, maxCombo) : 1;

        static bool skipTitleOnce;

        PlayerFish playerControl;
        Hud hud;
        Juice juice;
        int comboCount;
        float comboExpiresAt;
        float invulnerableUntil;
        float restartAllowedAt;
        bool[] discovered;
        int treasuresFound;
        int treasuresTotal;

        void Awake()
        {
            Instance = this;
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
            Time.timeScale = 1f;
        }

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        void Start()
        {
            hud = Hud.Create();
            juice = Juice.Create();
            Lives = startLives;

            if (database == null || database.playerPrefab == null)
            {
                hud.SetMessage("No fish found.\nRun FishTank > Build Game from the Unity menu.");
                enabled = false;
                return;
            }

            discovered = new bool[World.ZoneNames.Length];
            treasuresTotal = Treasure.All.Count;

            // Start just above the sea bed, among the rocks and weeds.
            Player = Instantiate(database.playerPrefab, new Vector3(0f, World.Floor(0f) + 7f, 0f), Quaternion.identity);
            Player.name = "Player";
            Player.destroyWhenEaten = false;
            Player.SetSize(startSize, true);
            Player.Face(Vector3.right, true);
            playerControl = Player.gameObject.AddComponent<PlayerFish>();

            hud.SetMessage(null);
            RefreshHud();

            // "Play again" after a game over goes straight back into the action.
            if (skipTitleOnce)
            {
                skipTitleOnce = false;
                BeginGame();
            }
            else
            {
                hud.ShowTitle(true);
            }
        }

        void BeginGame()
        {
            State = GameState.Playing;
            StartedAt = Time.time;
            hud.ShowTitle(false);
        }

        // The first time the player swims into a zone it is announced and rewarded.
        void CheckZone()
        {
            if (!Player.IsAlive) return;
            int zone = World.ZoneAt(Player.Position.x, Player.Position.y);
            if (zone < 0 || discovered[zone]) return;

            discovered[zone] = true;
            if (zone == World.HomeZone)
            {
                hud.Banner("WELCOME TO", World.ZoneNames[zone]);
                return;
            }

            Score += discoveryPoints;
            RefreshHud();
            juice.Chomp(maxCombo);
            hud.Banner("DISCOVERED  +" + discoveryPoints, World.ZoneNames[zone]);
        }

        /// <summary>The player swam into a pearl or a chest.</summary>
        public void Collect(Treasure treasure)
        {
            treasuresFound++;
            Score += treasure.points;
            RefreshHud();

            Vector3 at = treasure.transform.position;
            var gold = new Color(1f, 0.85f, 0.25f);
            juice.Chomp(treasure.big ? maxCombo : 5);
            juice.Bubbles(at, treasure.big ? 18 : 6, treasure.big ? 0.3f : 0.15f);
            hud.Popup(at, "+" + treasure.points, gold, treasure.big ? 1.8f : 1.1f);
            if (treasure.big)
            {
                hud.Banner("TREASURE CHEST  +" + treasure.points, treasuresFound + " / " + treasuresTotal + " FOUND");
                if (cameraRig != null) cameraRig.Shake(0.4f);
            }

            if (treasuresTotal > 0 && treasuresFound >= treasuresTotal)
                hud.Banner("EVERY TREASURE FOUND!", "MASTER EXPLORER");
        }

        public void VentBubble(Vector3 position, float size)
        {
            juice.Bubbles(position, 1, size, Vector3.up * (size * 14f));
        }

        public void OnPlayerDashed(Vector3 backwards)
        {
            juice.Whoosh();
            juice.Bubbles(Player.Position + backwards * (Player.Size * 0.4f), 6, Player.Size * 0.1f,
                backwards * (Player.Size * 3f));
        }

        public void OnPlayerDashTrail(Vector3 backwards)
        {
            juice.Bubbles(Player.Position + backwards * (Player.Size * 0.4f), 1, Player.Size * 0.08f);
        }

        public bool CanEat(Fish eater, Fish prey) => eater.Size >= prey.Size * eatMargin;

        void Update()
        {
            switch (State)
            {
                case GameState.Ready:
                    if (hud.NewGamePressed()) BeginGame();
                    break;

                case GameState.Playing:
                    if (Player.IsAlive)
                        Player.SetVisible(!PlayerInvulnerable || Mathf.FloorToInt(Time.time * 10f) % 2 == 0);
                    CheckZone();
                    break;

                case GameState.GameOver:
                case GameState.Won:
                    if (Time.unscaledTime >= restartAllowedAt && AnyPress())
                    {
                        skipTitleOnce = true;
                        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
                    }
                    break;
            }

            bool playing = State == GameState.Playing;
            hud.SetCombo(playing ? ComboMultiplier : 1, Mathf.Clamp01((comboExpiresAt - Time.time) / comboWindow));
            hud.SetDash(playerControl.Boost01);

            ResolveEating();
        }

        void ResolveEating()
        {
            var all = Fish.All;
            for (int i = 0; i < all.Count; i++)
            {
                Fish eater = all[i];
                if (!eater.IsAlive) continue;
                if (eater == Player && State != GameState.Playing) continue;

                for (int j = 0; j < all.Count; j++)
                {
                    Fish prey = all[j];
                    if (i == j || !prey.IsAlive || !CanEat(eater, prey)) continue;
                    if (prey == Player && (State != GameState.Playing || PlayerInvulnerable)) continue;

                    // The player gets a more generous bite than the other fish.
                    float reach = prey.Radius + eater.Size * (eater == Player ? 0.35f : 0.15f);
                    if ((eater.MouthPosition - prey.Position).sqrMagnitude > reach * reach) continue;

                    eater.Bite();
                    prey.GetEaten(eater);

                    if (eater == Player) OnPlayerAte(prey);
                    else if (prey == Player) OnPlayerEaten();
                }
            }
        }

        void OnPlayerAte(Fish prey)
        {
            comboCount = Time.time < comboExpiresAt ? comboCount + 1 : 1;
            comboExpiresAt = Time.time + comboWindow;
            int multiplier = Mathf.Min(comboCount, maxCombo);
            int points = Mathf.Max(1, Mathf.RoundToInt(prey.Size * 10f)) * multiplier;

            Score += points;
            playerControl.OnAte();
            Player.SetSize(Player.TargetSize + prey.Size * growthPerPreySize);
            RefreshHud();

            // Feedback scales with how big the mouthful was.
            bool bigBite = prey.Size >= Player.Size * 0.6f;
            float heat = Mathf.InverseLerp(1f, maxCombo, multiplier);
            juice.Chomp(comboCount);
            juice.Bubbles(prey.Position, bigBite ? 10 : 6, Player.Size * 0.12f);
            hud.Popup(prey.transform.position, "+" + points,
                Color.Lerp(Color.white, new Color(1f, 0.55f, 0.1f), heat), 1f + 0.6f * heat);
            if (cameraRig != null) cameraRig.Shake(bigBite ? 0.45f : 0.22f);
            if (bigBite) juice.HitStop(0.05f);

            if (Player.TargetSize >= winSize)
            {
                State = GameState.Won;
                restartAllowedAt = Time.unscaledTime + 1f;
                hud.SetMessage("YOU RULE THE TANK!\n\nScore: " + Score + "\n\nPress any key or tap to play again");
            }
        }

        void OnPlayerEaten()
        {
            Lives--;
            comboCount = 0;
            comboExpiresAt = 0f;
            RefreshHud();

            juice.Death();
            juice.Bubbles(Player.Position, 16, Player.Size * 0.15f);
            juice.HitStop(0.12f);
            if (cameraRig != null) cameraRig.Shake(1f);

            if (Lives <= 0)
            {
                State = GameState.GameOver;
                restartAllowedAt = Time.unscaledTime + 1f;
                hud.SetMessage("GAME OVER\n\nScore: " + Score + "\n\nPress any key or tap to try again");
            }
            else
            {
                StartCoroutine(Respawn());
            }
        }

        IEnumerator Respawn()
        {
            yield return new WaitForSeconds(respawnDelay);
            if (State != GameState.Playing) yield break;

            invulnerableUntil = Time.time + respawnInvulnerability;
            Player.Revive(Player.Position);
        }

        void RefreshHud()
        {
            float size = Player != null ? Player.TargetSize : startSize;
            hud.SetScore(Score);
            hud.SetLives(Lives);
            hud.SetTreasure(treasuresFound, treasuresTotal);
            hud.SetGrowth(Mathf.InverseLerp(startSize, winSize, size));
        }

        static bool AnyPress()
        {
            var k = Keyboard.current;
            if (k != null && k.anyKey.wasPressedThisFrame) return true;
            var p = Pointer.current;
            if (p != null && p.press.wasPressedThisFrame) return true;
            var g = Gamepad.current;
            return g != null && (g.buttonSouth.wasPressedThisFrame || g.startButton.wasPressedThisFrame);
        }
    }
}
