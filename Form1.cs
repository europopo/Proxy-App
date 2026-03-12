using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Security.Principal;
using System.Windows.Forms;
using AntdUI;
using AntButton = AntdUI.Button;
using AntMessage = AntdUI.Message;
using WinCheckBox = System.Windows.Forms.CheckBox;
using WinContextMenuStrip = System.Windows.Forms.ContextMenuStrip;
using WinLabel = System.Windows.Forms.Label;
using WinNotifyIcon = System.Windows.Forms.NotifyIcon;
using WinPanel = System.Windows.Forms.Panel;
using WinTimer = System.Windows.Forms.Timer;
using WinToolStripMenuItem = System.Windows.Forms.ToolStripMenuItem;

namespace ProxyApp;

public class Form1 : AntdUI.Window
{
    [DllImport("user32.dll")]
    private static extern bool ReleaseCapture();

    [DllImport("user32.dll")]
    private static extern IntPtr SendMessage(IntPtr hWnd, int msg, int wParam, int lParam);

    private const int WM_NCLBUTTONDOWN = 0xA1;
    private const int HTCAPTION = 0x2;

    private readonly WinPanel _header;
    private readonly WinLabel _headerTitle;
    private readonly WinLabel _headerDescription;
    private readonly WinPanel _windowActions;
    private readonly AntButton _minimizeButton;
    private readonly AntButton _closeButton;
    private readonly WinPanel _container;
    private readonly WinPanel _selectRow;
    private readonly Select _pacSelect;
    private readonly AntButton _configButton;
    private readonly AntButton _applyButton;
    private readonly WinLabel _statusLabel;
    private readonly Switch _proxySwitch;
    private readonly WinCheckBox _autoStartCheckBox;
    private readonly WinTimer _proxyMonitorTimer;
    private readonly WinNotifyIcon _trayIcon;
    private readonly WinContextMenuStrip _trayMenu;

    private readonly List<PacEntry> _pacEntries = new();
    private bool _isInitializing;
    private bool _isEnforcingProxy;
    private bool _trayHintShown;
    private string _managedPacUrl = string.Empty;
    private readonly bool _startInTray;

    public Form1(bool startInTray = false)
    {
        Text = "系统代理助手";
        StartPosition = FormStartPosition.CenterScreen;
        MinimumSize = new Size(760, 360);
        Size = new Size(920, 460);
        _startInTray = startInTray;

        _header = new WinPanel
        {
            Dock = DockStyle.Top,
            Height = 76,
            Padding = new Padding(24, 16, 24, 8),
            Cursor = Cursors.SizeAll
        };
        _header.MouseDown += (_, e) => HandleHeaderDrag(e);

        _windowActions = new WinPanel
        {
            Dock = DockStyle.Right,
            Width = 96,
            Height = 30
        };

        _closeButton = new AntButton { Text = "✕", Dock = DockStyle.Right, Width = 44 };
        _closeButton.Click += (_, _) => ExitApplication();

        _minimizeButton = new AntButton { Text = "—", Dock = DockStyle.Right, Width = 44 };
        _minimizeButton.Click += (_, _) => MinimizeToTray();

        _windowActions.Controls.Add(_closeButton);
        _windowActions.Controls.Add(_minimizeButton);

        _headerTitle = new WinLabel
        {
            Dock = DockStyle.Top,
            Height = 30,
            Font = new Font("Microsoft YaHei UI", 14F, FontStyle.Bold),
            Text = "系统代理助手",
            Cursor = Cursors.SizeAll
        };
        _headerTitle.MouseDown += (_, e) => HandleHeaderDrag(e);

        _headerDescription = new WinLabel
        {
            Dock = DockStyle.Fill,
            Font = new Font("Microsoft YaHei UI", 9F, FontStyle.Regular),
            ForeColor = Color.DimGray,
            Text = "管理和快速切换 Windows PAC 代理脚本（主界面仅允许选择）",
            Cursor = Cursors.SizeAll
        };
        _headerDescription.MouseDown += (_, e) => HandleHeaderDrag(e);

        _header.Controls.Add(_headerDescription);
        _header.Controls.Add(_headerTitle);
        _header.Controls.Add(_windowActions);

        _container = new WinPanel { Dock = DockStyle.Fill, Padding = new Padding(24) };

        _selectRow = new WinPanel { Dock = DockStyle.Top, Height = 54 };

        _pacSelect = new Select { Dock = DockStyle.Fill };
        _pacSelect.KeyPress += (_, e) => e.Handled = true;
        _pacSelect.KeyDown += (_, e) => e.SuppressKeyPress = true;

        _configButton = new AntButton { Text = "配置脚本", Dock = DockStyle.Right, Width = 120 };
        _configButton.Click += (_, _) => OpenConfigPage();

        _applyButton = new AntButton
        {
            Text = "应用",
            Dock = DockStyle.Right,
            Width = 120,
            Type = TTypeMini.Primary
        };
        _applyButton.Click += (_, _) => ApplyProxyFromSelected();

        _selectRow.Controls.Add(_pacSelect);
        _selectRow.Controls.Add(_configButton);
        _selectRow.Controls.Add(_applyButton);

        _statusLabel = new WinLabel
        {
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleLeft,
            Font = new Font("Microsoft YaHei UI", 10F, FontStyle.Bold),
            Text = "当前状态：未知"
        };

        _proxySwitch = new Switch
        {
            Dock = DockStyle.Right,
            Width = 80,
            CheckedText = "开启",
            UnCheckedText = "关闭"
        };
        _proxySwitch.CheckedChanged += (_, _) => ProxySwitch_CheckedChanged();

        _autoStartCheckBox = new WinCheckBox
        {
            Dock = DockStyle.Right,
            Width = 104,
            Text = "开机自启动",
            TextAlign = ContentAlignment.MiddleLeft,
            CheckAlign = ContentAlignment.MiddleLeft,
            Font = new Font("Microsoft YaHei UI", 8.8F, FontStyle.Regular)
        };
        _autoStartCheckBox.CheckedChanged += (_, _) => AutoStartCheckBox_CheckedChanged();

        var bottomBar = new WinPanel { Dock = DockStyle.Bottom, Height = 54, Padding = new Padding(0, 14, 0, 0) };
        bottomBar.Controls.Add(_statusLabel);
        bottomBar.Controls.Add(_autoStartCheckBox);
        bottomBar.Controls.Add(_proxySwitch);

        _container.Controls.Add(bottomBar);
        _container.Controls.Add(_selectRow);

        Controls.Add(_container);
        Controls.Add(_header);

        _proxyMonitorTimer = new WinTimer { Interval = 5000 };
        _proxyMonitorTimer.Tick += (_, _) => EnforceManagedProxyIfChanged();

        _trayMenu = new WinContextMenuStrip();
        _trayMenu.Items.Add(new WinToolStripMenuItem("退出程序", null, (_, _) => ExitApplication()));

        Icon = GetTrayIcon();

        _trayIcon = new WinNotifyIcon
        {
            Icon = this.Icon,
            Text = "系统代理助手",
            Visible = false,
            ContextMenuStrip = _trayMenu
        };
        _trayIcon.DoubleClick += (_, _) => RestoreFromTray();

        Load += Form1_Load;
        Resize += Form1_Resize;
        FormClosed += (_, _) => CleanupResources();
    }


    private Icon GetTrayIcon()
    {
        try
        {
            Icon? exeIcon = Icon.ExtractAssociatedIcon(Application.ExecutablePath);
            if (exeIcon is not null)
            {
                return exeIcon;
            }

            string icoPath = Path.Combine(AppContext.BaseDirectory, "proxy.ico");
            if (File.Exists(icoPath))
            {
                return new Icon(icoPath);
            }

            return Icon ?? SystemIcons.Application;
        }
        catch
        {
            return Icon ?? SystemIcons.Application;
        }
    }

    private void HandleHeaderDrag(MouseEventArgs e)
    {
        if (e.Button != MouseButtons.Left) return;
        ReleaseCapture();
        SendMessage(Handle, WM_NCLBUTTONDOWN, HTCAPTION, 0);
    }

    private void Form1_Load(object? sender, EventArgs e)
    {
        try
        {
            EnsurePrivilegeHint();
            LoadPacHistory();
            LoadCurrentState();
            LoadAutoStartState();
            _proxyMonitorTimer.Start();

            if (_startInTray)
            {
                BeginInvoke(new Action(() => MinimizeToTray(showTip: false)));
            }
        }
        catch (Exception ex)
        {
            AntMessage.error(this, $"初始化失败：{ex.Message}");
        }
    }

    private void Form1_Resize(object? sender, EventArgs e)
    {
        if (WindowState == FormWindowState.Minimized) MinimizeToTray();
    }

    private void MinimizeToTray(bool showTip = true)
    {
        Hide();
        _trayIcon.Visible = true;

        if (!showTip || _trayHintShown) return;

        _trayIcon.ShowBalloonTip(1500, "系统代理助手", "程序已最小化到托盘，右击图标可退出。", ToolTipIcon.Info);
        _trayHintShown = true;
    }

    private void RestoreFromTray()
    {
        Show();
        WindowState = FormWindowState.Normal;
        Activate();
        _trayIcon.Visible = false;
    }

    private void ExitApplication()
    {
        _trayIcon.Visible = false;
        Close();
    }

    private void CleanupResources()
    {
        _proxyMonitorTimer.Stop();
        _trayIcon.Visible = false;
        _trayIcon.Dispose();
        _trayMenu.Dispose();
    }

    private void LoadAutoStartState()
    {
        _isInitializing = true;
        try
        {
            _autoStartCheckBox.Checked = StartupManager.IsEnabled();
        }
        finally
        {
            _isInitializing = false;
        }
    }

    private void AutoStartCheckBox_CheckedChanged()
    {
        if (_isInitializing) return;

        try
        {
            StartupManager.SetEnabled(_autoStartCheckBox.Checked);
            AntMessage.success(this, _autoStartCheckBox.Checked ? "已开启开机自启动" : "已关闭开机自启动");
        }
        catch (Exception ex)
        {
            _isInitializing = true;
            _autoStartCheckBox.Checked = !_autoStartCheckBox.Checked;
            _isInitializing = false;
            AntMessage.error(this, $"设置开机自启动失败：{ex.Message}");
        }
    }

    private void OpenConfigPage()
    {
        using var configForm = new PacConfigForm();
        configForm.ShowDialog(this);
        LoadPacHistory();
    }

    private void EnsurePrivilegeHint()
    {
        using WindowsIdentity identity = WindowsIdentity.GetCurrent();
        WindowsPrincipal principal = new(identity);

        if (!principal.IsInRole(WindowsBuiltInRole.Administrator))
        {
            AntMessage.warn(this, "当前未以管理员身份运行。通常修改当前用户代理设置不受影响，如遇受策略限制请尝试管理员身份启动。");
        }
    }

    private void LoadPacHistory()
    {
        var history = PacHistoryStore.Load();
        _pacEntries.Clear();
        _pacEntries.AddRange(history);

        _pacSelect.Items.Clear();
        foreach (PacEntry entry in _pacEntries)
        {
            _pacSelect.Items.Add(new SelectItem(entry.Name, entry.Name));
        }

        if (_pacEntries.Count > 0)
        {
            _pacSelect.Text = _pacEntries[0].Name;
        }
    }

    private void LoadCurrentState()
    {
        _isInitializing = true;
        try
        {
            var (enabled, pacUrl) = global::ProxyApp.ProxyManager.GetCurrentProxyState();
            _proxySwitch.Checked = enabled;

            if (!string.IsNullOrWhiteSpace(pacUrl))
            {
                _managedPacUrl = pacUrl.Trim();
                PacEntry? entry = _pacEntries.FirstOrDefault(x => string.Equals(x.Url, _managedPacUrl, StringComparison.OrdinalIgnoreCase));
                if (entry is not null)
                {
                    _pacSelect.Text = entry.Name;
                }
            }

            UpdateStatus(enabled, pacUrl);
        }
        finally
        {
            _isInitializing = false;
        }
    }

    private void ProxySwitch_CheckedChanged()
    {
        if (_isInitializing) return;

        bool enabled = _proxySwitch.Checked;
        try
        {
            string pacUrl = GetSelectedPacUrl();
            global::ProxyApp.ProxyManager.SetProxy(enabled, pacUrl);

            if (enabled)
            {
                _managedPacUrl = pacUrl;
                PacEntry? selected = GetSelectedPacEntry();
                if (selected is not null)
                {
                    PacHistoryStore.SaveOrUpdate(selected.Name, selected.Url);
                    LoadPacHistory();
                    _pacSelect.Text = selected.Name;
                }
            }
            else
            {
                _managedPacUrl = string.Empty;
            }

            UpdateStatus(enabled, pacUrl);
            AntMessage.success(this, enabled ? "PAC 代理已开启" : "PAC 代理已关闭");
        }
        catch (Exception ex)
        {
            _isInitializing = true;
            _proxySwitch.Checked = !_proxySwitch.Checked;
            _isInitializing = false;
            AntMessage.error(this, $"切换失败：{ex.Message}");
        }
    }

    private void ApplyProxyFromSelected()
    {
        try
        {
            PacEntry selected = GetSelectedPacEntry() ?? throw new InvalidOperationException("请选择一个脚本名称。");
            global::ProxyApp.ProxyManager.SetProxy(true, selected.Url);
            _managedPacUrl = selected.Url;
            PacHistoryStore.SaveOrUpdate(selected.Name, selected.Url);
            LoadPacHistory();
            _pacSelect.Text = selected.Name;

            _isInitializing = true;
            _proxySwitch.Checked = true;
            _isInitializing = false;

            UpdateStatus(true, selected.Url);
            AntMessage.success(this, "代理脚本已应用");
        }
        catch (Exception ex)
        {
            AntMessage.error(this, $"应用失败：{ex.Message}");
        }
    }

    private PacEntry? GetSelectedPacEntry()
    {
        string selectedName = _pacSelect.Text?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(selectedName)) return null;

        return _pacEntries.FirstOrDefault(x => string.Equals(x.Name, selectedName, StringComparison.OrdinalIgnoreCase));
    }

    private string GetSelectedPacUrl()
    {
        PacEntry? selected = GetSelectedPacEntry();
        if (selected is null)
        {
            throw new InvalidOperationException("主界面仅支持从下拉中选择脚本名称，请先到“配置脚本”页面维护地址。");
        }

        return selected.Url;
    }

    private void EnforceManagedProxyIfChanged()
    {
        if (_isEnforcingProxy || !_proxySwitch.Checked || string.IsNullOrWhiteSpace(_managedPacUrl)) return;

        try
        {
            var (enabled, pacUrl) = global::ProxyApp.ProxyManager.GetCurrentProxyState();
            string normalizedCurrent = pacUrl.Trim();

            if (!enabled || !string.Equals(normalizedCurrent, _managedPacUrl, StringComparison.OrdinalIgnoreCase))
            {
                _isEnforcingProxy = true;
                global::ProxyApp.ProxyManager.SetProxy(true, _managedPacUrl);

                PacEntry? entry = _pacEntries.FirstOrDefault(x => string.Equals(x.Url, _managedPacUrl, StringComparison.OrdinalIgnoreCase));
                if (entry is not null)
                {
                    _pacSelect.Text = entry.Name;
                }

                UpdateStatus(true, _managedPacUrl);
                AntMessage.warn(this, "检测到 PAC 地址被外部修改，已自动恢复为本程序配置地址。");
            }
        }
        catch (Exception ex)
        {
            AntMessage.error(this, $"监听恢复失败：{ex.Message}");
        }
        finally
        {
            _isEnforcingProxy = false;
        }
    }

    private void UpdateStatus(bool enabled, string pacUrl)
    {
        _statusLabel.Text = enabled
            ? $"当前状态：已开启  |  PAC：{pacUrl}  |  监听保护：运行中"
            : "当前状态：已关闭  |  监听保护：已停止";
    }
}
