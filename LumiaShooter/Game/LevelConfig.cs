// ---------------------------------------------------------------------------
// LevelConfig.cs - THE PLACE TO TWEAK DIFFICULTY.
//
// There are exactly five levels. Each level is one LevelConfig object in the
// "Levels" array at the bottom of this file. Change the numbers, rebuild,
// deploy. Bigger/smaller values mean:
//
//   Duration          seconds of enemy waves before the level ends
//                     (the progress bar at the top fills up over this time)
//   SpawnInterval     average seconds between new enemies   (smaller = harder)
//   EnemySpeed        downward speed in pixels/second       (bigger  = harder)
//   Weight*           relative chance of each enemy type (0 = never appears)
//   FormationChance   0..1 chance that a spawn is a group of 3 basic enemies
//   EnemyFireInterval average seconds between shots of a "Shooter" enemy
//                     (0 = enemies never shoot)
//   EnemyBulletSpeed  speed of enemy bullets (keep it slow for kids)
//   DropChance        0..1 chance that a destroyed enemy drops a power-up
//   PowerUpInterval   a free power-up also falls every N seconds
//   HasBoss           a boss appears when the progress bar is full
//   StarSpeed         speed of the background starfield (just looks)
//
// Other global settings (lives, fire rate, power-up durations, boss health)
// are in GameTuning below.
// ---------------------------------------------------------------------------

namespace LumiaShooter.Game
{
    /// <summary>Global game settings that are the same for every level.</summary>
    public static class GameTuning
    {
        /// <summary>Lives at the start of a game.</summary>
        public const int StartLives = 5;
        /// <summary>You can never have more lives than this.</summary>
        public const int MaxLives = 9;

        /// <summary>Seconds between automatic player shots.</summary>
        public const float PlayerFireInterval = 0.16f;
        /// <summary>Player bullet speed (pixels/second, upwards).</summary>
        public const float PlayerBulletSpeed = 620f;
        /// <summary>
        /// How quickly the ship catches up with the finger (bigger = snappier).
        /// </summary>
        public const float PlayerFollowSharpness = 14f;
        /// <summary>
        /// With touch, the ship is placed this many pixels ABOVE the finger,
        /// so the finger does not cover the ship. (Mouse uses no offset.)
        /// </summary>
        public const float TouchOffsetY = -80f;

        /// <summary>Seconds of blinking invulnerability after losing a life.</summary>
        public const float InvulnerableTime = 2.5f;
        /// <summary>Power-up durations in seconds.</summary>
        public const float DoubleShotTime = 12f;
        public const float TripleShotTime = 12f;
        public const float ShieldTime = 10f;
        /// <summary>Points for the "$" pick-up.</summary>
        public const int BonusPointsValue = 100;
        /// <summary>Points given for "+" when lives are already at MaxLives.</summary>
        public const int ExtraLifeOverflowPoints = 500;
        /// <summary>
        /// Pick-up radius. Very generous so small children can grab power-ups.
        /// </summary>
        public const float PickupRadius = 40f;

        /// <summary>Boss hit points (one player bullet = 1 damage).</summary>
        public const int BossHealth = 100;
        public const int BossScore = 1000;
        /// <summary>Seconds between boss volleys.</summary>
        public const float BossFireInterval = 1.5f;

        /// <summary>How long "LEVEL n" is shown before enemies arrive.</summary>
        public const float LevelIntroTime = 2.5f;
        /// <summary>How long "LEVEL CLEAR!" is shown.</summary>
        public const float LevelClearTime = 2.0f;
    }

    /// <summary>Settings for one level. See the comment at the top of this file.</summary>
    public sealed class LevelConfig
    {
        public string Subtitle;
        public float Duration;
        public float SpawnInterval;
        public float EnemySpeed;
        public int WeightBasic;
        public int WeightZigZag;
        public int WeightShooter;
        public int WeightTank;
        public float FormationChance;
        public float EnemyFireInterval;
        public float EnemyBulletSpeed;
        public float DropChance;
        public float PowerUpInterval;
        public bool HasBoss;
        public float StarSpeed;

        /// <summary>The five levels, from easy to hard.</summary>
        public static readonly LevelConfig[] Levels = new LevelConfig[]
        {
            // LEVEL 1 - only slow basic enemies, nobody shoots.
            new LevelConfig
            {
                Subtitle = "GET READY!",
                Duration = 40f,
                SpawnInterval = 1.2f,
                EnemySpeed = 85f,
                WeightBasic = 100, WeightZigZag = 0, WeightShooter = 0, WeightTank = 0,
                FormationChance = 0f,
                EnemyFireInterval = 0f,
                EnemyBulletSpeed = 0f,
                DropChance = 0.12f,
                PowerUpInterval = 12f,
                HasBoss = false,
                StarSpeed = 70f
            },
            // LEVEL 2 - zig-zag enemies appear, small groups.
            new LevelConfig
            {
                Subtitle = "WIGGLY ENEMIES!",
                Duration = 45f,
                SpawnInterval = 1.0f,
                EnemySpeed = 100f,
                WeightBasic = 70, WeightZigZag = 30, WeightShooter = 0, WeightTank = 0,
                FormationChance = 0.12f,
                EnemyFireInterval = 0f,
                EnemyBulletSpeed = 0f,
                DropChance = 0.12f,
                PowerUpInterval = 12f,
                HasBoss = false,
                StarSpeed = 90f
            },
            // LEVEL 3 - first enemies that shoot (slow bullets).
            new LevelConfig
            {
                Subtitle = "THEY SHOOT BACK!",
                Duration = 50f,
                SpawnInterval = 0.9f,
                EnemySpeed = 115f,
                WeightBasic = 50, WeightZigZag = 30, WeightShooter = 20, WeightTank = 0,
                FormationChance = 0.15f,
                EnemyFireInterval = 2.8f,
                EnemyBulletSpeed = 140f,
                DropChance = 0.12f,
                PowerUpInterval = 11f,
                HasBoss = false,
                StarSpeed = 110f
            },
            // LEVEL 4 - tough "tank" enemies join in.
            new LevelConfig
            {
                Subtitle = "BIG ONES NEED MORE HITS!",
                Duration = 55f,
                SpawnInterval = 0.8f,
                EnemySpeed = 128f,
                WeightBasic = 35, WeightZigZag = 30, WeightShooter = 25, WeightTank = 10,
                FormationChance = 0.18f,
                EnemyFireInterval = 2.4f,
                EnemyBulletSpeed = 155f,
                DropChance = 0.13f,
                PowerUpInterval = 10f,
                HasBoss = false,
                StarSpeed = 130f
            },
            // LEVEL 5 - everything, faster, and a boss at the end.
            new LevelConfig
            {
                Subtitle = "BOSS AT THE END!",
                Duration = 60f,
                SpawnInterval = 0.7f,
                EnemySpeed = 140f,
                WeightBasic = 25, WeightZigZag = 30, WeightShooter = 30, WeightTank = 15,
                FormationChance = 0.2f,
                EnemyFireInterval = 2.1f,
                EnemyBulletSpeed = 165f,
                DropChance = 0.14f,
                PowerUpInterval = 9f,
                HasBoss = true,
                StarSpeed = 150f
            }
        };
    }
}
