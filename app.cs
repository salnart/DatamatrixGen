using System;
using System.IO;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Text;
using System.Drawing.Printing;
using System.Windows.Forms;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using ZXing;
using ZXing.Common;

namespace DataMatrixGenerator
{
    // Custom Log Entry Class
    public class LogEntry
    {
        private DateTime timestamp;
        private string level;
        private string message;

        public DateTime Timestamp { get { return timestamp; } set { timestamp = value; } }
        public string Level { get { return level; } set { level = value; } }
        public string Message { get { return message; } set { message = value; } }

        public LogEntry() {}
        public LogEntry(DateTime ts, string lvl, string msg)
        {
            this.timestamp = ts;
            this.level = lvl;
            this.message = msg;
        }
    }

    // --- UI HELPERS FOR HIGH-QUALITY PAINTING ---
    public static class UIHelpers
    {
        public static GraphicsPath CreateRoundedRectanglePath(Rectangle rect, int radius)
        {
            GraphicsPath path = new GraphicsPath();
            int diameter = radius * 2;
            Rectangle arc = new Rectangle(rect.X, rect.Y, diameter, diameter);

            // Top left
            path.AddArc(arc, 180, 90);
            // Top right
            arc.X = rect.Right - diameter;
            path.AddArc(arc, 270, 90);
            // Bottom right
            arc.Y = rect.Bottom - diameter;
            path.AddArc(arc, 0, 90);
            // Bottom left
            arc.X = rect.Left;
            path.AddArc(arc, 90, 90);

            path.CloseFigure();
            return path;
        }

        public static void DrawRoundedRectangle(Graphics g, Pen pen, Rectangle rect, int radius)
        {
            using (GraphicsPath path = CreateRoundedRectanglePath(rect, radius))
            {
                g.DrawPath(pen, path);
            }
        }

        public static void FillRoundedRectangle(Graphics g, Brush brush, Rectangle rect, int radius)
        {
            using (GraphicsPath path = CreateRoundedRectanglePath(rect, radius))
            {
                g.FillPath(brush, path);
            }
        }

        public static void DrawStar(Graphics g, Rectangle r, Color color)
        {
            g.SmoothingMode = SmoothingMode.AntiAlias;
            float centerX = r.Left + r.Width / 2f;
            float centerY = r.Top + r.Height / 2f;
            float rx = r.Width / 2f;
            float ry = r.Height / 2f;

            PointF pTop = new PointF(centerX, centerY - ry);
            PointF pRight = new PointF(centerX + rx, centerY);
            PointF pBottom = new PointF(centerX, centerY + ry);
            PointF pLeft = new PointF(centerX - rx, centerY);

            float px = rx * 0.25f;
            float py = ry * 0.25f;
            PointF cTR = new PointF(centerX + px, centerY - py);
            PointF cBR = new PointF(centerX + px, centerY + py);
            PointF cBL = new PointF(centerX - px, centerY + py);
            PointF cTL = new PointF(centerX - px, centerY - py);

            GraphicsPath path = new GraphicsPath();
            path.AddLine(pTop, cTR);
            path.AddLine(cTR, pRight);
            path.AddLine(pRight, cBR);
            path.AddLine(cBR, pBottom);
            path.AddLine(pBottom, cBL);
            path.AddLine(cBL, pLeft);
            path.AddLine(pLeft, cTL);
            path.AddLine(cTL, pTop);
            path.CloseAllFigures();

            using (Brush b = new SolidBrush(color))
            {
                g.FillPath(b, path);
            }
        }

        public static void DrawImagePlaceholder(Graphics g, Rectangle r, Color color)
        {
            g.SmoothingMode = SmoothingMode.AntiAlias;
            int radius = 6;
            using (Pen pen = new Pen(color, 2f))
            {
                DrawRoundedRectangle(g, pen, r, radius);
            }

            using (GraphicsPath mountPath = new GraphicsPath())
            {
                float x1 = r.Left + 4, y1 = r.Bottom - 4;
                float x2 = r.Left + r.Width * 0.35f, y2 = r.Top + r.Height * 0.4f;
                float x3 = r.Left + r.Width * 0.55f, y3 = r.Bottom - 4;
                mountPath.AddLine(x1, y1, x2, y2);
                mountPath.AddLine(x2, y2, x3, y3);
                mountPath.CloseFigure();

                mountPath.StartFigure();
                float x4 = r.Left + r.Width * 0.35f, y4 = r.Bottom - 4;
                float x5 = r.Left + r.Width * 0.65f, y5 = r.Top + r.Height * 0.55f;
                float x6 = r.Right - 4, y6 = r.Bottom - 4;
                mountPath.AddLine(x4, y4, x5, y5);
                mountPath.AddLine(x5, y5, x6, y6);
                mountPath.CloseFigure();

                using (Brush b = new SolidBrush(Color.FromArgb(30, color)))
                {
                    g.FillPath(b, mountPath);
                }
                using (Pen pen = new Pen(color, 1.5f))
                {
                    g.DrawPath(pen, mountPath);
                }
            }

            using (Brush b = new SolidBrush(color))
            {
                g.FillEllipse(b, r.Right - 16, r.Top + 10, 8, 8);
            }
        }

        }
    
    // --- CUSTOM DESIGNED TEXTBOX ---
    public class CustomTextBox : UserControl
    {
        private TextBox innerTextBox;
        private string placeholderText;
        private Color placeholderColor;
        private Color normalBorderColor;
        private Color focusedBorderColor;
        private bool isFocused;

        public event KeyEventHandler InnerKeyDown;
        public event EventHandler InnerTextChanged;

        public string PlaceholderText { get { return placeholderText; } set { placeholderText = value; Invalidate(); } }
        public override string Text { get { return innerTextBox.Text; } set { innerTextBox.Text = value; Invalidate(); } }

        public CustomTextBox()
        {
            this.DoubleBuffered = true;
            this.Padding = new Padding(35, 10, 10, 10);
            this.BackColor = Color.FromArgb(0x1F, 0x20, 0x23);
            this.placeholderText = "";
            this.placeholderColor = Color.FromArgb(0x6F, 0x70, 0x75);
            this.normalBorderColor = Color.FromArgb(0x2E, 0x30, 0x35);
            this.focusedBorderColor = Color.FromArgb(0x35, 0xA2, 0xEB);

            innerTextBox = new TextBox();
            innerTextBox.BorderStyle = BorderStyle.None;
            innerTextBox.BackColor = Color.FromArgb(0x1F, 0x20, 0x23);
            innerTextBox.ForeColor = Color.White;
            innerTextBox.Font = new Font("Segoe UI", 11F);
            innerTextBox.Location = new Point(35, 11);
            innerTextBox.Width = this.Width - 45;

            innerTextBox.GotFocus += (s, e) => { isFocused = true; Invalidate(); };
            innerTextBox.LostFocus += (s, e) => { isFocused = false; Invalidate(); };
            innerTextBox.KeyDown += (s, e) => { if (InnerKeyDown != null) InnerKeyDown(s, e); };
            innerTextBox.TextChanged += (s, e) => { if (InnerTextChanged != null) InnerTextChanged(s, e); Invalidate(); };

            this.Controls.Add(innerTextBox);
            this.SizeChanged += CustomTextBox_SizeChanged;
        }

        private void CustomTextBox_SizeChanged(object sender, EventArgs e)
        {
            innerTextBox.Width = this.Width - 45;
            innerTextBox.Location = new Point(35, (this.Height - innerTextBox.Height) / 2);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;

            // Fill background
            using (Brush bgBrush = new SolidBrush(this.BackColor))
            {
                UIHelpers.FillRoundedRectangle(g, bgBrush, new Rectangle(0, 0, Width - 1, Height - 1), 6);
            }

            // Draw border
            Color currentBorder = isFocused ? focusedBorderColor : normalBorderColor;
            using (Pen borderPen = new Pen(currentBorder, 1.5f))
            {
                UIHelpers.DrawRoundedRectangle(g, borderPen, new Rectangle(0, 0, Width - 1, Height - 1), 6);
            }

            // Draw Search Icon
            int centerY = Height / 2;
            using (Pen iconPen = new Pen(Color.FromArgb(0x7E, 0x80, 0x87), 2f))
            {
                g.DrawEllipse(iconPen, 12, centerY - 7, 10, 10);
                g.DrawLine(iconPen, 20, centerY + 1, 24, centerY + 5);
            }

            // Draw Placeholder
            if (string.IsNullOrEmpty(innerTextBox.Text) && !isFocused)
            {
                using (Font pFont = new Font("Segoe UI", 10.5F, FontStyle.Italic))
                using (Brush pBrush = new SolidBrush(placeholderColor))
                {
                    g.DrawString(placeholderText, pFont, pBrush, new PointF(33, (Height - g.MeasureString(placeholderText, pFont).Height) / 2));
                }
            }
        }
    }

    // --- CUSTOM TOGGLE SWITCH ---
    public class ToggleSwitch : Control
    {
        private bool @checked;
        public event EventHandler CheckedChanged;

        public bool Checked
        {
            get { return @checked; }
            set { @checked = value; Invalidate(); if (CheckedChanged != null) CheckedChanged(this, EventArgs.Empty); }
        }

        public ToggleSwitch()
        {
            SetStyle(ControlStyles.SupportsTransparentBackColor, true);
            this.DoubleBuffered = true;
            this.Size = new Size(46, 24);
            this.@checked = false;
            this.Cursor = Cursors.Hand;
            this.BackColor = Color.Transparent;
        }

        protected override void OnMouseClick(MouseEventArgs e)
        {
            base.OnMouseClick(e);
            Checked = !Checked;
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;

            Rectangle rect = new Rectangle(0, 0, Width - 1, Height - 1);
            int radius = Height / 2;

            if (Checked)
            {
                using (Brush bgBrush = new SolidBrush(Color.FromArgb(0x35, 0xA2, 0xEB)))
                {
                    UIHelpers.FillRoundedRectangle(g, bgBrush, rect, radius);
                }
                using (Brush knobBrush = new SolidBrush(Color.White))
                {
                    g.FillEllipse(knobBrush, Width - Height + 2, 2, Height - 5, Height - 5);
                }
            }
            else
            {
                using (Brush bgBrush = new SolidBrush(Color.FromArgb(0x2B, 0x2D, 0x31)))
                {
                    UIHelpers.FillRoundedRectangle(g, bgBrush, rect, radius);
                }
                using (Pen borderPen = new Pen(Color.FromArgb(0x3F, 0x41, 0x47), 1.5f))
                {
                    UIHelpers.DrawRoundedRectangle(g, borderPen, rect, radius);
                }
                using (Brush knobBrush = new SolidBrush(Color.FromArgb(0x7E, 0x80, 0x87)))
                {
                    g.FillEllipse(knobBrush, 3, 3, Height - 7, Height - 7);
                }
            }
        }
    }

    // --- CUSTOM DESIGNED BUTTON ---
    public enum ButtonIconType
    {
        None,
        Wand,
        Printer,
        Save,
        Settings
    }

    public class CustomButton : Control
    {
        private Color normalColor;
        private Color hoverColor;
        private Color pressedColor;
        private Color textColor;
        private Color borderColor;
        private ButtonIconType iconType;
        private bool isHovered;
        private bool isPressed;

        public ButtonIconType IconType { get { return iconType; } set { iconType = value; Invalidate(); } }
        public Color NormalColor { get { return normalColor; } set { normalColor = value; Invalidate(); } }
        public Color HoverColor { get { return hoverColor; } set { hoverColor = value; Invalidate(); } }
        public Color BorderColor { get { return borderColor; } set { borderColor = value; Invalidate(); } }

        public CustomButton()
        {
            this.DoubleBuffered = true;
            this.normalColor = Color.FromArgb(0x2B, 0x2D, 0x31);
            this.hoverColor = Color.FromArgb(0x3E, 0x40, 0x46);
            this.pressedColor = Color.FromArgb(0x1F, 0x20, 0x23);
            this.textColor = Color.White;
            this.borderColor = Color.FromArgb(0x3F, 0x41, 0x47);
            this.iconType = ButtonIconType.None;
            this.Cursor = Cursors.Hand;
            this.BackColor = Color.FromArgb(0x18, 0x18, 0x1A);
        }

        protected override void OnMouseEnter(EventArgs e) { isHovered = true; Invalidate(); base.OnMouseEnter(e); }
        protected override void OnMouseLeave(EventArgs e) { isHovered = false; isPressed = false; Invalidate(); base.OnMouseLeave(e); }
        protected override void OnMouseDown(MouseEventArgs e) { isPressed = true; Invalidate(); base.OnMouseDown(e); }
        protected override void OnMouseUp(MouseEventArgs e) { isPressed = false; Invalidate(); base.OnMouseUp(e); }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;

            Color currentBg = isPressed ? pressedColor : (isHovered ? hoverColor : normalColor);
            Rectangle rect = new Rectangle(0, 0, Width - 1, Height - 1);

            // Fill
            using (Brush bgBrush = new SolidBrush(currentBg))
            {
                UIHelpers.FillRoundedRectangle(g, bgBrush, rect, 6);
            }

            // Border
            if (borderColor != Color.Transparent)
            {
                using (Pen borderPen = new Pen(borderColor, 1.2f))
                {
                    UIHelpers.DrawRoundedRectangle(g, borderPen, rect, 6);
                }
            }

            // Draw Icon + Text
            int iconSize = 16;
            int spacing = 6;
            SizeF textSize = g.MeasureString(this.Text, this.Font);
            float totalWidth = textSize.Width + (iconType != ButtonIconType.None ? (iconSize + spacing) : 0);

            float startX = (Width - totalWidth) / 2f;
            float centerY = Height / 2f;

            if (iconType != ButtonIconType.None)
            {
                Rectangle iconRect = new Rectangle((int)startX, (int)(centerY - iconSize / 2f), iconSize, iconSize);
                DrawIcon(g, iconRect, textColor);
                startX += iconSize + spacing;
            }

            using (Brush textBrush = new SolidBrush(textColor))
            {
                g.DrawString(this.Text, this.Font, textBrush, startX, centerY - textSize.Height / 2f);
            }
        }

        private void DrawIcon(Graphics g, Rectangle r, Color color)
        {
            using (Pen p = new Pen(color, 2f))
            {
                if (iconType == ButtonIconType.Wand)
                {
                    g.DrawLine(p, r.Left + 2, r.Bottom - 2, r.Right - 5, r.Top + 5);
                    using (Brush b = new SolidBrush(color))
                    {
                        g.FillEllipse(b, r.Right - 4, r.Top + 1, 3, 3);
                        g.FillEllipse(b, r.Right - 1, r.Top + 5, 2, 2);
                        g.FillEllipse(b, r.Left + 7, r.Top + 3, 2, 2);
                    }
                }
                else if (iconType == ButtonIconType.Printer)
                {
                    g.DrawRectangle(p, r.Left + 2, r.Top + 5, r.Width - 4, r.Height - 10);
                    g.DrawLine(p, r.Left + 4, r.Top + 5, r.Left + 4, r.Top + 2);
                    g.DrawLine(p, r.Right - 4, r.Top + 5, r.Right - 4, r.Top + 2);
                    g.DrawLine(p, r.Left + 4, r.Top + 2, r.Right - 4, r.Top + 2);
                    g.DrawLine(p, r.Left + 4, r.Bottom - 5, r.Right - 4, r.Bottom - 5);
                }
                else if (iconType == ButtonIconType.Save)
                {
                    g.DrawRectangle(p, r.Left + 2, r.Top + 2, r.Width - 4, r.Height - 4);
                    using (Brush b = new SolidBrush(color))
                    {
                        g.FillRectangle(b, r.Left + 5, r.Top + 2, r.Width - 10, 4);
                    }
                    g.DrawRectangle(p, r.Left + 5, r.Bottom - 6, r.Width - 10, 4);
                }
                else if (iconType == ButtonIconType.Settings)
                {
                    g.DrawEllipse(p, r.Left + 4, r.Top + 4, r.Width - 8, r.Height - 8);
                    g.DrawEllipse(p, r.Left + 7, r.Top + 7, r.Width - 14, r.Height - 14);
                }
            }
        }
    }

    // --- CUSTOM PREVIEW PANEL WITH PLACEHOLDER ---
    public class PreviewPanel : Control
    {
        private Image image;
        private Color placeholderColor;

        public Image Image { get { return image; } set { image = value; Invalidate(); } }

        public PreviewPanel()
        {
            this.DoubleBuffered = true;
            this.BackColor = Color.FromArgb(0x1F, 0x20, 0x23);
            this.placeholderColor = Color.FromArgb(0x5F, 0x61, 0x65);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;

            Rectangle rect = new Rectangle(0, 0, Width - 1, Height - 1);

            // Fill Background
            using (Brush bgBrush = new SolidBrush(this.BackColor))
            {
                UIHelpers.FillRoundedRectangle(g, bgBrush, rect, 8);
            }

            // Draw Border
            using (Pen borderPen = new Pen(Color.FromArgb(0x2E, 0x30, 0x35), 1.5f))
            {
                UIHelpers.DrawRoundedRectangle(g, borderPen, rect, 8);
            }

            if (Image == null)
            {
                // Draw Placeholder Icon
                Rectangle iconRect = new Rectangle(Width / 2 - 30, Height / 2 - 45, 60, 45);
                UIHelpers.DrawImagePlaceholder(g, iconRect, placeholderColor);

                // Draw Text
                string placeholderText = "Generated content will appear here...";
                using (Font pFont = new Font("Segoe UI", 11F))
                using (Brush pBrush = new SolidBrush(placeholderColor))
                {
                    SizeF size = g.MeasureString(placeholderText, pFont);
                    g.DrawString(placeholderText, pFont, pBrush, (Width - size.Width) / 2f, Height / 2f + 15f);
                }
            }
            else
            {
                // Draw Image centered with proportions
                float ratioX = (float)Width / Image.Width;
                float ratioY = (float)Height / Image.Height;
                float ratio = Math.Min(ratioX, ratioY) * 0.9f; // Leave small margin

                int imgWidth = (int)(Image.Width * ratio);
                int imgHeight = (int)(Image.Height * ratio);

                int imgX = (Width - imgWidth) / 2;
                int imgY = (Height - imgHeight) / 2;

                g.InterpolationMode = InterpolationMode.HighQualityBicubic;
                g.DrawImage(Image, new Rectangle(imgX, imgY, imgWidth, imgHeight));
            }

            // Draw Decorative Star at the bottom-right corner
            Rectangle starRect = new Rectangle(Width - 35, Height - 35, 18, 18);
            UIHelpers.DrawStar(g, starRect, Color.FromArgb(0x3F, 0x41, 0x47));
        }
    }

    // --- CUSTOM LOG CONSOLE (OwnerDraw ListBox) ---
    public class LogConsole : ListBox
    {
        private List<LogEntry> logs;

        public List<LogEntry> Logs
        {
            get { return logs; }
            set
            {
                logs = value;
                Items.Clear();
                if (logs != null)
                {
                    foreach (var e in logs) Items.Add(e);
                }
            }
        }

        public LogConsole()
        {
            this.SetStyle(ControlStyles.ResizeRedraw, true);
            this.DoubleBuffered = true;
            this.BackColor = Color.FromArgb(0x0A, 0x0B, 0x0D);
            this.ForeColor = Color.FromArgb(0xD0, 0xD2, 0xD6);
            this.logs = new List<LogEntry>();
            this.DrawMode = DrawMode.OwnerDrawFixed;
            this.ItemHeight = 24;
            this.BorderStyle = BorderStyle.None;
            this.IntegralHeight = false;
        }

        public void ScrollToBottom()
        {
            if (Items.Count > 0) TopIndex = Items.Count - 1;
        }

        protected override void OnDrawItem(DrawItemEventArgs e)
        {
            if (e.Index < 0 || e.Index >= Items.Count) return;
            LogEntry entry = Items[e.Index] as LogEntry;
            if (entry == null) return;

            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;

            // Selection background
            if ((e.State & DrawItemState.Selected) != 0)
            {
                using (var b = new SolidBrush(Color.FromArgb(0x1F, 0x45, 0x7A)))
                    g.FillRectangle(b, e.Bounds);
            }
            else
            {
                using (var b = new SolidBrush(Color.FromArgb(0x0A, 0x0B, 0x0D)))
                    g.FillRectangle(b, e.Bounds);
            }

            using (Font font = new Font("Segoe UI Semibold", 10.5F))
            {
                string ts = string.Format("[{0:yyyy-MM-dd HH:mm:ss.fff}] ", entry.Timestamp);
                string lvl = string.Format("[{0}] ", entry.Level);
                Color lvlColor = Color.FromArgb(0x35, 0xA2, 0xEB);
                if (entry.Level == "WARN") lvlColor = Color.FromArgb(0xE2, 0xC0, 0x44);
                else if (entry.Level == "ERROR") lvlColor = Color.FromArgb(0xE0, 0x52, 0x52);
                else if (entry.Level == "LOAD" || entry.Level == "PREV") lvlColor = Color.FromArgb(0x4E, 0xC9, 0xB0);

                float tsW = g.MeasureString(ts, font).Width;
                float lvlW = g.MeasureString(lvl, font).Width;
                int y = e.Bounds.Y + 2;

                using (Brush b = new SolidBrush(Color.FromArgb(0x7E, 0x80, 0x87)))
                    g.DrawString(ts, font, b, 8, y);
                using (Brush b = new SolidBrush(lvlColor))
                    g.DrawString(lvl, font, b, 8 + tsW, y);
                using (Brush b = new SolidBrush(Color.FromArgb(0xD0, 0xD2, 0xD6)))
                    g.DrawString(entry.Message, font, b, 8 + tsW + lvlW, y);
            }

            e.DrawFocusRectangle();
        }
    }

    // --- MAIN FORM CLASS ---
    public class MainForm : Form
    {
        private Panel titleBar;
        private Label lblTitle;
        private CustomButton btnMin;
        private CustomButton btnMax;
        private CustomButton btnClose;

        // Custom tabs
        private Panel tabContainer;
        private Label btnTabGenerate;
        private Label btnTabLog;
        private bool isGenerateTabActive;

        // Generator Panel Controls
        private Panel panelGenerate;
        private CustomTextBox txtDataInput;
        private ToggleSwitch toggleAuto;
        private Label lblAuto;
        private CustomButton btnGenerate;
        private PreviewPanel pbPreview;
        private CustomButton btnPrint;
        private CustomButton btnSaveAs;
        private CustomButton btnSetPrinter;

        // Log Panel Controls
        private Panel panelLog;
        private CustomTextBox txtSearchLog;
        private LogConsole logConsole;

        // Logging state
        private List<LogEntry> sessionLogs;
        private string currentLogFile;
        private string selectedPrinter;
        private Rectangle normBounds;
        private int resizeDir;
        private Point resizeStart;
        private Rectangle formStart;

        public MainForm()
        {
            this.FormBorderStyle = FormBorderStyle.None;
            this.Size = new Size(950, 650);
            this.MinimumSize = new Size(700, 500);
            this.BackColor = Color.FromArgb(0x18, 0x18, 0x1A);
            this.StartPosition = FormStartPosition.CenterScreen;
            this.DoubleBuffered = true;
            this.SetStyle(ControlStyles.ResizeRedraw, true);

            // Load app icon from embedded resources
            try
            {
                var asm = Assembly.GetExecutingAssembly();
                using (var s = asm.GetManifestResourceStream("DataMatrixGen.app.png"))
                {
                    if (s != null)
                    {
                        using (var bmp = new Bitmap(s))
                        {
                            IntPtr hIcon = bmp.GetHicon();
                            using (var temp = Icon.FromHandle(hIcon))
                            {
                                this.Icon = (Icon)temp.Clone();
                            }
                        }
                    }
                }
            }
            catch { }

            sessionLogs = new List<LogEntry>();
            isGenerateTabActive = true;
            selectedPrinter = "";

            string baseDir = AppDomain.CurrentDomain.BaseDirectory;
            Directory.CreateDirectory(Path.Combine(baseDir, "data"));
            Directory.CreateDirectory(Path.Combine(baseDir, "Log"));
            currentLogFile = Path.Combine(baseDir, "Log", DateTime.Now.ToString("yyyy-MM-dd") + ".log");

            // Load today's log
            if (File.Exists(currentLogFile))
            {
                foreach (string line in File.ReadAllLines(currentLogFile))
                {
                    if (!string.IsNullOrEmpty(line))
                    {
                        sessionLogs.Add(new LogEntry(DateTime.Now, "PREV", line));
                    }
                }
            }

            InitializeWindowControls();
            InitializeGeneratorTab();
            InitializeLogTab();
            PerformCustomLayout();
            HookChildMouseEvents(this);
            this.KeyPreview = true;
            this.KeyDown += (s, e) => {
                if (e.KeyCode == Keys.Enter && txtDataInput.Focused && toggleAuto.Checked)
                { ExecuteGeneration(); e.Handled = true; e.SuppressKeyPress = true; }
            };

            Log("INFO", "Start:DatamatrixGen");
        }

        // --- Dragging ---
        [DllImport("user32.dll")]
        private static extern int SendMessage(IntPtr hWnd, int Msg, int wParam, int lParam);
        [DllImport("user32.dll")]
        private static extern bool ReleaseCapture();

        // --- Manual Resize ---
        private const int R_NONE = 0, R_LEFT = 1, R_RIGHT = 2, R_TOP = 3, R_BOTTOM = 4;
        private const int R_TL = 5, R_TR = 6, R_BL = 7, R_BR = 8;
        private const int GRIP = 4;

        private int HitTest(Point p)
        {
            bool l = p.X < GRIP, r = p.X > Width - GRIP;
            bool t = p.Y < GRIP, b = p.Y > Height - GRIP;
            if (t && l) return R_TL;
            if (t && r) return R_TR;
            if (b && l) return R_BL;
            if (b && r) return R_BR;
            if (l) return R_LEFT;
            if (r) return R_RIGHT;
            if (t) return R_TOP;
            if (b) return R_BOTTOM;
            return R_NONE;
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);
            if (e.Button == MouseButtons.Left)
            {
                resizeDir = HitTest(e.Location);
                if (resizeDir != R_NONE)
                {
                    resizeStart = e.Location;
                    formStart = Bounds;
                }
            }
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            if (resizeDir != R_NONE)
            {
                int dx = e.X - resizeStart.X;
                int dy = e.Y - resizeStart.Y;
                int x = formStart.X, y = formStart.Y, w = formStart.Width, h = formStart.Height;

                switch (resizeDir)
                {
                    case R_LEFT: x = formStart.X + dx; w = formStart.Width - dx; break;
                    case R_RIGHT: w = formStart.Width + dx; break;
                    case R_TOP: y = formStart.Y + dy; h = formStart.Height - dy; break;
                    case R_BOTTOM: h = formStart.Height + dy; break;
                    case R_TL: x = formStart.X + dx; w = formStart.Width - dx; y = formStart.Y + dy; h = formStart.Height - dy; break;
                    case R_TR: w = formStart.Width + dx; y = formStart.Y + dy; h = formStart.Height - dy; break;
                    case R_BL: x = formStart.X + dx; w = formStart.Width - dx; h = formStart.Height + dy; break;
                    case R_BR: w = formStart.Width + dx; h = formStart.Height + dy; break;
                }

                if (w < MinimumSize.Width) { w = MinimumSize.Width; if (x != formStart.X) x = formStart.Right - w; }
                if (h < MinimumSize.Height) { h = MinimumSize.Height; if (y != formStart.Y) y = formStart.Bottom - h; }
                Bounds = new Rectangle(x, y, w, h);
            }
            else
            {
                int ht = HitTest(e.Location);
                if (ht == R_LEFT || ht == R_RIGHT) Cursor = Cursors.SizeWE;
                else if (ht == R_TOP || ht == R_BOTTOM) Cursor = Cursors.SizeNS;
                else if (ht == R_TL || ht == R_BR) Cursor = Cursors.SizeNWSE;
                else if (ht == R_TR || ht == R_BL) Cursor = Cursors.SizeNESW;
                else Cursor = Cursors.Default;
            }
        }

        protected override void OnMouseUp(MouseEventArgs e)
        {
            base.OnMouseUp(e);
            resizeDir = R_NONE;
            Cursor = Cursors.Default;
        }

        private void HookChildMouseEvents(Control parent)
        {
            foreach (Control c in parent.Controls)
            {
                if (c == titleBar) continue;
                c.MouseDown += (s, e) => {
                    if (e.Button == MouseButtons.Left) OnMouseDown(new MouseEventArgs(e.Button, e.Clicks, e.X + c.Left, e.Y + c.Top, e.Delta));
                };
                c.MouseMove += (s, e) => OnMouseMove(new MouseEventArgs(e.Button, e.Clicks, e.X + c.Left, e.Y + c.Top, e.Delta));
                c.MouseUp += (s, e) => OnMouseUp(e);
                if (c.HasChildren) HookChildMouseEvents(c);
            }
        }

        private void InitializeWindowControls()
        {
            // Custom Title Bar
            titleBar = new Panel { BackColor = Color.FromArgb(0x1F, 0x1F, 0x22), Height = 45 };
            titleBar.MouseDown += (s, e) => {
                if (e.Button == MouseButtons.Left) {
                    ReleaseCapture();
                    SendMessage(Handle, 0xA1, 2, 0);
                }
            };

            lblTitle = new Label {
                Text = "DatamatrixGen",
                ForeColor = Color.White,
                Font = new Font("Segoe UI Semibold", 10.5F),
                Location = new Point(52, 12),
                AutoSize = true
            };
            titleBar.Controls.Add(lblTitle);

            // Top-left icon drawing in title bar
            titleBar.Paint += (s, e) => {
                e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;

                // Draw app icon
                if (Icon != null)
                {
                    try
                    {
                        int iconSize = 20;
                        using (var iconBmp = Icon.ToBitmap())
                        {
                            int iconY = (titleBar.Height - iconSize) / 2;
                            e.Graphics.DrawImage(iconBmp, 16, iconY, iconSize, iconSize);
                        }
                    }
                    catch { }
                }
            };

            // Minimize/Close buttons
            Color tbBg = Color.FromArgb(0x1F, 0x1F, 0x22);
            btnMin = new CustomButton { Text = "—", Width = 45, Height = 45, BorderColor = Color.Transparent, NormalColor = tbBg };
            btnMin.Click += (s, e) => this.WindowState = FormWindowState.Minimized;

            btnMax = new CustomButton { Text = "☐", Width = 45, Height = 45, BorderColor = Color.Transparent, NormalColor = tbBg };
            btnMax.Click += (s, e) => {
                if (WindowState == FormWindowState.Normal)
                {
                    normBounds = Bounds;
                    Bounds = Screen.FromControl(this).WorkingArea;
                    WindowState = FormWindowState.Maximized;
                }
                else
                {
                    WindowState = FormWindowState.Normal;
                    Bounds = normBounds;
                }
            };

            btnClose = new CustomButton { Text = "✕", Width = 45, Height = 45, BorderColor = Color.Transparent, NormalColor = tbBg };
            btnClose.HoverColor = Color.FromArgb(0xE8, 0x11, 0x23);
            btnClose.Click += (s, e) => Application.Exit();

            titleBar.Controls.Add(btnMin);
            titleBar.Controls.Add(btnMax);
            titleBar.Controls.Add(btnClose);
            this.Controls.Add(titleBar);

            // Tab bar container
            tabContainer = new Panel { BackColor = Color.FromArgb(0x18, 0x18, 0x1A), Height = 42 };

            Color tabActiveText = Color.FromArgb(0x35, 0xA2, 0xEB);
            Color tabInactiveText = Color.FromArgb(0x7E, 0x80, 0x87);
            Font tabFont = new Font("Segoe UI", 10.5F, FontStyle.Bold);

            btnTabGenerate = new Label {
                Text = "Generator",
                ForeColor = tabActiveText,
                Font = tabFont,
                TextAlign = ContentAlignment.MiddleCenter,
                Size = new Size(110, 42),
                Cursor = Cursors.Hand
            };
            btnTabGenerate.Click += (s, e) => SwitchTab(true);

            btnTabLog = new Label {
                Text = "Log",
                ForeColor = tabInactiveText,
                Font = tabFont,
                TextAlign = ContentAlignment.MiddleCenter,
                Size = new Size(80, 42),
                Cursor = Cursors.Hand
            };
            btnTabLog.Click += (s, e) => SwitchTab(false);

            tabContainer.Controls.Add(btnTabGenerate);
            tabContainer.Controls.Add(btnTabLog);

            // Tab underline Paint
            tabContainer.Paint += (s, e) => {
                Label active = isGenerateTabActive ? btnTabGenerate : btnTabLog;
                using (Pen p = new Pen(tabActiveText, 2.5f))
                {
                    int lx = active.Left + 16;
                    int rx = active.Right - 16;
                    e.Graphics.DrawLine(p, lx, tabContainer.Height - 2, rx, tabContainer.Height - 2);
                }
            };

            this.Controls.Add(tabContainer);
        }

        private void InitializeGeneratorTab()
        {
            panelGenerate = new Panel { BackColor = Color.Transparent };

            txtDataInput = new CustomTextBox {
                PlaceholderText = "Describe the image to generate... (e.g., A futuristic city skyline at night, with rain and neon lights)"
            };
            txtDataInput.Enter += (s, e) => {
                foreach (InputLanguage lang in InputLanguage.InstalledInputLanguages)
                    if (lang.Culture.Name.StartsWith("en"))
                    { InputLanguage.CurrentInputLanguage = lang; break; }
            };
            txtDataInput.InnerKeyDown += (s, e) => {
                if (e.KeyCode == Keys.Enter && toggleAuto.Checked)
                {
                    e.SuppressKeyPress = true;
                    ExecuteGeneration();
                }
            };

            toggleAuto = new ToggleSwitch();
            lblAuto = new Label {
                Text = "auto",
                ForeColor = Color.FromArgb(0x9E, 0x9E, 0x9E),
                Font = new Font("Segoe UI", 10.5F),
                AutoSize = true
            };

            btnGenerate = new CustomButton {
                Text = "Generate",
                IconType = ButtonIconType.Wand,
                NormalColor = Color.FromArgb(0x35, 0xA2, 0xEB),
                HoverColor = Color.FromArgb(0x4F, 0xB3, 0xE8),
                BorderColor = Color.Transparent,
                Font = new Font("Segoe UI Semibold", 10.5F)
            };
            btnGenerate.Click += (s, e) => ExecuteGeneration();

            pbPreview = new PreviewPanel();

            btnPrint = new CustomButton { Text = "Print", IconType = ButtonIconType.Printer, Font = new Font("Segoe UI Semibold", 9.5F) };
            btnPrint.Click += (s, e) => ExecuteDirectPrint();

            btnSaveAs = new CustomButton { Text = "Save as...", IconType = ButtonIconType.Save, Font = new Font("Segoe UI Semibold", 9.5F) };
            btnSaveAs.Click += (s, e) => ExecuteSaveAs();

            btnSetPrinter = new CustomButton { Text = "Set a printer", IconType = ButtonIconType.Settings, Font = new Font("Segoe UI Semibold", 9.5F) };
            btnSetPrinter.Click += (s, e) => ExecuteSetPrinter();

            panelGenerate.Controls.Add(txtDataInput);
            panelGenerate.Controls.Add(toggleAuto);
            panelGenerate.Controls.Add(lblAuto);
            panelGenerate.Controls.Add(btnGenerate);
            panelGenerate.Controls.Add(pbPreview);
            panelGenerate.Controls.Add(btnPrint);
            panelGenerate.Controls.Add(btnSaveAs);
            panelGenerate.Controls.Add(btnSetPrinter);

            this.Controls.Add(panelGenerate);
        }

        private void InitializeLogTab()
        {
            panelLog = new Panel { BackColor = Color.Transparent, Visible = false };

            txtSearchLog = new CustomTextBox {
                PlaceholderText = "Search logs (e.g., 'SDXL', 'error', 'finished')..."
            };
            txtSearchLog.InnerTextChanged += (s, e) => FilterLogs();

            logConsole = new LogConsole();

            panelLog.Controls.Add(txtSearchLog);
            panelLog.Controls.Add(logConsole);

            this.Controls.Add(panelLog);
        }

        private void SwitchTab(bool generateTab)
        {
            isGenerateTabActive = generateTab;
            Color active = Color.FromArgb(0x35, 0xA2, 0xEB);
            Color inactive = Color.FromArgb(0x7E, 0x80, 0x87);
            btnTabGenerate.ForeColor = isGenerateTabActive ? active : inactive;
            btnTabLog.ForeColor = !isGenerateTabActive ? active : inactive;
            btnTabGenerate.Invalidate();
            btnTabLog.Invalidate();
            panelGenerate.Visible = isGenerateTabActive;
            panelLog.Visible = !isGenerateTabActive;
            if (!isGenerateTabActive) FilterLogs();
            tabContainer.Invalidate();
        }

        private void PerformCustomLayout()
        {
            int w = ClientSize.Width;
            int h = ClientSize.Height;

            // Title bar
            titleBar.Location = new Point(0, 0);
            titleBar.Width = w;
            titleBar.Invalidate();
            btnMin.Location = new Point(w - 135, 0);
            btnMax.Location = new Point(w - 90, 0);
            btnClose.Location = new Point(w - 45, 0);

            // Tab bar
            tabContainer.Location = new Point(0, 45);
            tabContainer.Width = w;
            btnTabGenerate.Location = new Point(24, 0);
            btnTabLog.Location = new Point(140, 0);

            // Content area
            int contentY = 45 + tabContainer.Height;
            int contentH = h - contentY;
            int mx = 24;
            int cw = w - mx * 2;

            // Generate Panel
            panelGenerate.Location = new Point(0, contentY);
            panelGenerate.Size = new Size(w, contentH);

            // Text input
            txtDataInput.Location = new Point(mx, 20);
            txtDataInput.Size = new Size(cw, 42);

            // Toggle + Generate button row
            int rowY = 78;
            toggleAuto.Location = new Point(mx, rowY);
            lblAuto.Location = new Point(mx + 52, rowY + 1);
            btnGenerate.Location = new Point(cw - 180 + mx, rowY - 5);
            btnGenerate.Size = new Size(180, 40);

            // Preview
            int previewY = rowY + 44;
            int footerH = 52;
            int previewH = contentH - previewY - footerH - 8;
            if (previewH < 100) previewH = 100;
            pbPreview.Location = new Point(mx, previewY);
            pbPreview.Size = new Size(cw, previewH);

            // Footer buttons
            int btnY = previewY + previewH + 8;
            int btnW = 140;
            int gap = 15;
            int totalW = btnW * 3 + gap * 2;
            int startX = (w - totalW) / 2;
            btnPrint.Location = new Point(startX, btnY);
            btnPrint.Size = new Size(btnW, 36);
            btnSaveAs.Location = new Point(startX + btnW + gap, btnY);
            btnSaveAs.Size = new Size(btnW, 36);
            btnSetPrinter.Location = new Point(startX + (btnW + gap) * 2, btnY);
            btnSetPrinter.Size = new Size(btnW, 36);

            // Log Panel
            panelLog.Location = new Point(0, contentY);
            panelLog.Size = new Size(w, contentH);
            txtSearchLog.Location = new Point(mx, 20);
            txtSearchLog.Size = new Size(cw, 42);
            logConsole.Location = new Point(mx, 78);
            logConsole.Size = new Size(cw, contentH - 100);
        }

        protected override void OnResize(EventArgs e)
        {
            base.OnResize(e);
            if (titleBar != null) PerformCustomLayout();
        }

        // --- BUSINESS LOGIC ---

        private void Log(string level, string message)
        {
            LogEntry entry = new LogEntry(DateTime.Now, level, message);
            sessionLogs.Add(entry);

            // Write to disk
            try
            {
                string logLine = string.Format("[{0:yyyy-MM-dd HH:mm:ss.fff}] [{1}] {2}\r\n", entry.Timestamp, entry.Level, entry.Message);
                File.AppendAllText(currentLogFile, logLine);
            }
            catch (Exception ex)
            {
                MessageBox.Show("Ошибка записи лога: " + ex.Message);
            }

            if (!isGenerateTabActive)
            {
                FilterLogs();
            }
        }

        private void FilterLogs()
        {
            string search = txtSearchLog.Text.Trim();
            if (string.IsNullOrEmpty(search))
            {
                logConsole.Logs = new List<LogEntry>(sessionLogs);
                logConsole.ScrollToBottom();
                return;
            }

            // Search files inside Log/ folder
            List<LogEntry> searchResults = new List<LogEntry>();
            try
            {
                string baseDir = AppDomain.CurrentDomain.BaseDirectory;
                string logDir = Path.Combine(baseDir, "Log");
                string[] logFiles = Directory.GetFiles(logDir, "*.log");

                foreach (string file in logFiles)
                {
                    string[] lines = File.ReadAllLines(file);
                    foreach (string line in lines)
                    {
                        if (line.IndexOf(search, StringComparison.OrdinalIgnoreCase) >= 0)
                        {
                            searchResults.Add(ParseLogLine(line));
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Log("ERROR", "Failed to search logs. Reason: " + ex.Message + "\r\nStack trace:\r\n" + ex.StackTrace);
            }

            logConsole.Logs = searchResults;
            logConsole.ScrollToBottom();
        }

        private LogEntry ParseLogLine(string line)
        {
            try
            {
                int firstBracketClose = line.IndexOf(']');
                if (firstBracketClose > 1)
                {
                    string tsStr = line.Substring(1, firstBracketClose - 1);
                    DateTime ts;
                    if (DateTime.TryParseExact(tsStr, "yyyy-MM-dd HH:mm:ss.fff", null, System.Globalization.DateTimeStyles.None, out ts))
                    {
                        int secondBracketOpen = line.IndexOf('[', firstBracketClose);
                        int secondBracketClose = line.IndexOf(']', firstBracketClose + 1);
                        if (secondBracketOpen > -1 && secondBracketClose > secondBracketOpen)
                        {
                            string level = line.Substring(secondBracketOpen + 1, secondBracketClose - secondBracketOpen - 1);
                            string msg = line.Substring(secondBracketClose + 1).Trim();
                            return new LogEntry(ts, level, msg);
                        }
                    }
                }
            }
            catch {}
            return new LogEntry(DateTime.Now, "INFO", line);
        }

        private string CleanFileName(string name)
        {
            foreach (char c in Path.GetInvalidFileNameChars())
            {
                name = name.Replace(c, '_');
            }
            return name;
        }

        private string ConvertToGS1(string input)
        {
            // Case 1: Input has brackets like (01)data(21)data(91)data(92)data
            if (input.IndexOf('(') >= 0)
            {
                var result = new System.Text.StringBuilder();
                int i = 0;
                while (i < input.Length)
                {
                    if (input[i] == '(' && i + 3 < input.Length && input[i + 3] == ')')
                    {
                        string ai = input.Substring(i + 1, 2);
                        if (ai == "91" || ai == "92" || ai == "93")
                            result.Append('\u001D');
                        result.Append(ai);
                        i += 4;
                    }
                    else
                    {
                        result.Append(input[i]);
                        i++;
                    }
                }
                return result.ToString();
            }

            // Case 2: Raw AI data without brackets, no GS chars yet
            if (input.IndexOf('\u001D') < 0)
            {
                // Insert \u001D before known tail AIs (91, 92, 93)
                var result = new System.Text.StringBuilder();
                int i = 0;
                while (i < input.Length)
                {
                    if (i + 1 < input.Length)
                    {
                        string pair = input.Substring(i, 2);
                        if ((pair == "91" || pair == "92" || pair == "93") && i > 0)
                        {
                            result.Append('\u001D');
                        }
                    }
                    result.Append(input[i]);
                    i++;
                }
                return result.ToString();
            }

            // Case 3: Already has \u001D — pass through
            return input;
        }

        private void ExecuteGeneration()
        {
            string inputText = txtDataInput.Text.Trim();
            if (string.IsNullOrEmpty(inputText))
            {
                MessageBox.Show("Введите данные для кодирования в поле ввода!", "Ошибка", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            try
            {
                // Check if data already has GS1 control characters
                bool hasGS1 = inputText.IndexOf('\u001D') >= 0;
                string encodeData;
                var opts = new EncodingOptions { Height = 300, Width = 300, Margin = 2 };

                if (hasGS1)
                {
                    // Already raw GS1 with separators — use as-is, no extra FNC1
                    encodeData = inputText;
                }
                else
                {
                    // Strip brackets, add GS separators between AIs
                    encodeData = ConvertToGS1(inputText);
                    opts.GS1Format = true;
                }

                var writer = new BarcodeWriter
                {
                    Format = BarcodeFormat.DATA_MATRIX,
                    Options = opts
                };

                using (var bitmap = writer.Write(encodeData))
                {
                    if (bitmap == null)
                    {
                        throw new Exception("BarcodeWriter returned null. ZXing encoding failed.");
                    }

                    // Display preview (clone to avoid locking file)
                    if (pbPreview.Image != null)
                    {
                        pbPreview.Image.Dispose();
                    }
                    pbPreview.Image = (Image)bitmap.Clone();

                    // Generate safe filename from last 10 characters
                    string cleanText = CleanFileName(inputText);
                    string last10 = cleanText.Length > 10 ? cleanText.Substring(cleanText.Length - 10) : cleanText;
                    if (string.IsNullOrEmpty(last10)) last10 = "datamatrix";

                    string baseDir = AppDomain.CurrentDomain.BaseDirectory;
                    string relativePath = Path.Combine("data", last10 + ".png");
                    string fullPath = Path.Combine(baseDir, relativePath);

                    bitmap.Save(fullPath, System.Drawing.Imaging.ImageFormat.Png);

                    Log("INFO", string.Format("Code: \"{0}\" | File: data\\{1}", inputText, last10 + ".png"));

                    if (toggleAuto.Checked)
                    {
                        Log("INFO", "Auto-mode triggered printing.");
                        PrintImageDirect(pbPreview.Image);
                        txtDataInput.Text = ""; // Clear textbox immediately
                        pbPreview.Focus();
                    }
                }
            }
            catch (Exception ex)
            {
                string errMsg = string.Format("Ошибка при генерации DataMatrix. Что произошло: {0}. Почему произошло: возможно, текст содержит недопустимые для DataMatrix символы или отсутствует папка для сохранения. Детали ошибки:\r\n{1}", ex.Message, ex.ToString());
                Log("ERROR", errMsg);
                MessageBox.Show(errMsg, "Ошибка генерации", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void ExecuteDirectPrint()
        {
            if (pbPreview.Image == null)
            {
                MessageBox.Show("Сначала сгенерируйте код!", "Печать невозможна", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }
            PrintImageDirect(pbPreview.Image);
        }

        private void PrintImageDirect(Image img)
        {
            try
            {
                PrintDocument pd = new PrintDocument();
                if (!string.IsNullOrEmpty(selectedPrinter))
                {
                    pd.PrinterSettings.PrinterName = selectedPrinter;
                }

                pd.PrintPage += (sender, args) => {
                    // Center and scale image keeping aspect ratio
                    float pageW = args.PageBounds.Width;
                    float pageH = args.PageBounds.Height;

                    float imgW = img.Width;
                    float imgH = img.Height;

                    float ratio = Math.Min(pageW / imgW, pageH / imgH) * 0.8f; // Margin

                    float drawW = imgW * ratio;
                    float drawH = imgH * ratio;

                    float x = (pageW - drawW) / 2f;
                    float y = (pageH - drawH) / 2f;

                    args.Graphics.InterpolationMode = InterpolationMode.HighQualityBicubic;
                    args.Graphics.DrawImage(img, x, y, drawW, drawH);
                };

                pd.Print();
                Log("INFO", "Sent print job to printer: " + pd.PrinterSettings.PrinterName);
            }
            catch (Exception ex)
            {
                string errMsg = string.Format("Ошибка при печати. Что произошло: {0}. Почему произошло: проверьте настройки принтера или доступность устройства. Детали:\r\n{1}", ex.Message, ex.ToString());
                Log("ERROR", errMsg);
                MessageBox.Show(errMsg, "Ошибка печати", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void ExecuteSetPrinter()
        {
            try
            {
                using (PrintDialog dlg = new PrintDialog())
                {
                    if (dlg.ShowDialog() == DialogResult.OK)
                    {
                        selectedPrinter = dlg.PrinterSettings.PrinterName;
                        Log("INFO", "Printer for this session set to: " + selectedPrinter);
                    }
                }
            }
            catch (Exception ex)
            {
                string errMsg = string.Format("Ошибка при вызове диалога выбора принтера. Что произошло: {0}. Детали:\r\n{1}", ex.Message, ex.ToString());
                Log("ERROR", errMsg);
                MessageBox.Show(errMsg, "Ошибка настроек", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void ExecuteSaveAs()
        {
            if (pbPreview.Image == null)
            {
                MessageBox.Show("Сначала сгенерируйте код!", "Сохранение невозможно", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            try
            {
                string inputText = txtDataInput.Text.Trim();
                string cleanText = CleanFileName(inputText);
                string last10 = cleanText.Length > 10 ? cleanText.Substring(cleanText.Length - 10) : cleanText;
                if (string.IsNullOrEmpty(last10)) last10 = "datamatrix";

                using (SaveFileDialog sfd = new SaveFileDialog())
                {
                    sfd.Filter = "PNG Image|*.png";
                    sfd.FileName = last10;
                    if (sfd.ShowDialog() == DialogResult.OK)
                    {
                        pbPreview.Image.Save(sfd.FileName, System.Drawing.Imaging.ImageFormat.Png);
                        Log("INFO", "Manual export. Saved to: " + sfd.FileName);
                    }
                }
            }
            catch (Exception ex)
            {
                string errMsg = string.Format("Ошибка при сохранении изображения. Что произошло: {0}. Детали:\r\n{1}", ex.Message, ex.ToString());
                Log("ERROR", errMsg);
                MessageBox.Show(errMsg, "Ошибка сохранения", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        [STAThread]
        static void Main()
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.Run(new MainForm());
        }
    }
}
