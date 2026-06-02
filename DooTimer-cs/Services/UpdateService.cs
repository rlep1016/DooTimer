using System.Net.Http;
using System.Net.Http.Json;
using System.Reflection;
using System.Text.Json.Serialization;

namespace DooTimer.Services;

/// <summary>
/// 更新状态
/// </summary>
public enum UpdateState
{
    Idle,           // 还没检查
    Checking,       // 正在检查
    UpToDate,       // 已是最新
    Available,      // 有新版本可更新
    Downloading,    // 正在下载
    Downloaded,     // 下载完成，等待安装
    Error           // 出错
}

/// <summary>
/// 更新状态信息
/// </summary>
public class UpdateInfo
{
    public UpdateState State { get; set; } = UpdateState.Idle;
    public string? LatestVersion { get; set; }
    public string? CurrentVersion { get; set; }
    public string? Message { get; set; }  // 提示文字
    public string? InstallerPath { get; set; }
    public Action? InstallAction { get; set; }  // 安装回调
}

/// <summary>
/// 自动更新服务：后台检查 GitHub Release，手动触发下载和安装
/// </summary>
public class UpdateService
{
    private const string GitHubApiUrl = "https://api.github.com/repos/rlep1016/DooTimer/releases/latest";
    private static readonly HttpClient _httpClient = new()
    {
        Timeout = TimeSpan.FromSeconds(30)
    };
    static UpdateService()
    {
        _httpClient.DefaultRequestHeaders.UserAgent.ParseAdd("DooTimer-Updater/1.0");
    }

    private readonly string _currentVersion;
    private readonly Action<string, string> _log;
    private string? _downloadUrl;

    public UpdateInfo Info { get; } = new();
    public event Action? StateChanged;

    public UpdateService(Action<string, string> log)
    {
        _currentVersion = Assembly.GetExecutingAssembly()
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion
            ?? Assembly.GetExecutingAssembly().GetName().Version?.ToString()
            ?? "1.0.0";

        Info.CurrentVersion = _currentVersion;
        Info.State = UpdateState.Idle;
        _log = log;
    }

    /// <summary>
    /// 开始检查更新
    /// </summary>
    public async Task CheckAsync()
    {
        if (Info.State == UpdateState.Checking || Info.State == UpdateState.Downloading)
            return;

        SetState(UpdateState.Checking, "正在检查更新...");

        try
        {
            _log("UPDATE", $"检查更新... 当前版本: {_currentVersion}");

            var release = await _httpClient.GetFromJsonAsync<GitHubRelease>(GitHubApiUrl);
            if (release == null || string.IsNullOrEmpty(release.TagName))
            {
                SetState(UpdateState.Error, "无法获取版本信息");
                return;
            }

            var latestVersion = release.TagName.TrimStart('v');
            _log("UPDATE", $"最新版本: {latestVersion}");

            if (!IsNewerVersion(latestVersion, _currentVersion))
            {
                SetState(UpdateState.UpToDate, $"已是最新版本 ({_currentVersion})");
                return;
            }

            // 找到安装包下载链接
            var asset = release.Assets?.FirstOrDefault(a =>
                a.Name?.EndsWith(".exe", StringComparison.OrdinalIgnoreCase) == true &&
                a.Name?.Contains("Setup", StringComparison.OrdinalIgnoreCase) == true);

            if (asset == null || string.IsNullOrEmpty(asset.BrowserDownloadUrl))
            {
                SetState(UpdateState.Error, "未找到安装包");
                return;
            }

            _downloadUrl = asset.BrowserDownloadUrl;
            Info.LatestVersion = latestVersion;

            // 检查本地是否已有缓存
            var cachedPath = Path.Combine(Path.GetTempPath(), "DooTimer", "update",
                $"DooTimer-Setup-v{latestVersion}.exe");
            if (File.Exists(cachedPath))
            {
                Info.InstallerPath = cachedPath;
                Info.InstallAction = () => ExecuteInstall(cachedPath);
                SetState(UpdateState.Downloaded, $"v{latestVersion} 已下载，可以安装了");
            }
            else
            {
                SetState(UpdateState.Available, $"发现新版本 {latestVersion}");
            }
            _log("UPDATE", $"发现新版本: {latestVersion}");
        }
        catch (Exception ex)
        {
            _log("UPDATE", $"检查更新出错: {ex.Message}");
            SetState(UpdateState.Error, "检查更新失败");
        }
    }

    /// <summary>
    /// 开始下载更新
    /// </summary>
    public async Task DownloadAsync()
    {
        if (string.IsNullOrEmpty(_downloadUrl) || Info.State != UpdateState.Available)
            return;

        // 检查是否已经下载过
        var downloadDir = Path.Combine(Path.GetTempPath(), "DooTimer", "update");
        var fileName = $"DooTimer-Setup-v{Info.LatestVersion}.exe";
        var installerPath = Path.Combine(downloadDir, fileName);

        if (File.Exists(installerPath))
        {
            _log("UPDATE", $"安装包已存在，跳过下载: {installerPath}");
            Info.InstallerPath = installerPath;
            Info.InstallAction = () => ExecuteInstall(installerPath);
            SetState(UpdateState.Downloaded, $"v{Info.LatestVersion} 已下载，可以安装了");
            return;
        }

        SetState(UpdateState.Downloading, "正在下载...");

        try
        {
            Directory.CreateDirectory(downloadDir);
            using var response = await _httpClient.GetAsync(_downloadUrl,
                HttpCompletionOption.ResponseHeadersRead);

            if (!response.IsSuccessStatusCode)
            {
                SetState(UpdateState.Error, $"下载失败 (HTTP {(int)response.StatusCode})");
                return;
            }

            await using var stream = await response.Content.ReadAsStreamAsync();
            await using var fileStream = File.Create(installerPath);
            await stream.CopyToAsync(fileStream);

            Info.InstallerPath = installerPath;
            Info.InstallAction = () => ExecuteInstall(installerPath);

            SetState(UpdateState.Downloaded, $"v{Info.LatestVersion} 已下载，可以安装了");
            _log("UPDATE", $"下载完成: {installerPath}");
        }
        catch (Exception ex)
        {
            _log("UPDATE", $"下载出错: {ex.Message}");
            SetState(UpdateState.Error, "下载失败");
        }
    }

    /// <summary>
    /// 执行安装（关闭当前程序 → 运行安装包）
    /// </summary>
    private static void ExecuteInstall(string installerPath)
    {
        try
        {
            var startInfo = new System.Diagnostics.ProcessStartInfo
            {
                FileName = installerPath,
                UseShellExecute = true,
                Verb = "runas"
            };
            System.Diagnostics.Process.Start(startInfo);
        }
        catch
        {
            // 回退：直接打开安装包（无管理员权限）
            try
            {
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(installerPath)
                { UseShellExecute = true });
            }
            catch { }
        }

        System.Windows.Application.Current.Dispatcher.Invoke(() =>
        {
            System.Windows.Application.Current.Shutdown();
        });
    }

    private void SetState(UpdateState state, string message)
    {
        Info.State = state;
        Info.Message = message;
        StateChanged?.Invoke();
    }

    private static bool IsNewerVersion(string latest, string current)
    {
        try
        {
            var l = ParseVersion(latest);
            var c = ParseVersion(current);
            for (int i = 0; i < 4; i++)
            {
                if (l[i] > c[i]) return true;
                if (l[i] < c[i]) return false;
            }
            return false;
        }
        catch
        {
            return !string.Equals(latest, current, StringComparison.OrdinalIgnoreCase);
        }
    }

    private static int[] ParseVersion(string v)
    {
        var parts = v.Split('-')[0].Split('.');
        var result = new int[4];
        for (int i = 0; i < 4; i++)
            result[i] = i < parts.Length && int.TryParse(parts[i], out var n) ? n : 0;
        return result;
    }

    private class GitHubRelease
    {
        [JsonPropertyName("tag_name")]
        public string? TagName { get; set; }
        [JsonPropertyName("assets")]
        public List<GitHubAsset>? Assets { get; set; }
    }

    private class GitHubAsset
    {
        [JsonPropertyName("name")]
        public string? Name { get; set; }
        [JsonPropertyName("browser_download_url")]
        public string? BrowserDownloadUrl { get; set; }
    }
}
