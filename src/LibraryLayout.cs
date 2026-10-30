using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Windows.Forms;

namespace Astera
{
    // The background and character are separate assets. The character's canvas starts
    // above the background, so hair/hats can extend beyond the hero's upper edge.
    sealed partial class Banner : Panel
    {
        Bitmap backdropBuffer, frameBuffer;
        bool frameDirty = true;
        public string Game = "Minecraft²";
        bool transitionPaused;
        internal void PauseTransition(bool paused)
        {
            transitionPaused = paused;
            if (paused)
                rigTime.Stop();
            else
                rigTime.Start();
            UpdateRigClock();
        }

        bool rigDisposed;
        SkinRig liveRig;
        string liveRigPath = "";
        readonly System.Windows.Forms.Timer rigClock = new System.Windows.Forms.Timer
        {
            Interval = 10
        };
        readonly System.Diagnostics.Stopwatch rigTime = new System.Diagnostics.Stopwatch();
        Form rigOwner;
        internal bool LiveSkinActive
        {
            get
            {
                return liveRig != null;
            }
        }

        internal void LiveSkin(string pose)
        {
            pose = pose ?? "";
            if (pose == liveRigPath)
                return;
            if (liveRig != null)
                liveRig.Dispose();
            liveRig = null;
            liveRigPath = pose;
            if (pose.Length > 0)
                try
                {
                    liveRig = new SkinRig(pose);
                }
                catch (IOException)
                {
                }
                catch (ArgumentException)
                {
                }

            rigTime.Restart();
            if (transitionPaused)
                rigTime.Stop();
            UpdateRigClock();
            frameDirty = true;
            Invalidate();
        }

        void UpdateRigClock()
        {
            if (rigDisposed || rigClock == null || IsDisposed)
                return;
            var owner = FindForm();
            rigClock.Enabled = !transitionPaused && UiEffects.Enabled && (liveRig != null || HasAnimatedArtwork) && IsHandleCreated && Visible && owner != null && owner.Visible && owner.WindowState != FormWindowState.Minimized;
            SetArtworkPlayback(rigClock.Enabled);
            rigClock.Interval = liveRig == null ? ArtworkFrameDelay : 10;
            AnimationTiming(rigClock.Enabled);
        }

        void RigOwnerChanged(object sender, EventArgs e)
        {
            UpdateRigClock();
        }

        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            rigOwner = FindForm();
            if (rigOwner != null)
            {
                rigOwner.Resize += RigOwnerChanged;
                rigOwner.VisibleChanged += RigOwnerChanged;
            }

            UpdateRigClock();
        }

        protected override void OnHandleDestroyed(EventArgs e)
        {
            rigClock.Stop();
            SetArtworkPlayback(false);
            AnimationTiming(false);
            if (rigOwner != null)
            {
                rigOwner.Resize -= RigOwnerChanged;
                rigOwner.VisibleChanged -= RigOwnerChanged;
                rigOwner = null;
            }

            base.OnHandleDestroyed(e);
        }

        protected override void OnVisibleChanged(EventArgs e)
        {
            base.OnVisibleChanged(e);
            UpdateRigClock();
        }

        Image scene, character, defaultScene, vanillaScene, starDashScene, starDashLogo, ftlScene;
        string scenePath = "", characterPath = "";
        void EffectsChanged(object sender, EventArgs e)
        {
            UpdateRigClock();
            frameDirty = true;
            Invalidate();
        }

        public Banner()
        {
            UiEffects.Changed += EffectsChanged;
            rigClock.Tick += delegate
            {
                if (!rigDisposed)
                    TickArtworkAndRig();
            };
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.Opaque, true);
            DoubleBuffered = true;
            BackColor = Color.FromArgb(12, 17, 33);
            defaultScene = GameArtwork.Load("Astera.Backgrounds.MinecraftSquared");
            vanillaScene = GameArtwork.Load("Astera.Backgrounds.MinecraftVanilla");
            starDashScene = GameArtwork.Load("Astera.Backgrounds.StarDash");
            starDashLogo = GameArtwork.Load("Astera.Games.StarDashLogo");
            ftlScene = GameArtwork.Load("Astera.Backgrounds.FtlMultiverse");
        }

        internal Image CopyDefaultArtwork()
        {
            var art = Game == "Minecraft²" ? defaultScene : Game == "Minecraft" ? vanillaScene : Game == "Star Dash" ? starDashScene : Game == "FTL: Multiverse" ? ftlScene : null;
            return art == null ? null : new Bitmap(art);
        }

        public void Artwork(string background, string foreground)
        {
            Replace(ref scene, ref scenePath, ref sceneAnimation, background);
            Replace(ref character, ref characterPath, ref characterAnimation, foreground);
            UpdateRigClock();
            frameDirty = true;
            Invalidate();
        }

        static void Replace(ref Image image, ref string oldPath, ref ArtworkAnimation animation, string path)
        {
            path = path ?? "";
            if (path == oldPath)
                return;
            ArtworkAnimation next = null;
            if (path.Length > 0)
                try
                {
                    next = ArtworkAnimation.Open(path);
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

            if (animation != null)
                animation.Dispose();
            else if (image != null)
                image.Dispose();
            animation = next;
            image = next == null ? null : next.Image;
            oldPath = path;
        }

        internal static Image ReadArtwork(string path)
        {
            var file = new FileInfo(path);
            if (file.Length > 25 * 1024 * 1024)
                throw new IOException("Use artwork smaller than 25 MB.");
            using (var input = Image.FromFile(path))
            {
                if ((long)input.Width * input.Height > 20000000)
                    throw new IOException("Use artwork below 20 megapixels.");
                return new Bitmap(input);
            }
        }

        void PaintBackdrop(Graphics g)
        {
            if (Width < 2 || Height < 35)
                return;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.InterpolationMode = InterpolationMode.HighQualityBicubic;
            var area = new Rectangle(0, 30, Width, Height - 30);
            var backdrop = scene ?? (Game == "Minecraft²" ? defaultScene : Game == "Minecraft" ? vanillaScene : Game == "Star Dash" ? starDashScene : Game == "FTL: Multiverse" ? ftlScene : null);
            if (backdrop != null)
            {
                var clip = g.Save();
                g.SetClip(area, CombineMode.Intersect);
                Cover(g, backdrop, area);
                g.Restore(clip);
            }
            else
            {
                using (var fill = new LinearGradientBrush(area, Color.FromArgb(22, 31, 65), Color.FromArgb(54, 38, 88), 25F))
                    g.FillRectangle(fill, area);
                var random = new Random(17);
                for (int i = 0; i < 110; i++)
                {
                    int x = random.Next(Width), y = 30 + random.Next(Height - 30);
                    using (var star = new SolidBrush(Color.FromArgb(70 + i % 150, 155, 168, 241)))
                        g.FillEllipse(star, x, y, i % 7 == 0 ? 3 : 1.5F, i % 7 == 0 ? 3 : 1.5F);
                }

                using (var moon = new SolidBrush(Color.FromArgb(22, 164, 155, 240)))
                    g.FillEllipse(moon, Width * .68F, 45, 150, 150);
            }

            // Reserve the left side for live text and controls, even with bright art.
            using (var shade = new LinearGradientBrush(area, Color.FromArgb(235, 10, 15, 31), Color.FromArgb(15, 10, 15, 31), 0F))
            {
                shade.WrapMode = WrapMode.TileFlipXY;
                var smoothing = g.SmoothingMode;
                g.SmoothingMode = SmoothingMode.None;
                g.FillRectangle(shade, area);
                g.SmoothingMode = smoothing;
            }

            if (character == null && Game == "Star Dash")
                GameArtwork.Contain(g, starDashLogo, new Rectangle((int)(Width * .61F), 16, (int)(Width * .37F), Height - 32));
            if (liveRig == null && character != null)
            {
                float scale = Math.Min(Height / (float)character.Height, Width * .47F / character.Width);
                int w = (int)(character.Width * scale), h = (int)(character.Height * scale);
                g.DrawImage(character, new Rectangle(Width - w - 12, Height - h, w, h));
            }

            using (var edge = new Pen(Color.FromArgb(58, 76, 116)))
                g.DrawLine(edge, 0, Height - 1, Width, Height - 1);
        }

        // Animation advances only on the timer. Transparent child controls can ask
        // their parent to paint; those requests must reuse exactly the same pixels.
        protected override void OnPaintBackground(PaintEventArgs e)
        {
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            if (frameDirty || frameBuffer == null)
                RenderFrame();
            if (frameBuffer != null)
                e.Graphics.DrawImageUnscaled(frameBuffer, 0, 0);
            RecordPaint(e.ClipRectangle);
            base.OnPaint(e);
        }

        protected override void OnResize(EventArgs e)
        {
            frameDirty = true;
            base.OnResize(e);
            Invalidate();
        }

        void RenderFrame()
        {
            if (rigDisposed || Width < 2 || Height < 35)
                return;
            long frameStart = System.Diagnostics.Stopwatch.GetTimestamp();
            if (frameBuffer == null || frameBuffer.Width != Width || frameBuffer.Height != Height)
            {
                if (frameBuffer != null)
                    frameBuffer.Dispose();
                if (backdropBuffer != null)
                    backdropBuffer.Dispose();
                frameBuffer = new Bitmap(Width, Height);
                backdropBuffer = new Bitmap(Width, Height);
                frameDirty = true;
            }

            bool backdropChanged = frameDirty;
            if (frameDirty)
            {
                using (var g = Graphics.FromImage(backdropBuffer))
                {
                    g.Clear(BackColor);
                    PaintBackdrop(g);
                }

                frameDirty = false;
            }

            using (var g = Graphics.FromImage(frameBuffer))
            {
                if (!backdropChanged)
                    g.SetClip(new Rectangle((int)(Width * .60F), 0, Width - (int)(Width * .60F), Height));
                g.CompositingMode = CompositingMode.SourceCopy;
                g.DrawImageUnscaled(backdropBuffer, 0, 0);
                g.CompositingMode = CompositingMode.SourceOver;
                if (liveRig != null)
                    liveRig.Draw(g, new Rectangle((int)(Width * .53F), 0, (int)(Width * .47F), Height), PointToClient(Cursor.Position), rigTime.Elapsed.TotalSeconds);
            }

            RecordFrame((System.Diagnostics.Stopwatch.GetTimestamp() - frameStart) * 1000.0 / System.Diagnostics.Stopwatch.Frequency);
        }

        internal static void Cover(Graphics g, Image image, Rectangle target)
        {
            if (target.Width < 1 || target.Height < 1)
                return;
            float ratio = Math.Max(target.Width / (float)image.Width, target.Height / (float)image.Height), width = target.Width / ratio, height = target.Height / ratio;
            using (var attributes = new System.Drawing.Imaging.ImageAttributes())
            {
                attributes.SetWrapMode(WrapMode.TileFlipXY);
                g.DrawImage(image, target, (image.Width - width) / 2, (image.Height - height) / 2, width, height, GraphicsUnit.Pixel, attributes);
            }
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                rigDisposed = true;
                UiEffects.Changed -= EffectsChanged;
                rigClock.Dispose();
                AnimationTiming(false);
                if (frameBuffer != null)
                    frameBuffer.Dispose();
                if (backdropBuffer != null)
                    backdropBuffer.Dispose();
                if (liveRig != null)
                {
                    liveRig.Dispose();
                    liveRig = null;
                }

                if (sceneAnimation != null)
                    sceneAnimation.Dispose();
                else if (scene != null)
                    scene.Dispose();
                if (characterAnimation != null)
                    characterAnimation.Dispose();
                else if (character != null)
                    character.Dispose();
                if (defaultScene != null)
                    defaultScene.Dispose();
                if (vanillaScene != null)
                    vanillaScene.Dispose();
                if (starDashScene != null)
                    starDashScene.Dispose();
                if (starDashLogo != null)
                    starDashLogo.Dispose();
                if (ftlScene != null)
                    ftlScene.Dispose();
            }

            base.Dispose(disposing);
        }
    }

    sealed class Card : Panel
    {
        public Card()
        {
            SetStyle(ControlStyles.ResizeRedraw, true);
            DoubleBuffered = true;
            BackColor = Color.FromArgb(21, 28, 47);
            Padding = new Padding(16);
            Margin = new Padding(0, 0, 14, 0);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            if (Width > 2 && Height > 2)
                using (var pen = new Pen(Color.FromArgb(49, 64, 101)))
                using (var path = AsteraButton.Round(new Rectangle(0, 0, Width - 1, Height - 1), 9))
                    e.Graphics.DrawPath(pen, path);
        }
    }

    sealed class ScenePreview : Panel
    {
        public Image Artwork;
        public ScenePreview()
        {
            DoubleBuffered = true;
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            if (Width < 2 || Height < 2)
                return;
            if (Artwork != null)
                Banner.Cover(e.Graphics, Artwork, ClientRectangle);
            else
            {
                using (var fill = new LinearGradientBrush(ClientRectangle, Color.FromArgb(23, 36, 76), Color.FromArgb(61, 38, 91), 25F))
                    e.Graphics.FillRectangle(fill, ClientRectangle);
                var random = new Random(19);
                for (int i = 0; i < 40; i++)
                    e.Graphics.FillEllipse(Brushes.LightSlateGray, random.Next(Width), random.Next(Height), 2, 2);
            }
        }
    }

    sealed partial class LibraryForm
    {
        readonly System.ComponentModel.IContainer menuComponents = new System.ComponentModel.Container();
        ContextMenuStrip moreMenu;
        Label latestHeading;
        readonly GameTitleLabel heroTitle = new GameTitleLabel();
        readonly Label heroSubtitle = new Label(), heroTagline = new Label(), installationPath = new Label(), artLabel = new Label();
        readonly Panel overviewPage = new Panel(), backgroundPage = new Panel(), preferencesPage = new Panel();
        readonly TableLayoutPanel information = new TableLayoutPanel();
        readonly Panel overviewRight = new Panel(), activityCard = new Card();
        readonly FlowLayoutPanel tabs = new FlowLayoutPanel();
        readonly PictureBox artPreview = new PictureBox();
        readonly ScenePreview previewScene = new ScenePreview();
        readonly Button openFolder = new AsteraButton(), chooseBackground = new AsteraButton(), chooseCharacter = new AsteraButton(), chooseLibraryIcon = new AsteraButton();
        int selectedTab;
        string previewArtwork = "";
        TableLayoutPanel updateLayout;
        FlowLayoutPanel transferActions;
        void UpdateTransferSlot()
        {
            if (updateLayout == null)
                return;
            cancel.MinimumSize = new Size(download.GetPreferredSize(Size.Empty).Width, download.MinimumSize.Height);
            bool cancellable = busy && WorkBelongsToPage && cancellation != null && !transaction;
            cancel.Visible = cancellable;
            download.Visible = !cancellable && !IsCustomGame;
            install.Visible = restore.Visible = !IsCustomGame;
            updateLayout.RowStyles[2].Height = IsCustomGame && !cancellable ? 0 : 110;
        }

        void BuildLibraryLayout()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
            DoubleBuffered = true;
            FormBorderStyle = FormBorderStyle.None;
            Padding = new Padding(6);
            BackColor = Color.FromArgb(12, 17, 33);
            var header = new Panel
            {
                Dock = DockStyle.Top,
                Height = 72,
                BackColor = Color.FromArgb(11, 15, 29),
                Padding = new Padding(24, 12, 18, 12)
            };
            var brand = new Label
            {
                Text = "✦  ASTERA",
                Font = UiTheme.Text(30, true),
                ForeColor = Color.FromArgb(235, 237, 255),
                AutoSize = false,
                Width = 220,
                Dock = DockStyle.Left,
                TextAlign = ContentAlignment.MiddleLeft
            };
            var tagline = new Label
            {
                Text = "YOUR WORLDS. A LITTLE MORE ALIVE.",
                ForeColor = Color.FromArgb(145, 160, 230),
                Font = UiTheme.Text(11),
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleLeft
            };
            var top = new FlowLayoutPanel
            {
                Dock = DockStyle.Right,
                Width = 300,
                FlowDirection = FlowDirection.RightToLeft,
                WrapContents = false
            };
            var exit = CaptionButton(ButtonSymbol.Cancel, "Close Astera", delegate
            {
                Close();
            });
            windowMaximize = CaptionButton(ButtonSymbol.WindowMaximize, "Maximize", delegate
            {
                ToggleMaximize();
            });
            var minimize = CaptionButton(ButtonSymbol.WindowMinimize, "Minimize", delegate
            {
                WindowState = FormWindowState.Minimized;
            });
            settings.Text = "Settings";
            Style(settings);
            ((AsteraButton)settings).Symbol = ButtonSymbol.Settings;
            settings.Click += delegate
            {
                ConfigureLauncher();
            };
            top.Controls.Add(exit);
            top.Controls.Add(windowMaximize);
            top.Controls.Add(minimize);
            top.Controls.Add(settings);
            header.Controls.Add(tagline);
            header.Controls.Add(top);
            header.Controls.Add(brand);
            header.MouseDown += DragTitleBar;
            brand.MouseDown += DragTitleBar;
            tagline.MouseDown += DragTitleBar;
            top.MouseDown += DragTitleBar;
            var sidebar = new Panel
            {
                Dock = DockStyle.Left,
                Width = 260,
                BackColor = Color.FromArgb(14, 19, 36),
                Padding = new Padding(14, 26, 12, 16)
            };
            BuildLibraryFilters(sidebar);
            BuildLibrarySearch(sidebar);
            sidebar.Controls.Add(new Label { Text = "YOUR LIBRARY", ForeColor = Muted, Height = 36, Dock = DockStyle.Top, Font = UiTheme.Text(12) });
            games.Dock = DockStyle.Fill;
            games.BorderStyle = BorderStyle.None;
            games.BackColor = sidebar.BackColor;
            games.ForeColor = Color.White;
            games.Font = UiTheme.Text(15);
            games.ItemHeight = 76;
            games.DrawMode = DrawMode.OwnerDrawFixed;
            ReloadGames(null);
            games.DrawItem += DrawGame;
            games.SelectedIndexChanged += delegate
            {
                if (!reloadingLibrary && games.SelectedItem != null)
                {
                    var selected = (LibraryEntry)games.SelectedItem;
                    if (LibraryKey(activeLibraryEntry) == LibraryKey(selected))
                        return;
                    activeLibraryEntry = selected;
                    SwitchGame();
                }
            };
            BuildLibraryViewport(sidebar);
            BuildVersionFooter(sidebar);
            var addGame = Button("+ Add game", delegate
            {
                AddCustomGame();
            });
            addGame.Dock = DockStyle.Bottom;
            sidebar.Controls.Add(addGame);
            addGame.BringToFront();
            var viewport = new Panel
            {
                Dock = DockStyle.Fill
            };
            gameViewport = viewport;
            viewport.Resize += delegate
            {
                if (gameTransition != null)
                    gameTransition.Restage();
            };
            var body = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 4,
                Padding = new Padding(24, 0, 24, 12)
            };
            gameBody = body;
            body.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            body.RowStyles.Add(new RowStyle(SizeType.Absolute, 330));
            body.RowStyles.Add(new RowStyle(SizeType.Absolute, 54));
            body.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            body.RowStyles.Add(new RowStyle(SizeType.Absolute, 28));
            viewport.Controls.Add(body);
            viewport.Resize += delegate
            {
                LayoutHeroContent();
            };
            banner.Dock = DockStyle.Fill;
            banner.Margin = Padding.Empty;
            body.Controls.Add(banner, 0, 0);
            var heroText = new TableLayoutPanel
            {
                BackColor = Color.Transparent,
                ColumnCount = 1,
                RowCount = 5,
                Location = new Point(22, 66),
                Height = 160,
                Anchor = AnchorStyles.Left | AnchorStyles.Top
            };
            heroTextPanel = heroText;
            foreach (int height in new[]
            {
                56,
                40,
                0,
                28,
                28
            }

            )
                heroText.RowStyles.Add(new RowStyle(SizeType.Absolute, height));
            heroText.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            heroTitle.Text = "Minecraft²";
            heroTitle.Font = UiTheme.Text(40, true);
            heroTitle.ForeColor = Color.White;
            heroTitle.Dock = DockStyle.Fill;
            heroSubtitle.Font = UiTheme.Text(14);
            heroSubtitle.ForeColor = Color.FromArgb(191, 200, 246);
            heroSubtitle.Dock = DockStyle.Fill;
            heroTagline.Text = "You're not alone this time.";
            heroTagline.Font = UiTheme.Text(14, true);
            heroTagline.ForeColor = Color.White;
            heroTagline.Dock = DockStyle.Fill;
            status.Font = UiTheme.Text(12, true);
            status.Dock = DockStyle.Fill;
            status.AutoEllipsis = true;
            status.TextAlign = ContentAlignment.MiddleLeft;
            profileLabel.Dock = DockStyle.Fill;
            profileLabel.ForeColor = Muted;
            profileLabel.AutoEllipsis = true;
            profileLabel.Font = UiTheme.Text(12);
            heroText.Controls.Add(heroTitle, 0, 0);
            heroText.Controls.Add(heroSubtitle, 0, 1);
            heroText.Controls.Add(heroTagline, 0, 2);
            heroText.Controls.Add(profileLabel, 0, 4);
            banner.Controls.Add(heroText);
            status.Dock = DockStyle.None;
            banner.Controls.Add(status);
            var action = new FlowLayoutPanel
            {
                BackColor = Color.Transparent,
                Location = new Point(24, 258),
                Height = 58,
                WrapContents = false
            };
            heroActions = action;
            BuildModProfileStrip();
            play.Text = "PLAY";
            Style(play);
            ((AsteraButton)play).Role = ButtonRole.Primary;
            ((AsteraButton)play).Symbol = ButtonSymbol.Play;
            ((AsteraButton)play).OrbitGlow = true;
            play.Font = UiTheme.Button();
            play.MinimumSize = new Size(170, 48);
            play.Click += delegate
            {
                PrimaryGameAction();
            };
            check.Text = "Check for updates";
            Style(check);
            ((AsteraButton)check).Symbol = ButtonSymbol.Refresh;
            check.MinimumSize = new Size(195, 48);
            check.Click += async delegate
            {
                await Check(false);
            };
            moreMenu = AsteraMenuRenderer.Create(menuComponents);
            moreMenu.Items.Add("Open game folder", null, delegate
            {
                OpenGameFolder();
            });
            moreMenu.Items.Add("Game settings", null, delegate
            {
                Configure();
            });
            moreMenu.Items.Add("Update activity", null, delegate
            {
                SelectLibraryTab(1);
            });
            moreMenu.Items.Add("Open Astera logs", null, delegate
            {
                OpenDiagnostics();
            });
            moreMenu.Items.Add("Collect game logs", null, delegate
            {
                CollectGameLogs();
            });
            moreMenu.Items.Add("Astera update activity", null, delegate
            {
                ShowAsteraActivity();
            });
            moreMenu.Items.Add("Astera changelog", null, delegate
            {
                AppChangelog.Show(this);
            });
            moreMenu.Items.Add("Capture UI snapshot", null, delegate
            {
                CaptureUiFromMenu();
            });
            BuildManagedLibraryMenu();
            var more = Button("", delegate (object sender, EventArgs e)
            {
                var button = (Control)sender;
                moreMenu.Show(button, new Point(0, button.Height));
            });
            more.MinimumSize = new Size(48, 48);
            ((AsteraButton)more).Symbol = ButtonSymbol.More;
            more.AccessibleName = "More options";
            action.Controls.AddRange(new Control[] { play, check, more });
            action.Controls.SetChildIndex(modProfileStrip, 1);
            BuildPlaytimeStats(action);
            banner.Controls.Add(action);
            banner.Resize += delegate
            {
                LayoutHeroContent();
            };
            tabs.Dock = DockStyle.Fill;
            tabs.Padding = new Padding(0, 7, 0, 4);
            tabs.WrapContents = false;
            tabs.Margin = Padding.Empty;
            int index = 0;
            foreach (string title in new[]
            {
                "Overview",
                "Updates",
                "Backgrounds",
                "Settings"
            }

            )
            {
                int choice = index++;
                var tab = Button(title, delegate
                {
                    SelectLibraryTab(choice);
                });
                tab.Tag = choice;
                tab.Font = UiTheme.Text(14);
                ((AsteraButton)tab).Symbol = new[]
                {
                    ButtonSymbol.Overview,
                    ButtonSymbol.Updates,
                    ButtonSymbol.Backgrounds,
                    ButtonSymbol.Settings
                }[choice];
                tab.MinimumSize = new Size(110, 40);
                tabs.Controls.Add(tab);
            }

            body.Controls.Add(tabs, 0, 1);
            var pages = new Panel
            {
                Dock = DockStyle.Fill,
                Margin = Padding.Empty
            };
            foreach (var page in new[]
            {
                overviewPage,
                backgroundPage,
                preferencesPage
            }

            )
            {
                page.Dock = DockStyle.Fill;
                pages.Controls.Add(page);
            }

            body.Controls.Add(pages, 0, 2);
            information.Dock = DockStyle.Fill;
            information.ColumnCount = 2;
            information.RowCount = 1;
            information.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 60));
            information.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 40));
            information.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            overviewPage.Controls.Add(information);
            var latest = new Card
            {
                Dock = DockStyle.Fill
            };
            information.Controls.Add(latest, 0, 0);
            var latestLayout = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 3
            };
            latestLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            latestLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 36));
            latestLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            latestLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 110));
            latest.Controls.Add(latestLayout);
            updateLayout = latestLayout;
            latestHeading = Heading("Latest update");
            latestLayout.Controls.Add(latestHeading, 0, 0);
            ConfigureReadOnly(notes, latest.BackColor);
            var notesHost = new Panel
            {
                Dock = DockStyle.Fill,
                Margin = Padding.Empty
            };
            notesHost.Controls.Add(notes);
            BuildUpdateVideos(notesHost);
            latestLayout.Controls.Add(notesHost, 0, 1);
            var controls = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                Padding = new Padding(0, 9, 0, 0),
                WrapContents = true
            };
            download.Text = "Download";
            Style(download);
            ((AsteraButton)download).Symbol = ButtonSymbol.Download;
            download.Click += async delegate
            {
                await Download();
            };
            install.Text = "Install update";
            Style(install);
            ((AsteraButton)install).Role = ButtonRole.Primary;
            ((AsteraButton)install).Symbol = ButtonSymbol.Install;
            install.Click += async delegate
            {
                await Install();
            };
            restore.Text = "Restore backup";
            Style(restore);
            ((AsteraButton)restore).Role = ButtonRole.Quiet;
            ((AsteraButton)restore).Symbol = ButtonSymbol.Restore;
            restore.Click += async delegate
            {
                await Restore();
            };
            cancel.Text = "Cancel";
            Style(cancel);
            ((AsteraButton)cancel).Role = ButtonRole.Quiet;
            ((AsteraButton)cancel).Symbol = ButtonSymbol.Cancel;
            cancel.Click += delegate
            {
                if (cancellation != null)
                    cancellation.Cancel();
            };
            foreach (var button in new[]
            {
                download,
                install,
                restore,
                cancel
            }

            )
                button.Margin = new Padding(0, 0, 8, 6);
            transferActions = controls;
            controls.Controls.AddRange(new Control[] { cancel, download, install, restore });
            latestLayout.Controls.Add(controls, 0, 2);
            overviewRight.Dock = DockStyle.Fill;
            overviewRight.Margin = Padding.Empty;
            information.Controls.Add(overviewRight, 1, 0);
            var rightLayout = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                RowCount = 2,
                ColumnCount = 1
            };
            rightLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            rightLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 55));
            rightLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 45));
            overviewRight.Controls.Add(rightLayout);
            var background = new Card
            {
                Dock = DockStyle.Fill,
                Margin = new Padding(0, 0, 0, 12)
            };
            rightLayout.Controls.Add(background, 0, 0);
            var manage = Button("Manage", delegate
            {
                SelectLibraryTab(2);
            });
            manage.Dock = DockStyle.Right;
            manage.MinimumSize = new Size(90, 36);
            var artHeader = new Panel
            {
                Dock = DockStyle.Top,
                Height = 42
            };
            artHeader.Controls.Add(Heading("Game artwork"));
            artHeader.Controls.Add(manage);
            background.Controls.Add(artHeader);
            background.Resize += delegate
            {
                bool compact = background.ClientSize.Height < 165;
                artLabel.Visible = !compact;
                artHeader.Height = compact ? 36 : 42;
            };
            artLabel.Text = "Default starfield\nBackground + separate character layer";
            artLabel.Dock = DockStyle.Bottom;
            artLabel.Height = 46;
            artLabel.ForeColor = Muted;
            artLabel.Padding = new Padding(0, 6, 0, 0);
            background.Controls.Add(artLabel);
            previewScene.Dock = DockStyle.Fill;
            background.Controls.Add(previewScene);
            previewScene.BringToFront();
            var installation = new Card
            {
                Dock = DockStyle.Fill,
                Margin = Padding.Empty
            };
            rightLayout.Controls.Add(installation, 0, 1);
            installation.Resize += delegate
            {
                installation.Padding = new Padding(installation.ClientSize.Height < 145 ? 10 : 16);
            };
            openFolder.Text = "Open folder";
            Style(openFolder);
            ((AsteraButton)openFolder).Symbol = ButtonSymbol.Folder;
            openFolder.Dock = DockStyle.Bottom;
            openFolder.Click += delegate
            {
                OpenGameFolder();
            };
            installationPath.Dock = DockStyle.Fill;
            installationPath.ForeColor = Muted;
            installationPath.AutoEllipsis = true;
            installationPath.Padding = new Padding(0, 4, 0, 0);
            installation.Controls.Add(installationPath);
            installation.Controls.Add(openFolder);
            installation.Controls.Add(new Label { Text = "Installation", Font = UiTheme.Text(17, true), Dock = DockStyle.Top, Height = 28 });
            activityCard.Dock = DockStyle.Fill;
            activityCard.Margin = Padding.Empty;
            ConfigureReadOnly(log, activityCard.BackColor);
            activityCard.Controls.Add(log);
            activityCard.Controls.Add(new Label { Text = "Update activity", Dock = DockStyle.Top, Height = 34, Font = UiTheme.Text(17, true) });
            AddDiagnosticButtons();
            BuildArtworkPage();
            BuildPreferencesPage();
            releaseLabel.Dock = DockStyle.Fill;
            releaseLabel.ForeColor = Muted;
            releaseLabel.Font = UiTheme.Text(12);
            releaseLabel.TextAlign = ContentAlignment.MiddleLeft;
            BuildWorkProgress(body);
            Controls.Add(viewport);
            Controls.Add(sidebar);
            Controls.Add(header);
            SelectLibraryTab(0);
            BuildTopNavigation(header, sidebar, tagline);
            Disposed += delegate
            {
                CancelGameTransition();
                if (artPreview.Image != null)
                    artPreview.Image.Dispose();
                if (activityCard.Parent == null)
                    activityCard.Dispose();
                if (overviewRight.Parent == null)
                    overviewRight.Dispose();
                timer.Dispose();
                diagnosticsTimer.Dispose();
            };
        }

        static Label Heading(string title)
        {
            return new Label
            {
                Text = title,
                Font = UiTheme.Text(17, true),
                ForeColor = Color.FromArgb(235, 238, 255),
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleLeft
            };
        }

        static void ConfigureReadOnly(ScrollText box, Color background)
        {
            box.Multiline = true;
            box.ReadOnly = true;
            box.TabStop = false;
            box.HideSelection = true;
            box.ScrollBars = ScrollBars.Vertical;
            box.Dock = DockStyle.Fill;
            box.BackColor = background;
            box.ForeColor = Color.FromArgb(185, 196, 225);
            box.BorderStyle = BorderStyle.None;
        }

        void SelectLibraryTab(int index)
        {
            selectedTab = index;
            overviewPage.Visible = index < 2;
            backgroundPage.Visible = index == 2;
            preferencesPage.Visible = index == 3;
            foreach (AsteraButton tab in tabs.Controls)
                tab.Role = (int)tab.Tag == index ? ButtonRole.Primary : ButtonRole.Quiet;
            information.SuspendLayout();
            if (index < 2)
            {
                overviewRight.Visible = index == 0;
                activityCard.Visible = index == 1;
                if (index == 1)
                {
                    information.Controls.Remove(overviewRight);
                    information.Controls.Add(activityCard, 1, 0);
                }
                else
                {
                    information.Controls.Remove(activityCard);
                    information.Controls.Add(overviewRight, 1, 0);
                }
            }

            information.ResumeLayout();
        }

        void UpdateLayoutView()
        {
            UpdateGamePreferences();
            if (IsCustomGame)
            {
                UpdateCustomLayout();
                return;
            }

            chooseLibraryIcon.Visible = GameKind == 0;
            bool mc = GameKind == 0;
            bool ftl = IsMultiverse;
            if (mc)
                RefreshMinecraftPresentation();
            if (minecraftSkinChoice != null)
                minecraftSkinChoice.Visible = mc;
            heroTitle.Text = ftl ? "FTL: Multiverse" : mc ? MinecraftSelectedTitle : "Star Dash";
            banner.Game = mc ? (MinecraftSquaredSelected ? "Minecraft²" : "Minecraft") : banner.Game;
            heroSubtitle.Text = ftl ? "Explore new sectors, command powerful starships, encounter dangerous enemies, and discover countless adventures across the multiverse." : mc ? (MinecraftSquaredSelected ? "Explore a reimagined Minecraft world, meet Allies who fight and grow alongside you, and face new dangers together. Build, survive, and create your own adventures." : "Your Minecraft profiles. Choose a world to return to.") : "Ready for your next race.";
            LayoutHeroContent();
            string background = ftl ? store.Settings.BackgroundFtl : mc ? MinecraftBackground : store.Settings.BackgroundStarDash, character = ftl ? store.Settings.CharacterFtl : mc ? MinecraftCharacter : store.Settings.CharacterStarDash;
            if (mc && String.IsNullOrEmpty(character) && store.Settings.UseMinecraftSkin)
                character = minecraftSkinArt;
            banner.Artwork(background, character);
            banner.LiveSkin(mc && character == minecraftSkinArt && !String.IsNullOrEmpty(minecraftSkinArt) ? minecraftSkinArt : null);
            installationPath.Text = String.IsNullOrWhiteSpace(GameFolder()) ? "No installation linked" : GameFolder();
            openFolder.Enabled = !busy && Directory.Exists(GameFolder());
            chooseBackground.Enabled = chooseCharacter.Enabled = !busy;
            artLabel.Text = (String.IsNullOrEmpty(background) ? (ftl ? "FTL: Multiverse artwork" : mc ? (MinecraftSquaredSelected ? "Minecraft² sunrise" : "Minecraft artwork") : "StarDash — original artwork") : Path.GetFileName(background)) + (String.IsNullOrEmpty(character) ? "" : "\n" + (character == minecraftSkinArt ? "Your Minecraft skin" : Path.GetFileName(character)));
            string previewKey = (ftl ? "ftl:" : mc ? "minecraft:" + (MinecraftSquaredSelected ? "squared:" : "vanilla:") : "stardash:") + (background ?? "");
            if (previewArtwork != previewKey)
            {
                previewArtwork = previewKey;
                var old = artPreview.Image;
                previewScene.Artwork = null;
                artPreview.Image = null;
                try
                {
                    artPreview.Image = String.IsNullOrEmpty(background) ? banner.CopyDefaultArtwork() : Banner.ReadArtwork(background);
                }
                catch
                {
                }

                previewScene.Artwork = artPreview.Image;
                previewScene.Invalidate();
                if (old != null)
                    old.Dispose();
            }
        }

        string GameFolder()
        {
            if (IsCustomGame)
                return PlatformLibraries.Folder(CurrentCustom);
            return IsMultiverse ? store.Settings.FtlDirectory : GameKind == 0 ? store.Settings.Profile : String.IsNullOrWhiteSpace(store.Settings.StarDashExecutable) ? null : Path.GetDirectoryName(store.Settings.StarDashExecutable);
        }

        void OpenGameFolder()
        {
            string folder = GameFolder();
            if (!Directory.Exists(folder))
            {
                Configure();
                return;
            }

            try
            {
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(folder) { UseShellExecute = true });
            }
            catch (Exception error)
            {
                MessageBox.Show(this, error.Message, "Open game folder");
            }
        }

        CheckBox minecraftSkinChoice;
        void BuildArtworkPage()
        {
            var card = new Card
            {
                Dock = DockStyle.Fill,
                Margin = Padding.Empty
            };
            backgroundPage.Controls.Add(card);
            var row = new FlowLayoutPanel
            {
                Dock = DockStyle.Bottom,
                Height = 100,
                WrapContents = true,
                Padding = new Padding(0, 8, 0, 0)
            };
            chooseBackground.Text = "Choose background…";
            Style(chooseBackground);
            ((AsteraButton)chooseBackground).Symbol = ButtonSymbol.Backgrounds;
            chooseBackground.Click += delegate
            {
                ChooseArtwork(false);
            };
            chooseCharacter.Text = "Choose character…";
            Style(chooseCharacter);
            ((AsteraButton)chooseCharacter).Symbol = ButtonSymbol.AddArtwork;
            chooseCharacter.Click += delegate
            {
                ChooseArtwork(true);
            };
            chooseLibraryIcon.Text = "Choose icon…";
            Style(chooseLibraryIcon);
            chooseLibraryIcon.Click += delegate
            {
                ChooseCustomIcon();
            };
            var reset = Button("Reset artwork", delegate
            {
                if (busy)
                    return;
                if (IsCustomGame)
                {
                    CurrentCustom.Background = "";
                    CurrentCustom.Character = "";
                    CurrentCustom.Icon = "";
                    games.Invalidate();
                }
                else if (GameKind == 0)
                {
                    var art = MinecraftArt(false);
                    if (art != null)
                    {
                        art.Background = art.Character = art.Icon = "";
                    }

                    if (MinecraftSquaredSelected)
                    {
                        store.Settings.BackgroundMinecraft = "";
                        store.Settings.CharacterMinecraft = "";
                    }
                }
                else if (IsMultiverse)
                {
                    store.Settings.BackgroundFtl = "";
                    store.Settings.CharacterFtl = "";
                }
                else
                {
                    store.Settings.BackgroundStarDash = "";
                    store.Settings.CharacterStarDash = "";
                }

                store.Save();
                UpdateLayoutView();
                RefreshMinecraftSkin();
            });
            foreach (var button in new[]
            {
                chooseBackground,
                chooseCharacter,
                chooseLibraryIcon,
                reset
            }

            )
                button.Margin = new Padding(0, 0, 8, 6);
            row.Controls.AddRange(new Control[] { chooseBackground, chooseCharacter, chooseLibraryIcon, reset });
            var useSkin = new CheckBox
            {
                Text = "Use my Minecraft Launcher skin when no custom character is set",
                AutoSize = true,
                Checked = store.Settings.UseMinecraftSkin,
                Margin = new Padding(0, 6, 0, 0)
            };
            useSkin.CheckedChanged += delegate
            {
                if (busy)
                {
                    useSkin.Checked = store.Settings.UseMinecraftSkin;
                    return;
                }

                store.Settings.UseMinecraftSkin = useSkin.Checked;
                store.Save();
                UpdateLayoutView();
                if (useSkin.Checked)
                    RefreshMinecraftSkin();
            };
            row.Controls.Add(useSkin);
            minecraftSkinChoice = useSkin;
            artPreview.Dock = DockStyle.Fill;
            artPreview.SizeMode = PictureBoxSizeMode.Zoom;
            artPreview.BackColor = Color.FromArgb(15, 21, 39);
            card.Controls.Add(artPreview);
            card.Controls.Add(row);
            card.Controls.Add(new Label { Text = "Game artwork  ·  Background fills the scene. Transparent PNG/GIF character sits in front and can extend above it.", Dock = DockStyle.Top, Height = 45, ForeColor = Muted });
        }

        void ChooseArtwork(bool character)
        {
            if (busy)
                return;
            using (var dialog = new OpenFileDialog
            {
                Title = character ? "Choose transparent character artwork" : "Choose a game background",
                Filter = character ? "PNG or GIF artwork|*.png;*.gif" : "Artwork|*.png;*.jpg;*.jpeg;*.gif"
            }

            )
            {
                if (dialog.ShowDialog(this) != DialogResult.OK)
                    return;
                try
                {
                    using (var proof = ArtworkAnimation.Open(dialog.FileName))
                    {
                    }

                    if (IsCustomGame)
                    {
                        if (character)
                            CurrentCustom.Character = dialog.FileName;
                        else
                            CurrentCustom.Background = dialog.FileName;
                    }
                    else if (GameKind == 0)
                    {
                        SetMinecraftArtwork(dialog.FileName, character);
                    }
                    else if (IsMultiverse)
                    {
                        if (character)
                            store.Settings.CharacterFtl = dialog.FileName;
                        else
                            store.Settings.BackgroundFtl = dialog.FileName;
                    }
                    else
                    {
                        if (character)
                            store.Settings.CharacterStarDash = dialog.FileName;
                        else
                            store.Settings.BackgroundStarDash = dialog.FileName;
                    }

                    store.Save();
                    UpdateLayoutView();
                }
                catch (Exception error)
                {
                    MessageBox.Show(this, error.Message, "Game artwork");
                }
            }
        }

        void BuildPreferencesPage()
        {
            BuildGamePreferences();
        }
    }
}
