using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using System.Windows.Forms;

namespace BattleScarSelectorLegacy
{
    internal static class Program
    {
        [STAThread]
        private static void Main()
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            try
            {
                Application.Run(new SelectorForm());
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.ToString(), "Battle Scar Selector", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }
    }

    internal enum ScarKind
    {
        Blessing,
        Curse
    }

    internal sealed class Scar
    {
        public Scar(string name, int id, ScarKind kind, string description)
        {
            Name = name;
            Id = id;
            Kind = kind;
            Description = description ?? string.Empty;
        }

        public string Name { get; private set; }
        public int Id { get; private set; }
        public ScarKind Kind { get; private set; }
        public string Description { get; private set; }
    }

    internal static class EmbeddedAssets
    {
        private static readonly Assembly Assembly = typeof(Program).Assembly;

        public static Stream Open(string logicalName)
        {
            var names = Assembly.GetManifestResourceNames();
            foreach (var name in names)
            {
                if (string.Equals(name, logicalName, StringComparison.OrdinalIgnoreCase) ||
                    name.EndsWith("." + logicalName, StringComparison.OrdinalIgnoreCase))
                    return Assembly.GetManifestResourceStream(name);
            }
            return null;
        }

        public static string ReadText(string logicalName)
        {
            using (var stream = Open(logicalName))
            {
                if (stream == null)
                    return string.Empty;
                using (var reader = new StreamReader(stream, Encoding.UTF8, true))
                    return reader.ReadToEnd();
            }
        }

        public static Image LoadImage(string logicalName)
        {
            using (var stream = Open(logicalName))
            {
                if (stream == null)
                    return null;
                using (var source = Image.FromStream(stream))
                    return new Bitmap(source);
            }
        }
    }

    internal sealed class SelectorForm : Form
    {
        private const string ConfigName = "BattleScarSelector.cfg";
        private readonly Panel pageHost = new Panel();
        private readonly List<Scar> allScars;
        private readonly List<int> selectedBlessings = new List<int>();
        private readonly List<int> selectedCurses = new List<int>();
        private readonly Dictionary<int, Image> iconCache = new Dictionary<int, Image>();
        private string statusMessage = string.Empty;
        private HomePage homePage;
        private SelectionPage selectionPage;

        public SelectorForm()
        {
            Text = "Pass The Fear - Battle Scar Selector";
            FormBorderStyle = FormBorderStyle.FixedSingle;
            MaximizeBox = false;
            MinimizeBox = true;
            StartPosition = FormStartPosition.CenterScreen;
            BackColor = Color.Black;
            AutoScaleMode = AutoScaleMode.None;
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint | ControlStyles.OptimizedDoubleBuffer, true);

            ModFolder = ResolveModFolder();
            allScars = LoadScars();
            BackgroundAsset = LoadAssetImage("assets.home-background.png", "home-background.png");
            ButtonFrameAsset = LoadAssetImage("assets.button-frame.png", "button-frame.png");
            LoadExistingSelection();

            pageHost.Dock = DockStyle.Fill;
            pageHost.BackColor = Color.Black;
            Controls.Add(pageHost);
            ShowHome();
        }

        public string ModFolder { get; private set; }
        public Image BackgroundAsset { get; private set; }
        public Image ButtonFrameAsset { get; private set; }
        public IList<Scar> AllScars { get { return allScars; } }
        public bool CanStart { get { return selectedBlessings.Count == 3 && selectedCurses.Count == 3; } }
        public string StatusMessage { get { return statusMessage; } }

        public void ShowHome()
        {
            ClientSize = new Size(600, 900);
            pageHost.Controls.Clear();
            homePage = new HomePage(this);
            pageHost.Controls.Add(homePage);
            homePage.Focus();
        }

        public void ShowSelection(ScarKind kind)
        {
            ClientSize = new Size(1280, 760);
            pageHost.Controls.Clear();
            selectionPage = new SelectionPage(this, kind);
            pageHost.Controls.Add(selectionPage);
            selectionPage.Focus();
        }

        public IList<int> GetSelectedIds(ScarKind kind)
        {
            return kind == ScarKind.Blessing ? selectedBlessings : selectedCurses;
        }

        public bool IsSelected(ScarKind kind, int id)
        {
            return GetSelectedIds(kind).Contains(id);
        }

        public Scar FindScar(ScarKind kind, int id)
        {
            return allScars.FirstOrDefault(x => x.Kind == kind && x.Id == id);
        }

        public Image GetIcon(int id)
        {
            Image cached;
            if (iconCache.TryGetValue(id, out cached))
                return cached;

            var image = EmbeddedAssets.LoadImage("icons." + id.ToString(CultureInfo.InvariantCulture) + ".png");
            if (image == null)
            {
                var paths = new[]
                {
                    Path.Combine(ModFolder, "icons", id + ".png"),
                    Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "icons", id + ".png")
                };
                foreach (var path in paths)
                {
                    if (!File.Exists(path))
                        continue;
                    try
                    {
                        using (var source = Image.FromFile(path))
                            image = new Bitmap(source);
                        break;
                    }
                    catch
                    {
                        image = null;
                    }
                }
            }
            if (image != null)
                iconCache[id] = image;
            return image;
        }

        public void ToggleSelection(ScarKind kind, int id)
        {
            var list = GetSelectedIds(kind);
            if (list.Contains(id))
            {
                list.Remove(id);
                statusMessage = string.Empty;
            }
            else if (list.Count >= 3)
            {
                statusMessage = "Maximum 3 " + (kind == ScarKind.Blessing ? "Blessings" : "Curses") + " can be selected.";
            }
            else
            {
                list.Add(id);
                statusMessage = string.Empty;
            }
            if (homePage != null) homePage.Invalidate();
            if (selectionPage != null) selectionPage.Invalidate();
        }

        public void SetStatus(string message)
        {
            statusMessage = message ?? string.Empty;
            if (homePage != null) homePage.Invalidate();
            if (selectionPage != null) selectionPage.Invalidate();
        }

        public void StartRun()
        {
            string error;
            if (!SaveSelection(out error))
            {
                SetStatus(error);
                return;
            }
            MessageBox.Show("Selection saved. Start the game to apply it at the beginning of the next run.",
                "Battle Scar Selector", MessageBoxButtons.OK, MessageBoxIcon.Information);
            Close();
        }

        private bool SaveSelection(out string error)
        {
            error = string.Empty;
            if (selectedBlessings.Count != 3 || selectedCurses.Count != 3)
            {
                error = "Select 3 Blessings and 3 Curses before starting.";
                return false;
            }
            if (!Directory.Exists(ModFolder))
            {
                error = "The mod folder could not be found.";
                return false;
            }
            var lines = new List<string>
            {
                "# Pass The Fear Battle Scar Selector",
                "# Saved by the visual selector. Applied at the beginning of the next run."
            };
            for (var i = 0; i < 3; i++)
            {
                lines.Add("slot" + (i + 1) + "_blessing=" + selectedBlessings[i]);
                lines.Add("slot" + (i + 1) + "_curse=" + selectedCurses[i]);
            }
            try
            {
                File.WriteAllLines(Path.Combine(ModFolder, ConfigName), lines, Encoding.UTF8);
                return true;
            }
            catch (Exception ex)
            {
                error = "Could not save the selection: " + ex.Message;
                return false;
            }
        }

        private void LoadExistingSelection()
        {
            var path = Path.Combine(ModFolder, ConfigName);
            if (!File.Exists(path))
            {
                statusMessage = "Select 3 Blessings and 3 Curses to unlock Start.";
                return;
            }
            var values = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            foreach (var line in File.ReadLines(path))
            {
                var separator = line.IndexOf('=');
                int value;
                if (separator > 0 && int.TryParse(line.Substring(separator + 1).Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out value))
                    values[line.Substring(0, separator).Trim()] = value;
            }
            for (var i = 1; i <= 3; i++)
            {
                int blessing;
                int curse;
                if (values.TryGetValue("slot" + i + "_blessing", out blessing) && FindScar(ScarKind.Blessing, blessing) != null)
                    selectedBlessings.Add(blessing);
                if (values.TryGetValue("slot" + i + "_curse", out curse) && FindScar(ScarKind.Curse, curse) != null)
                    selectedCurses.Add(curse);
            }
            statusMessage = CanStart ? "Existing selection loaded." : "Select 3 Blessings and 3 Curses to unlock Start.";
        }

        private string ResolveModFolder()
        {
            var baseFolder = AppDomain.CurrentDomain.BaseDirectory;
            var candidates = new[]
            {
                Path.Combine(baseFolder, "BepInEx", "plugins", "PassTheFearBattleScarSelector"),
                baseFolder
            };
            foreach (var candidate in candidates)
            {
                if (File.Exists(Path.Combine(candidate, "PassTheFearBattleScarSelector.dll")))
                    return candidate;
            }
            return baseFolder;
        }

        private static Image LoadAssetImage(string embeddedName, string fallbackName)
        {
            var image = EmbeddedAssets.LoadImage(embeddedName);
            if (image != null)
                return image;
            var path = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, fallbackName);
            if (!File.Exists(path))
                return null;
            try
            {
                using (var source = Image.FromFile(path))
                    return new Bitmap(source);
            }
            catch
            {
                return null;
            }
        }

        private static List<Scar> LoadScars()
        {
            var csv = EmbeddedAssets.ReadText("data.battle_scar_ids.csv");
            var effects = LoadEffectDescriptions(EmbeddedAssets.ReadText("data.battle_scar_effects.tsv"));
            var result = new List<Scar>();
            if (string.IsNullOrWhiteSpace(csv))
                return result;
            foreach (var line in csv.Split(new[] { "\r\n", "\n" }, StringSplitOptions.RemoveEmptyEntries).Skip(1))
            {
                var parts = SplitCsv(line);
                int id;
                if (parts.Length != 3 || !int.TryParse(parts[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out id))
                    continue;
                var kind = string.Equals(parts[2].Trim(), "Curse", StringComparison.OrdinalIgnoreCase)
                    ? ScarKind.Curse : ScarKind.Blessing;
                string description;
                effects.TryGetValue(id, out description);
                result.Add(new Scar(parts[0].Trim(), id, kind, description ?? string.Empty));
            }
            result.Sort(delegate(Scar a, Scar b) { return string.Compare(a.Name, b.Name, StringComparison.OrdinalIgnoreCase); });
            return result;
        }

        private static string[] SplitCsv(string line)
        {
            var fields = new List<string>();
            var field = new StringBuilder();
            var quoted = false;
            for (var i = 0; i < line.Length; i++)
            {
                var ch = line[i];
                if (ch == '"')
                {
                    if (quoted && i + 1 < line.Length && line[i + 1] == '"')
                    {
                        field.Append('"');
                        i++;
                    }
                    else
                        quoted = !quoted;
                }
                else if (ch == ',' && !quoted)
                {
                    fields.Add(field.ToString());
                    field.Clear();
                }
                else
                    field.Append(ch);
            }
            fields.Add(field.ToString());
            return fields.ToArray();
        }

        private static Dictionary<int, string> LoadEffectDescriptions(string text)
        {
            var result = new Dictionary<int, string>();
            if (string.IsNullOrWhiteSpace(text))
                return result;
            foreach (var line in text.Split(new[] { "\r\n", "\n" }, StringSplitOptions.RemoveEmptyEntries).Skip(1))
            {
                var parts = line.Split('\t');
                int id;
                if (parts.Length < 4 || !int.TryParse(parts[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out id))
                    continue;
                result[id] = Unescape(parts[3]);
            }
            return result;
        }

        private static string Unescape(string value)
        {
            return value.Replace("\\n", "\n").Replace("\\r", "\r").Replace("\\t", "\t");
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                foreach (var image in iconCache.Values)
                    image.Dispose();
                if (BackgroundAsset != null) BackgroundAsset.Dispose();
                if (ButtonFrameAsset != null) ButtonFrameAsset.Dispose();
            }
            base.Dispose(disposing);
        }
    }

    internal sealed class HomePage : Panel
    {
        private readonly SelectorForm owner;
        private readonly Rectangle[] buttons = new Rectangle[3];
        private int hoverIndex = -1;

        public HomePage(SelectorForm owner)
        {
            this.owner = owner;
            Dock = DockStyle.Fill;
            BackColor = Color.Black;
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint | ControlStyles.OptimizedDoubleBuffer, true);
            MouseMove += HandleMouseMove;
            MouseLeave += delegate { hoverIndex = -1; Cursor = Cursors.Default; Invalidate(); };
            MouseClick += HandleMouseClick;
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.InterpolationMode = InterpolationMode.HighQualityBicubic;
            DrawBackground(g);
            var scale = Math.Min(Width / 600f, Height / 900f);
            var buttonWidth = Math.Max(1, (int)(250 * scale));
            var buttonHeight = Math.Max(1, (int)(68 * scale));
            var x = (Width - buttonWidth) / 2;
            var ys = new[] { 292, 368, 744 };
            var labels = new[] { "BLESSINGS", "CURSES", "START" };
            for (var i = 0; i < buttons.Length; i++)
            {
                buttons[i] = new Rectangle(x, (int)(ys[i] * scale), buttonWidth, buttonHeight);
                DrawGameButton(g, buttons[i], labels[i], hoverIndex == i, i == 2 && !owner.CanStart, scale);
            }
            if (!string.IsNullOrWhiteSpace(owner.StatusMessage))
            {
                using (var f = new Font("Segoe UI", Math.Max(11, 13 * scale), FontStyle.Regular, GraphicsUnit.Pixel))
                using (var brush = new SolidBrush(Color.FromArgb(220, 235, 210, 151)))
                using (var format = new StringFormat { Alignment = StringAlignment.Center })
                    g.DrawString(owner.StatusMessage, f, brush, new RectangleF(20, Height - 95 * scale, Width - 40, 28 * scale), format);
            }
        }

        private void DrawBackground(Graphics g)
        {
            if (owner.BackgroundAsset == null)
            {
                g.Clear(Color.Black);
                return;
            }
            var image = owner.BackgroundAsset;
            var scale = Math.Max((float)Width / image.Width, (float)Height / image.Height);
            var w = (int)(image.Width * scale);
            var h = (int)(image.Height * scale);
            g.DrawImage(image, new Rectangle((Width - w) / 2, (Height - h) / 2, w, h));
            using (var brush = new SolidBrush(Color.FromArgb(42, 7, 5, 15)))
                g.FillRectangle(brush, ClientRectangle);
            using (var pen = new Pen(Color.FromArgb(28, 255, 255, 255), 1))
            using (var path = RoundedRectangle(new Rectangle(105, 275, 390, 575), 26))
                g.DrawPath(pen, path);
        }

        private void DrawGameButton(Graphics g, Rectangle rect, string text, bool hover, bool disabled, float scale)
        {
            if (owner.ButtonFrameAsset != null)
                g.DrawImage(owner.ButtonFrameAsset, rect);
            else
            {
                using (var brush = new SolidBrush(Color.FromArgb(60, 42, 30)))
                    g.FillRectangle(brush, rect);
            }
            var inner = new Rectangle(rect.X + (int)(rect.Width * .16), rect.Y + (int)(rect.Height * .25),
                (int)(rect.Width * .75), (int)(rect.Height * .58));
            using (var brush = new SolidBrush(Color.FromArgb(255, 24, 20, 25)))
            using (var path = RoundedRectangle(inner, Math.Max(8, (int)(rect.Height * .22))))
            {
                g.FillPath(brush, path);
                using (var pen = new Pen(Color.FromArgb(200, 74, 61, 68), Math.Max(1, (int)(scale * 1.2f))))
                    g.DrawPath(pen, path);
            }
            using (var font = new Font("Segoe UI Light", Math.Max(12, rect.Height * .36f), FontStyle.Regular, GraphicsUnit.Pixel))
            using (var shadow = new SolidBrush(Color.FromArgb(225, 0, 0, 0)))
            using (var textBrush = new SolidBrush(Color.FromArgb(255, 247, 244, 242)))
            using (var format = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center })
            {
                var shadowRect = new RectangleF(rect.X + 2, rect.Y + 2, rect.Width, rect.Height);
                g.DrawString(text, font, shadow, shadowRect, format);
                g.DrawString(text, font, textBrush, rect, format);
            }
            if (hover && !disabled)
            {
                using (var pen = new Pen(Color.FromArgb(255, 255, 222, 111), Math.Max(2, (int)(scale * 2))))
                using (var path = RoundedRectangle(new Rectangle(rect.X + 2, rect.Y + 2, rect.Width - 4, rect.Height - 4), Math.Max(10, (int)(rect.Height * .22))))
                    g.DrawPath(pen, path);
            }
            if (disabled)
            {
                using (var brush = new SolidBrush(Color.FromArgb(130, 22, 23, 29)))
                    g.FillRectangle(brush, rect);
            }
        }

        private void HandleMouseMove(object sender, MouseEventArgs e)
        {
            var next = HitButton(e.Location);
            if (next == 2 && !owner.CanStart)
                next = -1;
            if (next != hoverIndex)
            {
                hoverIndex = next;
                Cursor = hoverIndex >= 0 ? Cursors.Hand : Cursors.Default;
                Invalidate();
            }
        }

        private void HandleMouseClick(object sender, MouseEventArgs e)
        {
            if (e.Button != MouseButtons.Left)
                return;
            var hit = HitButton(e.Location);
            if (hit == 0) owner.ShowSelection(ScarKind.Blessing);
            else if (hit == 1) owner.ShowSelection(ScarKind.Curse);
            else if (hit == 2 && owner.CanStart) owner.StartRun();
        }

        private int HitButton(Point point)
        {
            for (var i = 0; i < buttons.Length; i++)
                if (buttons[i].Contains(point)) return i;
            return -1;
        }

        internal static GraphicsPath RoundedRectangle(Rectangle rectangle, int radius)
        {
            var path = new GraphicsPath();
            var diameter = Math.Max(2, radius * 2);
            path.AddArc(rectangle.X, rectangle.Y, diameter, diameter, 180, 90);
            path.AddArc(rectangle.Right - diameter, rectangle.Y, diameter, diameter, 270, 90);
            path.AddArc(rectangle.Right - diameter, rectangle.Bottom - diameter, diameter, diameter, 0, 90);
            path.AddArc(rectangle.X, rectangle.Bottom - diameter, diameter, diameter, 90, 90);
            path.CloseFigure();
            return path;
        }
    }

    internal sealed class SelectionPage : Panel
    {
        private readonly SelectorForm owner;
        private readonly ScarKind kind;
        private readonly List<Scar> items;
        private readonly Rectangle[] cardRects;
        private int hoverIndex = -1;

        public SelectionPage(SelectorForm owner, ScarKind kind)
        {
            this.owner = owner;
            this.kind = kind;
            items = owner.AllScars.Where(x => x.Kind == kind).ToList();
            cardRects = new Rectangle[items.Count];
            Dock = DockStyle.Fill;
            BackColor = Color.Black;
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint | ControlStyles.OptimizedDoubleBuffer, true);
            MouseMove += HandleMouseMove;
            MouseLeave += delegate { hoverIndex = -1; Cursor = Cursors.Default; Invalidate(); };
            MouseClick += HandleMouseClick;
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.InterpolationMode = InterpolationMode.HighQualityBicubic;
            g.Clear(Color.FromArgb(3, 4, 6));
            var title = kind == ScarKind.Blessing ? "BLESSINGS" : "CURSES";
            using (var titleFont = new Font("Georgia", 32, FontStyle.Bold, GraphicsUnit.Pixel))
            using (var titleBrush = new SolidBrush(Color.FromArgb(214, 184, 91)))
                g.DrawString(title, titleFont, titleBrush, new Point(34, 18));
            using (var hintFont = new Font("Segoe UI", 15, FontStyle.Regular, GraphicsUnit.Pixel))
            using (var hintBrush = new SolidBrush(Color.FromArgb(165, 164, 160)))
                g.DrawString("Choose up to three", hintFont, hintBrush, new Point(704, 30));
            using (var linePen = new Pen(Color.FromArgb(112, 76, 52), 2))
                g.DrawLine(linePen, 34, 63, 812, 63);

            const int tile = 68;
            const int gap = 16;
            const int gridX = 36;
            const int gridY = 85;
            const int columns = 8;
            for (var i = 0; i < items.Count; i++)
            {
                var column = i % columns;
                var row = i / columns;
                var rectangle = new Rectangle(gridX + column * (tile + gap), gridY + row * (tile + gap), tile, tile);
                cardRects[i] = rectangle;
                DrawCard(g, rectangle, items[i], i == hoverIndex);
            }
            DrawDetails(g);
            DrawBackButton(g);
        }

        private void DrawCard(Graphics g, Rectangle rectangle, Scar scar, bool hovered)
        {
            var selected = owner.IsSelected(kind, scar.Id);
            var fill = selected
                ? (kind == ScarKind.Blessing ? Color.FromArgb(35, 27, 18) : Color.FromArgb(45, 20, 20))
                : Color.FromArgb(16, 18, 22);
            using (var path = HomePage.RoundedRectangle(rectangle, 10))
            using (var brush = new SolidBrush(fill))
            using (var pen = new Pen(Color.FromArgb(62, 55, 49), 1))
            {
                g.FillPath(brush, path);
                g.DrawPath(pen, path);
            }
            if (selected)
            {
                using (var path = HomePage.RoundedRectangle(new Rectangle(rectangle.X - 3, rectangle.Y - 3, rectangle.Width + 6, rectangle.Height + 6), 12))
                using (var pen = new Pen(Color.FromArgb(225, 180, 62), 3))
                    g.DrawPath(pen, path);
                DrawArrows(g, rectangle, Color.FromArgb(59, 244, 40));
            }
            else if (hovered)
                DrawArrows(g, rectangle, Color.FromArgb(59, 244, 40));

            var icon = owner.GetIcon(scar.Id);
            if (icon == null)
                return;
            var destination = FitRectangle(icon.Size, new Rectangle(rectangle.X + 8, rectangle.Y + 8, rectangle.Width - 16, rectangle.Height - 16));
            g.DrawImage(icon, destination);
        }

        private void DrawDetails(Graphics g)
        {
            const int panelX = 850;
            const int panelY = 18;
            const int panelW = 395;
            const int panelH = 680;
            using (var outer = HomePage.RoundedRectangle(new Rectangle(panelX, panelY, panelW, panelH), 15))
            using (var inner = HomePage.RoundedRectangle(new Rectangle(panelX + 9, panelY + 9, panelW - 18, panelH - 18), 11))
            using (var fill = new SolidBrush(Color.FromArgb(7, 8, 10)))
            using (var outerPen = new Pen(Color.FromArgb(123, 82, 57), 2))
            using (var innerPen = new Pen(Color.FromArgb(64, 43, 35), 1))
            {
                g.FillPath(fill, outer);
                g.DrawPath(outerPen, outer);
                g.DrawPath(innerPen, inner);
            }
            using (var labelFont = new Font("Segoe UI", 13, FontStyle.Regular, GraphicsUnit.Pixel))
            using (var labelBrush = new SolidBrush(Color.FromArgb(155, 151, 145)))
                g.DrawString("SELECTED " + (kind == ScarKind.Blessing ? "BLESSING" : "CURSE"), labelFont, labelBrush, new Point(panelX + 24, panelY + 25));
            using (var countFont = new Font("Segoe UI", 16, FontStyle.Regular, GraphicsUnit.Pixel))
            using (var countBrush = new SolidBrush(Color.FromArgb(95, 238, 70)))
            using (var countFormat = new StringFormat { Alignment = StringAlignment.Far })
                g.DrawString(owner.GetSelectedIds(kind).Count + " / 3", countFont, countBrush, new RectangleF(panelX + 230, panelY + 24, 138, 24), countFormat);

            var current = CurrentScar();
            if (current == null)
            {
                using (var emptyFont = new Font("Georgia", 23, FontStyle.Bold, GraphicsUnit.Pixel))
                using (var emptyBrush = new SolidBrush(Color.FromArgb(214, 184, 91)))
                using (var emptyFormat = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center })
                    g.DrawString("Select an icon", emptyFont, emptyBrush, new RectangleF(panelX + 35, panelY + 240, panelW - 70, 70), emptyFormat);
            }
            else
            {
                var icon = owner.GetIcon(current.Id);
                using (var ellipseBrush = new SolidBrush(Color.FromArgb(50, kind == ScarKind.Curse ? 28 : 32, 26)))
                using (var ellipsePen = new Pen(Color.FromArgb(137, 82, 57), 3))
                {
                    g.FillEllipse(ellipseBrush, new Rectangle(panelX + 113, panelY + 56, 168, 168));
                    g.DrawEllipse(ellipsePen, new Rectangle(panelX + 113, panelY + 56, 168, 168));
                }
                if (icon != null)
                    g.DrawImage(icon, FitRectangle(icon.Size, new Rectangle(panelX + 125, panelY + 68, 144, 144)));
                var accent = kind == ScarKind.Curse ? Color.FromArgb(222, 48, 47) : Color.FromArgb(220, 190, 100);
                using (var nameFont = new Font("Georgia", 25, FontStyle.Bold, GraphicsUnit.Pixel))
                using (var nameBrush = new SolidBrush(accent))
                using (var nameFormat = new StringFormat { Alignment = StringAlignment.Center })
                    g.DrawString(current.Name, nameFont, nameBrush, new RectangleF(panelX + 20, panelY + 244, panelW - 40, 35), nameFormat);
                using (var separator = new Pen(Color.FromArgb(157, 111, 81), 2))
                    g.DrawLine(separator, panelX + 36, panelY + 276, panelX + panelW - 36, panelY + 276);
                var description = CleanDescription(current.Description);
                using (var bodyFont = new Font("Georgia", 20, FontStyle.Regular, GraphicsUnit.Pixel))
                using (var bodyBrush = new SolidBrush(accent))
                using (var bodyFormat = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Near })
                    g.DrawString(WrapText(g, description, bodyFont, panelW - 62), bodyFont, bodyBrush, new RectangleF(panelX + 31, panelY + 308, panelW - 62, 200), bodyFormat);
            }
            using (var hintFont = new Font("Segoe UI", 14, FontStyle.Regular, GraphicsUnit.Pixel))
            using (var hintBrush = new SolidBrush(Color.FromArgb(147, 146, 140)))
            using (var hintFormat = new StringFormat { Alignment = StringAlignment.Center })
                g.DrawString(string.IsNullOrWhiteSpace(owner.StatusMessage) ? "Click an icon to select it" : owner.StatusMessage,
                    hintFont, hintBrush, new RectangleF(panelX + 25, panelY + panelH - 78, panelW - 50, 35), hintFormat);
        }

        private Scar CurrentScar()
        {
            if (hoverIndex >= 0 && hoverIndex < items.Count)
                return items[hoverIndex];
            var selected = owner.GetSelectedIds(kind);
            return selected.Count == 0 ? null : owner.FindScar(kind, selected[selected.Count - 1]);
        }

        private void DrawBackButton(Graphics g)
        {
            var rectangle = new Rectangle(30, 680, 61, 61);
            using (var path = HomePage.RoundedRectangle(rectangle, 10))
            using (var brush = new SolidBrush(Color.FromArgb(49, 31, 26)))
            using (var pen = new Pen(Color.FromArgb(143, 91, 57), 2))
            {
                g.FillPath(brush, path);
                g.DrawPath(pen, path);
            }
            using (var arrowPen = new Pen(Color.FromArgb(236, 207, 125), 5))
            using (var arrowBrush = new SolidBrush(Color.FromArgb(236, 207, 125)))
            {
                g.DrawLine(arrowPen, 47, 710, 76, 710);
                g.FillPolygon(arrowBrush, new[]
                {
                    new Point(42, 710), new Point(57, 698), new Point(57, 705),
                    new Point(75, 705), new Point(75, 715), new Point(57, 715), new Point(57, 722)
                });
            }
        }

        private void DrawArrows(Graphics g, Rectangle rectangle, Color color)
        {
            using (var brush = new SolidBrush(color))
            {
                g.FillPolygon(brush, new[] { new Point(rectangle.X - 9, rectangle.Y + 19), new Point(rectangle.X - 16, rectangle.Y + 9), new Point(rectangle.X - 16, rectangle.Y + 29) });
                g.FillPolygon(brush, new[] { new Point(rectangle.Right + 9, rectangle.Y + 19), new Point(rectangle.Right + 16, rectangle.Y + 9), new Point(rectangle.Right + 16, rectangle.Y + 29) });
                g.FillPolygon(brush, new[] { new Point(rectangle.X + rectangle.Width / 2, rectangle.Y - 9), new Point(rectangle.X + rectangle.Width / 2 - 10, rectangle.Y - 16), new Point(rectangle.X + rectangle.Width / 2 + 10, rectangle.Y - 16) });
                g.FillPolygon(brush, new[] { new Point(rectangle.X + rectangle.Width / 2, rectangle.Bottom + 9), new Point(rectangle.X + rectangle.Width / 2 - 10, rectangle.Bottom + 16), new Point(rectangle.X + rectangle.Width / 2 + 10, rectangle.Bottom + 16) });
            }
        }

        private void HandleMouseMove(object sender, MouseEventArgs e)
        {
            var next = HitCard(e.Location);
            if (next != hoverIndex)
            {
                hoverIndex = next;
                Cursor = hoverIndex >= 0 ? Cursors.Hand : Cursors.Default;
                Invalidate();
            }
        }

        private void HandleMouseClick(object sender, MouseEventArgs e)
        {
            if (e.Button != MouseButtons.Left)
                return;
            if (new Rectangle(30, 680, 61, 61).Contains(e.Location))
            {
                owner.ShowHome();
                return;
            }
            var hit = HitCard(e.Location);
            if (hit >= 0)
                owner.ToggleSelection(kind, items[hit].Id);
        }

        private int HitCard(Point point)
        {
            for (var i = 0; i < cardRects.Length; i++)
                if (cardRects[i].Contains(point)) return i;
            return -1;
        }

        private static Rectangle FitRectangle(Size source, Rectangle bounds)
        {
            var scale = Math.Min((float)bounds.Width / source.Width, (float)bounds.Height / source.Height);
            var width = Math.Max(1, (int)(source.Width * scale));
            var height = Math.Max(1, (int)(source.Height * scale));
            return new Rectangle(bounds.X + (bounds.Width - width) / 2, bounds.Y + (bounds.Height - height) / 2, width, height);
        }

        private static string CleanDescription(string value)
        {
            return Regex.Replace(value ?? string.Empty, "<[^>]+>", string.Empty).Replace("  ", " ").Trim();
        }

        private static string WrapText(Graphics g, string value, Font font, int width)
        {
            var lines = new List<string>();
            var current = string.Empty;
            foreach (var word in value.Split(new[] { ' ', '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries))
            {
                var candidate = string.IsNullOrEmpty(current) ? word : current + " " + word;
                if (g.MeasureString(candidate, font).Width <= width)
                    current = candidate;
                else
                {
                    if (!string.IsNullOrEmpty(current)) lines.Add(current);
                    current = word;
                }
            }
            if (!string.IsNullOrEmpty(current)) lines.Add(current);
            return string.Join("\n", lines.ToArray());
        }
    }
}
