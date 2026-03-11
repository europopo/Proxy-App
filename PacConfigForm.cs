using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using AntdUI;
using AntButton = AntdUI.Button;
using AntInput = AntdUI.Input;
using AntMessage = AntdUI.Message;
using WinListBox = System.Windows.Forms.ListBox;
using WinPanel = System.Windows.Forms.Panel;

namespace ProxyApp;

public class PacConfigForm : AntdUI.Window
{
    private readonly WinListBox _listBox;
    private readonly AntInput _input;
    private readonly AntButton _addButton;
    private readonly AntButton _updateButton;
    private readonly AntButton _deleteButton;
    private readonly AntButton _backButton;

    public PacConfigForm()
    {
        Text = "配置脚本地址";
        StartPosition = FormStartPosition.CenterParent;
        Size = new Size(760, 500);
        MinimumSize = new Size(680, 420);

        var root = new WinPanel { Dock = DockStyle.Fill, Padding = new Padding(20) };
        var listPanel = new WinPanel { Dock = DockStyle.Fill };
        var editorPanel = new WinPanel { Dock = DockStyle.Bottom, Height = 130, Padding = new Padding(0, 12, 0, 0) };

        _listBox = new WinListBox
        {
            Dock = DockStyle.Fill,
            Font = new Font("Microsoft YaHei UI", 10F)
        };
        _listBox.SelectedIndexChanged += (_, _) => _input.Text = _listBox.SelectedItem?.ToString() ?? string.Empty;

        _input = new AntInput { Dock = DockStyle.Top, Height = 40 };

        var buttonRow = new WinPanel { Dock = DockStyle.Bottom, Height = 44, Padding = new Padding(0, 8, 0, 0) };

        _backButton = new AntButton { Text = "返回", Dock = DockStyle.Right, Width = 90 };
        _deleteButton = new AntButton { Text = "删除", Dock = DockStyle.Right, Width = 90 };
        _updateButton = new AntButton { Text = "修改", Dock = DockStyle.Right, Width = 90 };
        _addButton = new AntButton { Text = "添加", Dock = DockStyle.Right, Width = 90, Type = TTypeMini.Primary };

        _backButton.Click += (_, _) => Close();
        _addButton.Click += (_, _) => AddPac();
        _updateButton.Click += (_, _) => UpdatePac();
        _deleteButton.Click += (_, _) => DeletePac();

        buttonRow.Controls.Add(_backButton);
        buttonRow.Controls.Add(_deleteButton);
        buttonRow.Controls.Add(_updateButton);
        buttonRow.Controls.Add(_addButton);

        listPanel.Controls.Add(_listBox);
        editorPanel.Controls.Add(buttonRow);
        editorPanel.Controls.Add(_input);

        root.Controls.Add(listPanel);
        root.Controls.Add(editorPanel);

        Controls.Add(root);
        Load += (_, _) => ReloadList();
    }

    private void ReloadList()
    {
        IReadOnlyList<string> history = PacHistoryStore.Load();
        _listBox.Items.Clear();
        foreach (string url in history)
        {
            _listBox.Items.Add(url);
        }
    }

    private void AddPac()
    {
        string url = _input.Text?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(url))
        {
            AntMessage.warn(this, "请输入 PAC 地址。");
            return;
        }

        List<string> urls = PacHistoryStore.Load().ToList();
        if (urls.Any(x => string.Equals(x, url, StringComparison.OrdinalIgnoreCase)))
        {
            AntMessage.warn(this, "该地址已存在。请选择后使用“修改”更新。");
            return;
        }

        urls.Insert(0, url);
        PacHistoryStore.ReplaceAll(urls);
        ReloadList();
        AntMessage.success(this, "已添加。");
    }

    private void UpdatePac()
    {
        int index = _listBox.SelectedIndex;
        if (index < 0)
        {
            AntMessage.warn(this, "请先在列表中选择要修改的地址。");
            return;
        }

        string newUrl = _input.Text?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(newUrl))
        {
            AntMessage.warn(this, "请输入修改后的 PAC 地址。");
            return;
        }

        List<string> urls = PacHistoryStore.Load().ToList();
        urls[index] = newUrl;
        PacHistoryStore.ReplaceAll(urls);
        ReloadList();
        _listBox.SelectedIndex = Math.Min(index, _listBox.Items.Count - 1);
        AntMessage.success(this, "已修改。");
    }

    private void DeletePac()
    {
        int index = _listBox.SelectedIndex;
        if (index < 0)
        {
            AntMessage.warn(this, "请先选择要删除的地址。");
            return;
        }

        List<string> urls = PacHistoryStore.Load().ToList();
        urls.RemoveAt(index);
        PacHistoryStore.ReplaceAll(urls);
        ReloadList();
        _input.Text = string.Empty;
        AntMessage.success(this, "已删除。");
    }
}
