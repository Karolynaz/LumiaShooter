// ---------------------------------------------------------------------------
// PlatformHelper.cs - phone vs. PC specifics, WITHOUT referencing the
// "Windows Mobile Extensions for the UWP" SDK:
//   * portrait lock
//   * full screen on the phone (hides the status bar and the on-screen
//     navigation bar) via ApplicationView.TryEnterFullScreenMode
//   * a phone-shaped window when testing on a PC
// Every API used here exists since Windows 10 build 10240; newer optional
// APIs are guarded with ApiInformation.
// ---------------------------------------------------------------------------

using System;
using Windows.Foundation;
using Windows.Foundation.Metadata;
using Windows.Graphics.Display;
using Windows.System.Profile;
using Windows.UI.ViewManagement;

namespace LumiaShooter.Services
{
    public static class PlatformHelper
    {
        private static bool? isMobile;

        /// <summary>True on Windows 10 Mobile (phones).</summary>
        public static bool IsMobile
        {
            get
            {
                if (!isMobile.HasValue)
                {
                    try
                    {
                        isMobile = string.Equals(AnalyticsInfo.VersionInfo.DeviceFamily, "Windows.Mobile",
                            StringComparison.OrdinalIgnoreCase);
                    }
                    catch (Exception)
                    {
                        isMobile = false;
                    }
                }
                return isMobile.Value;
            }
        }

        /// <summary>Lock the game to portrait (the manifest also asks for portrait).</summary>
        public static void LockPortrait()
        {
            try
            {
                DisplayInformation.AutoRotationPreferences = DisplayOrientations.Portrait;
            }
            catch (Exception)
            {
                // Not supported on some PCs - harmless.
            }
        }

        /// <summary>On a PC, open in a phone-shaped window (9:16) for testing.</summary>
        public static void ConfigureLaunchWindow()
        {
            if (IsMobile) return;
            try
            {
                ApplicationView.PreferredLaunchViewSize = new Size(405, 720);
                ApplicationView.PreferredLaunchWindowingMode = ApplicationViewWindowingMode.PreferredLaunchViewSize;
            }
            catch (Exception)
            {
                // Not critical.
            }
        }

        public static void SetMinimumWindowSize()
        {
            if (IsMobile) return;
            try
            {
                ApplicationView.GetForCurrentView().SetPreferredMinSize(new Size(240, 400));
            }
            catch (Exception)
            {
                // Not critical.
            }
        }

        /// <summary>Phone only: hide status bar + navigation bar (full screen).</summary>
        public static void EnterFullScreenOnMobile()
        {
            if (!IsMobile) return;
            try
            {
                ApplicationView view = ApplicationView.GetForCurrentView();
                if (ApiInformation.IsPropertyPresent("Windows.UI.ViewManagement.ApplicationView",
                    "FullScreenSystemOverlayMode"))
                {
                    // Edge swipes show only a small handle instead of the whole bar,
                    // so a child is less likely to leave the game by accident.
                    view.FullScreenSystemOverlayMode = FullScreenSystemOverlayMode.Minimal;
                }
                view.TryEnterFullScreenMode();
            }
            catch (Exception)
            {
                // Full screen is a nice-to-have.
            }
        }
    }
}
