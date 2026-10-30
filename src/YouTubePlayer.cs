using System;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using System.Windows.Forms;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.WinForms;

namespace Astera
{
    static class YouTubePlayer
    {
        internal const string Host = "reshawnstar.astera.gamelauncher", Origin = "https://reshawnstar.astera.gamelauncher";
        internal static string Html(string id)
        {
            if (YouTubeLinks.Id("https://youtu.be/" + id) != id)
                throw new IOException("Invalid YouTube video link");
            return "<!doctype html><meta charset='utf-8'><meta name='referrer' content='strict-origin-when-cross-origin'><style>html,body,#player{width:100%;height:100%;margin:0;background:#0c1121;color:white;overflow:hidden}</style><div id='player'></div><script>var player;window.asteraState='loading';function onYouTubeIframeAPIReady(){player=new YT.Player('player',{host:'https://www.youtube-nocookie.com',videoId:'" + id + "',playerVars:{autoplay:0,playsinline:1,origin:'" + Origin + "'},events:{onReady:function(){window.asteraState='ready'},onError:function(e){window.asteraState='error:'+e.data}}});}var s=document.createElement('script');s.src='https://www.youtube.com/iframe_api';document.head.appendChild(s);</script>";
        }

        internal static void Open(IWin32Window owner, string root, string id)
        {
            try
            {
                EmbeddedWebView.Prepare(root);
                ShowPrepared(owner, root, id);
            }
            catch (Exception error)
            {
                AsteraDiagnostics.Write("Embedded video unavailable", error);
                MessageBox.Show(owner, "The video player could not start. Microsoft Edge WebView2 is required to watch videos inside Astera.", "Update video");
            }
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        static void ShowPrepared(IWin32Window owner, string root, string id)
        {
            using (var form = new YouTubeDialog(root, id))
                form.ShowDialog(owner);
        }
    }

    sealed class YouTubeDialog : AsteraDialog
    {
        readonly string root, id;
        readonly WebView2 browser = new WebView2
        {
            Dock = DockStyle.Fill,
            DefaultBackgroundColor = Color.FromArgb(12, 17, 33)
        };
        readonly Label message = new Label
        {
            Dock = DockStyle.Top,
            Height = 30,
            ForeColor = Color.LightSteelBlue,
            Text = "Loading video…"
        };
        internal WebView2 Browser
        {
            get
            {
                return browser;
            }
        }

        internal Task Initialization
        {
            get; private set;
        }

        internal YouTubeDialog(string stateRoot, string video)
        {
            root = stateRoot;
            id = video;
            Text = "Update video";
            Size = new Size(960, 620);
            MinimumSize = new Size(600, 420);
            StartPosition = FormStartPosition.CenterParent;
            BackColor = Color.FromArgb(12, 17, 33);
            var footer = new FlowLayoutPanel
            {
                Dock = DockStyle.Bottom,
                Height = 45
            };
            var external = new AsteraButton
            {
                Text = "Open on YouTube",
                AutoSize = true
            };
            external.Click += delegate
            {
                Process.Start(new ProcessStartInfo("https://www.youtube.com/watch?v=" + id) { UseShellExecute = true });
            };
            var close = new AsteraButton
            {
                Text = "Close",
                AutoSize = true
            };
            close.Click += delegate
            {
                Close();
            };
            footer.Controls.Add(external);
            footer.Controls.Add(close);
            Controls.Add(browser);
            Controls.Add(message);
            Controls.Add(footer);
            Shown += delegate
            {
                Initialization = Initialize();
            };
            FormClosed += delegate
            {
                browser.Dispose();
            };
        }

        async Task Initialize()
        {
            try
            {
                CoreWebView2Environment.SetLoaderDllFolderPath(EmbeddedWebView.Prepare(root));
                var environment = await CoreWebView2Environment.CreateAsync(null, Path.Combine(root, "video-profile"));
                if (IsDisposed)
                    return;
                await browser.EnsureCoreWebView2Async(environment);
                if (IsDisposed)
                    return;
                var core = browser.CoreWebView2;
                core.Settings.AreDevToolsEnabled = false;
                core.Settings.AreDefaultContextMenusEnabled = false;
                core.Settings.IsStatusBarEnabled = false;
                core.PermissionRequested += delegate (object sender, CoreWebView2PermissionRequestedEventArgs e)
                {
                    e.State = CoreWebView2PermissionState.Deny;
                };
                core.DownloadStarting += delegate (object sender, CoreWebView2DownloadStartingEventArgs e)
                {
                    e.Cancel = true;
                };
                core.NewWindowRequested += delegate (object sender, CoreWebView2NewWindowRequestedEventArgs e)
                {
                    e.Handled = true;
                };
                core.NavigationStarting += delegate (object sender, CoreWebView2NavigationStartingEventArgs e)
                {
                    Uri u;
                    if (!Uri.TryCreate(e.Uri, UriKind.Absolute, out u) || u.Host != YouTubePlayer.Host || u.Scheme != "https")
                        e.Cancel = true;
                };
                string content = Path.Combine(root, "video-content");
                Directory.CreateDirectory(content);
                File.WriteAllText(Path.Combine(content, "player.html"), YouTubePlayer.Html(id));
                core.SetVirtualHostNameToFolderMapping(YouTubePlayer.Host, content, CoreWebView2HostResourceAccessKind.DenyCors);
                core.Navigate(YouTubePlayer.Origin + "/player.html");
                message.Text = "YouTube · Press Play to watch";
            }
            catch (Exception error)
            {
                if (!IsDisposed)
                {
                    message.Text = "Video unavailable here. You can open it on YouTube.";
                    AsteraDiagnostics.Write("YouTube player initialization failed", error);
                }
            }
        }
    }
}
