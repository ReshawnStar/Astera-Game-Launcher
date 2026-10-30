using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace Astera
{
    sealed partial class LibraryForm
    {
        sealed class LibraryIconResult : IDisposable
        {
            internal string Path;
            internal Image Image;
            public void Dispose()
            {
                if (Image != null)
                {
                    Image.Dispose();
                    Image = null;
                }
            }
        }

        readonly Dictionary<string, Image> visibleLibraryIcons = new Dictionary<string, Image>();
        readonly Dictionary<string, string> visibleLibraryKeys = new Dictionary<string, string>();
        readonly Dictionary<string, string> visibleLibraryPaths = new Dictionary<string, string>();
        readonly Dictionary<string, DateTime> libraryIconAttempts = new Dictionary<string, DateTime>();
        readonly HashSet<string> libraryIconsPending = new HashSet<string>();
        readonly SemaphoreSlim libraryIconGate = new SemaphoreSlim(2);
        bool libraryIconsPosted;
        static string LibraryIconKey(CustomGame game)
        {
            return String.Join("|", new[] { game.Id, game.Executable, game.Platform, game.PlatformId, game.Icon, game.RobloxIcon, game.UniverseId.ToString(), game.ManagedProjectId });
        }

        string VisibleLibraryIconPath(CustomGame game)
        {
            string path;
            return game != null && visibleLibraryPaths.TryGetValue(game.Id, out path) ? path : null;
        }

        Image VisibleLibraryIcon(CustomGame game)
        {
            Image image;
            return game != null && visibleLibraryIcons.TryGetValue(game.Id, out image) ? image : null;
        }

        void ScheduleLibraryIcons()
        {
            if (previewMode || libraryIconsPosted || IsDisposed || Disposing || !Visible || !IsHandleCreated)
                return;
            libraryIconsPosted = true;
            BeginInvoke((Action)delegate
            {
                libraryIconsPosted = false;
                if (!IsDisposed && !Disposing && Visible && WindowState != FormWindowState.Minimized)
                    WarmVisibleLibraryIcons(startupScreen == null);
            });
        }

        void WarmVisibleLibraryIcons(bool online)
        {
            if (IsDisposed || Disposing || games.Items.Count == 0)
                return;
            int first = games.TopIndex, last = Math.Min(games.Items.Count, first + games.ClientSize.Height / Math.Max(1, games.ItemHeight) + 1);
            for (int i = first; i < last; i++)
            {
                var row = games.Items[i] as LibraryEntry;
                if (row != null && row.Custom != null)
                    QueueLibraryIcon(row.Custom, online);
            }
        }

        bool IsLibraryIconVisible(string id)
        {
            if (IsDisposed || Disposing || !Visible || WindowState == FormWindowState.Minimized)
                return false;
            int first = games.Items.Count == 0 ? 0 : games.TopIndex, last = Math.Min(games.Items.Count, first + games.ClientSize.Height / Math.Max(1, games.ItemHeight) + 1);
            for (int i = first; i < last; i++)
            {
                var row = games.Items[i] as LibraryEntry;
                if (row != null && row.Custom != null && row.Custom.Id == id)
                    return true;
            }

            return false;
        }

        static LibraryIconResult ReadLibraryIcon(string path)
        {
            if (String.IsNullOrWhiteSpace(path) || !File.Exists(path))
                return null;
            try
            {
                return new LibraryIconResult
                {
                    Path = path,
                    Image = ReadCustomIcon(path)
                };
            }
            catch (IOException)
            {
            }
            catch (ArgumentException)
            {
            }
            catch (UnauthorizedAccessException)
            {
            }

            return null;
        }

        static LibraryIconResult FindLibraryIcon(CustomGame game, string root, bool online, CancellationToken token)
        {
            token.ThrowIfCancellationRequested();
            var own = ReadLibraryIcon(game.Icon);
            if (own != null)
                return own;
            if (game.ManagedProjectId == ManagedGames.Id)
                return new LibraryIconResult
                {
                    Image = new Bitmap(GameArtwork.NebulaIcon)
                };
            if (game.Platform == "roblox")
            {
                var saved = ReadLibraryIcon(game.RobloxIcon);
                if (saved != null)
                    return saved;
                string cache = Path.Combine(root, "artwork", "roblox");
                if (game.UniverseId > 0)
                {
                    saved = ReadLibraryIcon(Path.Combine(cache, "rbx-" + game.UniverseId + "-icon.png"));
                    if (saved != null)
                        return saved;
                }

                long place;
                if (online && RobloxSupport.Positive(game.PlatformId, out place))
                {
                    var info = RobloxSupport.Lookup(place, token);
                    return ReadLibraryIcon(RobloxSupport.Cache(info.Icon, cache, info.UniverseId, "icon", token));
                }

                return null;
            }

            var steam = PlatformLibraries.SteamArt(game);
            if (steam != null)
            {
                var local = ReadLibraryIcon(steam.Icon);
                if (local != null)
                    return local;
                string cache = Path.Combine(root, "artwork", "steam");
                foreach (string path in new[]
                {
                    Path.Combine(cache, steam.AppId + "-portrait.png"),
                    SteamPublicArtwork.CachePath(cache, steam.AppId),
                    steam.Hero
                }

                )
                {
                    local = ReadLibraryIcon(path);
                    if (local != null)
                        return local;
                }

                if (online)
                {
                    string path = SteamPublicArtwork.Optional(() => SteamPublicArtwork.ResolvePortrait(steam.AppId, cache, token), token);
                    local = ReadLibraryIcon(path);
                    if (local != null)
                        return local;
                }
            }

            if (game.Platform != "steam")
            {
                var metadata = ItchMetadata.Find(game);
                if (metadata != null)
                {
                    string cache = Path.Combine(root, "artwork", "itch");
                    foreach (bool legacy in new[]
                    {
                        false,
                        true
                    }

                    )
                    {
                        var local = ReadLibraryIcon(ItchArtwork.Cached(cache, metadata, legacy));
                        if (local != null)
                            return local;
                    }

                    if (online)
                    {
                        var local = ReadLibraryIcon(SteamPublicArtwork.Optional(() => ItchArtwork.Resolve(game, cache, token), token));
                        if (local != null)
                            return local;
                    }
                }
            }

            token.ThrowIfCancellationRequested();
            if (File.Exists(game.Executable) && Path.GetExtension(game.Executable).Equals(".exe", StringComparison.OrdinalIgnoreCase))
                try
                {
                    using (var icon = Icon.ExtractAssociatedIcon(game.Executable))
                        if (icon != null)
                            return new LibraryIconResult
                            {
                                Image = icon.ToBitmap()
                            };
                }
                catch (ArgumentException)
                {
                }
                catch (IOException)
                {
                }

            return null;
        }

        bool ApplyLibraryIcon(CustomGame requested, string key, LibraryIconResult result)
        {
            if (result == null || result.Image == null || IsDisposed || Disposing)
                return false;
            var current = store.Settings.CustomGames.FirstOrDefault(g => g.Id == requested.Id);
            if (current == null || LibraryIconKey(current) != key)
                return false;
            string fingerprint = key + "|" + ArtFileKey(result.Path ?? requested.Executable), priorKey;
            if (visibleLibraryKeys.TryGetValue(current.Id, out priorKey) && priorKey == fingerprint)
                return false;
            visibleLibraryKeys[current.Id] = fingerprint;
            Image old;
            if (visibleLibraryIcons.TryGetValue(current.Id, out old))
                old.Dispose();
            visibleLibraryIcons[current.Id] = result.Image;
            result.Image = null;
            if (!String.IsNullOrEmpty(result.Path))
            {
                visibleLibraryPaths[current.Id] = result.Path;
                RegisterIconAnimation(result.Path);
            }
            else
                visibleLibraryPaths.Remove(current.Id);
            UpdateIconAnimationPlayback();
            for (int i = 0; i < games.Items.Count; i++)
            {
                var row = games.Items[i] as LibraryEntry;
                if (row != null && row.Custom != null && row.Custom.Id == current.Id)
                {
                    var bounds = games.GetItemRectangle(i);
                    if (bounds.IntersectsWith(games.ClientRectangle))
                        games.Invalidate(new Rectangle(bounds.X + 8, bounds.Y + 13, 46, 46));
                }
            }

            return true;
        }

        async void QueueLibraryIcon(CustomGame game, bool online)
        {
            string key = LibraryIconKey(game);
            DateTime last;
            if (libraryIconsPending.Contains(key) || libraryIconAttempts.TryGetValue(key, out last) && DateTime.UtcNow - last < TimeSpan.FromMinutes(5))
                return;
            libraryIconsPending.Add(key);
            libraryIconAttempts[key] = DateTime.UtcNow;
            bool acquired = false;
            LibraryIconResult result = null;
            var token = itchArtworkStop.Token;
            var snapshot = new CustomGame
            {
                Id = game.Id,
                Name = game.Name,
                Executable = game.Executable,
                Platform = game.Platform,
                PlatformId = game.PlatformId,
                Icon = game.Icon,
                RobloxIcon = game.RobloxIcon,
                UniverseId = game.UniverseId,
                ManagedProjectId = game.ManagedProjectId
            };
            try
            {
                await libraryIconGate.WaitAsync(token);
                acquired = true;
                if (!IsLibraryIconVisible(game.Id))
                {
                    libraryIconAttempts.Remove(key);
                    return;
                }

                result = await Task.Run(() => FindLibraryIcon(snapshot, store.Root, false, token));
                token.ThrowIfCancellationRequested();
                if (result == null && online)
                {
                    libraryIconGate.Release();
                    acquired = false;
                    await libraryIconGate.WaitAsync(token);
                    acquired = true;
                    if (!IsLibraryIconVisible(game.Id))
                    {
                        libraryIconAttempts.Remove(key);
                        return;
                    }

                    result = await Task.Run(() => FindLibraryIcon(snapshot, store.Root, true, token));
                    token.ThrowIfCancellationRequested();
                }

                ApplyLibraryIcon(snapshot, key, result);
            }
            catch (OperationCanceledException)
            {
                libraryIconAttempts.Remove(key);
            }
            catch (Exception error)
            {
                if (!token.IsCancellationRequested && !IsDisposed)
                    AsteraDiagnostics.Write("Library icon unavailable", new IOException(error.GetType().Name));
            }
            finally
            {
                if (result != null)
                    result.Dispose();
                if (acquired)
                    libraryIconGate.Release();
                libraryIconsPending.Remove(key);
            }
        }

        void StartLibraryIconLoading()
        {
            Shown += delegate
            {
                ScheduleLibraryIcons();
            };
            VisibleChanged += delegate
            {
                ScheduleLibraryIcons();
            };
            Resize += delegate
            {
                ScheduleLibraryIcons();
            };
            Disposed += delegate
            {
                foreach (var image in visibleLibraryIcons.Values)
                    image.Dispose();
                visibleLibraryIcons.Clear();
                visibleLibraryPaths.Clear();
                visibleLibraryKeys.Clear();
            };
        }
    }
}
