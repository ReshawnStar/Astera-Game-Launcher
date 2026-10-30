using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Windows.Forms;

namespace Astera
{
    static partial class RobloxSupport
    {
        static IEnumerable<string> CompletedImages(object obj, int depth = 0)
        {
            if (obj == null || depth > 5)
                yield break;
            var map = Map(obj);
            if (map != null)
            {
                object state;
                if (map.TryGetValue("state", out state))
                {
                    if (!String.Equals(Convert.ToString(state), "Completed", StringComparison.OrdinalIgnoreCase))
                        yield break;
                    string url = Convert.ToString(Field(obj, "imageUrl"));
                    if (ImageAddress(url) && url.IndexOf("/UnapprovedImage/", StringComparison.OrdinalIgnoreCase) < 0)
                        yield return url;
                    yield break;
                }

                foreach (var item in map.Values)
                    foreach (var url in CompletedImages(item, depth + 1))
                        yield return url;
            }

            var array = obj as object[];
            if (array != null)
                foreach (var item in array)
                    foreach (var url in CompletedImages(item, depth + 1))
                        yield return url;
        }

        internal static string[] Gallery(object response, long universe)
        {
            var data = Field(response, "data") as object[];
            if (data == null)
                return new string[0];
            return data.Where(item => Number(item, "universeId") == universe).SelectMany(item => CompletedImages(Field(item, "thumbnails"))).Distinct().Take(10).ToArray();
        }

        internal static string CacheGallery(string url, string root, long universe, CancellationToken token)
        {
            if (!ImageAddress(url))
                return null;
            string key;
            using (var sha = SHA256.Create())
                key = BitConverter.ToString(sha.ComputeHash(Encoding.UTF8.GetBytes(url))).Replace("-", "").Substring(0, 20);
            return Cache(url, root, universe, "banner", token, key);
        }
    }

    sealed partial class LibraryForm
    {
        readonly System.Windows.Forms.Timer robloxSlideClock = new System.Windows.Forms.Timer
        {
            Interval = 12000
        };
        string robloxSlideKey = "";
        int robloxSlideIndex;
        string[] RobloxSlides(CustomGame game)
        {
            return game == null || game.RobloxArtworkVersion < 2 ? new string[0] : (game.RobloxBanners ?? new string[0]).Where(p => !String.IsNullOrEmpty(p) && File.Exists(p)).Distinct(StringComparer.OrdinalIgnoreCase).Take(10).ToArray();
        }

        string RobloxBackground(CustomGame game)
        {
            var slides = RobloxSlides(game);
            if (slides.Length == 0)
                return null;
            return slides[IsRoblox && CurrentCustom.Id == game.Id ? robloxSlideIndex % slides.Length : 0];
        }

        void UpdateRobloxSlidePlayback()
        {
            if (IsDisposed || Disposing)
                return;
            var paths = IsRoblox ? RobloxSlides(CurrentCustom) : new string[0];
            string key = IsRoblox ? CurrentCustom.Id + "|" + String.Join("|", paths) : "";
            if (key != robloxSlideKey)
            {
                robloxSlideKey = key;
                robloxSlideIndex = 0;
                robloxSlideClock.Stop();
            }

            robloxSlideClock.Enabled = IsRoblox && String.IsNullOrEmpty(CurrentCustom.Background) && paths.Length > 1 && UiEffects.Enabled && Visible && WindowState != FormWindowState.Minimized;
        }

        void AdvanceRobloxSlide()
        {
            UpdateRobloxSlidePlayback();
            if (!robloxSlideClock.Enabled)
                return;
            var paths = RobloxSlides(CurrentCustom);
            robloxSlideIndex = (robloxSlideIndex + 1) % paths.Length;
            UpdateCustomLayout();
        }

        void StartRobloxSlides()
        {
            robloxSlideClock.Tick += delegate
            {
                AdvanceRobloxSlide();
            };
            VisibleChanged += RobloxSlideStateChanged;
            Resize += RobloxSlideStateChanged;
            UiEffects.Changed += RobloxSlideStateChanged;
            Disposed += delegate
            {
                UiEffects.Changed -= RobloxSlideStateChanged;
                robloxSlideClock.Dispose();
            };
        }

        void RobloxSlideStateChanged(object sender, EventArgs e)
        {
            UpdateRobloxSlidePlayback();
        }
    }
}
