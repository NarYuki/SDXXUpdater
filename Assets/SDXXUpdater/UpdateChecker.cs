using UnityEngine;
using UnityEngine.UI;
using System.IO;
using System.Collections;
using System.Net.Http;
using System.Threading.Tasks;
using System.IO.Compression;
using Newtonsoft.Json.Linq;

public class Updater : MonoBehaviour
{
    public Text statusText;
    public Button updateButton;
    public Slider progressBar;
    public Text progressText;
    public Text sourceText;
    public Text latestVersionText; // 追加：最新バージョンを表示するテキスト

    private string apiUrl;
    private string gameBatchFile;
    private string downloadPath;
    private string extractPath;
    private string gameTitle;
    private string currentVersion;
    private string configFilePath = "config.json";

    void Start()
    {
        LoadConfig();
        CheckForUpdates();
    }

    void LoadConfig()
    {
        string configContent = File.ReadAllText(configFilePath);
        JObject config = JObject.Parse(configContent);

        apiUrl = config["api_url"].ToString();
        gameBatchFile = config["game_batch_file"].ToString();
        downloadPath = config["download_path"].ToString();
        extractPath = config["extract_path"].ToString();
        gameTitle = config["game_info"]["game_title"].ToString();
        currentVersion = config["game_info"]["current_version"].ToString();

        updateButton.onClick.AddListener(StartUpdate);
    }

    async void CheckForUpdates()
    {
        statusText.text = "アップデートを確認中";

        bool offlineUpdateAvailable = CheckOfflineUpdate();
        if (offlineUpdateAvailable)
        {
            sourceText.text = "外部メディア";
            statusText.text = "オフラインアップデートが利用可能です。";
            updateButton.interactable = true;
        }
        else
        {
            HttpClient client = new HttpClient();
            var response = await client.PostAsync(apiUrl, new StringContent($"{{\"game_title\": \"{gameTitle}\", \"current_version\": \"{currentVersion}\"}}", System.Text.Encoding.UTF8, "application/json"));

            if (response.IsSuccessStatusCode)
            {
                var content = await response.Content.ReadAsStringAsync();
                JObject jsonResponse = JObject.Parse(content);

                if (jsonResponse.ContainsKey("latest_version"))
                {
                    string latestVersion = jsonResponse["latest_version"].ToString();
                    latestVersionText.text = $"V. {latestVersion}"; // 追加：最新バージョンを表示
                    sourceText.text = "サーバー";
                    statusText.text = $"アップデートが利用可能です。";
                    updateButton.interactable = true;
                }
                else
                {
                    statusText.text = "アップデートは必要ありません。";
                    LaunchGame();
                }
            }
            else
            {
                statusText.text = "エラー:アップデート情報を取得することができませんでした。";
            }
        }
    }

    bool CheckOfflineUpdate()
    {
        string offlineUpdatePath = "E:/update.zip";
        return File.Exists(offlineUpdatePath);
    }

    async void StartUpdate()
    {
        updateButton.interactable = false;

        if (sourceText.text == "外部メディア")
        {
            string offlineUpdatePath = "E:/update.zip";
            await ExtractAndCopyFiles(offlineUpdatePath);
        }
        else
        {
            HttpClient client = new HttpClient();
            var response = await client.PostAsync(apiUrl, new StringContent($"{{\"game_title\": \"{gameTitle}\", \"current_version\": \"{currentVersion}\"}}", System.Text.Encoding.UTF8, "application/json"));

            if (response.IsSuccessStatusCode)
            {
                var content = await response.Content.ReadAsStringAsync();
                JObject jsonResponse = JObject.Parse(content);
                string updateUrl = jsonResponse["update_url"].ToString();

                await DownloadUpdate(updateUrl);
            }
        }
    }

    async Task DownloadUpdate(string url)
    {
        HttpClient client = new HttpClient();
        var response = await client.GetAsync(url, HttpCompletionOption.ResponseHeadersRead);

        if (!response.IsSuccessStatusCode)
        {
            statusText.text = "エラー:何らかの原因でダウンロードができませんでした";
            return;
        }

        var totalBytes = response.Content.Headers.ContentLength ?? 1;
        var downloadedBytes = 0L;

        using (var stream = await response.Content.ReadAsStreamAsync())
        {
            using (var fileStream = new FileStream(downloadPath, FileMode.Create, FileAccess.Write, FileShare.None, 8192, true))
            {
                var buffer = new byte[8192];
                int bytesRead;

                while ((bytesRead = await stream.ReadAsync(buffer, 0, buffer.Length)) > 0)
                {
                    await fileStream.WriteAsync(buffer, 0, bytesRead);
                    downloadedBytes += bytesRead;
                    UpdateProgress(downloadedBytes, totalBytes);
                }
            }
        }

        await ExtractAndCopyFiles(downloadPath);
    }

    void UpdateProgress(long downloadedBytes, long totalBytes)
    {
        float progress = (float)downloadedBytes / totalBytes;
        progressBar.value = progress;
        progressText.text = $"{(progress * 100):0.00}%";
    }

    async Task ExtractAndCopyFiles(string zipPath)
    {
        statusText.text = "アップデートを展開しています。";
        ZipFile.ExtractToDirectory(zipPath, extractPath);

        // ファイルをコピーする処理を追加
        // 例: DirectoryCopy(extractPath, gameDirectory, true);

        statusText.text = "アップデートが完了しました。";
        SaveNewVersion();
        LaunchGame();
    }

    void SaveNewVersion()
    {
        string configContent = File.ReadAllText(configFilePath);
        JObject config = JObject.Parse(configContent);
        config["game_info"]["current_version"] = GetNewVersion();
        File.WriteAllText(configFilePath, config.ToString());
    }

    string GetNewVersion()
    {
        // 新しいバージョン情報を取得するロジックを実装
        return "1.2.3"; // 例
    }

    void LaunchGame()
    {
        statusText.text = "ゲームプログラムを起動しています。";
        System.Diagnostics.Process.Start(gameBatchFile);
    }
}
