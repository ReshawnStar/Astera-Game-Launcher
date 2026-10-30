using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace Astera
{
    sealed partial class LibraryForm
    {
        readonly HashSet<string> publicSteamPending = new HashSet<string>();
        readonly Dictionary<string, DateTime> publicSteamNext = new Dictionary<string, DateTime>();
        CancellationTokenSource selectedPublicArtwork;
        string selectedPublicArtworkKey = "";
        void UpdatePublicArtworkSelection()
        {
            string key = IsCustomGame && CurrentCustom != null ? CurrentCustom.Executable : "";
            if (key == selectedPublicArtworkKey && selectedPublicArtwork != null)
                return;
            if (selectedPublicArtwork != null)
            {
                selectedPublicArtwork.Cancel();
                selectedPublicArtwork.Dispose();
            }

            selectedPublicArtworkKey = key;
            selectedPublicArtwork = CancellationTokenSource.CreateLinkedTokenSource(itchArtworkStop.Token);
        }

        bool ApplyPublicSteamResult(string executable, string id, string hero, string icon)
        {
            SteamGameArt current;
            if (IsDisposed || Disposing || !IsCustomGame || CurrentCustom == null || !String.Equals(CurrentCustom.Executable, executable, StringComparison.OrdinalIgnoreCase) || !steamArt.TryGetValue(executable, out current) || current == null || current.AppId != id)
                return false;
            ApplySteamArtwork(executable, new SteamGameArt { AppId = id, Name = current.Name, Hero = String.IsNullOrEmpty(current.Hero) ? hero : current.Hero, Icon = String.IsNullOrEmpty(current.Icon) ? icon : current.Icon, UpdatePending = current.UpdatePending });
            return true;
        }

        async void QueuePublicSteamArtwork(string executable, SteamGameArt art)
        {
            UpdatePublicArtworkSelection();
            if (art == null || previewMode || !IsCustomGame || CurrentCustom == null || !String.Equals(CurrentCustom.Executable, executable, StringComparison.OrdinalIgnoreCase) || (!String.IsNullOrEmpty(art.Hero) && !String.IsNullOrEmpty(art.Icon)))
                return;
            DateTime next;
            if (publicSteamNext.TryGetValue(art.AppId, out next) && next > DateTime.UtcNow || !publicSteamPending.Add(art.AppId))
                return;
            publicSteamNext[art.AppId] = DateTime.UtcNow.AddMinutes(5);
            bool acquired = false, cancelled = false;
            var token = selectedPublicArtwork.Token;
            try
            {
                await itchArtworkGate.WaitAsync(token);
                acquired = true;
                string root = Path.Combine(store.Root, "artwork", "steam");
                string hero = await Task.Run(() => String.IsNullOrEmpty(art.Hero) ? SteamPublicArtwork.Optional(() => SteamPublicArtwork.Resolve(art.AppId, root, token), token) : art.Hero);
                token.ThrowIfCancellationRequested();
                ApplyPublicSteamResult(executable, art.AppId, hero, null);
                string icon = await Task.Run(() => String.IsNullOrEmpty(art.Icon) ? SteamPublicArtwork.Optional(() => SteamPublicArtwork.ResolvePortrait(art.AppId, root, token), token) : art.Icon);
                token.ThrowIfCancellationRequested();
                ApplyPublicSteamResult(executable, art.AppId, hero, icon);
            }
            catch (OperationCanceledException)
            {
                cancelled = true;
                publicSteamNext.Remove(art.AppId);
            }
            catch (System.Net.WebException)
            {
                if (token.IsCancellationRequested)
                {
                    cancelled = true;
                    publicSteamNext.Remove(art.AppId);
                }
                else if (!IsDisposed)
                    AsteraDiagnostics.Write("Steam public artwork unavailable", new IOException("Network request failed"));
            }
            catch (Exception error)
            {
                if (!IsDisposed)
                    AsteraDiagnostics.Write("Steam public artwork unavailable", new IOException(error.GetType().Name));
            }
            finally
            {
                if (acquired)
                    itchArtworkGate.Release();
                publicSteamPending.Remove(art.AppId);
                if (cancelled && !IsDisposed && !Disposing && IsCustomGame && CurrentCustom != null && String.Equals(CurrentCustom.Executable, executable, StringComparison.OrdinalIgnoreCase))
                {
                    SteamGameArt current;
                    if (steamArt.TryGetValue(executable, out current))
                        QueuePublicSteamArtwork(executable, current);
                }
            }
        }
    }
}
