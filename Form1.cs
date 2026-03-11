using System;
using System.Drawing;
using System.Linq;
using System.Security.Principal;
using System.Windows.Forms;
using AntdUI;
using AntButton = AntdUI.Button;
using AntInput = AntdUI.Input;
using AntMessage = AntdUI.Message;
using WinPanel = System.Windows.Forms.Panel;
using WinLabel = System.Windows.Forms.Label;
using WinTimer = System.Windows.Forms.Timer;

namespace ProxyApp;

public class Form1 : AntdUI.Window
{
    private readonly WinPanel _header;
    private readonly WinLabel _headerTitle;
    private readonly WinLabel _headerDescription;
    private readonly WinPanel _container;
    private readonly WinPanel _manualInputRow;
    private readonly AntInput _pacInput;
    private readonly AntButton _addPacButton;
    private readonly WinPanel _selectRow;
    private readonly Select _pacSelect;
    private readonly AntButton _applyButton;
    private readonly WinLabel _statusLabel;
    private readonly Switch _proxySwitch;
    private readonly WinTimer _proxyMonitorTimer;

    private bool _isInitializing;
    private bool _isEnforcingProxy;
    private string _managedPacUrl = string.Empty;

    public Form1()
    {
        Text = "系统代理助手";
        StartPosition = FormStartPosition.CenterScreen;
        MinimumSize = new Size(760, 360);
        Size = new Size(920, 460);

        _header = new WinPanel
        {
            Dock = DockStyle.Top,
            Height = 76,
            Padding = new Padding(24, 16, 24, 8)
        };

        _headerTitle = new WinLabel
        {
            Dock = DockStyle.Top,
            Height = 30,
            Font = new Font("Microsoft YaHei UI", 14F, FontStyle.Bold),
            Text = "系统代理助手"
        };

        _headerDescription = new WinLabel
        {
            Dock = DockStyle.Fill,
            Font = new Font("Microsoft YaHei UI", 9F, FontStyle.Regular),
            ForeColor = Color.DimGray,
            Text = "管理和快速切换 Windows PAC 代理脚本"
        };

        _header.Controls.Add(_headerDescription);
        _header.Controls.Add(_headerTitle);

        _container = new WinPanel
        {
            Dock = DockStyle.Fill,
            Padding = new Padding(24)
        };

        _manualInputRow = new WinPanel
        {
            Dock = DockStyle.Top,
            Height = 54
        };

        _pacInput = new AntInput
        {
            Dock = DockStyle.Fill
        };

        _addPacButton = new AntButton
        {
            Text = "添加到历史",
            Dock = DockStyle.Right,
            Width = 120,
        };
        _addPacButton.Click += (_, _) => AddPacUrlToHistory();

        _manualInputRow.Controls.Add(_pacInput);
        _manualInputRow.Controls.Add(_addPacButton);

        _selectRow = new WinPanel
        {
            Dock = DockStyle.Top,
            Height = 54,
            Padding = new Padding(0, 10, 0, 0)
        };

        _pacSelect = new Select
        {
            Dock = DockStyle.Fill
        };

        _applyButton = new AntButton
        {
            Text = "应用",
            Dock = DockStyle.Right,
            Width = 120,
            Type = TTypeMini.Primary
        };
        _applyButton.Click += (_, _) => ApplyProxyFromInput();

        _selectRow.Controls.Add(_pacSelect);
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

        var bottomBar = new WinPanel
        {
            Dock = DockStyle.Bottom,
            Height = 54,
            Padding = new Padding(0, 14, 0, 0)
        };

        bottomBar.Controls.Add(_statusLabel);
        bottomBar.Controls.Add(_proxySwitch);

        _container.Controls.Add(bottomBar);
        _container.Controls.Add(_selectRow);
        _container.Controls.Add(_manualInputRow);

        Controls.Add(_container);
        Controls.Add(_header);

        _proxyMonitorTimer = new WinTimer { Interval = 5000 };
        _proxyMonitorTimer.Tick += (_, _) => EnforceManagedProxyIfChanged();

        Load += Form1_Load;
        FormClosed += (_, _) => _proxyMonitorTimer.Stop();
    }

    private void Form1_Load(object? sender, EventArgs e)
    {
        try
        {
            EnsurePrivilegeHint();
            LoadPacHistory();
            LoadCurrentState();
            _proxyMonitorTimer.Start();
        }
        catch (Exception ex)
        {
            AntMessage.error(this, $"初始化失败：{ex.Message}");
        }
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

    private void AddPacUrlToHistory()
    {
        string pacUrl = _pacInput.Text?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(pacUrl))
        {
            AntMessage.warn(this, "请先输入一个 PAC 脚本地址。");
            return;
        }

        PacHistoryStore.SaveOrUpdate(pacUrl);
        LoadPacHistory();
        _pacSelect.Text = pacUrl;
        _pacInput.Text = pacUrl;
        AntMessage.success(this, "已添加到历史地址。");
    }

    private void LoadPacHistory()
    {
        var history = PacHistoryStore.Load();

        _pacSelect.Items.Clear();
        foreach (string url in history)
        {
            _pacSelect.Items.Add(new SelectItem(url, url));
        }

        if (history.Count > 0)
        {
            _pacSelect.Text = history.First();
        }
    }

    private void LoadCurrentState()
    {
        _isInitializing = true;
        try
        {
            var (enabled, pacUrl) = ProxyManager.GetCurrentProxyState();
            _proxySwitch.Checked = enabled;

            if (!string.IsNullOrWhiteSpace(pacUrl))
            {
                _pacSelect.Text = pacUrl;
                _pacInput.Text = pacUrl;
                _managedPacUrl = pacUrl.Trim();
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
            ProxyManager.SetProxy(enabled, pacUrl);

            if (enabled)
            {
                _managedPacUrl = pacUrl;
                PacHistoryStore.SaveOrUpdate(pacUrl);
                LoadPacHistory();
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

    private void ApplyProxyFromInput()
    {
        try
        {
            string pacUrl = GetSelectedPacUrl();
            ProxyManager.SetProxy(true, pacUrl);
            _managedPacUrl = pacUrl;
            PacHistoryStore.SaveOrUpdate(pacUrl);
            LoadPacHistory();

            _isInitializing = true;
            _proxySwitch.Checked = true;
            _isInitializing = false;

            _pacInput.Text = pacUrl;
            UpdateStatus(true, pacUrl);
            AntMessage.success(this, "代理脚本已应用");
        }
        catch (Exception ex)
        {
            AntMessage.error(this, $"应用失败：{ex.Message}");
        }
    }

    private string GetSelectedPacUrl()
    {
        string pacUrl = _pacSelect.Text?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(pacUrl))
        {
            pacUrl = _pacInput.Text?.Trim() ?? string.Empty;
        }

        if (string.IsNullOrWhiteSpace(pacUrl))
        {
            throw new InvalidOperationException("请先输入或选择 PAC 脚本地址。");
        }

        return pacUrl;
    }

    private void EnforceManagedProxyIfChanged()
    {
        if (_isEnforcingProxy || !_proxySwitch.Checked || string.IsNullOrWhiteSpace(_managedPacUrl))
        {
            return;
        }

        try
        {
            var (enabled, pacUrl) = ProxyManager.GetCurrentProxyState();
            string normalizedCurrent = pacUrl.Trim();

            if (!enabled || !string.Equals(normalizedCurrent, _managedPacUrl, StringComparison.OrdinalIgnoreCase))
            {
                _isEnforcingProxy = true;
                ProxyManager.SetProxy(true, _managedPacUrl);
                _pacSelect.Text = _managedPacUrl;
                _pacInput.Text = _managedPacUrl;
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
