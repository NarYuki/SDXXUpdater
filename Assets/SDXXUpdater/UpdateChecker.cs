using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.UI;
using Newtonsoft.Json;
using System.IO.Compression;

public class UpdateManager : MonoBehaviour
{
    public Text versionText;
    public Slider progressBar;
    public Text progressPercentage;
    public Button updateButton;

    private string apiUrl;
    private string gameBatchFile;
    private string downloadLocation;
    private string unzipLocation;
    private string versionFile;
    private string gameTitle;
    private string currentVersion;

    void Start()
    {
        LoadSettings();
        LoadVersionInfo();
        versionText.text = $"Ver. {currentVersion}";
        updateButton.onClick.AddListener(CheckForUpdate);
    }

    void LoadSettings()
    {
        string settingsPath = Path.Combine(Application.streamingAssetsPath, "settings.json");
        string json = File.ReadAllText(settingsPath);
        var settings = JsonConvert.DeserializeObject<Dictionary<string, string>>(json);
        apiUrl = settings["api_url"];
        gameBatchFile = settings["game_batch_file"];
        downloadLocation = settings["download_location"];
        unzipLocation = settings["unzip_location"];
        versionFile = settings["version_file"];
    }

    void LoadVersionInfo()
    {
        string json = File.ReadAllText(versionFile);
        var versionInfo = JsonConvert.DeserializeObject<Dictionary<string, string>>(json);
        gameTitle = versionInfo["game_title"];
        currentVersion = versionInfo["current_version"];
    }

    void CheckForUpdate()
    {
        StartCoroutine(CheckForUpdateCoroutine());
    }

    IEnumerator CheckForUpdateCoroutine()
    {
        var requestData = new Dictionary<string, string>
        {
            { "game_title", gameTitle },
            { "current_version", currentVersion }
        };
        string json = JsonConvert.SerializeObject(requestData);

        UnityWebRequest request = new UnityWebRequest(apiUrl, "POST");
        byte[] bodyRaw = System.Text.Encoding.UTF8.GetBytes(json);
        request.uploadHandler = new UploadHandlerRaw(bodyRaw);
        request.downloadHandler = new DownloadHandlerBuffer();
        request.SetRequestHeader("Content-Type", "application/json");

        yield return request.SendWebRequest();

        if (request.result == UnityWebRequest.Result.ConnectionError || request.result == UnityWebRequest.Result.ProtocolError)
        {
            Debug.LogError(request.error);
        }
        else
        {
            var responseData = JsonConvert.DeserializeObject<Dictionary<string, string>>(request.downloadHandler.text);
            if (responseData.ContainsKey("update_url"))
            {
                string updateUrl = responseData["update_url"];
                StartCoroutine(DownloadAndUpdate(updateUrl));
            }
            else
            {
                LaunchGame();
            }
        }
    }

    IEnumerator DownloadAndUpdate(string updateUrl)
    {
        UnityWebRequest request = new UnityWebRequest(updateUrl, UnityWebRequest.kHttpVerbGET);
        string filePath = Path.Combine(downloadLocation, "update.zip");
        request.downloadHandler = new DownloadHandlerFile(filePath);

        request.SendWebRequest();

        while (!request.isDone)
        {
            progressBar.value = request.downloadProgress;
            progressPercentage.text = $"{(request.downloadProgress * 100).ToString("F0")}%";
            yield return null;
        }

        if (request.result == UnityWebRequest.Result.ConnectionError || request.result == UnityWebRequest.Result.ProtocolError)
        {
            Debug.LogError(request.error);
        }
        else
        {
            UnzipAndCopy(filePath);
            UpdateVersionInfo();
            LaunchGame();
        }
    }

    void UnzipAndCopy(string zipFilePath)
    {
        if (Directory.Exists(unzipLocation))
        {
            Directory.Delete(unzipLocation, true);
        }
        ZipFile.ExtractToDirectory(zipFilePath, unzipLocation);
        // アップデートファイルを適切なディレクトリにコピーする処理を追加
    }

    void UpdateVersionInfo()
    {
        // 新しいバージョン情報を取得し、version.jsonに書き込み
        string json = File.ReadAllText(Path.Combine(unzipLocation, "new_version.json"));
        File.WriteAllText(versionFile, json);
    }

    void LaunchGame()
    {
        System.Diagnostics.Process.Start(gameBatchFile);
        Application.Quit();
    }

    void CheckForOfflineUpdate()
    {
        string offlineUpdatePath = "E:/update.zip";
        if (File.Exists(offlineUpdatePath))
        {
            UnzipAndCopy(offlineUpdatePath);
            UpdateVersionInfo();
            LaunchGame();
        }
        else
        {
            CheckForUpdate();
        }
    }
}
