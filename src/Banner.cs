using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Windows.Forms;

namespace Astera
{
    // Background and character are separate layers; hats/hair may extend above the hero.
    sealed partial class Banner : Panel
    {
        Bitmap backdropBuffer, frameBuffer;
        bool frameDirty = true;
        public string Game = "Minecraft²";
        bool transitionPaused;
        bool launchCovered;
        internal bool LaunchAnimationPaused
        {
            get
            {
                return launchCovered;
            }
        }

        internal void PauseForLaunch(bool covered)
        {
            launchCovered = covered;
            UpdateRigClock();
        }

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
            rigClock.Enabled = !transitionPaused && !launchCovered && UiEffects.Enabled && (liveRig != null || HasAnimatedArtwork) && IsHandleCreated && Visible && owner != null && owner.Visible && owner.WindowState != FormWindowState.Minimized;
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
}
