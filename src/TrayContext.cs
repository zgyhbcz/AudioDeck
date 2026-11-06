using System;
using System.Diagnostics;
using System.Drawing;
using System.Threading;
using System.Windows.Forms;
using AudioDeck.Audio;
using AudioDeck.UI;

namespace AudioDeck
{
    internal sealed class TrayContext : ApplicationContext
    {
        private readonly AudioManager _audio;
        private readonly PanelForm _panel;
        private readonly NotifyIcon _notify;
        private readonly ContextMenuStrip _menu;
        private ToolStripMenuItem _startupItem;
        private readonly EventWaitHandle _showEvent;
        private readonly Thread _listener;
        private readonly Icon _icon;

        private bool _disposed;
        private int _lastToggleTick;

        public TrayContext(EventWaitHandle showEvent)
        {
            _showEvent = showEvent;

            _audio = new AudioManager();
            _audio.Changed += AudioChanged;

            _panel = new PanelForm(_audio);
            _panel.ToggleRequested += PanelToggleRequested;
            _panel.StateChanged += PanelStateChanged;

            _menu = new ContextMenuStrip();
            _menu.ShowImageMargin = false;
            _menu.ShowCheckMargin = true;
            _menu.Font = new Font("Segoe UI", 9.3f, FontStyle.Regular, GraphicsUnit.Point);
            _menu.Padding = new Padding(2, 4, 2, 4);
            _menu.Opening += MenuOpening;
            BuildMenu();

            _icon = IconPainter.CreateTrayIcon(32);
            _notify = new NotifyIcon();
            _notify.Icon = _icon;
            _notify.Text = "声控台";
            _notify.ContextMenuStrip = _menu;
            _notify.MouseUp += NotifyMouseUp;
            _notify.Visible = true;

            _panel.RefreshSnapshotQuiet();
            UpdateTooltip();

            _listener = new Thread(ListenForShow);
            _listener.IsBackground = true;
            _listener.Start();
        }

        private void BuildMenu()
        {
            _menu.Items.Clear();

            ToolStripMenuItem open = new ToolStripMenuItem("打开控制面板");
            open.ShortcutKeyDisplayString = "Ctrl+Alt+A";
            open.Click += OpenMenuClick;
            _menu.Items.Add(open);

            ToolStripMenuItem sound = new ToolStripMenuItem("Windows 声音设置");
            sound.Click += SoundSettingsClick;
            _menu.Items.Add(sound);

            ToolStripMenuItem moreSound = new ToolStripMenuItem("更多声音设置");
            moreSound.Click += ClassicSoundSettingsClick;
            _menu.Items.Add(moreSound);

            _menu.Items.Add(new ToolStripSeparator());

            _startupItem = new ToolStripMenuItem("开机自动启动");
            _startupItem.Click += StartupClick;
            _menu.Items.Add(_startupItem);

            _menu.Items.Add(new ToolStripSeparator());

            ToolStripMenuItem exit = new ToolStripMenuItem("退出声控台");
            exit.Click += ExitClick;
            _menu.Items.Add(exit);
        }

        private void MenuOpening(object sender, System.ComponentModel.CancelEventArgs e)
        {
            Theme theme = Theme.Create();
            _menu.Renderer = new ThemedMenuRenderer(theme);
            _menu.BackColor = theme.Card;
            _menu.ForeColor = theme.Text;
            for (int i = 0; i < _menu.Items.Count; i++)
            {
                _menu.Items[i].ForeColor = theme.Text;
                _menu.Items[i].BackColor = Color.Transparent;
                ToolStripMenuItem item = _menu.Items[i] as ToolStripMenuItem;
                if (item != null)
                {
                    item.Padding = new Padding(0, 3, 0, 3);
                }
            }

            _startupItem.Checked = StartupManager.IsEnabled();
        }

        private void NotifyMouseUp(object sender, MouseEventArgs e)
        {
            if (e.Button != MouseButtons.Left)
            {
                return;
            }

            int now = Environment.TickCount;
            int elapsed = unchecked(now - _lastToggleTick);
            if (elapsed >= 0 && elapsed < 220)
            {
                return;
            }

            _lastToggleTick = now;

            if (_panel.Visible)
            {
                _panel.HidePanel();
                return;
            }

            int hiddenElapsed = unchecked(now - _panel.LastHiddenTick);
            if (hiddenElapsed >= 0 && hiddenElapsed < 220)
            {
                return;
            }

            _panel.ShowPanel();
        }

        private void PanelToggleRequested(object sender, EventArgs e)
        {
            _panel.TogglePanel();
        }

        private void PanelStateChanged(object sender, EventArgs e)
        {
            UpdateTooltip();
        }

        private void AudioChanged(object sender, EventArgs e)
        {
            Post(delegate
            {
                if (_disposed)
                {
                    return;
                }

                if (_panel.Visible)
                {
                    _panel.ReloadDevices();
                }
                else
                {
                    _panel.RefreshSnapshotQuiet();
                }

                UpdateTooltip();
            });
        }

        private void UpdateTooltip()
        {
            if (_disposed || _notify == null)
            {
                return;
            }

            try
            {
                _notify.Text = _panel.TooltipText;
            }
            catch
            {
            }
        }

        private void OpenMenuClick(object sender, EventArgs e)
        {
            _panel.ShowPanel();
        }

        private void SoundSettingsClick(object sender, EventArgs e)
        {
            OpenSoundSettings();
        }

        private void ClassicSoundSettingsClick(object sender, EventArgs e)
        {
            OpenClassicSoundSettings();
        }

        private void StartupClick(object sender, EventArgs e)
        {
            try
            {
                bool enabled = StartupManager.IsEnabled();
                StartupManager.SetEnabled(!enabled);
                _startupItem.Checked = !enabled;
            }
            catch (Exception ex)
            {
                MessageBox.Show("无法修改开机自动启动设置。\r\n" + ex.Message, "声控台",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }

        private void ExitClick(object sender, EventArgs e)
        {
            ExitApplication();
        }

        private void Post(Action action)
        {
            if (_panel == null || _panel.IsDisposed || !_panel.IsHandleCreated)
            {
                return;
            }

            try
            {
                _panel.BeginInvoke(action);
            }
            catch
            {
            }
        }

        private void ListenForShow()
        {
            while (true)
            {
                try
                {
                    if (!_showEvent.WaitOne())
                    {
                        return;
                    }
                }
                catch
                {
                    return;
                }

                if (_disposed)
                {
                    return;
                }

                Post(delegate
                {
                    if (!_disposed)
                    {
                        _panel.ShowPanel();
                    }
                });
            }
        }

        public static void OpenSoundSettings()
        {
            try
            {
                ProcessStartInfo info = new ProcessStartInfo("ms-settings:sound");
                info.UseShellExecute = true;
                Process.Start(info);
            }
            catch
            {
                OpenClassicSoundSettings();
            }
        }

        public static void OpenClassicSoundSettings()
        {
            try
            {
                ProcessStartInfo info = new ProcessStartInfo("mmsys.cpl");
                info.UseShellExecute = true;
                Process.Start(info);
            }
            catch
            {
            }
        }

        private void ExitApplication()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            try
            {
                _notify.Visible = false;
            }
            catch
            {
            }

            _panel.HidePanel();
            _panel.Dispose();
            _audio.Dispose();
            _menu.Dispose();
            _notify.Dispose();
            _icon.Dispose();

            try
            {
                _showEvent.Set();
            }
            catch
            {
            }

            ExitThread();
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing && !_disposed)
            {
                ExitApplication();
            }

            base.Dispose(disposing);
        }
    }
}
