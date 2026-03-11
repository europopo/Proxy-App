using System;
using System.Drawing;
using System.Linq;
using System.Security.Principal;
using System.Windows.Forms;
using AntdUI;
using WinPanel = System.Windows.Forms.Panel;
using WinLabel = System.Windows.Forms.Label;

namespace ProxyApp;

public class Form1 : AntdUI.Window
{
    private readonly WinPanel _header;
    private readonly WinLabel _headerTitle;
    private readonly WinLabel _headerDescription;
    private readonly WinPanel _container;
    private readonly WinPanel _inputRow;
    private readonly Select _pacSelect;
    private readonly Button _applyButton;
    private readonly WinLabel _statusLabel;
    private readonly Switch _proxySwitch;

    private bool _isInitializing;

    public Form1()
    {
        Text = "系统代理助手";
        StartPosition = FormStartPosition.CenterScreen;
        MinimumSize = new Size(720, 320);
        Size = new Size(860, 420);

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

        _inputRow = new WinPanel
        {
            Dock = DockStyle.Top,
            Height = 54
        };

        _pacSelect = new Select
        {
            Dock = DockStyle.Fill
        };

        _applyButton = new Button
        {
            Text = "应用",
            Dock = DockStyle.Right,
            Width = 120,
            Type = TTypeMini.Primary
        };
        _applyButton.Click += (_, _) => ApplyProxyFromInput();

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
        _proxySwitch.CheckedChanged += ProxySwitch_CheckedChanged;

        var bottomBar = new WinPanel
        {
            Dock = DockStyle.Bottom,
            Height = 54,
            Padding = new Padding(0, 14, 0, 0)
        };

        _inputRow.Controls.Add(_pacSelect);
        _inputRow.Controls.Add(_applyButton);

        bottomBar.Controls.Add(_statusLabel);
        bottomBar.Controls.Add(_proxySwitch);

        _container.Controls.Add(bottomBar);
        _container.Controls.Add(_inputRow);

        Controls.Add(_container);
        Controls.Add(_header);

        Load += Form1_Load;
    }

    private void Form1_Load(object? sender, EventArgs e)
    {
        try
        {
            EnsurePrivilegeHint();
            LoadPacHistory();
            LoadCurrentState();
        }
        catch (Exception ex)
        {
            Message.error($"初始化失败：{ex.Message}");
        }
    }

    private void EnsurePrivilegeHint()
    {
        using WindowsIdentity identity = WindowsIdentity.GetCurrent();
        WindowsPrincipal principal = new(identity);

        if (!principal.IsInRole(WindowsBuiltInRole.Administrator))
        {
            Message.warn("当前未以管理员身份运行。通常修改当前用户代理设置不受影响，如遇受策略限制请尝试管理员身份启动。");
        }
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
            }

            UpdateStatus(enabled, pacUrl);
        }
        finally
        {
            _isInitializing = false;
        }
    }

    private void ProxySwitch_CheckedChanged(object? sender, EventArgs e)
    {
        if (_isInitializing) return;

        bool enabled = _proxySwitch.Checked;

        try
        {
            string pacUrl = _pacSelect.Text?.Trim() ?? string.Empty;
            ProxyManager.SetProxy(enabled, pacUrl);

            if (enabled)
            {
                PacHistoryStore.SaveOrUpdate(pacUrl);
                LoadPacHistory();
            }

            UpdateStatus(enabled, pacUrl);
            Message.success(enabled ? "PAC 代理已开启" : "PAC 代理已关闭");
        }
        catch (Exception ex)
        {
            _isInitializing = true;
            _proxySwitch.Checked = !_proxySwitch.Checked;
            _isInitializing = false;
            Message.error($"切换失败：{ex.Message}");
        }
    }

    private void ApplyProxyFromInput()
    {
        try
        {
            string pacUrl = _pacSelect.Text?.Trim() ?? string.Empty;
            ProxyManager.SetProxy(true, pacUrl);
            PacHistoryStore.SaveOrUpdate(pacUrl);
            LoadPacHistory();

            _isInitializing = true;
            _proxySwitch.Checked = true;
            _isInitializing = false;

            UpdateStatus(true, pacUrl);
            Message.success("代理脚本已应用");
        }
        catch (Exception ex)
        {
            Message.error($"应用失败：{ex.Message}");
        }
    }

    private void UpdateStatus(bool enabled, string pacUrl)
    {
        _statusLabel.Text = enabled
            ? $"当前状态：已开启  |  PAC：{pacUrl}"
            : "当前状态：已关闭";
    }
}
