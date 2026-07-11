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
using System.Text;
using System.Net.Http;
using System.Threading.Tasks;
using ZXing;
using ZXing.Common;

namespace DataMatrixGenerator
{
    // Custom Text Element Class
    public class TextElement
    {
        public string Value { get; set; } // Default text or dynamic template like {product}
        public double X { get; set; } // in mm
        public double Y { get; set; } // in mm
        public double W { get; set; } // in mm
        public double H { get; set; } // in mm
        public string Font { get; set; } // "1" - "5"
        public int XMul { get; set; }
        public int YMul { get; set; }
        public bool IsExpanded { get; set; }

        public TextElement()
        {
            Value = "{product}";
            X = 2.5; // mm
            Y = 2.5; // mm
            W = 52.5; // mm
            H = 10.0; // mm
            Font = "3";
            XMul = 1;
            YMul = 1;
            IsExpanded = true;
        }
    }

    // Custom UI Wrapper for Text Elements in Sidebar
    public class TextElementUI
    {
        public TextElement Element { get; set; }
        public FlowLayoutPanel HeaderPanel { get; set; }
        public FlowLayoutPanel BodyPanel { get; set; }
        public Label HeaderLabel { get; set; }
        public Label ToggleLabel { get; set; }
        public TextBox ValueTextBox { get; set; }
        public TextBox XTextBox { get; set; }
        public TextBox YTextBox { get; set; }
        public TextBox WTextBox { get; set; }
        public TextBox HTextBox { get; set; }
        public ComboBox FontComboBox { get; set; }
    }

    // Custom Log Entry Class
    public class LogEntry
    {
        private DateTime timestamp;
        private string level;
        private string message;

        public DateTime Timestamp { get { return timestamp; } set { timestamp = value; } }
        public string Level { get { return level; } set { level = value; } }
        public string Message { get { return message; } set { message = value; } }
        public int Height { get; set; }

        public LogEntry() { Height = -1; }
        public LogEntry(DateTime ts, string lvl, string msg)
        {
            this.timestamp = ts;
            this.level = lvl;
            this.message = msg;
            this.Height = -1;
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
        public bool RoundButton { get; set; }

        public CustomButton()
        {
            this.SetStyle(ControlStyles.SupportsTransparentBackColor, true);
            this.DoubleBuffered = true;
            this.normalColor = Color.FromArgb(0x2B, 0x2D, 0x31);
            this.hoverColor = Color.FromArgb(0x3E, 0x40, 0x46);
            this.pressedColor = Color.FromArgb(0x1F, 0x20, 0x23);
            this.textColor = Color.White;
            this.borderColor = Color.FromArgb(0x3F, 0x41, 0x47);
            this.iconType = ButtonIconType.None;
            this.Cursor = Cursors.Hand;
            this.BackColor = Color.Transparent;
            this.RoundButton = false;
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

            // Fill and Border
            if (RoundButton)
            {
                using (Brush bgBrush = new SolidBrush(currentBg))
                {
                    g.FillEllipse(bgBrush, rect);
                }
                if (borderColor != Color.Transparent)
                {
                    using (Pen borderPen = new Pen(borderColor, 1.2f))
                    {
                        g.DrawEllipse(borderPen, rect);
                    }
                }
            }
            else
            {
                using (Brush bgBrush = new SolidBrush(currentBg))
                {
                    UIHelpers.FillRoundedRectangle(g, bgBrush, rect, 6);
                }
                if (borderColor != Color.Transparent)
                {
                    using (Pen borderPen = new Pen(borderColor, 1.2f))
                    {
                        UIHelpers.DrawRoundedRectangle(g, borderPen, rect, 6);
                    }
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
                if (RoundButton || iconType == ButtonIconType.None)
                {
                    // Perfectly center single text character/string in button bounds using StringFormat
                    using (StringFormat sf = new StringFormat())
                    {
                        sf.Alignment = StringAlignment.Center;
                        sf.LineAlignment = StringAlignment.Center;
                        g.DrawString(this.Text, this.Font, textBrush, new RectangleF(0, 0, Width, Height), sf);
                    }
                }
                else
                {
                    g.DrawString(this.Text, this.Font, textBrush, startX, centerY - textSize.Height / 2f);
                }
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
        
        // Cached GDI+ brushes and font to prevent scroll lag
        private Font logFont = new Font("Segoe UI Semibold", 10.5F);
        private Brush tsBrush = new SolidBrush(Color.FromArgb(0x7E, 0x80, 0x87));
        private Brush msgBrush = new SolidBrush(Color.FromArgb(0xD0, 0xD2, 0xD6));
        private Brush infoBrush = new SolidBrush(Color.FromArgb(0x35, 0xA2, 0xEB));
        private Brush warnBrush = new SolidBrush(Color.FromArgb(0xE2, 0xC0, 0x44));
        private Brush errorBrush = new SolidBrush(Color.FromArgb(0xE0, 0x52, 0x52));
        private Brush prevBrush = new SolidBrush(Color.FromArgb(0x4E, 0xC9, 0xB0));
        private Brush selBrush = new SolidBrush(Color.FromArgb(0x1F, 0x45, 0x7A));
        private Brush bgBrush = new SolidBrush(Color.FromArgb(0x0A, 0x0B, 0x0D));

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
            this.DrawMode = DrawMode.OwnerDrawVariable;
            this.ItemHeight = 24;
            this.BorderStyle = BorderStyle.None;
            this.IntegralHeight = false;
            this.SelectionMode = SelectionMode.MultiExtended;
            this.KeyDown += LogConsole_KeyDown;
            this.MeasureItem += LogConsole_MeasureItem;
        }

        protected override void OnSizeChanged(EventArgs e)
        {
            base.OnSizeChanged(e);
            if (Items.Count > 0)
            {
                foreach (var item in Items)
                {
                    LogEntry entry = item as LogEntry;
                    if (entry != null) entry.Height = -1; // Invalidate cached heights
                }
                this.BeginInvoke((MethodInvoker)delegate {
                    if (Items.Count > 0)
                    {
                        for (int i = 0; i < Items.Count; i++)
                        {
                            Items[i] = Items[i];
                        }
                    }
                });
            }
        }

        private void LogConsole_MeasureItem(object sender, MeasureItemEventArgs e)
        {
            if (e.Index < 0 || e.Index >= Items.Count) { e.ItemHeight = 24; return; }
            LogEntry entry = Items[e.Index] as LogEntry;
            if (entry == null) { e.ItemHeight = 24; return; }

            // Use pre-calculated/cached height to prevent lag
            if (entry.Height > 0)
            {
                e.ItemHeight = entry.Height;
                return;
            }

            using (Graphics g = CreateGraphics())
            {
                string ts = string.Format("[{0:yyyy-MM-dd HH:mm:ss.fff}] ", entry.Timestamp);
                string lvl = string.Format("[{0}] ", entry.Level);
                float tsW = g.MeasureString(ts, logFont).Width;
                float lvlW = g.MeasureString(lvl, logFont).Width;
                float maxW = Width - 16 - tsW - lvlW;
                if (maxW < 50) maxW = 50;
                SizeF sz = g.MeasureString(entry.Message, logFont, (int)maxW);
                entry.Height = Math.Max(24, (int)sz.Height + 4);
                e.ItemHeight = entry.Height;
            }
        }

        private void LogConsole_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Control && e.KeyCode == Keys.C)
            {
                var sb = new System.Text.StringBuilder();
                foreach (int i in SelectedIndices)
                {
                    if (i >= 0 && i < Items.Count)
                    {
                        LogEntry entry = Items[i] as LogEntry;
                        if (entry != null)
                            sb.AppendFormat("[{0:yyyy-MM-dd HH:mm:ss.fff}] [{1}] {2}\r\n", entry.Timestamp, entry.Level, entry.Message);
                    }
                }
                if (sb.Length > 0)
                    Clipboard.SetText(sb.ToString().TrimEnd());
                e.SuppressKeyPress = true;
                e.Handled = true;
            }
            else if (e.Control && e.KeyCode == Keys.A)
            {
                for (int i = 0; i < Items.Count; i++) SetSelected(i, true);
                e.SuppressKeyPress = true;
                e.Handled = true;
            }
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
                g.FillRectangle(selBrush, e.Bounds);
            }
            else
            {
                g.FillRectangle(bgBrush, e.Bounds);
            }

            string ts = string.Format("[{0:yyyy-MM-dd HH:mm:ss.fff}] ", entry.Timestamp);
            string lvl = string.Format("[{0}] ", entry.Level);
            Brush lvlBrush = infoBrush;
            if (entry.Level == "WARN") lvlBrush = warnBrush;
            else if (entry.Level == "ERROR") lvlBrush = errorBrush;
            else if (entry.Level == "LOAD" || entry.Level == "PREV") lvlBrush = prevBrush;

            float tsW = g.MeasureString(ts, logFont).Width;
            float lvlW = g.MeasureString(lvl, logFont).Width;
            int y = e.Bounds.Y + 2;

            g.DrawString(ts, logFont, tsBrush, 8, y);
            g.DrawString(lvl, logFont, lvlBrush, 8 + tsW, y);
            
            float maxW = Width - 16 - tsW - lvlW;
            if (maxW < 50) maxW = 50;
            RectangleF rc = new RectangleF(8 + tsW + lvlW, y, maxW, e.Bounds.Height - 2);
            g.DrawString(entry.Message, logFont, msgBrush, rc);

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
        private Label btnTabDesigner;
        private Label btnTabLog;
        private int activeTab = 0; // 0 = Generator, 1 = Designer, 2 = Log

        // Generator Panel Controls
        private Panel panelGenerate;
        private CustomTextBox txtDataInput;
        private ToggleSwitch toggleAuto;
        private Label lblAuto;
        private ToggleSwitch toggleApi;
        private Label lblApi;
        private CustomButton btnGenerate;
        private PreviewPanel pbPreview;
        private CustomButton btnPrint;
        private CustomButton btnSaveAs;
        private CustomButton btnSetPrinter;

        // Designer Panel Controls
        private Panel panelDesigner;
        private FlowLayoutPanel panelDesignerSidebar;
        private Panel panelDesignerCanvasContainer;
        private PictureBox pbDesignerCanvas;
        private ToggleSwitch toggleRotate180;
        
        // Sidebar controls
        private TextBox txtLabelW;
        private TextBox txtLabelH;
        private TextBox txtLabelGap;
        private TextBox txtLabelMargin;
        private FlowLayoutPanel panelTextElementsContainer;
        private List<TextElementUI> textElementsUI = new List<TextElementUI>();
        private TextBox txtBarX;
        private TextBox txtBarY;
        private TextBox txtBarW;
        private TextBox txtBarH;
        private CustomButton btnAddText;
        private CustomButton btnSaveTemplate;

        // Designer State
        private double designerLabelW = 58; // mm
        private double designerLabelH = 40; // mm
        private double designerGap = 2; // mm
        private double designerMarginLeft = 1.3; // mm
        private bool rotate180 = false;
        private bool configAuto = false;
        private bool configApi = false;
        
        // Barcode position in mm
        private double dmatrixX = 16.5; // mm (132/8)
        private double dmatrixY = 13.75; // mm (110/8)
        private double dmatrixW = 25.0; // mm (200/8)
        private double dmatrixH = 25.0; // mm (200/8)

        // Multiple text elements support
        private List<TextElement> textElements = new List<TextElement>();
        private int selectedElementIndex = -1; // -1 = Label, -2 = Barcode, 0+ = Text Element

        // Interaction state
        private bool isDraggingText = false;
        private bool isResizingText = false;
        private bool isDraggingBarcode = false;
        private bool isResizingBarcode = false;
        private Point dragStartPoint;
        private double originalX;
        private double originalY;
        private double originalW;
        private double originalH;
        private bool isUpdatingSidebar = false;

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
        private string productName = "";
        private Label lblProductInfo;
        private static readonly HttpClient httpClient = new HttpClient();
        private string lastEncodedData = "";
        private Image lastBarcodeImage = null;

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
            activeTab = 0;
            selectedPrinter = "";

            try
            {
                httpClient.DefaultRequestHeaders.Clear();
                httpClient.DefaultRequestHeaders.UserAgent.ParseAdd("Mozilla/5.0 (iPhone; CPU iPhone OS 15_0 like Mac OS X) AppleWebKit/605.1.15 (KHTML, like Gecko) Version/15.0 Mobile/15E148 Safari/604.1");
                httpClient.DefaultRequestHeaders.Accept.ParseAdd("application/json");
            }
            catch { }

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
                        var entry = ParseLogLine(line);
                        entry.Level = "PREV";
                        sessionLogs.Add(entry);
                    }
                }
            }

            LoadConfig();
            InitializeWindowControls();
            InitializeGeneratorTab();
            InitializeDesignerTab();
            InitializeLogTab();
            PerformCustomLayout();
            HookChildMouseEvents(this);
            this.KeyPreview = true;
            this.KeyDown += (s, e) => {
                if (e.KeyCode == Keys.Enter && txtDataInput.Focused && toggleAuto.Checked)
                { ExecuteGeneration(); e.Handled = true; e.SuppressKeyPress = true; }
                else if (e.KeyCode == Keys.Delete && activeTab == 1)
                {
                    if (this.ActiveControl is TextBox) return;
                    if (selectedElementIndex >= 0 && selectedElementIndex < textElements.Count)
                    {
                        textElements.RemoveAt(selectedElementIndex);
                        selectedElementIndex = -1;
                        UpdateSidebarProperties();
                        pbDesignerCanvas.Invalidate();
                        UpdatePreview();
                        SaveConfig();
                        e.Handled = true;
                    }
                }
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
                    if (e.Button == MouseButtons.Left)
                    {
                        Point clientPoint = this.PointToClient(c.PointToScreen(e.Location));
                        OnMouseDown(new MouseEventArgs(e.Button, e.Clicks, clientPoint.X, clientPoint.Y, e.Delta));
                    }
                };
                c.MouseMove += (s, e) => {
                    Point clientPoint = this.PointToClient(c.PointToScreen(e.Location));
                    OnMouseMove(new MouseEventArgs(e.Button, e.Clicks, clientPoint.X, clientPoint.Y, e.Delta));
                };
                c.MouseUp += (s, e) => OnMouseUp(e);
                if (c.HasChildren) HookChildMouseEvents(c);
            }
        }

        private void SaveConfig()
        {
            try
            {
                string baseDir = AppDomain.CurrentDomain.BaseDirectory;
                string configPath = Path.Combine(baseDir, "config.cfg");
                
                StringBuilder sb = new StringBuilder();
                sb.AppendFormat(System.Globalization.CultureInfo.InvariantCulture,
                    "Printer={0}\r\n" +
                    "Auto={1}\r\n" +
                    "API={2}\r\n" +
                    "Rotate180={3}\r\n" +
                    "LabelW={4:0.##}\r\n" +
                    "LabelH={5:0.##}\r\n" +
                    "LabelGap={6:0.##}\r\n" +
                    "LabelMarginLeft={7:0.##}\r\n" +
                    "BarX={8:0.##}\r\n" +
                    "BarY={9:0.##}\r\n" +
                    "BarW={10:0.##}\r\n" +
                    "BarH={11:0.##}\r\n",
                    selectedPrinter,
                    toggleAuto != null ? toggleAuto.Checked : configAuto,
                    toggleApi != null ? toggleApi.Checked : configApi,
                    rotate180,
                    designerLabelW,
                    designerLabelH,
                    designerGap,
                    designerMarginLeft,
                    dmatrixX,
                    dmatrixY,
                    dmatrixW,
                    dmatrixH);

                sb.AppendLine("TextCount=" + textElements.Count);
                for (int i = 0; i < textElements.Count; i++)
                {
                    var te = textElements[i];
                    sb.AppendFormat(System.Globalization.CultureInfo.InvariantCulture,
                        "Text{0}_Val={1}\r\n" +
                        "Text{0}_X={2:0.##}\r\n" +
                        "Text{0}_Y={3:0.##}\r\n" +
                        "Text{0}_W={4:0.##}\r\n" +
                        "Text{0}_H={5:0.##}\r\n" +
                        "Text{0}_Font={6}\r\n" +
                        "Text{0}_XMul={7}\r\n" +
                        "Text{0}_YMul={8}\r\n",
                        i, te.Value, te.X, te.Y, te.W, te.H, te.Font, te.XMul, te.YMul);
                }

                File.WriteAllText(configPath, sb.ToString(), Encoding.UTF8);
                Log("INFO", "Saved config.cfg successfully.");
            }
            catch (Exception ex)
            {
                Log("ERROR", "Failed to save config: " + ex.Message);
            }
        }

        private void LoadConfig()
        {
            try
            {
                string baseDir = AppDomain.CurrentDomain.BaseDirectory;
                string configPath = Path.Combine(baseDir, "config.cfg");
                if (!File.Exists(configPath))
                {
                    // Fallback to load settings from config.txt if it exists to ease migration
                    string oldConfig = Path.Combine(baseDir, "config.txt");
                    if (File.Exists(oldConfig))
                    {
                        File.Move(oldConfig, configPath);
                        Log("INFO", "Migrated config.txt to config.cfg");
                    }
                    else
                    {
                        // Add one default text element for fresh start
                        textElements.Clear();
                        textElements.Add(new TextElement { Value = "{product}" });
                        SaveConfig();
                        Log("INFO", "Created default config.cfg");
                        return;
                    }
                }

                textElements.Clear();
                string[] lines = File.ReadAllLines(configPath, Encoding.UTF8);
                foreach (string line in lines)
                {
                    int eqIdx = line.IndexOf('=');
                    if (eqIdx > 0)
                    {
                        string key = line.Substring(0, eqIdx).Trim();
                        string val = line.Substring(eqIdx + 1).Trim();

                        if (key.Equals("Printer", StringComparison.OrdinalIgnoreCase))
                        {
                            selectedPrinter = val;
                        }
                        else if (key.Equals("Auto", StringComparison.OrdinalIgnoreCase))
                        {
                            configAuto = bool.Parse(val);
                        }
                        else if (key.Equals("API", StringComparison.OrdinalIgnoreCase))
                        {
                            configApi = bool.Parse(val);
                        }
                        else if (key.Equals("Rotate180", StringComparison.OrdinalIgnoreCase))
                        {
                            rotate180 = bool.Parse(val);
                        }
                        else if (key.Equals("LabelW", StringComparison.OrdinalIgnoreCase))
                        {
                            designerLabelW = double.Parse(val, System.Globalization.CultureInfo.InvariantCulture);
                        }
                        else if (key.Equals("LabelH", StringComparison.OrdinalIgnoreCase))
                        {
                            designerLabelH = double.Parse(val, System.Globalization.CultureInfo.InvariantCulture);
                        }
                        else if (key.Equals("LabelGap", StringComparison.OrdinalIgnoreCase))
                        {
                            designerGap = double.Parse(val, System.Globalization.CultureInfo.InvariantCulture);
                        }
                        else if (key.Equals("LabelMarginLeft", StringComparison.OrdinalIgnoreCase))
                        {
                            designerMarginLeft = double.Parse(val, System.Globalization.CultureInfo.InvariantCulture);
                        }
                        else if (key.Equals("BarX", StringComparison.OrdinalIgnoreCase))
                        {
                            dmatrixX = double.Parse(val, System.Globalization.CultureInfo.InvariantCulture);
                        }
                        else if (key.Equals("BarY", StringComparison.OrdinalIgnoreCase))
                        {
                            dmatrixY = double.Parse(val, System.Globalization.CultureInfo.InvariantCulture);
                        }
                        else if (key.Equals("BarW", StringComparison.OrdinalIgnoreCase))
                        {
                            dmatrixW = double.Parse(val, System.Globalization.CultureInfo.InvariantCulture);
                        }
                        else if (key.Equals("BarH", StringComparison.OrdinalIgnoreCase))
                        {
                            dmatrixH = double.Parse(val, System.Globalization.CultureInfo.InvariantCulture);
                        }
                        else
                        {
                            var textMatch = Regex.Match(key, @"Text(\d+)_([A-Za-z]+)", RegexOptions.IgnoreCase);
                            if (textMatch.Success)
                            {
                                int index = int.Parse(textMatch.Groups[1].Value);
                                string prop = textMatch.Groups[2].Value;

                                while (textElements.Count <= index)
                                {
                                    textElements.Add(new TextElement());
                                }

                                var te = textElements[index];
                                if (prop.Equals("Val", StringComparison.OrdinalIgnoreCase)) te.Value = val;
                                else if (prop.Equals("X", StringComparison.OrdinalIgnoreCase)) te.X = double.Parse(val, System.Globalization.CultureInfo.InvariantCulture);
                                else if (prop.Equals("Y", StringComparison.OrdinalIgnoreCase)) te.Y = double.Parse(val, System.Globalization.CultureInfo.InvariantCulture);
                                else if (prop.Equals("W", StringComparison.OrdinalIgnoreCase)) te.W = double.Parse(val, System.Globalization.CultureInfo.InvariantCulture);
                                else if (prop.Equals("H", StringComparison.OrdinalIgnoreCase)) te.H = double.Parse(val, System.Globalization.CultureInfo.InvariantCulture);
                                else if (prop.Equals("Font", StringComparison.OrdinalIgnoreCase)) te.Font = val;
                                else if (prop.Equals("XMul", StringComparison.OrdinalIgnoreCase)) te.XMul = int.Parse(val);
                                else if (prop.Equals("YMul", StringComparison.OrdinalIgnoreCase)) te.YMul = int.Parse(val);
                            }
                        }
                    }
                }

                if (textElements.Count == 0)
                {
                    textElements.Add(new TextElement { Value = "{product}" });
                }

                Log("INFO", "Config loaded successfully. Printer: " + selectedPrinter + ", LabelW: " + designerLabelW + ", LabelH: " + designerLabelH + ", Text Elements: " + textElements.Count);
            }
            catch (Exception ex)
            {
                Log("ERROR", "Failed to load config: " + ex.Message);
            }
        }

        private void InitializeWindowControls()
        {
            // Custom Title Bar
            titleBar = new Panel { BackColor = Color.FromArgb(0x28, 0x29, 0x2D), Height = 45 };
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
            Color tbBg = Color.FromArgb(0x28, 0x29, 0x2D);
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
            btnTabGenerate.Click += (s, e) => SwitchTab(0);

            btnTabDesigner = new Label {
                Text = "Designer",
                ForeColor = tabInactiveText,
                Font = tabFont,
                TextAlign = ContentAlignment.MiddleCenter,
                Size = new Size(110, 42),
                Cursor = Cursors.Hand
            };
            btnTabDesigner.Click += (s, e) => SwitchTab(1);

            btnTabLog = new Label {
                Text = "Log",
                ForeColor = tabInactiveText,
                Font = tabFont,
                TextAlign = ContentAlignment.MiddleCenter,
                Size = new Size(80, 42),
                Cursor = Cursors.Hand
            };
            btnTabLog.Click += (s, e) => SwitchTab(2);

            tabContainer.Controls.Add(btnTabGenerate);
            tabContainer.Controls.Add(btnTabDesigner);
            tabContainer.Controls.Add(btnTabLog);

            // Tab underline Paint
            tabContainer.Paint += (s, e) => {
                Label active = btnTabGenerate;
                if (activeTab == 1) active = btnTabDesigner;
                else if (activeTab == 2) active = btnTabLog;
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
            toggleAuto.Checked = configAuto;
            toggleAuto.CheckedChanged += (s, e) => SaveConfig();
            lblAuto = new Label {
                Text = "auto",
                ForeColor = Color.FromArgb(0x9E, 0x9E, 0x9E),
                Font = new Font("Segoe UI", 10.5F),
                AutoSize = true
            };

            toggleApi = new ToggleSwitch();
            toggleApi.Checked = configApi;
            toggleApi.CheckedChanged += (s, e) => SaveConfig();
            lblApi = new Label {
                Text = "API",
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

            lblProductInfo = new Label {
                Text = "",
                ForeColor = Color.FromArgb(0x9E, 0x9E, 0x9E),
                Font = new Font("Segoe UI", 9.5F, FontStyle.Bold),
                AutoSize = true,
                Visible = false
            };
            panelGenerate.Controls.Add(lblProductInfo);

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
            panelGenerate.Controls.Add(toggleApi);
            panelGenerate.Controls.Add(lblApi);
            panelGenerate.Controls.Add(btnGenerate);
            panelGenerate.Controls.Add(pbPreview);
            panelGenerate.Controls.Add(btnPrint);
            panelGenerate.Controls.Add(btnSaveAs);
            panelGenerate.Controls.Add(btnSetPrinter);

            this.Controls.Add(panelGenerate);
        }

        private void InitializeDesignerTab()
        {
            panelDesigner = new Panel { BackColor = Color.Transparent, Visible = false };

            // Sidebar panel (FlowLayoutPanel)
            panelDesignerSidebar = new FlowLayoutPanel {
                BackColor = Color.FromArgb(0x1F, 0x20, 0x23),
                Padding = new Padding(15),
                FlowDirection = FlowDirection.TopDown,
                WrapContents = false,
                AutoScroll = true
            };

            // Sidebar labels and textboxes
            var fontLbl = new Font("Segoe UI", 9F);
            var fontInput = new Font("Segoe UI", 9.5F);
            var foreColor = Color.FromArgb(0xD0, 0xD2, 0xD6);
            var inputBg = Color.FromArgb(0x2B, 0x2D, 0x31);
            var inputFore = Color.White;

            // Helper to create label
            Func<string, Label> createLbl = (text) => new Label {
                Text = text,
                ForeColor = foreColor,
                Font = fontLbl,
                AutoSize = false,
                Width = 100,
                Height = 24,
                TextAlign = ContentAlignment.MiddleLeft,
                Margin = new Padding(0)
            };

            // Helper to create textbox
            Func<string, TextBox> createTxt = (initialVal) => new TextBox {
                Text = initialVal,
                BackColor = inputBg,
                ForeColor = inputFore,
                BorderStyle = BorderStyle.FixedSingle,
                Font = fontInput,
                Width = 70,
                Height = 20,
                Margin = new Padding(0)
            };

            // Helper to create a horizontal row
            Func<string, Control, Panel> createRow = (labelText, inputControl) => {
                var p = new FlowLayoutPanel {
                    FlowDirection = FlowDirection.LeftToRight,
                    Width = 200,
                    Height = 28,
                    Margin = new Padding(0, 1, 0, 1)
                };
                p.Controls.Add(createLbl(labelText));
                p.Controls.Add(inputControl);
                return p;
            };

            // Group Label Size
            var lblGroupSize = new Label {
                Text = "LABEL SIZE (mm)",
                ForeColor = Color.FromArgb(0x35, 0xA2, 0xEB),
                Font = new Font("Segoe UI", 9.5F, FontStyle.Bold),
                AutoSize = true
            };
            panelDesignerSidebar.Controls.Add(lblGroupSize);

            txtLabelW = createTxt(designerLabelW.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture));
            txtLabelW.TextChanged += (s, e) => {
                if (isUpdatingSidebar) return;
                double val;
                if (double.TryParse(txtLabelW.Text, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out val))
                { designerLabelW = val; PerformCustomLayout(); pbDesignerCanvas.Invalidate(); UpdatePreview(); }
            };
            panelDesignerSidebar.Controls.Add(createRow("Width (mm):", txtLabelW));

            txtLabelH = createTxt(designerLabelH.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture));
            txtLabelH.TextChanged += (s, e) => {
                if (isUpdatingSidebar) return;
                double val;
                if (double.TryParse(txtLabelH.Text, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out val))
                { designerLabelH = val; PerformCustomLayout(); pbDesignerCanvas.Invalidate(); UpdatePreview(); }
            };
            panelDesignerSidebar.Controls.Add(createRow("Height (mm):", txtLabelH));

            txtLabelGap = createTxt(designerGap.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture));
            txtLabelGap.TextChanged += (s, e) => {
                if (isUpdatingSidebar) return;
                double val;
                if (double.TryParse(txtLabelGap.Text, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out val))
                { designerGap = val; UpdatePreview(); }
            };
            panelDesignerSidebar.Controls.Add(createRow("Gap (mm):", txtLabelGap));

            txtLabelMargin = createTxt(designerMarginLeft.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture));
            txtLabelMargin.TextChanged += (s, e) => {
                if (isUpdatingSidebar) return;
                double val;
                if (double.TryParse(txtLabelMargin.Text, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out val))
                { designerMarginLeft = val; pbDesignerCanvas.Invalidate(); UpdatePreview(); }
            };
            panelDesignerSidebar.Controls.Add(createRow("Margin L (mm):", txtLabelMargin));

            toggleRotate180 = new ToggleSwitch();
            toggleRotate180.Checked = rotate180;
            toggleRotate180.CheckedChanged += (s, e) => {
                if (isUpdatingSidebar) return;
                rotate180 = toggleRotate180.Checked;
                SaveConfig();
                UpdatePreview();
            };
            panelDesignerSidebar.Controls.Add(createRow("Rotate 180:", toggleRotate180));

            // Group Text Elements
            var lblGroupText = new Label {
                Text = "TEXT ELEMENTS",
                ForeColor = Color.FromArgb(0x35, 0xA2, 0xEB),
                Font = new Font("Segoe UI", 9.5F, FontStyle.Bold),
                AutoSize = true,
                Margin = new Padding(0, 15, 0, 5)
            };
            panelDesignerSidebar.Controls.Add(lblGroupText);

            panelTextElementsContainer = new FlowLayoutPanel {
                FlowDirection = FlowDirection.TopDown,
                WrapContents = false,
                Width = 200,
                AutoSize = true,
                Margin = new Padding(0)
            };
            panelDesignerSidebar.Controls.Add(panelTextElementsContainer);

            btnAddText = new CustomButton {
                Text = "Add Text Element",
                NormalColor = Color.FromArgb(0x2E, 0x7D, 0x32),
                HoverColor = Color.FromArgb(0x38, 0x8E, 0x3C),
                BorderColor = Color.Transparent,
                Font = new Font("Segoe UI Semibold", 9F),
                Margin = new Padding(0, 5, 0, 5),
                Width = 180,
                Height = 28
            };
            btnAddText.Click += (s, e) => {
                var te = new TextElement {
                    Value = "Text " + (textElements.Count + 1),
                    X = 2.0,
                    Y = 2.0,
                    W = 30.0,
                    H = 5.0,
                    Font = "2"
                };
                textElements.Add(te);
                selectedElementIndex = textElements.Count - 1;
                RebuildTextElementsUI();
                UpdateSidebarProperties();
                pbDesignerCanvas.Invalidate();
                UpdatePreview();
                SaveConfig();
            };
            panelDesignerSidebar.Controls.Add(btnAddText);

            // Group Barcode
            var lblGroupBar = new Label {
                Text = "DATAMATRIX BARCODE",
                ForeColor = Color.FromArgb(0x35, 0xA2, 0xEB),
                Font = new Font("Segoe UI", 9.5F, FontStyle.Bold),
                AutoSize = true,
                Margin = new Padding(0, 15, 0, 5)
            };
            panelDesignerSidebar.Controls.Add(lblGroupBar);

            txtBarX = createTxt(dmatrixX.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture));
            txtBarX.TextChanged += (s, e) => {
                if (isUpdatingSidebar) return;
                double val;
                if (double.TryParse(txtBarX.Text, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out val))
                { dmatrixX = val; pbDesignerCanvas.Invalidate(); UpdatePreview(); }
            };
            panelDesignerSidebar.Controls.Add(createRow("X (mm):", txtBarX));

            txtBarY = createTxt(dmatrixY.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture));
            txtBarY.TextChanged += (s, e) => {
                if (isUpdatingSidebar) return;
                double val;
                if (double.TryParse(txtBarY.Text, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out val))
                { dmatrixY = val; pbDesignerCanvas.Invalidate(); UpdatePreview(); }
            };
            panelDesignerSidebar.Controls.Add(createRow("Y (mm):", txtBarY));

            txtBarW = createTxt(dmatrixW.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture));
            txtBarW.TextChanged += (s, e) => {
                if (isUpdatingSidebar) return;
                double val;
                if (double.TryParse(txtBarW.Text, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out val))
                { dmatrixW = val; pbDesignerCanvas.Invalidate(); UpdatePreview(); }
            };
            panelDesignerSidebar.Controls.Add(createRow("Width (mm):", txtBarW));

            txtBarH = createTxt(dmatrixH.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture));
            txtBarH.TextChanged += (s, e) => {
                if (isUpdatingSidebar) return;
                double val;
                if (double.TryParse(txtBarH.Text, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out val))
                { dmatrixH = val; pbDesignerCanvas.Invalidate(); UpdatePreview(); }
            };
            panelDesignerSidebar.Controls.Add(createRow("Height (mm):", txtBarH));

            // Save button
            btnSaveTemplate = new CustomButton {
                Text = "Save Template",
                NormalColor = Color.FromArgb(0x35, 0xA2, 0xEB),
                HoverColor = Color.FromArgb(0x4F, 0xB3, 0xE8),
                BorderColor = Color.Transparent,
                Font = new Font("Segoe UI Semibold", 9.5F),
                Margin = new Padding(0, 12, 0, 0),
                Width = 180,
                Height = 35
            };
            btnSaveTemplate.Click += (s, e) => {
                SaveConfig();
                UpdatePreview();
                MessageBox.Show("Шаблон успешно сохранен!", "Успех", MessageBoxButtons.OK, MessageBoxIcon.Information);
            };
            panelDesignerSidebar.Controls.Add(btnSaveTemplate);

            // Bottom spacer to ensure Save Template button is fully scrollable and not cut off
            panelDesignerSidebar.Controls.Add(new Panel {
                Height = 35,
                Width = 180,
                BackColor = Color.Transparent,
                Margin = new Padding(0)
            });

            // Canvas Container Panel
            panelDesignerCanvasContainer = new Panel {
                BackColor = Color.FromArgb(0x0E, 0x0F, 0x11),
                BorderStyle = BorderStyle.None
            };

            // PictureBox canvas
            pbDesignerCanvas = new PictureBox {
                BackColor = Color.FromArgb(0xEE, 0xF0, 0xF4),
                BorderStyle = BorderStyle.FixedSingle,
                Cursor = Cursors.Default
            };
            pbDesignerCanvas.Paint += PbDesignerCanvas_Paint;
            pbDesignerCanvas.MouseDown += PbDesignerCanvas_MouseDown;
            pbDesignerCanvas.MouseMove += PbDesignerCanvas_MouseMove;
            pbDesignerCanvas.MouseUp += PbDesignerCanvas_MouseUp;

            panelDesignerCanvasContainer.Controls.Add(pbDesignerCanvas);

            panelDesigner.Controls.Add(panelDesignerSidebar);
            panelDesigner.Controls.Add(panelDesignerCanvasContainer);

            this.Controls.Add(panelDesigner);
            RebuildTextElementsUI();
            UpdateSidebarProperties();
        }

        private void PbDesignerCanvas_Paint(object sender, PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;

            // Calculate total backing paper dimensions in dots at 8 dots/mm (203 DPI)
            int dotsW = (int)((designerLabelW + 2 * designerMarginLeft) * 8);
            int dotsH = (int)((designerLabelH + 2.0) * 8);
            if (dotsW <= 0 || dotsH <= 0) return;

            // Leave space for rulers (offset canvas by 25 pixels at top and left)
            int rulerSize = 25;
            int canvasAvailableW = pbDesignerCanvas.Width - rulerSize - 10;
            int canvasAvailableH = pbDesignerCanvas.Height - rulerSize - 10;
            if (canvasAvailableW < 50) canvasAvailableW = 50;
            if (canvasAvailableH < 50) canvasAvailableH = 50;

            float scale = Math.Min((float)canvasAvailableW / dotsW, (float)canvasAvailableH / dotsH);
            int renderW = (int)(dotsW * scale);
            int renderH = (int)(dotsH * scale);
            int startX = rulerSize + (canvasAvailableW - renderW) / 2;
            int startY = rulerSize + (canvasAvailableH - renderH) / 2;

            // Draw backing paper and white rounded label using RenderGdiPreview
            Rectangle backingRect = new Rectangle(startX, startY, renderW, renderH);
            
            string sampleText = string.IsNullOrEmpty(productName) ? "НАЗВАНИЕ ТОВАРА (ТЕСТ)" : productName;
            using (Image labelImg = RenderGdiPreview(lastBarcodeImage, sampleText))
            {
                g.DrawImage(labelImg, backingRect, new Rectangle(0, 0, labelImg.Width, labelImg.Height), GraphicsUnit.Pixel);
            }

            // Outline of the physical label inside the backing paper (for exact design borders)
            int labelX = startX + (int)(designerMarginLeft * 8 * scale);
            int labelY = startY + (int)(1.0 * 8 * scale);
            int labelW = (int)(designerLabelW * 8 * scale);
            int labelH = (int)(designerLabelH * 8 * scale);
            Rectangle labelRect = new Rectangle(labelX, labelY, labelW, labelH);

            // Draw visual grid (every 5 mm) inside the label bounds
            using (Pen gridPen = new Pen(Color.FromArgb(40, Color.Gray), 1f))
            {
                gridPen.DashStyle = DashStyle.Dash;
                
                // Vertical grid lines
                for (double mm = 5; mm < designerLabelW; mm += 5)
                {
                    float xPos = labelX + (float)(mm * 8 * scale);
                    g.DrawLine(gridPen, xPos, labelY, xPos, labelY + labelH);
                }
                
                // Horizontal grid lines
                for (double mm = 5; mm < designerLabelH; mm += 5)
                {
                    float yPos = labelY + (float)(mm * 8 * scale);
                    g.DrawLine(gridPen, labelX, yPos, labelX + labelW, yPos);
                }
            }

            // Draw a distinct border around the physical rounded label bounds on the canvas
            int labelRadius = (int)(2 * 8 * scale);
            if (labelRadius > labelH / 2) labelRadius = labelH / 2;
            if (labelRadius < 2) labelRadius = 2;
            using (GraphicsPath roundedLabelPath = UIHelpers.CreateRoundedRectanglePath(labelRect, labelRadius))
            using (Pen labelBorderPen = new Pen(Color.FromArgb(130, 140, 150), 1f))
            {
                g.DrawPath(labelBorderPen, roundedLabelPath);
            }

            // Draw border around the entire backing paper (Y-cut limits)
            using (Pen backingBorderPen = new Pen(Color.FromArgb(160, 170, 180), 1f))
            {
                g.DrawRectangle(backingBorderPen, backingRect.X, backingRect.Y, backingRect.Width - 1, backingRect.Height - 1);
            }

            // Draw Millimeter Rulers (Top and Left scales)
            using (Pen rulerPen = new Pen(Color.FromArgb(90, 100, 110), 1f))
            using (Font rulerFont = new Font("Segoe UI", 7F))
            using (Brush rulerBrush = new SolidBrush(Color.FromArgb(120, 130, 140)))
            {
                // Top Ruler (Horizontal)
                g.DrawLine(rulerPen, labelX, startY - 1, labelX + labelW, startY - 1);
                for (double mm = 0; mm <= designerLabelW; mm += 1.0)
                {
                    float xPos = labelX + (float)(mm * 8 * scale);
                    if (mm % 10 == 0)
                    {
                        g.DrawLine(rulerPen, xPos, startY - 10, xPos, startY - 1);
                        g.DrawString(mm.ToString(), rulerFont, rulerBrush, xPos - 5, startY - 22);
                    }
                    else if (mm % 5 == 0)
                    {
                        g.DrawLine(rulerPen, xPos, startY - 6, xPos, startY - 1);
                    }
                    else
                    {
                        g.DrawLine(rulerPen, xPos, startY - 3, xPos, startY - 1);
                    }
                }

                // Left Ruler (Vertical)
                g.DrawLine(rulerPen, labelX - 1, labelY, labelX - 1, labelY + labelH);
                for (double mm = 0; mm <= designerLabelH; mm += 1.0)
                {
                    float yPos = labelY + (float)(mm * 8 * scale);
                    if (mm % 10 == 0)
                    {
                        g.DrawLine(rulerPen, labelX - 10, yPos, labelX - 1, yPos);
                        g.DrawString(mm.ToString(), rulerFont, rulerBrush, labelX - 22, yPos - 5);
                    }
                    else if (mm % 5 == 0)
                    {
                        g.DrawLine(rulerPen, labelX - 6, yPos, labelX - 1, yPos);
                    }
                    else
                    {
                        g.DrawLine(rulerPen, labelX - 3, yPos, labelX - 1, yPos);
                    }
                }
            }

            // Draw bounding boxes and resize handles on top of elements (relative to the white label)

            // 1. Text Elements
            for (int i = 0; i < textElements.Count; i++)
            {
                var te = textElements[i];
                int tx = labelX + (int)(te.X * 8 * scale);
                int ty = labelY + (int)(te.Y * 8 * scale);
                int tw = (int)(te.W * 8 * scale);
                int th = (int)(te.H * 8 * scale);
                Rectangle textRect = new Rectangle(tx, ty, tw, th);

                bool isSelected = (selectedElementIndex == i);
                bool isInteracting = isSelected && (isDraggingText || isResizingText);

                if (isInteracting)
                {
                    UIHelpers.DrawRoundedRectangle(g, Pens.Blue, textRect, 4);
                    g.FillEllipse(Brushes.Blue, textRect.Right - 6, textRect.Bottom - 6, 6, 6);
                    using (Pen p = new Pen(Color.White, 1f)) g.DrawEllipse(p, textRect.Right - 6, textRect.Bottom - 6, 6, 6);
                }
                else if (isSelected)
                {
                    using (Pen p = new Pen(Color.Blue, 1.5f))
                    {
                        UIHelpers.DrawRoundedRectangle(g, p, textRect, 4);
                    }
                    g.FillEllipse(Brushes.Blue, textRect.Right - 6, textRect.Bottom - 6, 6, 6);
                    using (Pen p = new Pen(Color.White, 1f)) g.DrawEllipse(p, textRect.Right - 6, textRect.Bottom - 6, 6, 6);
                }
                else
                {
                    using (Pen p = new Pen(Color.FromArgb(80, Color.Blue), 1f))
                    {
                        p.DashStyle = DashStyle.Dash;
                        UIHelpers.DrawRoundedRectangle(g, p, textRect, 4);
                    }
                }
            }

            // 2. Barcode Element
            int bx = labelX + (int)(dmatrixX * 8 * scale);
            int by = labelY + (int)(dmatrixY * 8 * scale);
            int bw = (int)(dmatrixW * 8 * scale);
            int bh = (int)(dmatrixH * 8 * scale);
            Rectangle barRect = new Rectangle(bx, by, bw, bh);

            bool isBarSelected = (selectedElementIndex == -2);
            bool isBarInteracting = isBarSelected && (isDraggingBarcode || isResizingBarcode);

            if (isBarInteracting)
            {
                UIHelpers.DrawRoundedRectangle(g, Pens.Blue, barRect, 4);
                g.FillEllipse(Brushes.Blue, barRect.Right - 6, barRect.Bottom - 6, 6, 6);
                using (Pen p = new Pen(Color.White, 1f)) g.DrawEllipse(p, barRect.Right - 6, barRect.Bottom - 6, 6, 6);
            }
            else if (isBarSelected)
            {
                using (Pen p = new Pen(Color.Blue, 1.5f))
                {
                    UIHelpers.DrawRoundedRectangle(g, p, barRect, 4);
                }
                g.FillEllipse(Brushes.Blue, barRect.Right - 6, barRect.Bottom - 6, 6, 6);
                using (Pen p = new Pen(Color.White, 1f)) g.DrawEllipse(p, barRect.Right - 6, barRect.Bottom - 6, 6, 6);
            }
            else
            {
                using (Pen p = new Pen(Color.FromArgb(80, Color.Blue), 1f))
                {
                    p.DashStyle = DashStyle.Dash;
                    UIHelpers.DrawRoundedRectangle(g, p, barRect, 4);
                }
            }
        }

        private void PbDesignerCanvas_MouseDown(object sender, MouseEventArgs e)
        {
            if (e.Button != MouseButtons.Left) return;

            int dotsW = (int)((designerLabelW + 2 * designerMarginLeft) * 8);
            int dotsH = (int)((designerLabelH + 2.0) * 8);
            if (dotsW <= 0 || dotsH <= 0) return;

            int rulerSize = 25;
            int canvasAvailableW = pbDesignerCanvas.Width - rulerSize - 10;
            int canvasAvailableH = pbDesignerCanvas.Height - rulerSize - 10;
            float scale = Math.Min((float)canvasAvailableW / dotsW, (float)canvasAvailableH / dotsH);
            int renderW = (int)(dotsW * scale);
            int startX = rulerSize + (canvasAvailableW - renderW) / 2;
            int startY = rulerSize + (canvasAvailableH - (int)(dotsH * scale)) / 2;

            int labelX = startX + (int)(designerMarginLeft * 8 * scale);
            int labelY = startY + (int)(1.0 * 8 * scale);

            // Check Barcode Hit
            int bx = labelX + (int)(dmatrixX * 8 * scale);
            int by = labelY + (int)(dmatrixY * 8 * scale);
            int bw = (int)(dmatrixW * 8 * scale);
            int bh = (int)(dmatrixH * 8 * scale);
            Rectangle barRect = new Rectangle(bx, by, bw, bh);
            Rectangle barResizeHandle = new Rectangle(barRect.Right - 8, barRect.Bottom - 8, 8, 8);

            if (selectedElementIndex == -2 && barResizeHandle.Contains(e.Location))
            {
                isResizingBarcode = true;
                dragStartPoint = e.Location;
                originalW = dmatrixW;
                originalH = dmatrixH;
                return;
            }
            if (barRect.Contains(e.Location))
            {
                selectedElementIndex = -2;
                isDraggingBarcode = true;
                dragStartPoint = e.Location;
                originalX = dmatrixX;
                originalY = dmatrixY;
                UpdateSidebarProperties();
                pbDesignerCanvas.Invalidate();
                return;
            }

            // Check Text Elements Hit (Loop from top to bottom)
            for (int i = textElements.Count - 1; i >= 0; i--)
            {
                var te = textElements[i];
                int tx = labelX + (int)(te.X * 8 * scale);
                int ty = labelY + (int)(te.Y * 8 * scale);
                int tw = (int)(te.W * 8 * scale);
                int th = (int)(te.H * 8 * scale);
                Rectangle textRect = new Rectangle(tx, ty, tw, th);
                Rectangle textResizeHandle = new Rectangle(textRect.Right - 8, textRect.Bottom - 8, 8, 8);

                if (selectedElementIndex == i && textResizeHandle.Contains(e.Location))
                {
                    isResizingText = true;
                    dragStartPoint = e.Location;
                    originalW = te.W;
                    originalH = te.H;
                    return;
                }

                if (textRect.Contains(e.Location))
                {
                    selectedElementIndex = i;
                    isDraggingText = true;
                    dragStartPoint = e.Location;
                    originalX = te.X;
                    originalY = te.Y;
                    
                    // Auto-expand the selected text element panel in the sidebar
                    if (selectedElementIndex >= 0 && selectedElementIndex < textElementsUI.Count)
                    {
                        var ui = textElementsUI[selectedElementIndex];
                        ui.Element.IsExpanded = true;
                        ui.ToggleLabel.Text = "▼";
                        ui.BodyPanel.Visible = true;
                    }

                    UpdateSidebarProperties();
                    pbDesignerCanvas.Invalidate();
                    return;
                }
            }

            // Clicked empty space on label
            selectedElementIndex = -1; // Select general label properties
            UpdateSidebarProperties();
            pbDesignerCanvas.Invalidate();
        }

        private void PbDesignerCanvas_MouseMove(object sender, MouseEventArgs e)
        {
            int dotsW = (int)((designerLabelW + 2 * designerMarginLeft) * 8);
            int dotsH = (int)((designerLabelH + 2.0) * 8);
            if (dotsW <= 0 || dotsH <= 0) return;

            int rulerSize = 25;
            int canvasAvailableW = pbDesignerCanvas.Width - rulerSize - 10;
            int canvasAvailableH = pbDesignerCanvas.Height - rulerSize - 10;
            float scale = Math.Min((float)canvasAvailableW / dotsW, (float)canvasAvailableH / dotsH);
            int renderW = (int)(dotsW * scale);
            int startX = rulerSize + (canvasAvailableW - renderW) / 2;
            int startY = rulerSize + (canvasAvailableH - (int)(dotsH * scale)) / 2;

            int labelX = startX + (int)(designerMarginLeft * 8 * scale);
            int labelY = startY + (int)(1.0 * 8 * scale);

            // Set cursor style
            bool overHandle = false;
            bool overElement = false;

            // Check Barcode
            int bx = labelX + (int)(dmatrixX * 8 * scale);
            int by = labelY + (int)(dmatrixY * 8 * scale);
            int bw = (int)(dmatrixW * 8 * scale);
            int bh = (int)(dmatrixH * 8 * scale);
            Rectangle barRect = new Rectangle(bx, by, bw, bh);
            Rectangle barResizeHandle = new Rectangle(barRect.Right - 8, barRect.Bottom - 8, 8, 8);

            if (selectedElementIndex == -2 && barResizeHandle.Contains(e.Location)) overHandle = true;
            if (barRect.Contains(e.Location)) overElement = true;

            // Check Text Elements
            for (int i = 0; i < textElements.Count; i++)
            {
                var te = textElements[i];
                int tx = labelX + (int)(te.X * 8 * scale);
                int ty = labelY + (int)(te.Y * 8 * scale);
                int tw = (int)(te.W * 8 * scale);
                int th = (int)(te.H * 8 * scale);
                Rectangle textRect = new Rectangle(tx, ty, tw, th);
                Rectangle textResizeHandle = new Rectangle(textRect.Right - 8, textRect.Bottom - 8, 8, 8);

                if (selectedElementIndex == i && textResizeHandle.Contains(e.Location)) overHandle = true;
                if (textRect.Contains(e.Location)) overElement = true;
            }

            if (overHandle || isResizingBarcode || isResizingText)
            {
                pbDesignerCanvas.Cursor = Cursors.SizeNWSE;
            }
            else if (overElement || isDraggingBarcode || isDraggingText)
            {
                pbDesignerCanvas.Cursor = Cursors.Hand;
            }
            else
            {
                pbDesignerCanvas.Cursor = Cursors.Default;
            }

            int dx = e.X - dragStartPoint.X;
            int dy = e.Y - dragStartPoint.Y;

            // Convert offsets from screen pixels to millimeters (1 pixel = 1 / (8 * scale) mm)
            double dxMm = dx / (8 * scale);
            double dyMm = dy / (8 * scale);

            if (isDraggingText && selectedElementIndex >= 0 && selectedElementIndex < textElementsUI.Count)
            {
                var te = textElements[selectedElementIndex];
                te.X = originalX + dxMm;
                te.Y = originalY + dyMm;
                isUpdatingSidebar = true;
                var ui = textElementsUI[selectedElementIndex];
                ui.XTextBox.Text = te.X.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture);
                ui.YTextBox.Text = te.Y.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture);
                isUpdatingSidebar = false;
                pbDesignerCanvas.Invalidate();
            }
            else if (isResizingText && selectedElementIndex >= 0 && selectedElementIndex < textElementsUI.Count)
            {
                var te = textElements[selectedElementIndex];
                te.W = originalW + dxMm;
                te.H = originalH + dyMm;
                if (te.W < 2.0) te.W = 2.0;
                if (te.H < 1.0) te.H = 1.0;
                isUpdatingSidebar = true;
                var ui = textElementsUI[selectedElementIndex];
                ui.WTextBox.Text = te.W.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture);
                ui.HTextBox.Text = te.H.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture);
                isUpdatingSidebar = false;
                pbDesignerCanvas.Invalidate();
            }
            else if (isDraggingBarcode)
            {
                dmatrixX = originalX + dxMm;
                dmatrixY = originalY + dyMm;
                isUpdatingSidebar = true;
                txtBarX.Text = dmatrixX.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture);
                txtBarY.Text = dmatrixY.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture);
                isUpdatingSidebar = false;
                pbDesignerCanvas.Invalidate();
            }
            else if (isResizingBarcode)
            {
                dmatrixW = originalW + dxMm;
                dmatrixH = originalH + dyMm;
                if (dmatrixW < 3.0) dmatrixW = 3.0;
                if (dmatrixH < 3.0) dmatrixH = 3.0;
                isUpdatingSidebar = true;
                txtBarW.Text = dmatrixW.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture);
                txtBarH.Text = dmatrixH.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture);
                isUpdatingSidebar = false;
                pbDesignerCanvas.Invalidate();
            }
        }

        private void PbDesignerCanvas_MouseUp(object sender, MouseEventArgs e)
        {
            isDraggingText = false;
            isResizingText = false;
            isDraggingBarcode = false;
            isResizingBarcode = false;
            pbDesignerCanvas.Invalidate();
            SaveConfig();
            UpdatePreview();
        }

        private void RebuildTextElementsUI()
        {
            if (panelTextElementsContainer == null) return;
            
            panelTextElementsContainer.Controls.Clear();
            textElementsUI.Clear();

            var fontLbl = new Font("Segoe UI", 8.5F);
            var fontInput = new Font("Segoe UI", 9F);
            var foreColor = Color.FromArgb(0xD0, 0xD2, 0xD6);
            var inputBg = Color.FromArgb(0x2B, 0x2D, 0x31);
            var inputFore = Color.White;

            Func<string, Label> createLbl = (text) => new Label {
                Text = text,
                ForeColor = foreColor,
                Font = fontLbl,
                AutoSize = false,
                Width = 80,
                Height = 22,
                TextAlign = ContentAlignment.MiddleLeft,
                Margin = new Padding(0)
            };

            Func<string, TextBox> createTxt = (initialVal) => new TextBox {
                Text = initialVal,
                BackColor = inputBg,
                ForeColor = inputFore,
                BorderStyle = BorderStyle.FixedSingle,
                Font = fontInput,
                Width = 60,
                Height = 18,
                Margin = new Padding(0)
            };

            Func<string, Control, Panel> createRow = (labelText, inputControl) => {
                var p = new FlowLayoutPanel {
                    FlowDirection = FlowDirection.LeftToRight,
                    Width = 180,
                    Height = 24,
                    Margin = new Padding(0, 1, 0, 1)
                };
                p.Controls.Add(createLbl(labelText));
                p.Controls.Add(inputControl);
                return p;
            };

            for (int i = 0; i < textElements.Count; i++)
            {
                int index = i;
                var te = textElements[i];

                var ui = new TextElementUI { Element = te };

                // Group Panel for this element
                var groupPanel = new FlowLayoutPanel {
                    FlowDirection = FlowDirection.TopDown,
                    WrapContents = false,
                    Width = 190,
                    AutoSize = true,
                    BackColor = Color.Transparent, // Transparent background so our rounded custom painting is visible
                    Margin = new Padding(0, 3, 0, 3),
                    Padding = new Padding(5)
                };
                ui.HeaderPanel = groupPanel;

                groupPanel.Paint += (s, ev) => {
                    Graphics gBg = ev.Graphics;
                    gBg.SmoothingMode = SmoothingMode.AntiAlias;
                    Rectangle r = new Rectangle(0, 0, groupPanel.Width - 1, groupPanel.Height - 1);
                    
                    Color bg = (selectedElementIndex == index)
                        ? Color.FromArgb(0x2B, 0x3A, 0x4F) // Blue highlight
                        : Color.FromArgb(0x25, 0x26, 0x29); // Dark gray

                    using (Brush brush = new SolidBrush(bg))
                    {
                        UIHelpers.FillRoundedRectangle(gBg, brush, r, 6);
                    }
                };

                // Header Row (title, toggle button, delete button)
                var headerRow = new FlowLayoutPanel {
                    FlowDirection = FlowDirection.LeftToRight,
                    Width = 180,
                    Height = 24,
                    Margin = new Padding(0)
                };

                var toggleLbl = new Label {
                    Text = te.IsExpanded ? "▼" : "▶",
                    ForeColor = Color.FromArgb(0x35, 0xA2, 0xEB),
                    Font = new Font("Segoe UI", 9F, FontStyle.Bold),
                    Width = 15,
                    Height = 20,
                    TextAlign = ContentAlignment.MiddleCenter,
                    Cursor = Cursors.Hand,
                    Margin = new Padding(0, 2, 0, 0)
                };
                ui.ToggleLabel = toggleLbl;

                var titleLbl = new Label {
                    Text = string.Format("Текст #{0}: {1}", index + 1, te.Value.Length > 10 ? te.Value.Substring(0, 8) + ".." : te.Value),
                    ForeColor = Color.White,
                    Font = new Font("Segoe UI", 9F, FontStyle.Bold),
                    Width = 125,
                    Height = 20,
                    TextAlign = ContentAlignment.MiddleLeft,
                    Cursor = Cursors.Hand,
                    Margin = new Padding(2, 2, 0, 0)
                };
                ui.HeaderLabel = titleLbl;

                // Click header or toggle to expand/collapse
                EventHandler toggleExpand = (s, e) => {
                    te.IsExpanded = !te.IsExpanded;
                    toggleLbl.Text = te.IsExpanded ? "▼" : "▶";
                    ui.BodyPanel.Visible = te.IsExpanded;
                    selectedElementIndex = index;
                    UpdateSidebarProperties();
                    pbDesignerCanvas.Invalidate();
                };
                toggleLbl.Click += toggleExpand;
                titleLbl.Click += toggleExpand;

                var deleteBtn = new CustomButton {
                    Text = "✕",
                    NormalColor = Color.Transparent,
                    HoverColor = Color.FromArgb(0xC6, 0x28, 0x28),
                    BorderColor = Color.Transparent,
                    ForeColor = Color.FromArgb(0xE0, 0x52, 0x52),
                    Font = new Font("Segoe UI", 7.5F, FontStyle.Bold),
                    Width = 20,
                    Height = 20,
                    Margin = new Padding(5, 2, 0, 0)
                };
                deleteBtn.Click += (s, e) => {
                    textElements.RemoveAt(index);
                    if (selectedElementIndex == index) selectedElementIndex = -1;
                    else if (selectedElementIndex > index) selectedElementIndex--;
                    RebuildTextElementsUI();
                    UpdateSidebarProperties();
                    pbDesignerCanvas.Invalidate();
                    UpdatePreview();
                    SaveConfig();
                };

                headerRow.Controls.Add(toggleLbl);
                headerRow.Controls.Add(titleLbl);
                headerRow.Controls.Add(deleteBtn);
                groupPanel.Controls.Add(headerRow);

                // Body Panel (Value, X, Y, W, H, Font)
                var bodyPanel = new FlowLayoutPanel {
                    FlowDirection = FlowDirection.TopDown,
                    WrapContents = false,
                    Width = 180,
                    AutoSize = true,
                    Visible = te.IsExpanded,
                    Margin = new Padding(0, 5, 0, 0)
                };
                ui.BodyPanel = bodyPanel;

                ui.ValueTextBox = new TextBox {
                    Text = te.Value,
                    BackColor = inputBg,
                    ForeColor = inputFore,
                    BorderStyle = BorderStyle.FixedSingle,
                    Font = fontInput,
                    Width = 90,
                    Height = 18,
                    Margin = new Padding(0)
                };
                ui.ValueTextBox.TextChanged += (s, e) => {
                    if (isUpdatingSidebar) return;
                    te.Value = ui.ValueTextBox.Text;
                    titleLbl.Text = string.Format("Текст #{0}: {1}", index + 1, te.Value.Length > 10 ? te.Value.Substring(0, 8) + ".." : te.Value);
                    pbDesignerCanvas.Invalidate();
                    UpdatePreview();
                };
                bodyPanel.Controls.Add(createRow("Значение:", ui.ValueTextBox));

                ui.XTextBox = createTxt(te.X.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture));
                ui.XTextBox.TextChanged += (s, e) => {
                    if (isUpdatingSidebar) return;
                    double val;
                    if (double.TryParse(ui.XTextBox.Text, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out val))
                    { te.X = val; pbDesignerCanvas.Invalidate(); UpdatePreview(); }
                };
                bodyPanel.Controls.Add(createRow("X (мм):", ui.XTextBox));

                ui.YTextBox = createTxt(te.Y.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture));
                ui.YTextBox.TextChanged += (s, e) => {
                    if (isUpdatingSidebar) return;
                    double val;
                    if (double.TryParse(ui.YTextBox.Text, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out val))
                    { te.Y = val; pbDesignerCanvas.Invalidate(); UpdatePreview(); }
                };
                bodyPanel.Controls.Add(createRow("Y (мм):", ui.YTextBox));

                ui.WTextBox = createTxt(te.W.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture));
                ui.WTextBox.TextChanged += (s, e) => {
                    if (isUpdatingSidebar) return;
                    double val;
                    if (double.TryParse(ui.WTextBox.Text, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out val))
                    { te.W = val; pbDesignerCanvas.Invalidate(); UpdatePreview(); }
                };
                bodyPanel.Controls.Add(createRow("Ширина (мм):", ui.WTextBox));

                ui.HTextBox = createTxt(te.H.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture));
                ui.HTextBox.TextChanged += (s, e) => {
                    if (isUpdatingSidebar) return;
                    double val;
                    if (double.TryParse(ui.HTextBox.Text, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out val))
                    { te.H = val; pbDesignerCanvas.Invalidate(); UpdatePreview(); }
                };
                bodyPanel.Controls.Add(createRow("Высота (мм):", ui.HTextBox));

                ui.FontComboBox = new ComboBox {
                    BackColor = inputBg,
                    ForeColor = inputFore,
                    DropDownStyle = ComboBoxStyle.DropDownList,
                    Font = fontInput,
                    Width = 90,
                    Margin = new Padding(0)
                };
                ui.FontComboBox.Items.AddRange(new object[] { "1", "2", "3", "4", "5" });
                ui.FontComboBox.SelectedItem = te.Font;
                ui.FontComboBox.SelectedIndexChanged += (s, e) => {
                    if (isUpdatingSidebar) return;
                    te.Font = ui.FontComboBox.SelectedItem.ToString();
                    pbDesignerCanvas.Invalidate();
                    UpdatePreview();
                };
                bodyPanel.Controls.Add(createRow("Шрифт:", ui.FontComboBox));

                groupPanel.Controls.Add(bodyPanel);

                panelTextElementsContainer.Controls.Add(groupPanel);
                textElementsUI.Add(ui);
            }
        }

        private void UpdateSidebarProperties()
        {
            if (isUpdatingSidebar) return;
            isUpdatingSidebar = true;
            try
            {
                txtLabelW.Text = designerLabelW.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture);
                txtLabelH.Text = designerLabelH.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture);
                txtLabelGap.Text = designerGap.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture);
                txtLabelMargin.Text = designerMarginLeft.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture);
                toggleRotate180.Checked = rotate180;

                txtBarX.Text = dmatrixX.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture);
                txtBarY.Text = dmatrixY.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture);
                txtBarW.Text = dmatrixW.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture);
                txtBarH.Text = dmatrixH.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture);

                // Update Text Elements Textboxes and Highlights
                for (int i = 0; i < textElementsUI.Count; i++)
                {
                    var ui = textElementsUI[i];
                    ui.ValueTextBox.Text = ui.Element.Value;
                    ui.XTextBox.Text = ui.Element.X.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture);
                    ui.YTextBox.Text = ui.Element.Y.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture);
                    ui.WTextBox.Text = ui.Element.W.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture);
                    ui.HTextBox.Text = ui.Element.H.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture);
                    ui.FontComboBox.SelectedItem = ui.Element.Font;
                    
                    ui.HeaderPanel.Invalidate(); // Redraw with rounded borders
                    if (selectedElementIndex == i)
                    {
                        ui.HeaderLabel.ForeColor = Color.FromArgb(0x35, 0xA2, 0xEB);
                    }
                    else
                    {
                        ui.HeaderLabel.ForeColor = Color.White;
                    }
                }
            }
            finally
            {
                isUpdatingSidebar = false;
            }
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

        private void SwitchTab(int tabIndex)
        {
            activeTab = tabIndex;
            Color activeColor = Color.FromArgb(0x35, 0xA2, 0xEB);
            Color inactiveColor = Color.FromArgb(0x7E, 0x80, 0x87);

            btnTabGenerate.ForeColor = (activeTab == 0) ? activeColor : inactiveColor;
            btnTabDesigner.ForeColor = (activeTab == 1) ? activeColor : inactiveColor;
            btnTabLog.ForeColor = (activeTab == 2) ? activeColor : inactiveColor;

            btnTabGenerate.Invalidate();
            btnTabDesigner.Invalidate();
            btnTabLog.Invalidate();

            panelGenerate.Visible = (activeTab == 0);
            panelDesigner.Visible = (activeTab == 1);
            panelLog.Visible = (activeTab == 2);

            if (activeTab == 2) FilterLogs();
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
            btnTabDesigner.Location = new Point(140, 0);
            btnTabLog.Location = new Point(260, 0);

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
            toggleApi.Location = new Point(mx + 110, rowY);
            lblApi.Location = new Point(mx + 162, rowY + 1);
            btnGenerate.Location = new Point(cw - 180 + mx, rowY - 5);
            btnGenerate.Size = new Size(180, 40);

            // Product info label
            int infoY = rowY + 44;
            if (lblProductInfo.Visible)
            {
                lblProductInfo.Location = new Point(mx, infoY);
                infoY += lblProductInfo.Height + 4;
            }

            // Preview
            int previewY = infoY;
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

            // Designer Panel
            if (panelDesigner != null)
            {
                panelDesigner.Location = new Point(0, contentY);
                panelDesigner.Size = new Size(w, contentH);

                int sidebarW = 230;
                int canvasContainerW = w - sidebarW;

                if (panelDesignerSidebar != null)
                {
                    panelDesignerSidebar.Location = new Point(0, 0);
                    panelDesignerSidebar.Size = new Size(sidebarW, contentH);
                }

                if (panelDesignerCanvasContainer != null)
                {
                    panelDesignerCanvasContainer.Location = new Point(sidebarW, 0);
                    panelDesignerCanvasContainer.Size = new Size(canvasContainerW, contentH);
                }

                if (pbDesignerCanvas != null)
                {
                    double aspect = (designerLabelW + 2 * designerMarginLeft) / (designerLabelH + 2.0);
                    if (aspect <= 0) aspect = 1;

                    int maxCanvasW = canvasContainerW - 40;
                    int maxCanvasH = contentH - 40;
                    if (maxCanvasW < 50) maxCanvasW = 50;
                    if (maxCanvasH < 50) maxCanvasH = 50;

                    int canvasW = maxCanvasW;
                    int canvasH = (int)(maxCanvasW / aspect);
                    if (canvasH > maxCanvasH)
                    {
                        canvasH = maxCanvasH;
                        canvasW = (int)(maxCanvasH * aspect);
                    }

                    pbDesignerCanvas.Size = new Size(canvasW, canvasH);
                    pbDesignerCanvas.Location = new Point((canvasContainerW - canvasW) / 2, (contentH - canvasH) / 2);
                }
            }

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

            if (activeTab == 2)
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

        private async Task<string> FetchProductInfoAsync(string data)
        {
            try
            {
                // Escape control characters for valid JSON  
                string safeData = data.Replace("\\", "\\\\").Replace("\"", "\\\"")
                    .Replace("\u001D", "\\u001d");
                string jsonBody = "{\"code\":\"" + safeData + "\"}";
                Log("INFO", "API request: " + jsonBody);

                using (var content = new StringContent(jsonBody, Encoding.UTF8, "application/json"))
                {
                    var responseMessage = await httpClient.PostAsync("https://mobile.api.crpt.ru/mobile/check", content).ConfigureAwait(false);
                    responseMessage.EnsureSuccessStatusCode();
                    byte[] responseBytes = await responseMessage.Content.ReadAsByteArrayAsync().ConfigureAwait(false);
                    string response = Encoding.UTF8.GetString(responseBytes);

                    if (string.IsNullOrEmpty(response)) { Log("ERROR", "API: empty response"); return ""; }
                    Log("INFO", "API response: " + response);

                    // Extract productName from JSON (handles escaped quotes)
                    string search = "\"productName\":\"";
                    int idx = response.IndexOf(search);
                    if (idx >= 0)
                    {
                        idx += search.Length;
                        var sb = new System.Text.StringBuilder();
                        while (idx < response.Length)
                        {
                            if (response[idx] == '\\' && idx + 1 < response.Length && response[idx + 1] == '"')
                            {
                                sb.Append('"');
                                idx += 2;
                            }
                            else if (response[idx] == '"')
                            {
                                break;
                            }
                            else
                            {
                                sb.Append(response[idx]);
                                idx++;
                            }
                        }
                        string fetchedName = sb.ToString();
                        this.BeginInvoke((MethodInvoker)delegate {
                            productName = fetchedName;
                            lblProductInfo.Text = productName;
                            lblProductInfo.Visible = false;
                            PerformCustomLayout();
                            UpdatePreview();
                        });
                        return fetchedName;
                    }
                }
            }
            catch (Exception ex)
            {
                Log("ERROR", "API error: " + ex.Message);
            }
            return "";
        }

        private async void ExecuteGeneration()
        {
            string inputText = txtDataInput.Text.Trim();
            if (string.IsNullOrEmpty(inputText))
            {
                MessageBox.Show("Введите данные для кодирования в поле ввода!", "Ошибка", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            btnGenerate.Enabled = false;
            btnPrint.Enabled = false;

            try
            {
                productName = "";
                lblProductInfo.Visible = false;

                // Check if data already has GS1 control characters (either raw ASCII 29 or literal \u001D/\u001d)
                bool hasGS1 = inputText.IndexOf('\u001D') >= 0 || inputText.IndexOf("\\u001D") >= 0 || inputText.IndexOf("\\u001d") >= 0;
                string encodeData;
                var opts = new EncodingOptions { Height = 300, Width = 300, Margin = 0 };

                if (hasGS1)
                {
                    // Already raw GS1 with separators — use as-is (converting literal escape sequences to raw ASCII 29)
                    encodeData = inputText.Replace("\\u001D", "\u001D").Replace("\\u001d", "\u001D");
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

                    lastEncodedData = encodeData;

                    if (lastBarcodeImage != null)
                    {
                        lastBarcodeImage.Dispose();
                    }
                    lastBarcodeImage = (Image)bitmap.Clone();

                    UpdatePreview();

                    // Generate safe filename from last 10 characters
                    string cleanText = CleanFileName(inputText);
                    string last10 = cleanText.Length > 10 ? cleanText.Substring(cleanText.Length - 10) : cleanText;
                    if (string.IsNullOrEmpty(last10)) last10 = "datamatrix";

                    string baseDir = AppDomain.CurrentDomain.BaseDirectory;
                    string relativePath = Path.Combine("data", last10 + ".png");
                    string fullPath = Path.Combine(baseDir, relativePath);

                    var bitmapClone = (Image)bitmap.Clone();
                    await Task.Run(() => {
                        try
                        {
                            bitmapClone.Save(fullPath, System.Drawing.Imaging.ImageFormat.Png);
                        }
                        catch (Exception ex)
                        {
                            Log("ERROR", "Failed to save file: " + ex.Message);
                        }
                        finally
                        {
                            bitmapClone.Dispose();
                        }
                    }).ConfigureAwait(true);

                    Log("INFO", string.Format("Scanned: \"{0}\" | Converted: \"{1}\" | File: data\\{2}", inputText, encodeData, last10 + ".png"));

                    string localProdName = "";
                    // Fetch product info from CRPT API (if enabled)
                    if (toggleApi.Checked)
                    {
                        localProdName = await FetchProductInfoAsync(encodeData).ConfigureAwait(true);
                    }

                    if (toggleAuto.Checked)
                    {
                        Log("INFO", "Auto-mode triggered printing.");
                        txtDataInput.Text = ""; // Clear textbox immediately
                        pbPreview.Focus();

                        var printImg = RenderGdiPreview(lastBarcodeImage, localProdName, true);
                        await Task.Run(() => {
                            try
                            {
                                PrintImageDirect(printImg, localProdName);
                            }
                            finally
                            {
                                printImg.Dispose();
                            }
                        }).ConfigureAwait(true);
                    }
                }
            }
            catch (Exception ex)
            {
                string errMsg = string.Format("Ошибка при генерации DataMatrix. Что произошло: {0}. Почему произошло: возможно, текст содержит недопустимые для DataMatrix символы или отсутствует папка для сохранения. Детали ошибки:\r\n{1}", ex.Message, ex.ToString());
                Log("ERROR", errMsg);
                MessageBox.Show(errMsg, "Ошибка генерации", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            finally
            {
                btnGenerate.Enabled = true;
                btnPrint.Enabled = true;
            }
        }

        private void ExecuteDirectPrint()
        {
            if (pbPreview.Image == null || lastBarcodeImage == null)
            {
                MessageBox.Show("Сначала сгенерируйте код!", "Печать невозможна", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }
            var printImg = RenderGdiPreview(lastBarcodeImage, productName, true);
            string localProdName = productName;
            Task.Run(() => {
                try
                {
                    PrintImageDirect(printImg, localProdName);
                }
                finally
                {
                    printImg.Dispose();
                }
            });
        }

        private void PrintImageDirect(Image img, string prodName)
        {
            try
            {
                PrintDocument pd = new PrintDocument();
                if (!string.IsNullOrEmpty(selectedPrinter))
                {
                    pd.PrinterSettings.PrinterName = selectedPrinter;
                }

                // Do NOT override PaperSize and Landscape to respect the printer's driver settings (like in BarTender)
                pd.DefaultPageSettings.Margins = new Margins(0, 0, 0, 0);

                pd.PrintPage += (sender, args) => {
                    Graphics g = args.Graphics;
                    float pageW = args.PageBounds.Width;
                    float pageH = args.PageBounds.Height;

                    // Automatically rotate the image 90 degrees if the design orientation (landscape/portrait)
                    // does not match the printer driver's page orientation. This prevents stretching.
                    Image printImg = img;
                    bool rotate90 = (pageW > pageH) != (img.Width > img.Height);

                    if (rotate90 || rotate180)
                    {
                        printImg = (Image)img.Clone();
                        if (rotate90 && rotate180)
                        {
                            printImg.RotateFlip(RotateFlipType.Rotate270FlipNone);
                        }
                        else if (rotate90)
                        {
                            printImg.RotateFlip(RotateFlipType.Rotate90FlipNone);
                        }
                        else if (rotate180)
                        {
                            printImg.RotateFlip(RotateFlipType.Rotate180FlipNone);
                        }
                    }

                    g.InterpolationMode = InterpolationMode.HighQualityBicubic;
                    g.DrawImage(printImg, 0, 0, pageW, pageH);

                    if (rotate90 || rotate180)
                    {
                        printImg.Dispose();
                    }
                };

                pd.Print();
                Log("INFO", "Sent print job to printer: " + pd.PrinterSettings.PrinterName);
            }
            catch (Exception ex)
            {
                string errMsg = string.Format("Ошибка при печати. Что произошло: {0}. Детали:\r\n{1}", ex.Message, ex.ToString());
                Log("ERROR", errMsg);
                this.BeginInvoke((MethodInvoker)delegate {
                    MessageBox.Show(errMsg, "Ошибка печати", MessageBoxButtons.OK, MessageBoxIcon.Error);
                });
            }
        }

        private void UpdatePreview()
        {
            if (lastBarcodeImage == null)
                return;

            Image newPreview = RenderGdiPreview(lastBarcodeImage, productName);

            if (pbPreview.Image != null)
            {
                pbPreview.Image.Dispose();
            }
            pbPreview.Image = newPreview;
        }

        private Image RenderGdiPreview(Image barcodeImage, string prodName, bool isForPrint = false)
        {
            // Calculate total backing paper dimensions at 8 dots/mm (203 DPI)
            int totalW = (int)((designerLabelW + 2 * designerMarginLeft) * 8);
            int totalH = (int)((designerLabelH + 2.0) * 8);
            if (totalW <= 0) totalW = 485; // 58 + 2 * 1.3 mm
            if (totalH <= 0) totalH = 336; // 40 + 2 mm

            Bitmap bmp = new Bitmap(totalW, totalH);
            using (Graphics g = Graphics.FromImage(bmp))
            {
                // Clear the backing paper (white for print, grey/blue for screen preview)
                if (isForPrint)
                {
                    g.Clear(Color.White);
                }
                else
                {
                    g.Clear(Color.FromArgb(215, 220, 228));
                }
                g.SmoothingMode = SmoothingMode.AntiAlias;
                g.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;

                // Draw the white label shifted by Left Margin and Top Margin, with rounded corners (2mm radius)
                int labelX = (int)(designerMarginLeft * 8);
                int labelY = (int)(1.0 * 8); // 1 mm top margin
                int labelW = (int)(designerLabelW * 8);
                int labelH = (int)(designerLabelH * 8);
                Rectangle labelRect = new Rectangle(labelX, labelY, labelW, labelH);

                int radius = (int)(2 * 8); // 2 mm corner radius in dots
                if (radius > labelH / 2) radius = labelH / 2;
                if (radius < 2) radius = 2;

                using (GraphicsPath path = UIHelpers.CreateRoundedRectanglePath(labelRect, radius))
                {
                    g.FillPath(Brushes.White, path);
                    if (!isForPrint)
                    {
                        g.DrawPath(new Pen(Color.FromArgb(170, 175, 182), 1f), path); // Fine soft grey label border
                    }
                }

                // Clip drawing to the white label area so nothing bleeds onto the backing paper
                using (GraphicsPath clipPath = UIHelpers.CreateRoundedRectanglePath(labelRect, radius))
                {
                    g.SetClip(clipPath);

                    // Draw all Text Elements
                    foreach (var te in textElements)
                    {
                        string content = te.Value;
                        if (content.Equals("{product}", StringComparison.OrdinalIgnoreCase))
                        {
                            content = prodName;
                        }

                        if (string.IsNullOrEmpty(content))
                            continue;

                        // Convert mm coordinates of the text element relative to the label
                        float tx = labelX + (float)(te.X * 8);
                        float ty = labelY + (float)(te.Y * 8);
                        float tw = (float)(te.W * 8);
                        float th = (float)(te.H * 8);
                        RectangleF textRect = new RectangleF(tx, ty, tw, th);

                        int pixelSize = 24; // default font 3
                        if (te.Font == "1") pixelSize = 12;
                        else if (te.Font == "2") pixelSize = 20;
                        else if (te.Font == "3") pixelSize = 24;
                        else if (te.Font == "4") pixelSize = 32;
                        else if (te.Font == "5") pixelSize = 48;
                        int fontSize = pixelSize * te.YMul;

                        using (Font font = new Font("Segoe UI", fontSize, FontStyle.Bold, GraphicsUnit.Pixel))
                        using (Brush brush = new SolidBrush(Color.Black))
                        {
                            using (StringFormat sf = new StringFormat())
                            {
                                sf.Alignment = StringAlignment.Near;
                                sf.LineAlignment = StringAlignment.Near;
                                sf.Trimming = StringTrimming.EllipsisCharacter;
                                g.DrawString(content, font, brush, textRect, sf);
                            }
                        }
                    }

                    // Draw Barcode (DataMatrix) centered correctly according to X/Y/W/H in mm
                    if (barcodeImage != null)
                    {
                        float bx = labelX + (float)(dmatrixX * 8);
                        float by = labelY + (float)(dmatrixY * 8);
                        float bw = (float)(dmatrixW * 8);
                        float bh = (float)(dmatrixH * 8);
                        RectangleF barRect = new RectangleF(bx, by, bw, bh);

                        g.InterpolationMode = InterpolationMode.HighQualityBicubic;
                        g.DrawImage(barcodeImage, barRect, new RectangleF(0, 0, barcodeImage.Width, barcodeImage.Height), GraphicsUnit.Pixel);
                    }

                    g.ResetClip();
                }
            }
            return bmp;
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
                        SaveConfig();
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
            Application.ThreadException += (sender, e) => {
                File.WriteAllText("crash.txt", "ThreadException:\r\n" + e.Exception.ToString());
                MessageBox.Show("ThreadException:\n" + e.Exception.Message);
            };
            AppDomain.CurrentDomain.UnhandledException += (sender, e) => {
                File.WriteAllText("crash.txt", "UnhandledException:\r\n" + e.ExceptionObject.ToString());
                MessageBox.Show("UnhandledException:\n" + e.ExceptionObject.ToString());
            };
            try
            {
                Application.EnableVisualStyles();
                Application.SetCompatibleTextRenderingDefault(false);
                Application.Run(new MainForm());
            }
            catch (Exception ex)
            {
                File.WriteAllText("crash.txt", "Main Exception:\r\n" + ex.ToString());
                MessageBox.Show("Exception:\n" + ex.Message);
            }
        }
    }
}
