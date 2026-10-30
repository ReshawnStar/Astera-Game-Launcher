using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace Astera
{
    sealed partial class LibraryForm
    {
        bool ApplyCurrentItchArtwork(CustomGame game, string path)
        {
            if (IsDisposed || Disposing || !IsCustomGame || CurrentCustom == null || CurrentCustom.Id != game.Id)
                return false;
            ApplyItchArtwork(game, path);
            return true;
        }

        async void QueueItchArtwork(CustomGame game)
        {
            UpdatePublicArtworkSelection();
            if (previewMode || game == null || (game.Platform == "steam" || game.Platform == "roblox") || String.IsNullOrWhiteSpace(game.Executable) || !IsCustomGame || CurrentCustom == null || CurrentCustom.Id != game.Id)
                return;
            DateTime last;
            if (itchPending.Contains(game.Executable) || itchChecked.TryGetValue(game.Executable, out last) && DateTime.UtcNow - last < TimeSpan.FromMinutes(5))
                return;
            itchPending.Add(game.Executable);
            itchChecked[game.Executable] = DateTime.UtcNow;
            bool acquired = false, cancelled = false;
            var token = selectedPublicArtwork.Token;
            try
            {
                await itchArtworkGate.WaitAsync(token);
                acquired = true;
                string image = await Task.Run(() => ItchArtwork.Resolve(game, Path.Combine(store.Root, "artwork", "itch"), token));
                token.ThrowIfCancellationRequested();
                ApplyCurrentItchArtwork(game, image);
            }
            catch (OperationCanceledException)
            {
                cancelled = true;
                itchChecked.Remove(game.Executable);
            }
            catch (System.Net.WebException)
            {
                if (token.IsCancellationRequested)
                {
                    cancelled = true;
                    itchChecked.Remove(game.Executable);
                }
                else if (!IsDisposed)
                    AsteraDiagnostics.Write("itch cover unavailable", new IOException("Network request failed"));
            }
            catch (Exception error)
            {
                if (!IsDisposed)
                    AsteraDiagnostics.Write("itch cover unavailable", new IOException(error.GetType().Name));
            }
            finally
            {
                if (acquired)
                    itchArtworkGate.Release();
                itchPending.Remove(game.Executable);
                if (cancelled && !IsDisposed && !Disposing && IsCustomGame && CurrentCustom != null && CurrentCustom.Id == game.Id)
                    QueueItchArtwork(game);
            }
        }
    }
}
