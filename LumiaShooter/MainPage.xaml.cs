// ---------------------------------------------------------------------------
// MainPage.xaml.cs - the only page. Connects UWP to the game:
//   * game loop (CompositionTarget.Rendering + Stopwatch delta time)
//   * touch / mouse / keyboard input -> GameEngine
//   * Back button, suspend / hide -> pause
//   * HUD and message texts
//   * keeps the screen on while playing
// ---------------------------------------------------------------------------

using System;
using System.Diagnostics;
using LumiaShooter.Game;
using LumiaShooter.Rendering;
using LumiaShooter.Services;
using Windows.ApplicationModel;
using Windows.Devices.Input;
using Windows.System;
using Windows.System.Display;
using Windows.UI.Core;
using Windows.UI.Input;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;

namespace LumiaShooter
{
    public sealed partial class MainPage : Page
    {
        private readonly GameEngine engine;
        private readonly GameRenderer renderer;
        private readonly Stopwatch clock = new Stopwatch();
        private long lastTicks;
        private bool hooked;

        private uint activePointerId;
        private bool hasActivePointer;

        private DisplayRequest displayRequest;
        private bool displayRequestActive;
        private GameState lastState = (GameState)(-1);

        // Cached HUD values, so TextBlocks are only updated when something changes.
        private int shownScore = -1;
        private int shownLives = -1;
        private int shownLevel = -1;
        private double shownProgress = -1;
        private int shownPowerKey = -1;
        private bool shownBossBar;
        private double shownBossFraction = -1;
        private string shownPickup = "";
        private int shownOverlayKey = -1;
        private bool shownHud;

        public MainPage()
        {
            this.InitializeComponent();

            engine = new GameEngine(HighScoreStore.Load());
            engine.NewHighScore += OnNewHighScore;
            renderer = new GameRenderer(engine, StarLayer, EntityLayer);

            InputRoot.PointerPressed += OnPointerPressed;
            InputRoot.PointerMoved += OnPointerMoved;
            InputRoot.PointerReleased += OnPointerReleased;
            InputRoot.PointerCanceled += OnPointerReleased;
            InputRoot.PointerCaptureLost += OnPointerReleased;

            Loaded += OnLoaded;
            Unloaded += OnUnloaded;
        }

        // ---- Lifetime --------------------------------------------------------

        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            PlatformHelper.SetMinimumWindowSize();
            PlatformHelper.EnterFullScreenOnMobile();

            if (hooked) return;
            hooked = true;

            SystemNavigationManager.GetForCurrentView().BackRequested += OnBackRequested;
            Window.Current.VisibilityChanged += OnVisibilityChanged;
            Window.Current.Activated += OnWindowActivated;
            Window.Current.CoreWindow.KeyDown += OnKeyDown;
            Application.Current.Suspending += OnSuspending;
            Application.Current.Resuming += OnResuming;

            clock.Start();
            lastTicks = clock.ElapsedTicks;
            CompositionTarget.Rendering += OnRendering;
        }

        private void OnUnloaded(object sender, RoutedEventArgs e)
        {
            if (!hooked) return;
            hooked = false;

            CompositionTarget.Rendering -= OnRendering;
            SystemNavigationManager.GetForCurrentView().BackRequested -= OnBackRequested;
            Window.Current.VisibilityChanged -= OnVisibilityChanged;
            Window.Current.Activated -= OnWindowActivated;
            Window.Current.CoreWindow.KeyDown -= OnKeyDown;
            Application.Current.Suspending -= OnSuspending;
            Application.Current.Resuming -= OnResuming;
            SetKeepScreenOn(false);
            clock.Stop();
        }

        private void OnSuspending(object sender, SuspendingEventArgs e)
        {
            engine.Pause();
            HighScoreStore.SaveIfHigher(engine.BestScoreSoFar);
            SetKeepScreenOn(false);
        }

        private void OnResuming(object sender, object e)
        {
            // Stay paused; the player taps to continue. Avoid a huge first time step.
            lastTicks = clock.ElapsedTicks;
            PlatformHelper.EnterFullScreenOnMobile();
        }

        private void OnVisibilityChanged(object sender, VisibilityChangedEventArgs e)
        {
            if (!e.Visible)
            {
                engine.Pause();
                HighScoreStore.SaveIfHigher(engine.BestScoreSoFar);
            }
            lastTicks = clock.ElapsedTicks;
        }

        private void OnWindowActivated(object sender, WindowActivatedEventArgs e)
        {
            // Window lost focus (e.g. notification centre pulled down): pause.
            if (e.WindowActivationState == CoreWindowActivationState.Deactivated)
            {
                engine.Pause();
            }
        }

        private void OnNewHighScore(int score)
        {
            HighScoreStore.SaveIfHigher(score);
        }

        // ---- Back button / keyboard -----------------------------------------

        private void OnBackRequested(object sender, BackRequestedEventArgs e)
        {
            // Playing -> pause. Paused / game over -> title. Title -> not handled,
            // so the phone does its normal thing (leaves the app).
            if (engine.HandleBack())
            {
                e.Handled = true;
            }
        }

        private void OnKeyDown(CoreWindow sender, KeyEventArgs args)
        {
            switch (args.VirtualKey)
            {
                case VirtualKey.Escape:
                    engine.HandleBack();
                    args.Handled = true;
                    break;
                case VirtualKey.P:
                    if (engine.State == GameState.Paused) engine.Resume();
                    else engine.Pause();
                    args.Handled = true;
                    break;
                case VirtualKey.Enter:
                case VirtualKey.Space:
                    engine.Tap();
                    args.Handled = true;
                    break;
            }
        }

        // ---- Pointer input (touch + mouse + pen) -----------------------------

        private void OnPointerPressed(object sender, PointerRoutedEventArgs e)
        {
            bool isMouse = e.Pointer.PointerDeviceType == PointerDeviceType.Mouse;
            // The newest finger always takes control (robust if a "release" was missed).
            activePointerId = e.Pointer.PointerId;
            hasActivePointer = true;
            InputRoot.CapturePointer(e.Pointer);

            PointerPoint p = e.GetCurrentPoint(GameRoot); // already in 360x640 world units
            engine.PointerDown((float)p.Position.X, (float)p.Position.Y, isMouse);
            e.Handled = true;
        }

        private void OnPointerMoved(object sender, PointerRoutedEventArgs e)
        {
            bool isMouse = e.Pointer.PointerDeviceType == PointerDeviceType.Mouse;
            if (isMouse)
            {
                PointerPoint mp = e.GetCurrentPoint(GameRoot);
                engine.PointerMove((float)mp.Position.X, (float)mp.Position.Y, true, mp.IsInContact);
            }
            else if (hasActivePointer && e.Pointer.PointerId == activePointerId)
            {
                PointerPoint tp = e.GetCurrentPoint(GameRoot);
                engine.PointerMove((float)tp.Position.X, (float)tp.Position.Y, false, true);
            }
            e.Handled = true;
        }

        private void OnPointerReleased(object sender, PointerRoutedEventArgs e)
        {
            if (hasActivePointer && e.Pointer.PointerId == activePointerId)
            {
                hasActivePointer = false;
            }
        }

        // ---- Game loop -----------------------------------------------------------

        private void OnRendering(object sender, object e)
        {
            long now = clock.ElapsedTicks;
            float dt = (float)((now - lastTicks) / (double)Stopwatch.Frequency);
            lastTicks = now;

            engine.Update(dt);
            renderer.Render();
            UpdateHud();
            UpdateOverlay();
            UpdateSystemState();
        }

        /// <summary>Things that only need to change when the game state changes.</summary>
        private void UpdateSystemState()
        {
            GameState state = engine.State;
            if (state == lastState) return;
            lastState = state;

            bool active = state == GameState.Playing || state == GameState.LevelIntro;
            SetKeepScreenOn(active);

            // PC: show the title-bar back arrow everywhere except the title screen.
            try
            {
                SystemNavigationManager.GetForCurrentView().AppViewBackButtonVisibility =
                    state == GameState.Title ? AppViewBackButtonVisibility.Collapsed : AppViewBackButtonVisibility.Visible;
            }
            catch (Exception)
            {
                // Not critical.
            }
        }

        /// <summary>Keep the phone screen from turning off while a game is running.</summary>
        private void SetKeepScreenOn(bool keepOn)
        {
            if (keepOn == displayRequestActive) return;
            try
            {
                if (displayRequest == null) displayRequest = new DisplayRequest();
                if (keepOn) displayRequest.RequestActive();
                else displayRequest.RequestRelease();
                displayRequestActive = keepOn;
            }
            catch (Exception)
            {
                // Not critical.
            }
        }

        // ---- HUD -------------------------------------------------------------------

        private void UpdateHud()
        {
            bool showHud = engine.State != GameState.Title;
            if (showHud != shownHud)
            {
                shownHud = showHud;
                HudLayer.Visibility = showHud ? Visibility.Visible : Visibility.Collapsed;
            }
            if (!showHud) return;

            if (engine.Score != shownScore)
            {
                shownScore = engine.Score;
                ScoreText.Text = shownScore.ToString();
            }
            if (engine.Lives != shownLives)
            {
                shownLives = engine.Lives;
                LivesText.Text = "x" + shownLives;
            }
            if (engine.LevelNumber != shownLevel)
            {
                shownLevel = engine.LevelNumber;
                LevelText.Text = "LEVEL " + shownLevel + "/" + engine.LevelCount;
            }

            double progress = engine.LevelProgress;
            if (Math.Abs(progress - shownProgress) > 0.002 || (progress == 0 && shownProgress != 0))
            {
                shownProgress = progress;
                ProgressScale.ScaleX = progress;
            }

            int dbl = SecondsLeft(engine.DoubleShotTimer);
            int tri = SecondsLeft(engine.TripleShotTimer);
            int shd = SecondsLeft(engine.ShieldTimer);
            int powerKey = dbl + tri * 100 + shd * 10000;
            if (powerKey != shownPowerKey)
            {
                shownPowerKey = powerKey;
                string text = "";
                if (tri > 0) text += "TRIPLE " + tri + "   ";
                if (dbl > 0) text += "DOUBLE " + dbl + "   ";
                if (shd > 0) text += "SHIELD " + shd;
                PowerText.Text = text;
            }

            bool bossBar = engine.Boss != null;
            if (bossBar != shownBossBar)
            {
                shownBossBar = bossBar;
                BossBar.Visibility = bossBar ? Visibility.Visible : Visibility.Collapsed;
            }
            if (bossBar)
            {
                double f = engine.BossHealthFraction;
                if (f != shownBossFraction)
                {
                    shownBossFraction = f;
                    BossScale.ScaleX = f;
                }
            }

            string pickup = engine.PickupMessage ?? "";
            if (!string.Equals(pickup, shownPickup))
            {
                shownPickup = pickup;
                PickupText.Text = pickup;
            }
        }

        private static int SecondsLeft(float timer)
        {
            return timer > 0f ? (int)Math.Ceiling(timer) : 0;
        }

        // ---- Centered messages ------------------------------------------------

        private void UpdateOverlay()
        {
            int key = (int)engine.State * 1000 + engine.LevelIndex * 10
                + (engine.IsLevelCleared ? 1 : 0)
                + (engine.IsBossWarning ? 2 : 0)
                + (engine.CanTapToContinue ? 4 : 0);
            if (key == shownOverlayKey) return;
            shownOverlayKey = key;

            bool overlay = true;
            bool dim = false;
            bool legend = false;
            string title = "", line1 = "", line2 = "", line3 = "";
            string highScoreLine = engine.IsNewHighScore
                ? "NEW HIGH SCORE!"
                : "HIGH SCORE " + engine.HighScore;

            switch (engine.State)
            {
                case GameState.Title:
                    title = "LUMIA SHOOTER";
                    line1 = "TAP TO START";
                    line2 = "HIGH SCORE " + engine.HighScore;
                    line3 = "DRAG YOUR FINGER TO FLY.\nYOUR SHIP SHOOTS BY ITSELF!";
                    legend = true;
                    break;

                case GameState.LevelIntro:
                    title = "LEVEL " + engine.LevelNumber;
                    line1 = engine.Level.Subtitle;
                    break;

                case GameState.Playing:
                    if (engine.IsLevelCleared)
                    {
                        title = "LEVEL " + engine.LevelNumber + " CLEAR!";
                        line1 = "BONUS +" + engine.LastLevelBonus;
                    }
                    else if (engine.IsBossWarning)
                    {
                        title = "WARNING!";
                        line1 = "BOSS IS COMING!";
                    }
                    else
                    {
                        overlay = false;
                    }
                    break;

                case GameState.Paused:
                    dim = true;
                    title = "PAUSED";
                    line1 = "TAP TO CONTINUE";
                    line2 = "BACK = MAIN MENU";
                    break;

                case GameState.GameOver:
                    dim = true;
                    title = "GAME OVER";
                    line1 = "SCORE " + engine.Score;
                    line2 = highScoreLine;
                    line3 = engine.CanTapToContinue ? "TAP TO PLAY AGAIN" : "";
                    break;

                case GameState.Victory:
                    dim = true;
                    title = "YOU WIN!";
                    line1 = "SCORE " + engine.Score;
                    line2 = highScoreLine;
                    line3 = engine.CanTapToContinue ? "TAP TO PLAY AGAIN" : "";
                    break;
            }

            OverlayPanel.Visibility = overlay ? Visibility.Visible : Visibility.Collapsed;
            DimRect.Visibility = dim ? Visibility.Visible : Visibility.Collapsed;
            LegendPanel.Visibility = legend ? Visibility.Visible : Visibility.Collapsed;
            SetLine(OverlayTitle, title);
            SetLine(OverlayLine1, line1);
            SetLine(OverlayLine2, line2);
            SetLine(OverlayLine3, line3);
        }

        private static void SetLine(TextBlock block, string text)
        {
            block.Text = text ?? "";
            block.Visibility = string.IsNullOrEmpty(text) ? Visibility.Collapsed : Visibility.Visible;
        }
    }
}
