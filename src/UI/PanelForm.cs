using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Text;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using AudioDeck.Audio;

namespace AudioDeck.UI
{
    public sealed class PanelForm : Form
    {
        private const int WmHotkey = 0x0312;
        private const int HotkeyId = 0x4A01;
        private const uint ModAlt = 0x0001;
        private const uint ModControl = 0x0002;
        private const uint ModNoRepeat = 0x4000;
        private const uint VkA = 0x41;

        private const int HitNone = 0;
        private const int HitClose = 1;
        private const int HitMute = 2;
        private const int HitSlider = 3;
        private const int HitSettings = 4;
        private const int HitRetry = 5;
        private const int HitDeviceBase = 100;

        private readonly AudioManager _audio;
        private readonly Timer _timer;

        private Theme _theme;
        private AudioSnapshot _snapshot;
        private List<AudioDevice> _devices;

        private Font _fontTitle;
        private Font _fontSubtitle;
        private Font _fontLabel;
        private Font _fontDevice;
        private Font _fontDeviceSmall;
        private Font _fontSection;
        private Font _fontPercent;
        private Font _fontMuted;
        private Font _fontFooter;

        private float _scale;
        private Screen _screen;

        private int _width;
        private int _pad;
        private int _nowY;
        private int _nowH;
        private int _volY;
        private int _volH;
        private int _secY;
        private int _secH;
        private int _listY;
        private int _listH;
        private int _footerTop;
        private int _rowH;
        private int _rowGap;
        private int _visibleRows;

        private Rectangle _closeRect;
        private Rectangle _muteRect;
        private Rectangle _sliderTrack;
        private Rectangle _sliderHit;
        private Rectangle _settingsRect;
        private Rectangle _listRect;
        private List<Rectangle> _rowRects;

        private int _hover;
        private bool _dragging;
        private int _scrollOffset;
        private bool _hotkeyRegistered;

        public event EventHandler ToggleRequested;
        public event EventHandler StateChanged;

        public int LastHiddenTick { get; private set; }
        public bool HotkeyRegistered { get { return _hotkeyRegistered; } }

        public PanelForm(AudioManager audio)
        {
            _audio = audio;
            _snapshot = new AudioSnapshot();
            _devices = new List<AudioDevice>();
            _rowRects = new List<Rectangle>();
            _theme = Theme.Create();
            _scale = 1f;

            SetStyle(ControlStyles.UserPaint |
                     ControlStyles.AllPaintingInWmPaint |
                     ControlStyles.OptimizedDoubleBuffer |
                     ControlStyles.ResizeRedraw, true);
            UpdateStyles();

            FormBorderStyle = FormBorderStyle.None;
            StartPosition = FormStartPosition.Manual;
            ShowInTaskbar = false;
            TopMost = true;
            AutoScaleMode = AutoScaleMode.None;
            KeyPreview = true;
            BackColor = _theme.Background;
            DoubleBuffered = true;
            Text = "声控台";

            CreateFonts();

            _timer = new Timer();
            _timer.Interval = 300;
            _timer.Tick += TimerTick;

            IntPtr forceHandle = Handle;
        }

        protected override CreateParams CreateParams
        {
            get
            {
                CreateParams cp = base.CreateParams;
                cp.ExStyle |= 0x00000080; // WS_EX_TOOLWINDOW
                cp.ClassStyle |= 0x00020000; // CS_DROPSHADOW
                return cp;
            }
        }

        public string TooltipText
        {
            get
            {
                if (_snapshot == null)
                {
                    return "声控台";
                }

                string name = _snapshot.DefaultDeviceName;
                if (string.IsNullOrEmpty(name))
                {
                    name = "未选择输出设备";
                }

                string state = _snapshot.Muted
                    ? "静音"
                    : string.Format("{0}%", (int)Math.Round(_snapshot.Volume * 100f));
                string text = string.Format("声控台 · {0} · {1}", name, state);
                if (text.Length > 62)
                {
                    text = text.Substring(0, 62);
                }

                return text;
            }
        }

        public void TogglePanel()
        {
            if (Visible)
            {
                HidePanel();
            }
            else
            {
                ShowPanel();
            }
        }

        public void ShowPanel()
        {
            if (Visible)
            {
                return;
            }

            _screen = Screen.FromPoint(Cursor.Position);
            UpdateScale(_screen);
            ApplyTheme();
            _scrollOffset = 0;
            LoadSnapshot(true);
            ApplyWindowChrome();
            PositionWindow();
            Show();
            Activate();
            try
            {
                Interop.SetForegroundWindow(Handle);
            }
            catch
            {
            }
        }

        public void HidePanel()
        {
            _dragging = false;
            Capture = false;
            Hide();
        }

        public void ReloadDevices()
        {
            if (!Visible)
            {
                return;
            }

            LoadSnapshot(false);
        }

        public void RefreshSnapshotQuiet()
        {
            LoadSnapshot(false);
        }

        private void LoadSnapshot(bool resetScroll)
        {
            _snapshot = _audio.GetSnapshot();
            _devices = _snapshot.Devices;
            if (resetScroll)
            {
                _scrollOffset = 0;
            }

            RebuildLayout();
            if (Visible)
            {
                PositionWindow();
            }

            Invalidate();
        }

        private void TimerTick(object sender, EventArgs e)
        {
            if (!Visible || _dragging || _snapshot == null)
            {
                return;
            }

            float oldVolume = _snapshot.Volume;
            bool oldMuted = _snapshot.Muted;
            _audio.RefreshVolume(_snapshot);
            if (Math.Abs(oldVolume - _snapshot.Volume) > 0.003f || oldMuted != _snapshot.Muted)
            {
                InvalidateVolume();
            }
        }

        private void InvalidateVolume()
        {
            if (_width > 0)
            {
                Invalidate(new Rectangle(_pad, _volY, _width - _pad * 2, _volH));
            }
        }

        private void ApplyTheme()
        {
            _theme = Theme.Create();
            BackColor = _theme.Background;
            ApplyWindowChrome();
        }

        private void ApplyWindowChrome()
        {
            if (Handle == IntPtr.Zero)
            {
                return;
            }

            try
            {
                int dark = _theme.IsDark ? 1 : 0;
                Interop.DwmSetWindowAttribute(Handle, 20, ref dark, 4);
                int corner = 2;
                Interop.DwmSetWindowAttribute(Handle, 33, ref corner, 4);
            }
            catch
            {
            }
        }

        private void UpdateScale(Screen screen)
        {
            float detected = 1f;
            try
            {
                Point point = Cursor.Position;
                IntPtr monitor = Interop.MonitorFromPoint(new Interop.POINT(point.X, point.Y), 2);
                uint dpiX;
                uint dpiY;
                if (Interop.GetDpiForMonitor(monitor, 0, out dpiX, out dpiY) == 0 && dpiX > 0)
                {
                    detected = dpiX / 96f;
                }
            }
            catch
            {
            }

            if (detected < 1f)
            {
                detected = 1f;
            }

            if (detected > 2.5f)
            {
                detected = 2.5f;
            }

            _scale = detected;
        }

        private int S(int value)
        {
            int scaled = (int)Math.Round(value * _scale);
            return scaled < 1 ? 1 : scaled;
        }

        private int S(float value)
        {
            int scaled = (int)Math.Round(value * _scale);
            return scaled < 1 ? 1 : scaled;
        }

        private int EstimateHeight(int rows)
        {
            int listHeight = rows <= 0
                ? S(76)
                : rows * S(58) + (rows - 1) * S(6);
            return S(14) + S(40) + S(10) + S(80) + S(10) + S(116) + S(16) + S(20) + S(8) +
                   listHeight + S(10) + S(36) + S(12);
        }

        private void RebuildLayout()
        {
            int count = _devices == null ? 0 : _devices.Count;
            int maxAvailableHeight = _screen == null
                ? 1000
                : _screen.WorkingArea.Height - S(16);
            int maxRows = Math.Min(5, count);
            while (maxRows > 1 && EstimateHeight(maxRows) > maxAvailableHeight)
            {
                maxRows--;
            }

            _visibleRows = count == 0 ? 0 : maxRows;
            int maxScroll = Math.Max(0, count - _visibleRows);
            if (_scrollOffset > maxScroll)
            {
                _scrollOffset = maxScroll;
            }

            if (_scrollOffset < 0)
            {
                _scrollOffset = 0;
            }

            _pad = S(14);
            _width = S(384);
            int headerTop = S(14);
            int headerHeight = S(40);
            _nowY = headerTop + headerHeight + S(10);
            _nowH = S(80);
            _volY = _nowY + _nowH + S(10);
            _volH = S(116);
            _secY = _volY + _volH + S(16);
            _secH = S(20);
            _listY = _secY + _secH + S(8);
            _rowH = S(58);
            _rowGap = S(6);

            int listHeight;
            if (_visibleRows == 0)
            {
                listHeight = S(76);
            }
            else
            {
                listHeight = _visibleRows * _rowH + (_visibleRows - 1) * _rowGap;
            }

            _listH = listHeight;
            _footerTop = _listY + _listH + S(10);
            int height = _footerTop + S(36) + S(12);

            _closeRect = new Rectangle(_width - _pad - S(28), headerTop + S(4), S(28), S(28));
            _muteRect = new Rectangle(_pad + S(16), _volY + S(20), S(38), S(38));
            _sliderTrack = new Rectangle(_pad + S(18), _volY + S(84), _width - _pad * 2 - S(36), S(6));
            _sliderHit = new Rectangle(_sliderTrack.X - S(8), _sliderTrack.Y - S(15),
                _sliderTrack.Width + S(16), S(36));
            _settingsRect = new Rectangle(_width - _pad - S(86), _footerTop + S(4), S(86), S(28));
            _listRect = new Rectangle(_pad, _listY, _width - _pad * 2, _listH);

            _rowRects.Clear();
            bool scrollable = count > _visibleRows;
            int rowWidth = _width - _pad * 2 - (scrollable ? S(8) : 0);
            for (int i = 0; i < _visibleRows; i++)
            {
                int y = _listY + i * (_rowH + _rowGap);
                _rowRects.Add(new Rectangle(_pad, y, rowWidth, _rowH));
            }

            ClientSize = new Size(_width, height);
        }

        private void PositionWindow()
        {
            if (_screen == null)
            {
                return;
            }

            Rectangle work = _screen.WorkingArea;
            int margin = S(10);
            int x = work.Right - Width - margin;
            int y = work.Bottom - Height - margin;
            if (x < work.Left + margin)
            {
                x = work.Left + margin;
            }

            if (y < work.Top + margin)
            {
                y = work.Top + margin;
            }

            Location = new Point(x, y);
        }

        private void CreateFonts()
        {
            string ui = PickFont("Segoe UI Variable Text", "Segoe UI");
            string display = PickFont("Segoe UI Variable Display", ui);

            _fontTitle = new Font(display, 14.5f, FontStyle.Bold, GraphicsUnit.Point);
            _fontSubtitle = new Font(ui, 8.8f, FontStyle.Regular, GraphicsUnit.Point);
            _fontLabel = new Font(ui, 8.8f, FontStyle.Regular, GraphicsUnit.Point);
            _fontDevice = new Font(ui, 10.2f, FontStyle.Bold, GraphicsUnit.Point);
            _fontDeviceSmall = new Font(ui, 8.5f, FontStyle.Regular, GraphicsUnit.Point);
            _fontSection = new Font(ui, 9.3f, FontStyle.Bold, GraphicsUnit.Point);
            _fontPercent = new Font(display, 19f, FontStyle.Bold, GraphicsUnit.Point);
            _fontMuted = new Font(display, 13.5f, FontStyle.Bold, GraphicsUnit.Point);
            _fontFooter = new Font(ui, 8.4f, FontStyle.Regular, GraphicsUnit.Point);
        }

        private static string PickFont(string preferred, string fallback)
        {
            try
            {
                FontFamily[] families = FontFamily.Families;
                for (int i = 0; i < families.Length; i++)
                {
                    if (string.Equals(families[i].Name, preferred, StringComparison.OrdinalIgnoreCase))
                    {
                        return preferred;
                    }
                }
            }
            catch
            {
            }

            return fallback;
        }

        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            ApplyWindowChrome();
            _hotkeyRegistered = Interop.RegisterHotKey(Handle, HotkeyId, ModControl | ModAlt | ModNoRepeat, VkA);
        }

        protected override void OnHandleDestroyed(EventArgs e)
        {
            if (_hotkeyRegistered)
            {
                Interop.UnregisterHotKey(Handle, HotkeyId);
                _hotkeyRegistered = false;
            }

            base.OnHandleDestroyed(e);
        }

        protected override void WndProc(ref Message m)
        {
            if (m.Msg == WmHotkey && m.WParam.ToInt32() == HotkeyId)
            {
                EventHandler handler = ToggleRequested;
                if (handler != null)
                {
                    handler(this, EventArgs.Empty);
                }

                return;
            }

            base.WndProc(ref m);
        }

        protected override void OnVisibleChanged(EventArgs e)
        {
            base.OnVisibleChanged(e);
            if (Visible)
            {
                _timer.Start();
            }
            else
            {
                _timer.Stop();
                LastHiddenTick = Environment.TickCount;
                _hover = HitNone;
                _dragging = false;
                Capture = false;
                Invalidate();
                EventHandler handler = StateChanged;
                if (handler != null)
                {
                    handler(this, EventArgs.Empty);
                }
            }
        }

        protected override void OnDeactivate(EventArgs e)
        {
            base.OnDeactivate(e);
            if (Visible)
            {
                HidePanel();
            }
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            if (e.CloseReason == CloseReason.UserClosing)
            {
                e.Cancel = true;
                HidePanel();
                return;
            }

            base.OnFormClosing(e);
        }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Escape)
            {
                HidePanel();
                e.Handled = true;
                return;
            }

            base.OnKeyDown(e);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.PixelOffsetMode = PixelOffsetMode.HighQuality;
            g.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;
            g.Clear(_theme.Background);

            DrawHeader(g);
            DrawNowCard(g);
            DrawVolumeCard(g);
            DrawSection(g);
            DrawDeviceList(g);
            DrawFooter(g);
            DrawWindowBorder(g);
        }

        private void DrawHeader(Graphics g)
        {
            RectangleF logo = new RectangleF(_pad, S(18), S(32), S(32));
            IconPainter.DrawLogo(g, logo, _theme.Accent, _theme.AccentText);

            RectangleF titleRect = new RectangleF(_pad + S(44), S(12), _width - _pad * 2 - S(86), S(26));
            DrawText(g, "声控台", _fontTitle, _theme.Text, titleRect, StringAlignment.Near);
            RectangleF subtitleRect = new RectangleF(_pad + S(44), S(36), _width - _pad * 2 - S(86), S(18));
            DrawText(g, "输出设备与系统音量", _fontSubtitle, _theme.TextSecondary, subtitleRect,
                StringAlignment.Near);

            if (_hover == HitClose)
            {
                FillRounded(g, _closeRect, S(7), _theme.Hover);
            }

            RectangleF closeGlyph = new RectangleF(_closeRect.X + S(7), _closeRect.Y + S(7),
                _closeRect.Width - S(14), _closeRect.Height - S(14));
            IconPainter.DrawClose(g, closeGlyph, _theme.TextSecondary);
        }

        private void DrawNowCard(Graphics g)
        {
            Rectangle card = new Rectangle(_pad, _nowY, _width - _pad * 2, _nowH);
            DrawCard(g, card, _theme.Card, Color.Empty);

            AudioDevice current = FindDevice(_snapshot.DefaultDeviceId);
            DeviceIconKind kind = IconPainter.KindFor(current);
            RectangleF iconBox = new RectangleF(card.X + S(16), card.Y + S(21), S(34), S(34));
            FillRounded(g, iconBox, S(10), _theme.AccentSoft);
            RectangleF glyph = new RectangleF(iconBox.X + S(7), iconBox.Y + S(7), S(20), S(20));
            IconPainter.DrawDeviceIcon(g, glyph, kind, _theme.Accent);

            RectangleF label = new RectangleF(card.X + S(62), card.Y + S(11),
                card.Width - S(78), S(18));
            DrawText(g, "当前输出设备", _fontLabel, _theme.TextSecondary, label, StringAlignment.Near);

            string name = current == null ? "未选择输出设备" : current.Name;
            RectangleF nameRect = new RectangleF(card.X + S(62), card.Y + S(29),
                card.Width - S(78), S(24));
            DrawText(g, name, _fontDevice, _theme.Text, nameRect, StringAlignment.Near);

            string description = current == null
                ? "打开声音设置以选择输出设备"
                : current.Description;
            RectangleF descRect = new RectangleF(card.X + S(62), card.Y + S(53),
                card.Width - S(78), S(17));
            DrawText(g, description, _fontDeviceSmall, _theme.TextSecondary, descRect,
                StringAlignment.Near);
        }

        private void DrawVolumeCard(Graphics g)
        {
            Rectangle card = new Rectangle(_pad, _volY, _width - _pad * 2, _volH);
            DrawCard(g, card, _theme.Card, Color.Empty);

            bool muted = _snapshot.Muted;
            Color iconColor = muted ? _theme.Danger : _theme.Accent;
            if (_hover == HitMute)
            {
                FillRounded(g, _muteRect, S(19), _theme.Hover);
            }
            else
            {
                FillRounded(g, _muteRect, S(19), _theme.AccentSoft);
            }

            RectangleF muteGlyph = new RectangleF(_muteRect.X + S(9), _muteRect.Y + S(9), S(20), S(20));
            IconPainter.DrawSpeaker(g, muteGlyph, iconColor, muted, true);

            RectangleF label = new RectangleF(card.X + S(68), card.Y + S(16),
                card.Width - S(150), S(18));
            DrawText(g, "系统音量", _fontLabel, _theme.TextSecondary, label, StringAlignment.Near);

            string valueText;
            Font valueFont;
            Color valueColor;
            if (muted)
            {
                valueText = "静音";
                valueFont = _fontMuted;
                valueColor = _theme.TextSecondary;
            }
            else
            {
                valueText = string.Format("{0}%", (int)Math.Round(_snapshot.Volume * 100f));
                valueFont = _fontPercent;
                valueColor = _theme.Text;
            }

            RectangleF valueRect = new RectangleF(card.X + S(120), card.Y + S(7),
                card.Width - S(138), S(38));
            DrawText(g, valueText, valueFont, valueColor, valueRect, StringAlignment.Far);

            Rectangle track = _sliderTrack;
            FillRounded(g, track, S(3), _theme.SliderTrack);
            float fraction = _snapshot.Volume;
            int fillWidth = (int)Math.Round(track.Width * fraction);
            if (fillWidth > 0)
            {
                Rectangle fill = new Rectangle(track.X, track.Y, fillWidth, track.Height);
                FillRounded(g, fill, S(3), muted ? _theme.TextSecondary : _theme.Accent);
            }

            float knobRadius = (_hover == HitSlider || _dragging) ? S(9) : S(8);
            float knobX = track.X + track.Width * fraction;
            float knobY = track.Y + track.Height / 2f;
            using (SolidBrush knobBrush = new SolidBrush(_theme.Card))
            {
                g.FillEllipse(knobBrush, knobX - knobRadius, knobY - knobRadius,
                    knobRadius * 2f, knobRadius * 2f);
            }

            using (Pen knobPen = new Pen(muted ? _theme.TextSecondary : _theme.Accent,
                       Math.Max(1.5f, S(2))))
            {
                g.DrawEllipse(knobPen, knobX - knobRadius, knobY - knobRadius,
                    knobRadius * 2f, knobRadius * 2f);
            }
        }

        private void DrawSection(Graphics g)
        {
            RectangleF section = new RectangleF(_pad + S(4), _secY, S(200), _secH);
            DrawText(g, "输出设备", _fontSection, _theme.Text, section, StringAlignment.Near);

            int count = _devices == null ? 0 : _devices.Count;
            string countText = string.Format("{0} 个", count);
            RectangleF countRect = new RectangleF(_width - _pad - S(170), _secY, S(166), _secH);
            DrawText(g, countText, _fontDeviceSmall, _theme.TextSecondary, countRect, StringAlignment.Far);
        }

        private void DrawDeviceList(Graphics g)
        {
            int count = _devices == null ? 0 : _devices.Count;
            if (count == 0)
            {
                DrawEmptyState(g);
                return;
            }

            for (int i = 0; i < _rowRects.Count; i++)
            {
                int index = _scrollOffset + i;
                if (index >= count)
                {
                    break;
                }

                AudioDevice device = _devices[index];
                Rectangle row = _rowRects[i];
                bool selected = device.IsDefault;
                bool hovered = _hover == HitDeviceBase + i;

                Color fill = selected ? _theme.RowSelected : _theme.Card;
                if (hovered && !selected)
                {
                    fill = _theme.Hover;
                }

                DrawCard(g, row, fill, selected ? _theme.RowSelectedBorder : Color.Empty);

                DeviceIconKind kind = IconPainter.KindFor(device);
                RectangleF iconRect = new RectangleF(row.X + S(14), row.Y + S(15), S(28), S(28));
                IconPainter.DrawDeviceIcon(g, iconRect, kind,
                    selected ? _theme.Accent : _theme.TextSecondary);

                RectangleF nameRect = new RectangleF(row.X + S(54), row.Y + S(9),
                    row.Width - S(54) - S(44), S(22));
                DrawText(g, device.Name, _fontDevice, _theme.Text, nameRect, StringAlignment.Near);

                RectangleF descRect = new RectangleF(row.X + S(54), row.Y + S(30),
                    row.Width - S(54) - S(44), S(17));
                DrawText(g, device.Description, _fontDeviceSmall, _theme.TextSecondary, descRect,
                    StringAlignment.Near);

                float radioSize = S(18);
                RectangleF radio = new RectangleF(row.Right - S(28) - radioSize / 2f,
                    row.Y + row.Height / 2f - radioSize / 2f, radioSize, radioSize);

                if (selected)
                {
                    using (SolidBrush brush = new SolidBrush(_theme.Accent))
                    {
                        g.FillEllipse(brush, radio);
                    }

                    float dot = S(6);
                    using (SolidBrush brush = new SolidBrush(_theme.AccentText))
                    {
                        g.FillEllipse(brush, radio.X + radio.Width / 2f - dot / 2f,
                            radio.Y + radio.Height / 2f - dot / 2f, dot, dot);
                    }
                }
                else
                {
                    Color borderColor = hovered ? _theme.Accent : Color.FromArgb(170, _theme.TextSecondary);
                    using (Pen pen = new Pen(borderColor, Math.Max(1.3f, S(1.5f))))
                    {
                        g.DrawEllipse(pen, radio);
                    }
                }
            }

            if (count > _visibleRows)
            {
                DrawScrollbar(g, count);
            }
        }

        private void DrawScrollbar(Graphics g, int count)
        {
            int trackX = _width - _pad - S(3);
            int trackTop = _listY + S(8);
            int trackHeight = _listH - S(16);
            FillRounded(g, new RectangleF(trackX, trackTop, S(3), trackHeight), S(1.5f),
                Color.FromArgb(70, _theme.TextSecondary));

            int maxScroll = Math.Max(1, count - _visibleRows);
            int thumbHeight = Math.Max(S(24), (int)((float)trackHeight * _visibleRows / count));
            int thumbY = trackTop + (trackHeight - thumbHeight) * _scrollOffset / maxScroll;
            FillRounded(g, new RectangleF(trackX, thumbY, S(3), thumbHeight), S(1.5f),
                Color.FromArgb(150, _theme.TextSecondary));
        }

        private void DrawEmptyState(Graphics g)
        {
            DrawCard(g, _listRect, _theme.Card, Color.Empty);
            bool error = _snapshot != null && !string.IsNullOrEmpty(_snapshot.Error);
            string title = error ? "音频服务暂时不可用" : "未找到输出设备";
            string subtitle = error ? "单击此处重试" : "连接音频设备后单击重试";
            Color titleColor = _theme.Text;

            RectangleF titleRect = new RectangleF(_listRect.X + S(16), _listRect.Y + S(16),
                _listRect.Width - S(32), S(24));
            DrawText(g, title, _fontDevice, titleColor, titleRect, StringAlignment.Center);
            RectangleF subtitleRect = new RectangleF(_listRect.X + S(16), _listRect.Y + S(42),
                _listRect.Width - S(32), S(18));
            DrawText(g, subtitle, _fontDeviceSmall, _theme.Accent, subtitleRect, StringAlignment.Center);
        }

        private void DrawFooter(Graphics g)
        {
            string hint = _hotkeyRegistered ? "Ctrl+Alt+A  打开面板" : "单击托盘图标打开面板";
            RectangleF hintRect = new RectangleF(_pad + S(4), _footerTop, _width - _pad * 2 - S(100),
                S(36));
            DrawText(g, hint, _fontFooter, _theme.TextSecondary, hintRect, StringAlignment.Near);

            string link = "声音设置";
            RectangleF linkRect = new RectangleF(_settingsRect.X, _settingsRect.Y,
                _settingsRect.Width, _settingsRect.Height);
            DrawText(g, link, _fontFooter, _theme.Accent, linkRect, StringAlignment.Far);

            if (_hover == HitSettings)
            {
                SizeF size = g.MeasureString(link, _fontFooter);
                float x = linkRect.Right - size.Width;
                float y = linkRect.Y + linkRect.Height / 2f + S(6);
                using (Pen pen = new Pen(_theme.Accent, Math.Max(1f, S(1))))
                {
                    g.DrawLine(pen, x, y, linkRect.Right, y);
                }
            }
        }

        private void DrawWindowBorder(Graphics g)
        {
            RectangleF rect = new RectangleF(0.5f, 0.5f, ClientSize.Width - 1f, ClientSize.Height - 1f);
            using (GraphicsPath path = IconPainter.RoundedRect(rect, S(8)))
            using (Pen pen = new Pen(_theme.Border, Math.Max(1f, S(1))))
            {
                g.DrawPath(pen, path);
            }
        }

        private void DrawCard(Graphics g, Rectangle rect, Color fill, Color border)
        {
            using (GraphicsPath path = IconPainter.RoundedRect(rect, S(11)))
            using (SolidBrush brush = new SolidBrush(fill))
            {
                g.FillPath(brush, path);
                if (border != Color.Empty)
                {
                    using (Pen pen = new Pen(border, Math.Max(1f, S(1))))
                    {
                        g.DrawPath(pen, path);
                    }
                }
            }
        }

        private void FillRounded(Graphics g, RectangleF rect, float radius, Color color)
        {
            using (GraphicsPath path = IconPainter.RoundedRect(rect, radius))
            using (SolidBrush brush = new SolidBrush(color))
            {
                g.FillPath(brush, path);
            }
        }

        private static void DrawText(Graphics g, string text, Font font, Color color, RectangleF rect,
            StringAlignment align)
        {
            using (StringFormat format = new StringFormat())
            {
                format.Alignment = align;
                format.LineAlignment = StringAlignment.Center;
                format.Trimming = StringTrimming.EllipsisCharacter;
                format.FormatFlags = StringFormatFlags.NoWrap;
                using (SolidBrush brush = new SolidBrush(color))
                {
                    g.DrawString(text, font, brush, rect, format);
                }
            }
        }

        private AudioDevice FindDevice(string id)
        {
            if (string.IsNullOrEmpty(id) || _devices == null)
            {
                return null;
            }

            for (int i = 0; i < _devices.Count; i++)
            {
                if (string.Equals(_devices[i].Id, id, StringComparison.OrdinalIgnoreCase))
                {
                    return _devices[i];
                }
            }

            return null;
        }

        private int HitTest(Point point)
        {
            if (_closeRect.Contains(point))
            {
                return HitClose;
            }

            if (_muteRect.Contains(point))
            {
                return HitMute;
            }

            if (_sliderHit.Contains(point))
            {
                return HitSlider;
            }

            if (_settingsRect.Contains(point))
            {
                return HitSettings;
            }

            for (int i = 0; i < _rowRects.Count; i++)
            {
                if (_rowRects[i].Contains(point))
                {
                    return HitDeviceBase + i;
                }
            }

            if (_devices == null || _devices.Count == 0)
            {
                if (_listRect.Contains(point))
                {
                    return HitRetry;
                }
            }

            return HitNone;
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            if (_dragging)
            {
                SetVolumeFromPoint(e.X);
                return;
            }

            int hit = HitTest(e.Location);
            if (hit != _hover)
            {
                _hover = hit;
                if (hit == HitSlider)
                {
                    InvalidateVolume();
                }
                else
                {
                    Invalidate();
                }
            }

            Cursor = hit == HitNone ? Cursors.Default : Cursors.Hand;
        }

        protected override void OnMouseLeave(EventArgs e)
        {
            base.OnMouseLeave(e);
            if (!_dragging && _hover != HitNone)
            {
                _hover = HitNone;
                Invalidate();
            }
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);
            if (e.Button != MouseButtons.Left)
            {
                return;
            }

            int hit = HitTest(e.Location);
            if (hit == HitClose)
            {
                HidePanel();
                return;
            }

            if (hit == HitRetry)
            {
                LoadSnapshot(false);
                return;
            }

            if (hit == HitSettings)
            {
                HidePanel();
                TrayContext.OpenSoundSettings();
                return;
            }

            if (_snapshot == null || string.IsNullOrEmpty(_snapshot.DefaultDeviceId))
            {
                return;
            }

            if (hit == HitMute)
            {
                ToggleMute();
                return;
            }

            if (hit == HitSlider)
            {
                _dragging = true;
                Capture = true;
                SetVolumeFromPoint(e.X);
                return;
            }

            if (hit >= HitDeviceBase)
            {
                int index = _scrollOffset + (hit - HitDeviceBase);
                if (index >= 0 && index < _devices.Count)
                {
                    SelectDevice(index);
                }
            }
        }

        protected override void OnMouseUp(MouseEventArgs e)
        {
            base.OnMouseUp(e);
            if (e.Button == MouseButtons.Left && _dragging)
            {
                _dragging = false;
                Capture = false;
                RaiseStateChanged();
            }
        }

        protected override void OnMouseWheel(MouseEventArgs e)
        {
            base.OnMouseWheel(e);
            int count = _devices == null ? 0 : _devices.Count;
            if (count > _visibleRows && _listRect.Contains(e.Location))
            {
                int next = _scrollOffset + (e.Delta > 0 ? -1 : 1);
                int max = Math.Max(0, count - _visibleRows);
                if (next < 0)
                {
                    next = 0;
                }

                if (next > max)
                {
                    next = max;
                }

                if (next != _scrollOffset)
                {
                    _scrollOffset = next;
                    _hover = HitNone;
                    Invalidate();
                }

                return;
            }

            if (_snapshot == null || string.IsNullOrEmpty(_snapshot.DefaultDeviceId))
            {
                return;
            }

            float delta = e.Delta > 0 ? 0.02f : -0.02f;
            SetVolumeValue(_snapshot.Volume + delta);
            RaiseStateChanged();
        }

        private void SetVolumeFromPoint(int x)
        {
            if (_sliderTrack.Width <= 0)
            {
                return;
            }

            float fraction = (x - _sliderTrack.X) / (float)_sliderTrack.Width;
            if (fraction < 0f)
            {
                fraction = 0f;
            }

            if (fraction > 1f)
            {
                fraction = 1f;
            }

            SetVolumeValue(fraction);
        }

        private void SetVolumeValue(float value)
        {
            if (_snapshot == null || string.IsNullOrEmpty(_snapshot.DefaultDeviceId))
            {
                return;
            }

            if (value < 0f)
            {
                value = 0f;
            }

            if (value > 1f)
            {
                value = 1f;
            }

            float previous = _snapshot.Volume;
            bool wasMuted = _snapshot.Muted;
            if (!wasMuted && Math.Abs(previous - value) < 0.001f)
            {
                return;
            }

            try
            {
                if (_snapshot.Muted)
                {
                    _audio.SetMute(false);
                    _snapshot.Muted = false;
                }

                _audio.SetVolume(value);
                _snapshot.Volume = value;
                InvalidateVolume();
                SoundFeedback.VolumeTick();
            }
            catch
            {
            }
        }

        private void ToggleMute()
        {
            if (_snapshot == null || string.IsNullOrEmpty(_snapshot.DefaultDeviceId))
            {
                return;
            }

            try
            {
                bool muted = !_snapshot.Muted;
                _audio.SetMute(muted);
                _snapshot.Muted = muted;
                if (!muted && _snapshot.Volume < 0.005f)
                {
                    _audio.SetVolume(0.2f);
                    _snapshot.Volume = 0.2f;
                }

                InvalidateVolume();
                RaiseStateChanged();
                if (!muted)
                {
                    SoundFeedback.VolumeTick(true);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, "无法切换静音状态。\r\n" + ex.Message, "声控台",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }

        private void SelectDevice(int index)
        {
            AudioDevice device = _devices[index];
            if (device.IsDefault)
            {
                return;
            }

            try
            {
                _audio.SetDefaultDevice(device.Id);
                _snapshot = _audio.GetSnapshot();
                _devices = _snapshot.Devices;
                RebuildLayout();
                PositionWindow();
                _hover = HitNone;
                Invalidate();
                RaiseStateChanged();
                SoundFeedback.DeviceSwitched();
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, "无法切换输出设备。\r\n" + ex.Message, "声控台",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }

        private void RaiseStateChanged()
        {
            EventHandler handler = StateChanged;
            if (handler != null)
            {
                handler(this, EventArgs.Empty);
            }
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                if (_timer != null)
                {
                    _timer.Stop();
                    _timer.Dispose();
                }

                if (_fontTitle != null) _fontTitle.Dispose();
                if (_fontSubtitle != null) _fontSubtitle.Dispose();
                if (_fontLabel != null) _fontLabel.Dispose();
                if (_fontDevice != null) _fontDevice.Dispose();
                if (_fontDeviceSmall != null) _fontDeviceSmall.Dispose();
                if (_fontSection != null) _fontSection.Dispose();
                if (_fontPercent != null) _fontPercent.Dispose();
                if (_fontMuted != null) _fontMuted.Dispose();
                if (_fontFooter != null) _fontFooter.Dispose();
            }

            base.Dispose(disposing);
        }
    }
}
