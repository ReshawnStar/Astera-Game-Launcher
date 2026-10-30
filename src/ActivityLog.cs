using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Threading;
using System.Windows.Forms;

namespace Astera
{
    sealed class GameActivity
    {
        internal readonly Queue<string> Messages = new Queue<string>();
        internal string Last;
        internal int Characters, Scroll;
        internal bool Add(string message)
        {
            if (message.Length > 16000)
                message = message.Substring(0, 16000) + "…";
            if (message == Last)
                return false;
            Last = message;
            Messages.Enqueue(message);
            Characters += message.Length;
            while (Messages.Count > 200 || Characters > 64000)
            {
                Characters -= Messages.Dequeue().Length;
            }

            return true;
        }

        internal string Text
        {
            get
            {
                return String.Join(Environment.NewLine, Messages) + Environment.NewLine;
            }
        }
    }

    sealed partial class LibraryForm
    {
        readonly Dictionary<string, GameActivity> gameActivities = new Dictionary<string, GameActivity>();
        readonly AsyncLocal<string> activityContext = new AsyncLocal<string>();
        string displayedActivityKey;
        string SelectedActivityKey
        {
            get
            {
                return LibraryKey(games.SelectedItem as LibraryEntry ?? activeLibraryEntry);
            }
        }

        GameActivity Activity(string key)
        {
            GameActivity history;
            if (!gameActivities.TryGetValue(key, out history))
            {
                history = new GameActivity();
                gameActivities.Add(key, history);
            }

            return history;
        }

        void Log(string message)
        {
            string key = activityContext.Value;
            if (key == null)
                key = InvokeRequired ? "app" : SelectedActivityKey ?? "app";
            LogActivity(key, message);
        }

        void LogActivity(string key, string message)
        {
            if (IsDisposed || Disposing || String.IsNullOrEmpty(message))
                return;
            if (InvokeRequired)
            {
                try
                {
                    BeginInvoke(new Action<string, string>(LogActivity), key, message);
                }
                catch (InvalidOperationException)
                {
                }

                return;
            }

            key = key ?? "app";
            AsteraDiagnostics.Write("Update activity [" + key + "]: " + message, null);
            var history = Activity(key);
            if (!history.Add(message))
                return;
            if (key == SelectedActivityKey)
                RefreshActivityLog(true);
        }

        void RefreshActivityLog(bool changed = false)
        {
            if (log == null)
                return;
            string key = SelectedActivityKey;
            if (key == displayedActivityKey && !changed)
                return;
            if (displayedActivityKey != null && displayedActivityKey != key)
                Activity(displayedActivityKey).Scroll = log.FirstLine;
            displayedActivityKey = key;
            GameActivity history = key == null ? null : Activity(key);
            string text = history == null || history.Messages.Count == 0 ? "No update activity for this game yet." : history.Text;
            if (log.Text != text)
                log.Text = text;
            if (history != null)
                log.MoveTo(changed ? Math.Max(0, Int32.MaxValue) : history.Scroll);
        }

        void ShowAsteraActivity()
        {
            using (var dialog = new AsteraDialog
            {
                Text = "Astera update activity",
                Size = new Size(760, 490),
                StartPosition = FormStartPosition.CenterParent
            }

            )
            {
                var text = new ScrollText
                {
                    Dock = DockStyle.Fill
                };
                ConfigureReadOnly(text, Color.FromArgb(21, 28, 47));
                text.Text = Activity("app").Messages.Count == 0 ? "No Astera update activity yet." : Activity("app").Text;
                var content = new Panel
                {
                    Dock = DockStyle.Fill,
                    Padding = new Padding(18)
                };
                content.Controls.Add(text);
                dialog.Controls.Add(content);
                dialog.ShowDialog(this);
            }
        }
    }
}
