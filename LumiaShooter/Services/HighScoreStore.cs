// ---------------------------------------------------------------------------
// HighScoreStore.cs - saves the best score in the app's local settings
// (ApplicationData.Current.LocalSettings). Survives app restarts; removed
// only when the app is uninstalled.
// ---------------------------------------------------------------------------

using System;
using Windows.Storage;

namespace LumiaShooter.Services
{
    public static class HighScoreStore
    {
        private const string HighScoreKey = "HighScore";

        public static int Load()
        {
            try
            {
                object value;
                if (ApplicationData.Current.LocalSettings.Values.TryGetValue(HighScoreKey, out value)
                    && value is int)
                {
                    return (int)value;
                }
            }
            catch (Exception)
            {
                // Settings unavailable - just start from 0.
            }
            return 0;
        }

        /// <summary>Stores the score if it is better than the saved one.</summary>
        public static void SaveIfHigher(int score)
        {
            try
            {
                if (score > Load())
                {
                    ApplicationData.Current.LocalSettings.Values[HighScoreKey] = score;
                }
            }
            catch (Exception)
            {
                // Not critical.
            }
        }
    }
}
