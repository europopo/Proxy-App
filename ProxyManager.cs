using System;
using System.Runtime.InteropServices;
using Microsoft.Win32;

namespace ProxyApp;

public static class ProxyManager
{
    private const string InternetSettingsKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Internet Settings";
    private const int INTERNET_OPTION_SETTINGS_CHANGED = 39;
    private const int INTERNET_OPTION_REFRESH = 37;

    [DllImport("wininet.dll", SetLastError = true)]
    private static extern bool InternetSetOption(IntPtr hInternet, int dwOption, IntPtr lpBuffer, int dwBufferLength);

    public static void SetProxy(bool enable, string pacUrl)
    {
        if (enable && string.IsNullOrWhiteSpace(pacUrl))
        {
            throw new ArgumentException("启用 PAC 时脚本地址不能为空。", nameof(pacUrl));
        }

        using RegistryKey? key = Registry.CurrentUser.OpenSubKey(InternetSettingsKeyPath, writable: true)
            ?? throw new InvalidOperationException("无法打开 Internet Settings 注册表键。\n请确认当前用户有写入权限。");

        if (enable)
        {
            key.SetValue("AutoConfigURL", pacUrl.Trim(), RegistryValueKind.String);
            key.SetValue("ProxyEnable", 0, RegistryValueKind.DWord);
        }
        else
        {
            key.DeleteValue("AutoConfigURL", throwOnMissingValue: false);
        }

        RefreshInternetOptions();
    }

    public static (bool Enabled, string PacUrl) GetCurrentProxyState()
    {
        using RegistryKey? key = Registry.CurrentUser.OpenSubKey(InternetSettingsKeyPath, writable: false)
            ?? throw new InvalidOperationException("无法读取 Internet Settings 注册表键。");

        string? pacUrl = key.GetValue("AutoConfigURL") as string;
        bool enabled = !string.IsNullOrWhiteSpace(pacUrl);

        return (enabled, pacUrl ?? string.Empty);
    }

    private static void RefreshInternetOptions()
    {
        bool settingsChanged = InternetSetOption(IntPtr.Zero, INTERNET_OPTION_SETTINGS_CHANGED, IntPtr.Zero, 0);
        bool refreshed = InternetSetOption(IntPtr.Zero, INTERNET_OPTION_REFRESH, IntPtr.Zero, 0);

        if (!settingsChanged || !refreshed)
        {
            throw new InvalidOperationException("系统代理配置已写入注册表，但刷新系统设置失败。请尝试手动重启浏览器。\n" +
                                                $"Win32Error={Marshal.GetLastWin32Error()}");
        }
    }
}
