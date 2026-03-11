using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using AntdUI;
using AntButton = AntdUI.Button;
using AntInput = AntdUI.Input;
using AntMessage = AntdUI.Message;
using WinLabel = System.Windows.Forms.Label;
using WinListView = System.Windows.Forms.ListView;
using WinPanel = System.Windows.Forms.Panel;

namespace ProxyApp;

public class PacConfigForm : AntdUI.Window
{
    private readonly WinListView _listView;
    private readonly AntInput _nameInput;
    private readonly AntInput _urlInput;
    private readonly AntButton _addButton;
    private readonly AntButton _updateButton;
    private readonly AntButton _deleteButton;
    private readonly AntButton _backButton;
    private readonly List<PacEntry> _entries = new();

    public PacConfigForm()
    {
        Text = "配置脚本地址";
        StartPosition = FormStartPosition.CenterParent;
        Size = new Size(860, 560);
        MinimumSize = new Size(760, 480);

        var root = new WinPanel { Dock = DockStyle.Fill, Padding = new Padding(20) };
        var listPanel = new WinPanel { Dock = DockStyle.Fill };
        var editorPanel = new WinPanel { Dock = DockStyle.Bottom, Height = 210, Padding = new Padding(0, 14, 0, 0) };

        var listTitle = new WinLabel
        {
            Dock = DockStyle.Top,
            Height = 34,
            Font = new Font("Microsoft YaHei UI", 11F, FontStyle.Bold),
            Text = "已配置脚本（点击行可编辑）"
        };

        _listView = new WinListView
        {
            Dock = DockStyle.Fill,
            View = View.Details,
            FullRowSelect = true,
            GridLines = true,
            MultiSelect = false,
            Font = new Font("Microsoft YaHei UI", 9.5F)
        };
        _listView.Columns.Add("名称", 220);
        _listView.Columns.Add("PAC 地址", 560);
        _listView.SelectedIndexChanged += (_, _) => FillEditorBySelection();

        var editorTitle = new WinLabel
        {
            Dock = DockStyle.Top,
            Height = 34,
            Font = new Font("Microsoft YaHei UI", 11F, FontStyle.Bold),
            Text = "新增 / 修改脚本（请优先填写此区域）",
            ForeColor = Color.FromArgb(22, 119, 255)
        };

        var nameLabel = new WinLabel
        {
            Dock = DockStyle.Top,
            Height = 24,
            Text = "自定义名称",
            Font = new Font("Microsoft YaHei UI", 9.5F, FontStyle.Bold)
        };

        _nameInput = new AntInput { Dock = DockStyle.Top, Height = 40 };

        var urlLabel = new WinLabel
        {
            Dock = DockStyle.Top,
            Height = 24,
            Text = "PAC 地址",
            Font = new Font("Microsoft YaHei UI", 9.5F, FontStyle.Bold),
            Padding = new Padding(0, 8, 0, 0)
        };

        _urlInput = new AntInput { Dock = DockStyle.Top, Height = 40 };

        var buttonRow = new WinPanel { Dock = DockStyle.Bottom, Height = 48, Padding = new Padding(0, 10, 0, 0) };

        _backButton = new AntButton { Text = "返回", Dock = DockStyle.Right, Width = 90 };
        _deleteButton = new AntButton { Text = "删除", Dock = DockStyle.Right, Width = 90 };
        _updateButton = new AntButton { Text = "修改", Dock = DockStyle.Right, Width = 90 };
        _addButton = new AntButton { Text = "＋ 添加", Dock = DockStyle.Right, Width = 110, Type = TTypeMini.Primary };

        _backButton.Click += (_, _) => Close();
        _addButton.Click += (_, _) => AddPac();
        _updateButton.Click += (_, _) => UpdatePac();
        _deleteButton.Click += (_, _) => DeletePac();

        buttonRow.Controls.Add(_backButton);
        buttonRow.Controls.Add(_deleteButton);
        buttonRow.Controls.Add(_updateButton);
        buttonRow.Controls.Add(_addButton);

        listPanel.Controls.Add(_listView);
        listPanel.Controls.Add(listTitle);

        editorPanel.Controls.Add(buttonRow);
        editorPanel.Controls.Add(_urlInput);
        editorPanel.Controls.Add(urlLabel);
        editorPanel.Controls.Add(_nameInput);
        editorPanel.Controls.Add(nameLabel);
        editorPanel.Controls.Add(editorTitle);

        root.Controls.Add(listPanel);
        root.Controls.Add(editorPanel);

        Controls.Add(root);
        Load += (_, _) => ReloadList();
    }

    private void ReloadList()
    {
        _entries.Clear();
        _entries.AddRange(PacHistoryStore.Load());

        _listView.Items.Clear();
        foreach (PacEntry entry in _entries)
        {
            var item = new ListViewItem(entry.Name);
            item.SubItems.Add(entry.Url);
            _listView.Items.Add(item);
        }
    }

    private void FillEditorBySelection()
    {
        if (_listView.SelectedIndices.Count == 0) return;

        int index = _listView.SelectedIndices[0];
        if (index < 0 || index >= _entries.Count) return;

        _nameInput.Text = _entries[index].Name;
        _urlInput.Text = _entries[index].Url;
    }

    private void AddPac()
    {
        string name = _nameInput.Text?.Trim() ?? string.Empty;
        string url = _urlInput.Text?.Trim() ?? string.Empty;

        if (string.IsNullOrWhiteSpace(name) || string.IsNullOrWhiteSpace(url))
        {
            AntMessage.warn(this, "请输入自定义名称和 PAC 地址。");
            return;
        }

        if (_entries.Any(x => string.Equals(x.Url, url, StringComparison.OrdinalIgnoreCase)))
        {
            AntMessage.warn(this, "该 PAC 地址已存在。请选择后使用“修改”更新名称或地址。");
            return;
        }

        _entries.Insert(0, new PacEntry { Name = name, Url = url });
        PacHistoryStore.ReplaceAll(_entries);
        ReloadList();
        AntMessage.success(this, "已添加。");
    }

    private void UpdatePac()
    {
        if (_listView.SelectedIndices.Count == 0)
        {
            AntMessage.warn(this, "请先在列表中选择要修改的地址。");
            return;
        }

        string name = _nameInput.Text?.Trim() ?? string.Empty;
        string url = _urlInput.Text?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(name) || string.IsNullOrWhiteSpace(url))
        {
            AntMessage.warn(this, "请输入自定义名称和 PAC 地址。");
            return;
        }

        int index = _listView.SelectedIndices[0];
        if (index < 0 || index >= _entries.Count)
        {
            AntMessage.warn(this, "当前选择项已失效，请重新选择。");
            return;
        }

        _entries[index] = new PacEntry { Name = name, Url = url };
        PacHistoryStore.ReplaceAll(_entries);
        ReloadList();
        if (_listView.Items.Count > 0)
        {
            int nextIndex = Math.Min(index, _listView.Items.Count - 1);
            _listView.Items[nextIndex].Selected = true;
        }

        AntMessage.success(this, "已修改。");
    }

    private void DeletePac()
    {
        if (_listView.SelectedIndices.Count == 0)
        {
            AntMessage.warn(this, "请先选择要删除的地址。");
            return;
        }

        int index = _listView.SelectedIndices[0];
        if (index < 0 || index >= _entries.Count)
        {
            AntMessage.warn(this, "当前选择项已失效，请重新选择。");
            return;
        }

        _entries.RemoveAt(index);
        PacHistoryStore.ReplaceAll(_entries);
        ReloadList();
        _nameInput.Text = string.Empty;
        _urlInput.Text = string.Empty;
        AntMessage.success(this, "已删除。");
    }
}
