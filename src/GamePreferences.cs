using System;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Windows.Forms;

namespace Astera
{
    sealed partial class LibraryForm
    {
        readonly Label gameSettingsTitle = new Label(), gameSettingsDescription = new Label(), gameSettingsLocation = new Label();
        Button gameSettingsEdit, gameSettingsArtwork, gameSettingsFolder;
        void BuildGamePreferences()
        {
            var card = new Card
            {
                Dock = DockStyle.Fill,
                Margin = Padding.Empty
            };
            preferencesPage.Controls.Add(card);
            var content = new FlowLayoutPanel
            {
                Dock = DockStyle.Top,
                AutoSize = true,
                FlowDirection = FlowDirection.TopDown,
                WrapContents = false
            };
            gameSettingsTitle.Font = UiTheme.Text(22, true);
            gameSettingsTitle.AutoSize = true;
            gameSettingsTitle.Margin = new Padding(0, 0, 0, 16);
            gameSettingsDescription.AutoSize = true;
            gameSettingsDescription.MaximumSize = new Size(850, 0);
            gameSettingsDescription.ForeColor = Muted;
            gameSettingsDescription.Margin = new Padding(0, 0, 0, 18);
            gameSettingsLocation.AutoSize = true;
            gameSettingsLocation.MaximumSize = new Size(850, 0);
            gameSettingsLocation.ForeColor = Muted;
            gameSettingsLocation.Margin = new Padding(0, 0, 0, 20);
            content.Controls.Add(gameSettingsTitle);
            content.Controls.Add(gameSettingsDescription);
            content.Controls.Add(gameSettingsLocation);
            var actions = new FlowLayoutPanel
            {
                AutoSize = true,
                WrapContents = true,
                MaximumSize = new Size(850, 0)
            };
            gameSettingsEdit = Button("Game settings", delegate
            {
                Configure();
            });
            gameSettingsArtwork = Button("Game artwork", delegate
            {
                SelectLibraryTab(2);
            });
            gameSettingsFolder = Button("Open folder", delegate
            {
                OpenGameFolder();
            });
            actions.Controls.AddRange(new Control[] { gameSettingsEdit, gameSettingsArtwork, gameSettingsFolder });
            content.Controls.Add(actions);
            card.Controls.Add(content);
            card.Resize += delegate
            {
                int width = Math.Max(240, card.ClientSize.Width - 40);
                gameSettingsDescription.MaximumSize = gameSettingsLocation.MaximumSize = actions.MaximumSize = new Size(width, 0);
            };
        }

        void UpdateGamePreferences()
        {
            if (gameSettingsEdit == null)
                return;
            string name = IsCustomGame ? CurrentCustom.Name : IsMultiverse ? "FTL: Multiverse" : GameKind == 1 ? "Star Dash" : MinecraftSelectedTitle;
            gameSettingsTitle.Text = name + " settings";
            gameSettingsDescription.Text = IsCustomGame ? (CurrentCustom.Platform == "roblox" ? "Roblox manages this experience. Choose its name and artwork here." : CustomSteam(CurrentCustom) != null || CurrentCustom.Platform == "steam" ? "Steam manages this game's installation and updates. Choose its name, location and artwork here." : CurrentCustom.Platform == "itch" ? "itch.io manages this game. Choose its name, install location and artwork here." : "Choose this game's name, application and artwork.") : IsMultiverse ? "Manage the FTL installation, Multiverse setup and backups for this game." : GameKind == 1 ? "Choose your Star Dash application and artwork." : "Choose your Minecraft launcher and profile. Minecraft² profiles also include pack updates and resource-pack setup.";
            if (IsCustomGame && CurrentCustom.ManagedProjectId == ManagedGames.Id)
                gameSettingsDescription.Text = "Astera manages this game. Saves and Recovery data are kept when uninstalling.";
            if (SelectedThunderGame != null)
                gameSettingsDescription.Text = "Steam manages the base game. Choose a mod profile and apply share-code updates from the game page.";
            gameSettingsLocation.Text = "Location: " + (String.IsNullOrWhiteSpace(GameFolder()) ? "Not linked" : GameFolder());
            gameSettingsEdit.Enabled = gameSettingsArtwork.Enabled = !busy;
            gameSettingsFolder.Enabled = !busy && Directory.Exists(GameFolder());
        }
    }
}
