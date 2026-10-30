using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace Astera
{
    static class YouTubeAudit
    {
        internal static int Run(string root, bool live)
        {
            try
            {
                if (Directory.Exists(root))
                    throw new IOException("Use a new video audit folder");
                Directory.CreateDirectory(root);
                var checks = new List<string>();
                string id = "M7lc1UVf-VE";
                foreach (string url in new[]
                {
                    "https://youtu.be/" + id,
                    "https://www.youtube.com/watch?v=" + id + "&t=3",
                    "https://youtube.com/shorts/" + id,
                    "https://www.youtube-nocookie.com/embed/" + id
                }

                )
                    if (YouTubeLinks.Id(url) != id)
                        throw new IOException("A supported YouTube URL failed");
                foreach (string url in new[]
                {
                    "http://youtu.be/" + id,
                    "https://youtube.com.evil/watch?v=" + id,
                    "https://u:p@youtube.com/watch?v=" + id,
                    "javascript:alert(1)",
                    "https://youtu.be/not-valid"
                }

                )
                    if (YouTubeLinks.Id(url) != null)
                        throw new IOException("An unsafe or invalid video URL was accepted");
                checks.Add("Only validated HTTPS YouTube video URLs and exact video IDs are accepted");
                string tags = YouTubeLinks.SteamTags("[previewyoutube=" + id + ";full][/previewyoutube]");
                if (YouTubeLinks.Find(tags).Length != 1 || YouTubeLinks.Find(tags + tags).Length != 1)
                    throw new IOException("Steam video tags or deduplication failed");
                checks.Add("Steam previewyoutube tags survive as playable deduplicated video links");
                string html = YouTubePlayer.Html(id);
                if (!html.Contains("autoplay:0") || !html.Contains("strict-origin-when-cross-origin") || !html.Contains(YouTubePlayer.Origin))
                    throw new IOException("Player identity or no-autoplay policy missing");
                checks.Add("Embedded player uses its app origin/referrer and does not autoplay");
                EmbeddedWebView.Prepare(root);
                if (!File.Exists(Path.Combine(root, "runtime", "webview2", "1.0.4314.35", "WebView2Loader.dll")))
                    throw new IOException("Embedded runtime adapters missing");
                checks.Add("Single-file release extracts its pinned embedded WebView2 adapters without changing game files");
                if (live)
                    return LivePrepared(root, id, checks);
                File.WriteAllText(Path.Combine(root, "result.json"), MC2Pack.Engine.Json.Serialize(new
                {
                    Passed = true,
                    Checks = checks,
                    LiveGamesChanged = false,
                    VideoLoaded = false
                }));
                return 0;
            }
            catch (Exception error)
            {
                Directory.CreateDirectory(root);
                File.WriteAllText(Path.Combine(root, "failure.txt"), error.ToString());
                return 1;
            }
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        static int LivePrepared(string root, string id, List<string> checks)
        {
            return LiveVideoAudit.Run(root, id, checks);
        }
    }
}
