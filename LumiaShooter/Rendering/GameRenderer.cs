// ---------------------------------------------------------------------------
// GameRenderer.cs - draws the GameEngine state with plain XAML shapes.
//
// Performance notes (Lumia 735 = slow ARM CPU, 1 GB RAM):
//  * Every shape is created ONCE at start-up (object pools) and only moved,
//    shown or hidden afterwards. Nothing is allocated per frame.
//  * Shapes are moved with a TranslateTransform (RenderTransform), which does
//    not trigger XAML layout, instead of Canvas.Left/Top.
//  * Visibility/opacity are only touched when they actually change.
// ---------------------------------------------------------------------------

using LumiaShooter.Game;
using Windows.Foundation;
using Windows.UI;
using Windows.UI.Text;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Shapes;

namespace LumiaShooter.Rendering
{
    public sealed class GameRenderer
    {
        /// <summary>One pooled visual: a XAML element plus its translate transform.</summary>
        private sealed class Sprite
        {
            public readonly FrameworkElement Element;
            private readonly TranslateTransform transform;
            private bool visible;
            private double opacity = 1.0;

            public Sprite(FrameworkElement element, Canvas layer)
            {
                Element = element;
                transform = new TranslateTransform();
                element.RenderTransform = transform;
                element.IsHitTestVisible = false;
                element.Visibility = Visibility.Collapsed;
                layer.Children.Add(element);
            }

            public void SetVisible(bool value)
            {
                if (value == visible) return;
                visible = value;
                Element.Visibility = value ? Visibility.Visible : Visibility.Collapsed;
            }

            /// <summary>Moves the element so that its top-left corner is at (left, top).</summary>
            public void MoveTo(double left, double top)
            {
                transform.X = left;
                transform.Y = top;
            }

            public void SetOpacity(double value)
            {
                if (value == opacity) return;
                opacity = value;
                Element.Opacity = value;
            }
        }

        private readonly GameEngine engine;

        private readonly SolidColorBrush whiteBrush = new SolidColorBrush(Colors.White);
        private readonly SolidColorBrush blackBrush = new SolidColorBrush(Colors.Black);
        private readonly SolidColorBrush greyBrush = new SolidColorBrush(Color.FromArgb(255, 128, 128, 128));
        private readonly SolidColorBrush lightGreyBrush = new SolidColorBrush(Color.FromArgb(255, 200, 200, 200));

        private readonly Sprite[] stars;
        private readonly Sprite[] particles;
        private readonly Sprite[] powerUps;
        private readonly TextBlock[] powerUpLabels;
        private readonly PowerUpType[] powerUpShownType;
        private readonly Sprite[] enemyBullets;
        private readonly Sprite[] enemies;
        private readonly Polygon[] enemyShapes;
        private readonly EnemyType[] enemyShownType;
        private readonly float[] enemyShownWidth;
        private readonly Sprite[] playerBullets;
        private readonly Sprite player;
        private readonly Sprite shield;

        private const double PlayerBulletWidth = 4;
        private const double PlayerBulletHeight = 12;
        private const double EnemyBulletSize = 8;
        private const double ParticleSize = 3;
        private const double ShieldSize = 56;

        public GameRenderer(GameEngine engine, Canvas starLayer, Canvas entityLayer)
        {
            this.engine = engine;

            // Background stars.
            stars = new Sprite[engine.Stars.Length];
            for (int i = 0; i < stars.Length; i++)
            {
                Star s = engine.Stars[i];
                Rectangle r = new Rectangle();
                r.Width = s.Size;
                r.Height = s.Size;
                r.Fill = whiteBrush;
                stars[i] = new Sprite(r, starLayer);
                stars[i].SetOpacity(s.Brightness);
                stars[i].SetVisible(true);
            }

            // Order of creation = drawing order (later = on top).
            particles = new Sprite[engine.Particles.Length];
            for (int i = 0; i < particles.Length; i++)
            {
                Rectangle r = new Rectangle();
                r.Width = ParticleSize;
                r.Height = ParticleSize;
                r.Fill = whiteBrush;
                particles[i] = new Sprite(r, entityLayer);
            }

            powerUps = new Sprite[engine.PowerUps.Length];
            powerUpLabels = new TextBlock[powerUps.Length];
            powerUpShownType = new PowerUpType[powerUps.Length];
            for (int i = 0; i < powerUps.Length; i++)
            {
                Grid g = new Grid();
                g.Width = PowerUp.Size;
                g.Height = PowerUp.Size;

                Rectangle box = new Rectangle();
                box.Stroke = whiteBrush;
                box.StrokeThickness = 2;
                box.Fill = blackBrush;
                box.RadiusX = 4;
                box.RadiusY = 4;
                g.Children.Add(box);

                TextBlock label = new TextBlock();
                label.Foreground = whiteBrush;
                label.FontSize = 18;
                label.FontWeight = FontWeights.Bold;
                label.HorizontalAlignment = HorizontalAlignment.Center;
                label.VerticalAlignment = VerticalAlignment.Center;
                label.TextAlignment = TextAlignment.Center;
                label.Text = PowerUpLabel(PowerUpType.DoubleShot);
                g.Children.Add(label);

                powerUpLabels[i] = label;
                powerUpShownType[i] = PowerUpType.DoubleShot;
                powerUps[i] = new Sprite(g, entityLayer);
            }

            enemyBullets = new Sprite[engine.EnemyBullets.Length];
            for (int i = 0; i < enemyBullets.Length; i++)
            {
                Rectangle r = new Rectangle();
                r.Width = EnemyBulletSize;
                r.Height = EnemyBulletSize;
                r.Fill = lightGreyBrush;
                enemyBullets[i] = new Sprite(r, entityLayer);
            }

            enemies = new Sprite[engine.Enemies.Length];
            enemyShapes = new Polygon[enemies.Length];
            enemyShownType = new EnemyType[enemies.Length];
            enemyShownWidth = new float[enemies.Length];
            for (int i = 0; i < enemies.Length; i++)
            {
                Polygon p = new Polygon();
                enemyShapes[i] = p;
                enemyShownWidth[i] = -1f; // forces shape setup on first use
                enemies[i] = new Sprite(p, entityLayer);
            }

            playerBullets = new Sprite[engine.PlayerBullets.Length];
            for (int i = 0; i < playerBullets.Length; i++)
            {
                Rectangle r = new Rectangle();
                r.Width = PlayerBulletWidth;
                r.Height = PlayerBulletHeight;
                r.Fill = whiteBrush;
                playerBullets[i] = new Sprite(r, entityLayer);
            }

            // The player's ship: a small white arrow/fighter shape (30 x 34).
            Polygon ship = new Polygon();
            ship.Fill = whiteBrush;
            ship.Points = MakePoints(new double[]
            {
                15, 0,   19, 12,  30, 26,  30, 32,  19, 28,  17, 34,
                13, 34,  11, 28,  0, 32,   0, 26,   11, 12
            });
            player = new Sprite(ship, entityLayer);

            Ellipse ring = new Ellipse();
            ring.Width = ShieldSize;
            ring.Height = ShieldSize;
            ring.Stroke = whiteBrush;
            ring.StrokeThickness = 2;
            shield = new Sprite(ring, entityLayer);
        }

        /// <summary>Call once per frame after GameEngine.Update.</summary>
        public void Render()
        {
            // Stars
            for (int i = 0; i < stars.Length; i++)
            {
                Star s = engine.Stars[i];
                stars[i].MoveTo(s.X - s.Size * 0.5f, s.Y - s.Size * 0.5f);
            }

            // Particles (fade out)
            for (int i = 0; i < particles.Length; i++)
            {
                Particle p = engine.Particles[i];
                Sprite sp = particles[i];
                sp.SetVisible(p.Active);
                if (!p.Active) continue;
                sp.MoveTo(p.X - ParticleSize * 0.5, p.Y - ParticleSize * 0.5);
                // Quantised to 10 steps so opacity is not re-set every frame.
                double o = System.Math.Ceiling(p.Life / p.MaxLife * 10.0) / 10.0;
                sp.SetOpacity(o);
            }

            // Power-ups
            for (int i = 0; i < powerUps.Length; i++)
            {
                PowerUp p = engine.PowerUps[i];
                Sprite sp = powerUps[i];
                sp.SetVisible(p.Active);
                if (!p.Active) continue;
                if (powerUpShownType[i] != p.Type)
                {
                    powerUpShownType[i] = p.Type;
                    powerUpLabels[i].Text = PowerUpLabel(p.Type);
                }
                sp.MoveTo(p.X - PowerUp.Size * 0.5f, p.Y - PowerUp.Size * 0.5f);
            }

            // Enemy bullets
            for (int i = 0; i < enemyBullets.Length; i++)
            {
                Bullet b = engine.EnemyBullets[i];
                Sprite sp = enemyBullets[i];
                sp.SetVisible(b.Active);
                if (!b.Active) continue;
                sp.MoveTo(b.X - EnemyBulletSize * 0.5, b.Y - EnemyBulletSize * 0.5);
            }

            // Enemies
            for (int i = 0; i < enemies.Length; i++)
            {
                Enemy e = engine.Enemies[i];
                Sprite sp = enemies[i];
                sp.SetVisible(e.Active);
                if (!e.Active) continue;
                if (enemyShownType[i] != e.Type || enemyShownWidth[i] != e.Width)
                {
                    enemyShownType[i] = e.Type;
                    enemyShownWidth[i] = e.Width;
                    SetupEnemyShape(enemyShapes[i], e);
                }
                sp.MoveTo(e.X - e.Width * 0.5f, e.Y - e.Height * 0.5f);
                sp.SetOpacity(e.FlashTimer > 0f ? 0.4 : 1.0); // flash when hit
            }

            // Player bullets
            for (int i = 0; i < playerBullets.Length; i++)
            {
                Bullet b = engine.PlayerBullets[i];
                Sprite sp = playerBullets[i];
                sp.SetVisible(b.Active);
                if (!b.Active) continue;
                sp.MoveTo(b.X - PlayerBulletWidth * 0.5, b.Y - PlayerBulletHeight * 0.5);
            }

            // Player + shield
            Player pl = engine.Player;
            bool showPlayer = engine.IsPlayerVisible;
            player.SetVisible(showPlayer);
            if (showPlayer)
            {
                player.MoveTo(pl.X - Player.Width * 0.5f, pl.Y - Player.Height * 0.5f);
            }
            bool showShield = engine.IsShieldVisible;
            shield.SetVisible(showShield);
            if (showShield)
            {
                shield.MoveTo(pl.X - ShieldSize * 0.5, pl.Y - ShieldSize * 0.5);
            }
        }

        /// <summary>Letter shown inside a power-up box.</summary>
        public static string PowerUpLabel(PowerUpType type)
        {
            switch (type)
            {
                case PowerUpType.DoubleShot: return "2";
                case PowerUpType.TripleShot: return "3";
                case PowerUpType.Shield: return "S";
                case PowerUpType.ExtraLife: return "+";
                default: return "$";
            }
        }

        /// <summary>Gives an enemy polygon the look of its type. Only runs when a pooled
        /// shape is reused for a different enemy type, never every frame.</summary>
        private void SetupEnemyShape(Polygon p, Enemy e)
        {
            double w = e.Width;
            double h = e.Height;
            switch (e.Type)
            {
                case EnemyType.ZigZag:
                    // Filled diamond.
                    p.Points = MakePoints(new double[] { w / 2, 0, w, h / 2, w / 2, h, 0, h / 2 });
                    p.Fill = whiteBrush;
                    p.Stroke = null;
                    p.StrokeThickness = 0;
                    break;

                case EnemyType.Shooter:
                    // Arrow head pointing down (towards the player).
                    p.Points = MakePoints(new double[] { 0, 0, w / 2, h * 0.35, w, 0, w / 2, h });
                    p.Fill = whiteBrush;
                    p.Stroke = null;
                    p.StrokeThickness = 0;
                    break;

                case EnemyType.Tank:
                    // Big hollow square with a thick outline.
                    p.Points = MakePoints(new double[] { 2, 2, w - 2, 2, w - 2, h - 2, 2, h - 2 });
                    p.Fill = blackBrush;
                    p.Stroke = whiteBrush;
                    p.StrokeThickness = 4;
                    break;

                case EnemyType.Boss:
                    // Wide grey mothership with white outline.
                    p.Points = MakePoints(new double[]
                    {
                        0, h * 0.3,  w * 0.2, 0,  w * 0.8, 0,  w, h * 0.3,
                        w, h * 0.7,  w * 0.75, h,  w * 0.6, h * 0.8,  w * 0.4, h * 0.8,
                        w * 0.25, h,  0, h * 0.7
                    });
                    p.Fill = greyBrush;
                    p.Stroke = whiteBrush;
                    p.StrokeThickness = 3;
                    break;

                default:
                    // Basic: filled square.
                    p.Points = MakePoints(new double[] { 0, 0, w, 0, w, h, 0, h });
                    p.Fill = whiteBrush;
                    p.Stroke = null;
                    p.StrokeThickness = 0;
                    break;
            }
        }

        private static PointCollection MakePoints(double[] xy)
        {
            PointCollection points = new PointCollection();
            for (int i = 0; i + 1 < xy.Length; i += 2)
            {
                points.Add(new Point(xy[i], xy[i + 1]));
            }
            return points;
        }
    }
}
