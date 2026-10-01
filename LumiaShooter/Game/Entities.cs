// ---------------------------------------------------------------------------
// Entities.cs - plain data classes for everything that moves in the game.
//
// All coordinates are in "world units" (see GameEngine.WorldWidth/Height),
// which match the effective pixels of a Lumia 735 in portrait (360 x 640).
// X/Y always describe the CENTER of an object.
//
// These classes contain no UWP/XAML code at all, so the game logic can be
// unit-tested or reused anywhere. The renderer (Rendering/GameRenderer.cs)
// reads them every frame and moves the matching XAML shapes.
// ---------------------------------------------------------------------------

namespace LumiaShooter.Game
{
    /// <summary>The different kinds of enemies. See GameEngine.SpawnEnemy for their stats.</summary>
    public enum EnemyType
    {
        /// <summary>Small filled square, flies straight down.</summary>
        Basic,
        /// <summary>Diamond that wiggles left/right while flying down.</summary>
        ZigZag,
        /// <summary>Downward-pointing triangle that shoots slow bullets at the player.</summary>
        Shooter,
        /// <summary>Big hollow square, slow, needs several hits.</summary>
        Tank,
        /// <summary>End-of-game boss (level 5 only).</summary>
        Boss
    }

    /// <summary>Pick-ups that fall down the screen. Shown as a white box with a letter.</summary>
    public enum PowerUpType
    {
        /// <summary>"2" - two parallel bullets for a while.</summary>
        DoubleShot,
        /// <summary>"3" - three bullets in a fan for a while.</summary>
        TripleShot,
        /// <summary>"S" - shield: nothing can hurt you for a while.</summary>
        Shield,
        /// <summary>"+" - one extra life.</summary>
        ExtraLife,
        /// <summary>"$" - bonus points.</summary>
        BonusPoints
    }

    /// <summary>The player's ship.</summary>
    public sealed class Player
    {
        /// <summary>Size of the drawn ship.</summary>
        public const float Width = 30f;
        public const float Height = 34f;

        /// <summary>
        /// Half-size of the box used for "the player got hit" checks.
        /// Deliberately much smaller than the drawn ship to be forgiving.
        /// </summary>
        public const float HitHalfWidth = 8f;
        public const float HitHalfHeight = 10f;

        public float X;
        public float Y;

        /// <summary>Where the ship is flying towards (finger / mouse position).</summary>
        public float TargetX;
        public float TargetY;
        public bool HasTarget;
    }

    /// <summary>A bullet (used both for player bullets and enemy bullets).</summary>
    public sealed class Bullet
    {
        public bool Active;
        public float X;
        public float Y;
        public float VX;
        public float VY;
    }

    /// <summary>An enemy ship (including the boss).</summary>
    public sealed class Enemy
    {
        public bool Active;
        public EnemyType Type;
        public float X;
        public float Y;
        public float VX;
        public float VY;
        public float Width;
        public float Height;
        public int Health;
        public int MaxHealth;
        public int ScoreValue;

        /// <summary>Seconds since spawn.</summary>
        public float Age;
        /// <summary>Center line for zig-zag movement.</summary>
        public float BaseX;
        public float ZigAmplitude;
        public float ZigFrequency;
        public float Phase;
        /// <summary>Counts down to the next shot.</summary>
        public float FireTimer;
        /// <summary>Counts how many volleys have been fired (boss pattern).</summary>
        public int VolleyCount;
        /// <summary>&gt; 0 right after being hit (drawn semi-transparent).</summary>
        public float FlashTimer;
    }

    /// <summary>A falling power-up box.</summary>
    public sealed class PowerUp
    {
        /// <summary>Drawn size of the box.</summary>
        public const float Size = 30f;

        public bool Active;
        public PowerUpType Type;
        public float X;
        public float Y;
        public float BaseX;
        public float Age;
    }

    /// <summary>A tiny square flying out of an explosion.</summary>
    public sealed class Particle
    {
        public bool Active;
        public float X;
        public float Y;
        public float VX;
        public float VY;
        public float Life;
        public float MaxLife;
    }

    /// <summary>A background star. Stars scroll down to show forward motion.</summary>
    public sealed class Star
    {
        public float X;
        public float Y;
        /// <summary>1 = far/slow/dim star, 2 = near/fast/bright star.</summary>
        public float Size;
        /// <summary>Speed multiplier (near stars move faster = parallax).</summary>
        public float SpeedFactor;
        public float Brightness;
    }
}
