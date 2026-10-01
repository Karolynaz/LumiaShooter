// ---------------------------------------------------------------------------
// GameEngine.cs - all game rules: states, movement, spawning, collisions,
// power-ups, scoring, levels.
//
// This class is pure C# with NO UWP/XAML dependencies. The page feeds it
// input (PointerDown/PointerMove/HandleBack/...) and calls Update(dt) once
// per frame; the renderer then reads the public state and draws it.
//
// All objects live in fixed-size arrays ("pools") that are allocated once,
// so nothing is allocated while playing (important on a 1 GB ARM phone).
//
// Difficulty numbers live in LevelConfig.cs (per level) and GameTuning
// (global), not here.
// ---------------------------------------------------------------------------

using System;

namespace LumiaShooter.Game
{
    /// <summary>The screens / phases of the game.</summary>
    public enum GameState
    {
        /// <summary>Title screen, "TAP TO START".</summary>
        Title,
        /// <summary>"LEVEL n" banner shown before enemies arrive.</summary>
        LevelIntro,
        /// <summary>Normal gameplay.</summary>
        Playing,
        /// <summary>Paused (Back button, app hidden/suspended).</summary>
        Paused,
        /// <summary>No lives left.</summary>
        GameOver,
        /// <summary>All five levels done.</summary>
        Victory
    }

    public sealed class GameEngine
    {
        // ---- World ----------------------------------------------------------

        /// <summary>World size = effective pixels of a 720x1280 phone at 200% scale.</summary>
        public const float WorldWidth = 360f;
        public const float WorldHeight = 640f;
        /// <summary>Top strip used by the score/progress HUD; the ship stays below it.</summary>
        public const float HudHeight = 56f;

        // ---- Pool sizes (maximum number of objects on screen) ----------------

        public const int MaxPlayerBullets = 48;
        public const int MaxEnemyBullets = 40;
        public const int MaxEnemies = 24;
        public const int MaxPowerUps = 6;
        public const int MaxParticles = 64;
        public const int StarCount = 45;

        /// <summary>Largest time step simulated at once (avoids huge jumps after a hiccup).</summary>
        public const float MaxTimeStep = 0.05f;

        private const float TitleStarSpeed = 60f;
        private const float PowerUpFallSpeed = 80f;
        private const float BossStopY = 130f;
        private const float TapDelayTitle = 0.3f;
        private const float TapDelayPaused = 0.2f;
        /// <summary>Taps are ignored this long on GAME OVER / YOU WIN so a child
        /// who is still tapping wildly doesn't instantly restart.</summary>
        public const float TapDelayEndScreens = 1.0f;

        // ---- Objects ----------------------------------------------------------

        public readonly Player Player = new Player();
        public readonly Bullet[] PlayerBullets = new Bullet[MaxPlayerBullets];
        public readonly Bullet[] EnemyBullets = new Bullet[MaxEnemyBullets];
        public readonly Enemy[] Enemies = new Enemy[MaxEnemies];
        public readonly PowerUp[] PowerUps = new PowerUp[MaxPowerUps];
        public readonly Particle[] Particles = new Particle[MaxParticles];
        public readonly Star[] Stars = new Star[StarCount];

        private readonly Random rng;

        // ---- Private bookkeeping ---------------------------------------------

        private GameState pausedState;
        private float pausedStateTime;
        private float levelTime;
        private float spawnTimer;
        private float freePowerUpTimer;
        private float fireTimer;
        private float levelClearTimer = -1f;
        private float bossWarningTimer;
        private float pickupMessageTimer;
        private bool bossSpawned;
        private bool bossDefeated;

        /// <summary>Raised when a new high score is reached (at game over / win / quit).</summary>
        public event Action<int> NewHighScore;

        public GameEngine(int savedHighScore) : this(savedHighScore, Environment.TickCount)
        {
        }

        public GameEngine(int savedHighScore, int randomSeed)
        {
            rng = new Random(randomSeed);
            HighScore = savedHighScore > 0 ? savedHighScore : 0;

            for (int i = 0; i < PlayerBullets.Length; i++) PlayerBullets[i] = new Bullet();
            for (int i = 0; i < EnemyBullets.Length; i++) EnemyBullets[i] = new Bullet();
            for (int i = 0; i < Enemies.Length; i++) Enemies[i] = new Enemy();
            for (int i = 0; i < PowerUps.Length; i++) PowerUps[i] = new PowerUp();
            for (int i = 0; i < Particles.Length; i++) Particles[i] = new Particle();
            for (int i = 0; i < Stars.Length; i++)
            {
                Star s = new Star();
                bool near = (i % 3) == 0;
                s.X = RandomRange(0f, WorldWidth);
                s.Y = RandomRange(0f, WorldHeight);
                s.Size = near ? 2f : 1f;
                s.SpeedFactor = near ? 1f : RandomRange(0.45f, 0.65f);
                s.Brightness = near ? 1f : RandomRange(0.35f, 0.7f);
                Stars[i] = s;
            }

            ResetPlayer();
            State = GameState.Title;
        }

        // ---- Public read-only state (used by the renderer / HUD) -------------

        public GameState State { get; private set; }
        /// <summary>Seconds spent in the current state.</summary>
        public float StateTime { get; private set; }
        public int Score { get; private set; }
        public int Lives { get; private set; }
        public int HighScore { get; private set; }
        /// <summary>True after a game that beat the previous high score.</summary>
        public bool IsNewHighScore { get; private set; }
        /// <summary>0-based index into LevelConfig.Levels.</summary>
        public int LevelIndex { get; private set; }
        public int LevelNumber { get { return LevelIndex + 1; } }
        public int LevelCount { get { return LevelConfig.Levels.Length; } }
        public LevelConfig Level { get { return LevelConfig.Levels[LevelIndex]; } }
        /// <summary>0..1, how far through the current level we are (top bar).</summary>
        public float LevelProgress { get; private set; }

        public float InvulnerableTimer { get; private set; }
        public float DoubleShotTimer { get; private set; }
        public float TripleShotTimer { get; private set; }
        public float ShieldTimer { get; private set; }

        /// <summary>The boss while it is alive, otherwise null.</summary>
        public Enemy Boss { get; private set; }
        public float BossHealthFraction
        {
            get { return Boss == null ? 0f : Math.Max(0f, (float)Boss.Health / Boss.MaxHealth); }
        }

        /// <summary>True while "LEVEL n CLEAR!" is shown.</summary>
        public bool IsLevelCleared { get { return levelClearTimer >= 0f; } }
        /// <summary>Bonus points given for the level just cleared.</summary>
        public int LastLevelBonus { get; private set; }
        /// <summary>True for a moment when the boss appears.</summary>
        public bool IsBossWarning { get { return bossWarningTimer > 0f; } }

        /// <summary>Short text such as "SHIELD!" after grabbing a power-up (or null).</summary>
        public string PickupMessage { get; private set; }

        /// <summary>The best score so far, including the game in progress.</summary>
        public int BestScoreSoFar { get { return Math.Max(HighScore, Score); } }

        /// <summary>True when GAME OVER / YOU WIN accept a tap.</summary>
        public bool CanTapToContinue
        {
            get
            {
                return (State == GameState.GameOver || State == GameState.Victory)
                    && StateTime >= TapDelayEndScreens;
            }
        }

        /// <summary>Whether the ship should be drawn this frame (blinks while invulnerable).</summary>
        public bool IsPlayerVisible
        {
            get
            {
                if (State == GameState.GameOver) return false;
                if (InvulnerableTimer > 0f)
                {
                    return ((int)(InvulnerableTimer * 10f) % 2) == 0;
                }
                return true;
            }
        }

        /// <summary>Whether the shield ring should be drawn (blinks when about to run out).</summary>
        public bool IsShieldVisible
        {
            get
            {
                if (State == GameState.GameOver || ShieldTimer <= 0f) return false;
                if (ShieldTimer > 2f) return true;
                return ((int)(ShieldTimer * 8f) % 2) == 0;
            }
        }

        /// <summary>True while a game is in progress (playing, intro or paused).</summary>
        public bool IsInGame
        {
            get
            {
                return State == GameState.Playing || State == GameState.LevelIntro
                    || State == GameState.Paused;
            }
        }

        // ---- Input -------------------------------------------------------------

        /// <summary>Finger touched / mouse button pressed at world position (x, y).</summary>
        public void PointerDown(float x, float y, bool isMouse)
        {
            Tap();
            if (State == GameState.Playing || State == GameState.LevelIntro)
            {
                SetTarget(x, y, isMouse);
            }
        }

        /// <summary>Finger / mouse moved. Mouse steers even without a button pressed.</summary>
        public void PointerMove(float x, float y, bool isMouse, bool isPressed)
        {
            if ((State == GameState.Playing || State == GameState.LevelIntro) && (isMouse || isPressed))
            {
                SetTarget(x, y, isMouse);
            }
        }

        /// <summary>A "tap" without position (also used for Enter/Space on a PC keyboard).</summary>
        public void Tap()
        {
            switch (State)
            {
                case GameState.Title:
                    if (StateTime >= TapDelayTitle) StartNewGame();
                    break;
                case GameState.Paused:
                    if (StateTime >= TapDelayPaused) Resume();
                    break;
                case GameState.GameOver:
                case GameState.Victory:
                    if (CanTapToContinue) StartNewGame();
                    break;
            }
        }

        /// <summary>
        /// Back button (phone hardware/software Back, PC title-bar back, Escape).
        /// Returns true if the game used it; false means "let the system handle
        /// it" (on the phone that leaves the app, which is what users expect on
        /// the title screen).
        /// </summary>
        public bool HandleBack()
        {
            switch (State)
            {
                case GameState.Playing:
                case GameState.LevelIntro:
                    Pause();
                    return true;
                case GameState.Paused:
                case GameState.GameOver:
                case GameState.Victory:
                    GoToTitle();
                    return true;
                default:
                    return false;
            }
        }

        /// <summary>Pause (safe to call any time; only does something during a game).</summary>
        public void Pause()
        {
            if (State != GameState.Playing && State != GameState.LevelIntro) return;
            pausedState = State;
            pausedStateTime = StateTime;
            State = GameState.Paused;
            StateTime = 0f;
            Player.HasTarget = false;
        }

        public void Resume()
        {
            if (State != GameState.Paused) return;
            State = pausedState;
            StateTime = pausedStateTime;
        }

        /// <summary>Leave the current game and show the title screen.</summary>
        public void GoToTitle()
        {
            if (IsInGame) FinalizeScore();
            ClearAll();
            ResetPlayerTimers();
            ResetPlayer();
            LevelProgress = 0f;
            levelClearTimer = -1f;
            SetState(GameState.Title);
        }

        // ---- Main update -------------------------------------------------------

        /// <summary>Advance the game by dt seconds.</summary>
        public void Update(float dt)
        {
            if (dt <= 0f) return;
            if (dt > MaxTimeStep) dt = MaxTimeStep;

            StateTime += dt;
            if (State == GameState.Paused) return; // everything frozen

            UpdateStars(dt, State == GameState.Title ? TitleStarSpeed : Level.StarSpeed);
            UpdateParticles(dt);

            switch (State)
            {
                case GameState.LevelIntro:
                    UpdatePlayerTimers(dt);
                    UpdatePlayerMovement(dt);
                    UpdatePlayerFiring(dt);
                    UpdateBullets(dt);
                    UpdatePowerUps(dt);
                    CheckPowerUpPickups();
                    if (StateTime >= GameTuning.LevelIntroTime) StartLevelPlay();
                    break;

                case GameState.Playing:
                    UpdatePlaying(dt);
                    break;

                case GameState.GameOver:
                case GameState.Victory:
                    // Let leftovers drift away; nothing can hurt anyone now.
                    UpdatePlayerTimers(dt);
                    UpdatePlayerMovement(dt);
                    UpdateEnemies(dt, false);
                    UpdateBullets(dt);
                    UpdatePowerUps(dt);
                    break;
            }
        }

        private void UpdatePlaying(float dt)
        {
            levelTime += dt;
            LevelProgress = Clamp01(levelTime / Level.Duration);
            if (bossWarningTimer > 0f) bossWarningTimer -= dt;

            UpdatePlayerTimers(dt);
            UpdatePlayerMovement(dt);
            UpdatePlayerFiring(dt);
            UpdateSpawning(dt);
            UpdateEnemies(dt, !IsLevelCleared);
            UpdateBullets(dt);
            UpdatePowerUps(dt);

            CheckBulletHits();
            CheckPlayerHits();
            if (State != GameState.Playing) return; // game over

            CheckPowerUpPickups();
            UpdateLevelEnd(dt);
        }

        // ---- State changes -----------------------------------------------------

        private void SetState(GameState s)
        {
            State = s;
            StateTime = 0f;
        }

        public void StartNewGame()
        {
            Score = 0;
            Lives = GameTuning.StartLives;
            LevelIndex = 0;
            IsNewHighScore = false;
            ClearAll();
            ResetPlayerTimers();
            ResetPlayer();
            EnterLevelIntro();
        }

        private void EnterLevelIntro()
        {
            levelTime = 0f;
            LevelProgress = 0f;
            levelClearTimer = -1f;
            bossSpawned = false;
            bossDefeated = false;
            bossWarningTimer = 0f;
            Boss = null;
            SetState(GameState.LevelIntro);
        }

        private void StartLevelPlay()
        {
            levelTime = 0f;
            spawnTimer = 0.8f;
            freePowerUpTimer = Level.PowerUpInterval * 0.5f;
            SetState(GameState.Playing);
        }

        private void UpdateLevelEnd(float dt)
        {
            if (levelClearTimer >= 0f)
            {
                levelClearTimer += dt;
                if (levelClearTimer >= GameTuning.LevelClearTime)
                {
                    if (LevelIndex >= LevelCount - 1)
                    {
                        FinalizeScore();
                        SetState(GameState.Victory);
                    }
                    else
                    {
                        LevelIndex++;
                        ClearHostiles();
                        EnterLevelIntro();
                    }
                }
                return;
            }

            if (levelTime < Level.Duration) return;

            bool done = Level.HasBoss
                ? (bossDefeated && CountActiveEnemies() == 0)
                : CountActiveEnemies() == 0;

            if (done)
            {
                levelClearTimer = 0f;
                LastLevelBonus = LevelNumber * 100;
                AddScore(LastLevelBonus);
                ClearEnemyBullets();
            }
        }

        private void FinalizeScore()
        {
            if (Score > HighScore)
            {
                HighScore = Score;
                IsNewHighScore = true;
                Action<int> handler = NewHighScore;
                if (handler != null) handler(HighScore);
            }
        }

        private void AddScore(int points)
        {
            Score += points;
        }

        // ---- Player --------------------------------------------------------------

        private void ResetPlayer()
        {
            Player.X = WorldWidth * 0.5f;
            Player.Y = WorldHeight - 100f;
            Player.TargetX = Player.X;
            Player.TargetY = Player.Y;
            Player.HasTarget = false;
            fireTimer = 0f;
        }

        private void ResetPlayerTimers()
        {
            InvulnerableTimer = 0f;
            DoubleShotTimer = 0f;
            TripleShotTimer = 0f;
            ShieldTimer = 0f;
            pickupMessageTimer = 0f;
            PickupMessage = null;
        }

        private void SetTarget(float x, float y, bool isMouse)
        {
            Player.TargetX = ClampPlayerX(x);
            Player.TargetY = ClampPlayerY(y + (isMouse ? 0f : GameTuning.TouchOffsetY));
            if (Player.TargetY < PlayerMinY) Player.TargetY = PlayerMinY;
            Player.HasTarget = true;
        }

        /// <summary>
        /// Highest point the ship may fly to. During the boss fight the ship is kept
        /// below the boss, so a child cannot lose all lives by touching it.
        /// </summary>
        private float PlayerMinY
        {
            get
            {
                if (Boss != null) return BossStopY + Boss.Height * 0.5f + Player.Height * 0.5f + 8f;
                return HudHeight + Player.Height * 0.5f;
            }
        }

        private static float ClampPlayerX(float x)
        {
            return Clamp(x, Player.Width * 0.5f, WorldWidth - Player.Width * 0.5f);
        }

        private static float ClampPlayerY(float y)
        {
            return Clamp(y, HudHeight + Player.Height * 0.5f, WorldHeight - Player.Height * 0.5f - 4f);
        }

        private void UpdatePlayerTimers(float dt)
        {
            if (InvulnerableTimer > 0f) InvulnerableTimer = Math.Max(0f, InvulnerableTimer - dt);
            if (DoubleShotTimer > 0f) DoubleShotTimer = Math.Max(0f, DoubleShotTimer - dt);
            if (TripleShotTimer > 0f) TripleShotTimer = Math.Max(0f, TripleShotTimer - dt);
            if (ShieldTimer > 0f) ShieldTimer = Math.Max(0f, ShieldTimer - dt);
            if (pickupMessageTimer > 0f)
            {
                pickupMessageTimer -= dt;
                if (pickupMessageTimer <= 0f) PickupMessage = null;
            }
        }

        private void UpdatePlayerMovement(float dt)
        {
            if (Player.HasTarget)
            {
                // Smoothly glide towards the finger instead of teleporting.
                float k = dt * GameTuning.PlayerFollowSharpness;
                if (k > 1f) k = 1f;
                Player.X += (Player.TargetX - Player.X) * k;
                Player.Y += (Player.TargetY - Player.Y) * k;
            }
            float minY = PlayerMinY;
            if (Player.Y < minY)
            {
                // Gently push the ship down (e.g. when the boss arrives).
                float push = dt * 8f;
                if (push > 1f) push = 1f;
                Player.Y += (minY - Player.Y) * push;
                if (Player.TargetY < minY) Player.TargetY = minY;
            }
            Player.X = ClampPlayerX(Player.X);
            Player.Y = ClampPlayerY(Player.Y);
        }

        private void UpdatePlayerFiring(float dt)
        {
            fireTimer -= dt;
            if (fireTimer > 0f) return;
            fireTimer = GameTuning.PlayerFireInterval;

            float x = Player.X;
            float y = Player.Y - Player.Height * 0.5f;
            float s = GameTuning.PlayerBulletSpeed;

            if (TripleShotTimer > 0f)
            {
                SpawnPlayerBullet(x, y, 0f, -s);
                SpawnPlayerBullet(x - 6f, y + 4f, -s * 0.2f, -s * 0.98f);
                SpawnPlayerBullet(x + 6f, y + 4f, s * 0.2f, -s * 0.98f);
            }
            else if (DoubleShotTimer > 0f)
            {
                SpawnPlayerBullet(x - 7f, y + 4f, 0f, -s);
                SpawnPlayerBullet(x + 7f, y + 4f, 0f, -s);
            }
            else
            {
                SpawnPlayerBullet(x, y, 0f, -s);
            }
        }

        private void HurtPlayer()
        {
            Lives--;
            Explode(Player.X, Player.Y, 14);
            ClearEnemyBullets(); // forgiving: no unfair follow-up hits

            if (Lives <= 0)
            {
                Lives = 0;
                Player.HasTarget = false;
                FinalizeScore();
                SetState(GameState.GameOver);
                return;
            }
            InvulnerableTimer = GameTuning.InvulnerableTime;
        }

        // ---- Spawning ------------------------------------------------------------

        private void UpdateSpawning(float dt)
        {
            if (IsLevelCleared) return;
            LevelConfig level = Level;

            // Free power-ups keep coming (also during the boss fight).
            freePowerUpTimer -= dt;
            if (freePowerUpTimer <= 0f)
            {
                freePowerUpTimer = level.PowerUpInterval;
                SpawnPowerUp(PickPowerUpType(), RandomRange(40f, WorldWidth - 40f), -PowerUp.Size);
            }

            if (levelTime < level.Duration)
            {
                spawnTimer -= dt;
                if (spawnTimer <= 0f)
                {
                    spawnTimer = level.SpawnInterval * RandomRange(0.7f, 1.3f);
                    if (rng.NextDouble() < level.FormationChance)
                    {
                        SpawnFormation();
                    }
                    else
                    {
                        SpawnEnemy(PickEnemyType(level), RandomRange(30f, WorldWidth - 30f), -30f);
                    }
                }
            }
            else if (level.HasBoss && !bossSpawned)
            {
                // Wait until the remaining enemies are gone (or 5 s), then the boss.
                if (CountActiveEnemies() == 0 || levelTime > level.Duration + 5f)
                {
                    SpawnBoss();
                }
            }
        }

        private EnemyType PickEnemyType(LevelConfig level)
        {
            int total = level.WeightBasic + level.WeightZigZag + level.WeightShooter + level.WeightTank;
            if (total <= 0) return EnemyType.Basic;
            int r = rng.Next(total);
            if (r < level.WeightBasic) return EnemyType.Basic;
            r -= level.WeightBasic;
            if (r < level.WeightZigZag) return EnemyType.ZigZag;
            r -= level.WeightZigZag;
            if (r < level.WeightShooter) return EnemyType.Shooter;
            return EnemyType.Tank;
        }

        /// <summary>Three basic enemies in a "V".</summary>
        private void SpawnFormation()
        {
            float cx = RandomRange(70f, WorldWidth - 70f);
            SpawnEnemy(EnemyType.Basic, cx, -30f);
            SpawnEnemy(EnemyType.Basic, cx - 40f, -60f);
            SpawnEnemy(EnemyType.Basic, cx + 40f, -60f);
        }

        private Enemy FindFreeEnemy()
        {
            for (int i = 0; i < Enemies.Length; i++)
            {
                if (!Enemies[i].Active) return Enemies[i];
            }
            return null;
        }

        /// <summary>Creates an enemy. The stats of each enemy type are set here.</summary>
        private Enemy SpawnEnemy(EnemyType type, float x, float y)
        {
            Enemy e = FindFreeEnemy();
            if (e == null) return null;

            float speed = Level.EnemySpeed;
            e.Active = true;
            e.Type = type;
            e.Age = 0f;
            e.FlashTimer = 0f;
            e.VX = 0f;
            e.Phase = RandomRange(0f, 6.2832f);
            e.ZigAmplitude = 0f;
            e.ZigFrequency = 0f;
            e.FireTimer = 0f;
            e.VolleyCount = 0;

            switch (type)
            {
                case EnemyType.ZigZag:
                    e.Width = 28f; e.Height = 28f; e.Health = 1; e.ScoreValue = 20;
                    e.VY = speed * 0.85f;
                    e.ZigAmplitude = RandomRange(35f, 60f);
                    e.ZigFrequency = RandomRange(2f, 3f);
                    break;
                case EnemyType.Shooter:
                    e.Width = 32f; e.Height = 28f; e.Health = 2; e.ScoreValue = 30;
                    e.VY = speed * 0.6f;
                    e.FireTimer = RandomRange(0.5f, 1.0f) * Math.Max(1f, Level.EnemyFireInterval);
                    break;
                case EnemyType.Tank:
                    e.Width = 44f; e.Height = 44f; e.Health = 5; e.ScoreValue = 50;
                    e.VY = speed * 0.45f;
                    break;
                default: // Basic
                    e.Type = EnemyType.Basic;
                    e.Width = 26f; e.Height = 26f; e.Health = 1; e.ScoreValue = 10;
                    e.VY = speed;
                    break;
            }
            e.MaxHealth = e.Health;

            float margin = e.Width * 0.5f + 4f + e.ZigAmplitude;
            e.BaseX = Clamp(x, margin, WorldWidth - margin);
            e.X = e.BaseX;
            if (type == EnemyType.ZigZag) e.X = e.BaseX + Sin(e.Phase) * e.ZigAmplitude;
            e.Y = y;
            return e;
        }

        private void SpawnBoss()
        {
            Enemy e = FindFreeEnemy();
            if (e == null) return; // try again next frame

            e.Active = true;
            e.Type = EnemyType.Boss;
            e.Width = 150f;
            e.Height = 70f;
            e.Health = GameTuning.BossHealth;
            e.MaxHealth = GameTuning.BossHealth;
            e.ScoreValue = GameTuning.BossScore;
            e.X = WorldWidth * 0.5f;
            e.BaseX = e.X;
            e.Y = -e.Height * 0.5f;
            e.VX = 0f;
            e.VY = 60f;
            e.Age = 0f;
            e.Phase = 0f; // used as "time since arrival"
            e.FireTimer = 1.5f;
            e.VolleyCount = 0;
            e.FlashTimer = 0f;
            e.ZigAmplitude = 0f;

            Boss = e;
            bossSpawned = true;
            bossWarningTimer = 2.5f;
        }

        // ---- Enemies -------------------------------------------------------------

        private void UpdateEnemies(float dt, bool allowFiring)
        {
            LevelConfig level = Level;
            for (int i = 0; i < Enemies.Length; i++)
            {
                Enemy e = Enemies[i];
                if (!e.Active) continue;

                e.Age += dt;
                if (e.FlashTimer > 0f) e.FlashTimer -= dt;

                switch (e.Type)
                {
                    case EnemyType.ZigZag:
                        e.Y += e.VY * dt;
                        e.X = e.BaseX + Sin(e.Age * e.ZigFrequency + e.Phase) * e.ZigAmplitude;
                        break;

                    case EnemyType.Shooter:
                        e.Y += e.VY * dt;
                        if (allowFiring && level.EnemyFireInterval > 0f)
                        {
                            e.FireTimer -= dt;
                            if (e.FireTimer <= 0f)
                            {
                                e.FireTimer = level.EnemyFireInterval * RandomRange(0.8f, 1.2f);
                                // Only shoot from the upper part of the screen (fair).
                                if (e.Y > HudHeight && e.Y < WorldHeight * 0.65f)
                                {
                                    FireAimed(e.X, e.Y + e.Height * 0.5f, level.EnemyBulletSpeed, 0f);
                                }
                            }
                        }
                        break;

                    case EnemyType.Boss:
                        UpdateBoss(e, dt, allowFiring);
                        break;

                    default:
                        e.X += e.VX * dt;
                        e.Y += e.VY * dt;
                        break;
                }

                if (e.Type != EnemyType.Boss && e.Y - e.Height * 0.5f > WorldHeight + 10f)
                {
                    e.Active = false; // flew past the bottom - no penalty
                }
            }
        }

        private void UpdateBoss(Enemy e, float dt, bool allowFiring)
        {
            if (e.Y < BossStopY)
            {
                // Entering from the top.
                e.Y += e.VY * dt;
                if (e.Y > BossStopY) e.Y = BossStopY;
                return;
            }

            // Slide left and right.
            e.Phase += dt;
            float range = (WorldWidth - e.Width) * 0.5f - 10f;
            e.X = WorldWidth * 0.5f + Sin(e.Phase * 0.8f) * range;

            if (!allowFiring) return;
            e.FireTimer -= dt;
            if (e.FireTimer > 0f) return;
            e.FireTimer = GameTuning.BossFireInterval;
            e.VolleyCount++;

            float x = e.X;
            float y = e.Y + e.Height * 0.5f;
            float speed = Level.EnemyBulletSpeed > 0f ? Level.EnemyBulletSpeed : 150f;

            if (e.VolleyCount % 3 == 0)
            {
                // Every third volley: three bullets aimed at the player.
                FireAimed(x, y, speed, -0.25f);
                FireAimed(x, y, speed, 0f);
                FireAimed(x, y, speed, 0.25f);
            }
            else
            {
                // Fan of five bullets straight down, with gaps to fly through.
                for (int k = -2; k <= 2; k++)
                {
                    float a = k * 0.3f;
                    SpawnEnemyBullet(x + k * 20f, y, Sin(a) * speed * 0.9f, Cos(a) * speed * 0.9f);
                }
            }
        }

        /// <summary>Shoots one enemy bullet towards the player (never upwards).</summary>
        private void FireAimed(float x, float y, float speed, float angleOffset)
        {
            float dx = Player.X - x;
            float dy = Player.Y - y;
            // Angle measured from "straight down"; positive = to the right.
            float a = (float)Math.Atan2(dx, dy);
            a = Clamp(a, -1.05f, 1.05f) + angleOffset;
            SpawnEnemyBullet(x, y, Sin(a) * speed, Cos(a) * speed);
        }

        private void DamageEnemy(Enemy e, int damage)
        {
            e.Health -= damage;
            e.FlashTimer = 0.08f;
            if (e.Health <= 0) KillEnemy(e);
        }

        private void KillEnemy(Enemy e)
        {
            e.Active = false;
            AddScore(e.ScoreValue);

            if (e.Type == EnemyType.Boss)
            {
                Boss = null;
                bossDefeated = true;
                ClearEnemyBullets();
                Explode(e.X, e.Y, 14);
                Explode(e.X - 50f, e.Y - 15f, 12);
                Explode(e.X + 50f, e.Y - 15f, 12);
                Explode(e.X - 30f, e.Y + 20f, 12);
                Explode(e.X + 30f, e.Y + 20f, 12);
                return;
            }

            Explode(e.X, e.Y, e.Type == EnemyType.Tank ? 16 : 10);
            if (rng.NextDouble() < Level.DropChance)
            {
                SpawnPowerUp(PickPowerUpType(), e.X, e.Y);
            }
        }

        private int CountActiveEnemies()
        {
            int n = 0;
            for (int i = 0; i < Enemies.Length; i++)
            {
                if (Enemies[i].Active) n++;
            }
            return n;
        }

        // ---- Bullets -------------------------------------------------------------

        private void SpawnPlayerBullet(float x, float y, float vx, float vy)
        {
            for (int i = 0; i < PlayerBullets.Length; i++)
            {
                Bullet b = PlayerBullets[i];
                if (b.Active) continue;
                b.Active = true;
                b.X = x; b.Y = y; b.VX = vx; b.VY = vy;
                return;
            }
        }

        private void SpawnEnemyBullet(float x, float y, float vx, float vy)
        {
            for (int i = 0; i < EnemyBullets.Length; i++)
            {
                Bullet b = EnemyBullets[i];
                if (b.Active) continue;
                b.Active = true;
                b.X = x; b.Y = y; b.VX = vx; b.VY = vy;
                return;
            }
        }

        private void UpdateBullets(float dt)
        {
            for (int i = 0; i < PlayerBullets.Length; i++)
            {
                Bullet b = PlayerBullets[i];
                if (!b.Active) continue;
                b.X += b.VX * dt;
                b.Y += b.VY * dt;
                if (b.Y < -20f || b.X < -20f || b.X > WorldWidth + 20f) b.Active = false;
            }
            for (int i = 0; i < EnemyBullets.Length; i++)
            {
                Bullet b = EnemyBullets[i];
                if (!b.Active) continue;
                b.X += b.VX * dt;
                b.Y += b.VY * dt;
                if (b.Y > WorldHeight + 20f || b.Y < -40f || b.X < -20f || b.X > WorldWidth + 20f)
                {
                    b.Active = false;
                }
            }
        }

        private void ClearEnemyBullets()
        {
            for (int i = 0; i < EnemyBullets.Length; i++) EnemyBullets[i].Active = false;
        }

        // ---- Collisions ----------------------------------------------------------

        private void CheckBulletHits()
        {
            for (int i = 0; i < PlayerBullets.Length; i++)
            {
                Bullet b = PlayerBullets[i];
                if (!b.Active) continue;
                for (int j = 0; j < Enemies.Length; j++)
                {
                    Enemy e = Enemies[j];
                    if (!e.Active) continue;
                    // Slightly bigger than the enemy so near misses still count.
                    if (Math.Abs(b.X - e.X) <= e.Width * 0.5f + 4f &&
                        Math.Abs(b.Y - e.Y) <= e.Height * 0.5f + 8f)
                    {
                        b.Active = false;
                        DamageEnemy(e, 1);
                        break;
                    }
                }
            }
        }

        private void CheckPlayerHits()
        {
            bool shielded = ShieldTimer > 0f;
            bool blinking = InvulnerableTimer > 0f;
            float px = Player.X;
            float py = Player.Y;

            for (int i = 0; i < EnemyBullets.Length; i++)
            {
                Bullet b = EnemyBullets[i];
                if (!b.Active) continue;
                if (Math.Abs(b.X - px) <= Player.HitHalfWidth + 3f &&
                    Math.Abs(b.Y - py) <= Player.HitHalfHeight + 3f)
                {
                    if (shielded) { b.Active = false; continue; } // shield eats bullets
                    if (blinking) continue;
                    b.Active = false;
                    HurtPlayer();
                    return;
                }
            }

            for (int i = 0; i < Enemies.Length; i++)
            {
                Enemy e = Enemies[i];
                if (!e.Active) continue;
                // Enemy box shrunk to 80% to be forgiving.
                if (Math.Abs(e.X - px) <= e.Width * 0.4f + Player.HitHalfWidth &&
                    Math.Abs(e.Y - py) <= e.Height * 0.4f + Player.HitHalfHeight)
                {
                    if (shielded)
                    {
                        if (e.Type != EnemyType.Boss) KillEnemy(e); // ram them!
                        continue;
                    }
                    if (blinking) continue;
                    if (e.Type != EnemyType.Boss) KillEnemy(e);
                    HurtPlayer();
                    return;
                }
            }
        }

        // ---- Power-ups -----------------------------------------------------------

        private PowerUpType PickPowerUpType()
        {
            int wDouble = 25;
            int wTriple = 15;
            int wShield = 20;
            int wLife = Lives >= GameTuning.MaxLives ? 0 : (Lives <= 2 ? 25 : 10);
            int wPoints = 28;
            int r = rng.Next(wDouble + wTriple + wShield + wLife + wPoints);
            if (r < wDouble) return PowerUpType.DoubleShot;
            r -= wDouble;
            if (r < wTriple) return PowerUpType.TripleShot;
            r -= wTriple;
            if (r < wShield) return PowerUpType.Shield;
            r -= wShield;
            if (r < wLife) return PowerUpType.ExtraLife;
            return PowerUpType.BonusPoints;
        }

        private void SpawnPowerUp(PowerUpType type, float x, float y)
        {
            for (int i = 0; i < PowerUps.Length; i++)
            {
                PowerUp p = PowerUps[i];
                if (p.Active) continue;
                float margin = PowerUp.Size * 0.5f + 14f;
                p.Active = true;
                p.Type = type;
                p.BaseX = Clamp(x, margin, WorldWidth - margin);
                p.X = p.BaseX;
                p.Y = y;
                p.Age = 0f;
                return;
            }
        }

        private void UpdatePowerUps(float dt)
        {
            for (int i = 0; i < PowerUps.Length; i++)
            {
                PowerUp p = PowerUps[i];
                if (!p.Active) continue;
                p.Age += dt;
                p.Y += PowerUpFallSpeed * dt;
                p.X = p.BaseX + Sin(p.Age * 2.5f) * 12f;
                if (p.Y - PowerUp.Size > WorldHeight) p.Active = false;
            }
        }

        private void CheckPowerUpPickups()
        {
            float r2 = GameTuning.PickupRadius * GameTuning.PickupRadius;
            for (int i = 0; i < PowerUps.Length; i++)
            {
                PowerUp p = PowerUps[i];
                if (!p.Active) continue;
                float dx = p.X - Player.X;
                float dy = p.Y - Player.Y;
                if (dx * dx + dy * dy <= r2)
                {
                    p.Active = false;
                    ApplyPowerUp(p.Type);
                    Explode(p.X, p.Y, 6);
                }
            }
        }

        private void ApplyPowerUp(PowerUpType type)
        {
            string message;
            switch (type)
            {
                case PowerUpType.DoubleShot:
                    DoubleShotTimer = GameTuning.DoubleShotTime;
                    message = "DOUBLE SHOT!";
                    break;
                case PowerUpType.TripleShot:
                    TripleShotTimer = GameTuning.TripleShotTime;
                    message = "TRIPLE SHOT!";
                    break;
                case PowerUpType.Shield:
                    ShieldTimer = GameTuning.ShieldTime;
                    message = "SHIELD!";
                    break;
                case PowerUpType.ExtraLife:
                    if (Lives < GameTuning.MaxLives)
                    {
                        Lives++;
                        message = "EXTRA LIFE!";
                    }
                    else
                    {
                        AddScore(GameTuning.ExtraLifeOverflowPoints);
                        message = "+" + GameTuning.ExtraLifeOverflowPoints;
                    }
                    break;
                default:
                    AddScore(GameTuning.BonusPointsValue);
                    message = "+" + GameTuning.BonusPointsValue;
                    break;
            }
            PickupMessage = message;
            pickupMessageTimer = 1.5f;
        }

        // ---- Particles & stars -----------------------------------------------

        private void Explode(float x, float y, int count)
        {
            for (int i = 0; i < Particles.Length && count > 0; i++)
            {
                Particle p = Particles[i];
                if (p.Active) continue;
                float angle = RandomRange(0f, 6.2832f);
                float speed = RandomRange(60f, 220f);
                p.Active = true;
                p.X = x;
                p.Y = y;
                p.VX = Cos(angle) * speed;
                p.VY = Sin(angle) * speed;
                p.MaxLife = RandomRange(0.35f, 0.7f);
                p.Life = p.MaxLife;
                count--;
            }
        }

        private void UpdateParticles(float dt)
        {
            float drag = 1f - 2f * dt;
            if (drag < 0f) drag = 0f;
            for (int i = 0; i < Particles.Length; i++)
            {
                Particle p = Particles[i];
                if (!p.Active) continue;
                p.Life -= dt;
                if (p.Life <= 0f) { p.Active = false; continue; }
                p.X += p.VX * dt;
                p.Y += p.VY * dt;
                p.VX *= drag;
                p.VY *= drag;
            }
        }

        private void UpdateStars(float dt, float speed)
        {
            for (int i = 0; i < Stars.Length; i++)
            {
                Star s = Stars[i];
                s.Y += speed * s.SpeedFactor * dt;
                if (s.Y > WorldHeight + 2f)
                {
                    s.Y -= WorldHeight + 4f;
                    s.X = RandomRange(0f, WorldWidth);
                }
            }
        }

        // ---- Clearing ----------------------------------------------------------

        private void ClearHostiles()
        {
            for (int i = 0; i < Enemies.Length; i++) Enemies[i].Active = false;
            ClearEnemyBullets();
            Boss = null;
        }

        private void ClearAll()
        {
            ClearHostiles();
            for (int i = 0; i < PlayerBullets.Length; i++) PlayerBullets[i].Active = false;
            for (int i = 0; i < PowerUps.Length; i++) PowerUps[i].Active = false;
        }

        // ---- Math helpers ------------------------------------------------------

        private float RandomRange(float min, float max)
        {
            return min + (float)rng.NextDouble() * (max - min);
        }

        private static float Clamp(float v, float min, float max)
        {
            if (v < min) return min;
            if (v > max) return max;
            return v;
        }

        private static float Clamp01(float v)
        {
            return Clamp(v, 0f, 1f);
        }

        private static float Sin(float a)
        {
            return (float)Math.Sin(a);
        }

        private static float Cos(float a)
        {
            return (float)Math.Cos(a);
        }
    }
}
