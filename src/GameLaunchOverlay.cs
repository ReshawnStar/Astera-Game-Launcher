using System;
using System.Windows.Forms;

namespace Astera
{
    sealed partial class LibraryForm
    {
        StartupScreen launchScreen;
        string launchScreenKey;
        Action leaveLaunchScreen;
        bool launchScreenEvents;
        void RemoveLaunchScreen()
        {
            var old = launchScreen;
            launchScreen = null;
            launchScreenKey = null;
            leaveLaunchScreen = null;
            if (old != null)
            {
                Controls.Remove(old);
                old.Dispose();
                if (!IsDisposed && !Disposing)
                {
                    banner.PauseForLaunch(false);
                    UpdateIconAnimationPlayback();
                }
            }
        }

        void LeaveLaunchScreen()
        {
            var action = leaveLaunchScreen;
            if (action != null)
                action();
            RemoveLaunchScreen();
        }

        void UpdateLaunchOverlay()
        {
            if (IsDisposed || Disposing)
                return;
            string key = null, status = null;
            Action leave = null, open = null;
            bool minecraft = false;
            if (MinecraftLaunchVisible && !minecraftLaunch.TimedOut && !minecraftLaunch.OverlayDismissed)
            {
                var notice = minecraftLaunch;
                key = "minecraft:" + notice.GameId + ":" + notice.Started.Ticks;
                status = "Preparing Minecraft… · " + notice.Name;
                minecraft = true;
                leave = delegate
                {
                    notice.OverlayDismissed = true;
                };
                open = ShowMinecraftLaunchManager;
            }
            else
            {
                var wait = SelectedLaunchWait;
                if (wait != null && !wait.TimedOut && !wait.OverlayDismissed)
                {
                    key = wait.Id + ":" + wait.Started.Ticks;
                    status = "Starting " + wait.Name + "…";
                    minecraft = wait.Activity == "builtin:0";
                    leave = delegate
                    {
                        wait.OverlayDismissed = true;
                    };
                }
            }

            if (key == null)
            {
                RemoveLaunchScreen();
                return;
            }

            if (!Visible || !IsHandleCreated)
                return;
            if (launchScreen == null || launchScreenKey != key)
            {
                RemoveLaunchScreen();
                launchScreenKey = key;
                leaveLaunchScreen = leave;
                launchScreen = new StartupScreen(StartupFlavorPool.Load(store.Root).ForLaunch(minecraft), store.Settings.StartupFlavor, LeaveLaunchScreen, open)
                {
                    Dock = DockStyle.None,
                    Bounds = ClientRectangle
                };
                Controls.Add(launchScreen);
                launchScreen.BringToFront();
                // Sibling controls remain Visible under an opaque cover. Stop their
                // animation work explicitly; do not disable the cover's loading dots.
                banner.PauseForLaunch(true);
                UpdateIconAnimationPlayback();
                if (!launchScreenEvents)
                {
                    launchScreenEvents = true;
                    Resize += delegate
                    {
                        if (launchScreen != null && !launchScreen.IsDisposed)
                            launchScreen.Bounds = ClientRectangle;
                    };
                }
            }

            launchScreen.SetStatus(status);
        }
    }
}
