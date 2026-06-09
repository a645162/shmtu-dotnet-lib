using System.Text.Json;
using System.Text.Json.Serialization;
using shmtu.captcha.onnx.Utils;

namespace shmtu.captcha.onnx.Backend;

/// <summary>
/// v2 模型智能下载：
/// 1) 拉取 {base}/{tag}/model-assets.json
/// 2) 在 artifacts 中筛选 engine=onnx, backbone, precision
/// 3) 下载每个 release_asset_name 到 destDir
/// 4) 校验 SHA256；主源失败尝试备用 mirror
/// </summary>
public static class V2Downloader
{
    /// <summary>下载 v2 模型。返回是否成功（已存在 / 下载完成）。</summary>
    public static async Task<bool> DownloadAsync(
        string destDir,
        string tag,
        string backbone,
        string precision,
        IProgress<float>? progress = null,
        HttpClient? httpClient = null,
        Action<string>? log = null,
        string primaryMirror = "github",
        string fallbackMirror = "gitee")
    {
        destDir = Path.GetFullPath(destDir);
        if (!Directory.Exists(destDir))
        {
            Directory.CreateDirectory(destDir);
            log?.Invoke($"创建模型目录: {destDir}");
        }

        var expectedFile = ConstValue.V2.BuildModelName(backbone, precision);
        var localPath = Path.Combine(destDir, expectedFile);
        if (File.Exists(localPath))
        {
            log?.Invoke($"v2 模型已存在: {localPath}");
            progress?.Report(100f);
            return true;
        }

        var ownClient = httpClient is null;
        var client = httpClient ?? new HttpClient();
        try
        {
            var mirrors = primaryMirror.Equals("gitee", StringComparison.OrdinalIgnoreCase)
                ? new[] { "gitee", "github" }
                : new[] { "github", "gitee" };
            // 允许显式传入顺序
            if (!string.IsNullOrWhiteSpace(fallbackMirror) &&
                !fallbackMirror.Equals(primaryMirror, StringComparison.OrdinalIgnoreCase))
            {
                mirrors = new[] { primaryMirror.ToLowerInvariant(), fallbackMirror.ToLowerInvariant() };
            }

            Exception? lastError = null;
            foreach (var mirror in mirrors)
            {
                var baseUrl = mirror == "gitee" ? ConstValue.V2.BaseUrlGitee : ConstValue.V2.BaseUrlGithub;
                var manifestUrl = $"{baseUrl}/{tag}/{ConstValue.V2.ManifestName}";
                try
                {
                    log?.Invoke($"v2 智能下载：尝试 mirror={mirror}, manifest={manifestUrl}");
                    var manifestJson = await NetworkFile.DownloadStringAsync(client, manifestUrl);
                    var manifest = JsonSerializer.Deserialize<V2Manifest>(manifestJson,
                        new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                    if (manifest == null || manifest.Artifacts == null)
                    {
                        log?.Invoke("v2 manifest 解析为空，跳过该 mirror");
                        continue;
                    }

                    var artifact = manifest.Artifacts.FirstOrDefault(a =>
                        string.Equals(a.Engine, "onnx", StringComparison.OrdinalIgnoreCase) &&
                        string.Equals(a.Backbone, backbone, StringComparison.OrdinalIgnoreCase) &&
                        string.Equals(a.Precision, precision, StringComparison.OrdinalIgnoreCase));

                    if (artifact == null || artifact.Files == null || artifact.Files.Count == 0)
                    {
                        log?.Invoke(
                            $"v2 manifest 未匹配 (engine=onnx, backbone={backbone}, precision={precision})，跳过该 mirror");
                        continue;
                    }

                    var total = artifact.Files.Count;
                    var increment = total > 0 ? 100f / total : 100f;

                    for (var i = 0; i < total; i++)
                    {
                        var fileMeta = artifact.Files[i];
                        var fileName = fileMeta.ReleaseAssetName;
                        var fileUrl = $"{baseUrl}/{tag}/{fileName}";
                        var fileLocal = Path.Combine(destDir, fileName);
                        var start = i * increment;

                        log?.Invoke($"下载 v2 文件 {i + 1}/{total}: {fileName} <- {fileUrl}");
                        await NetworkFile.DownloadFileAsync(client, fileUrl, fileLocal,
                            new Progress<float>(p =>
                            {
                                var adjusted = start + p / 100f * increment;
                                if (adjusted > 100) adjusted = 100;
                                progress?.Report(adjusted);
                            }));

                        if (!string.IsNullOrWhiteSpace(fileMeta.Sha256))
                        {
                            var actual = await NetworkFile.ComputeSha256Async(fileLocal);
                            if (!string.Equals(actual, fileMeta.Sha256, StringComparison.OrdinalIgnoreCase))
                            {
                                log?.Invoke(
                                    $"v2 校验和不匹配: {fileName} (期望 {fileMeta.Sha256[..16]}..., 实际 {actual[..16]}...)");
                                try { File.Delete(fileLocal); } catch { /* best effort */ }
                                throw new InvalidOperationException(
                                    $"sha256 mismatch for {fileName}; mirror={mirror}");
                            }

                            log?.Invoke($"v2 文件校验通过: {fileName}");
                        }
                    }

                    progress?.Report(100f);
                    log?.Invoke($"v2 模型下载完成: {destDir} (mirror={mirror})");
                    return true;
                }
                catch (Exception ex)
                {
                    log?.Invoke($"mirror={mirror} 下载失败: {ex.Message}");
                    lastError = ex;
                    // 清理半成品
                    try
                    {
                        if (File.Exists(localPath)) File.Delete(localPath);
                    }
                    catch { /* ignore */ }
                }
            }

            log?.Invoke($"v2 模型下载全部 mirror 失败: {lastError?.Message ?? "unknown"}");
            return false;
        }
        finally
        {
            if (ownClient) client.Dispose();
        }
    }

    /// <summary>检查 v2 模型是否存在。</summary>
    public static bool CheckModelIsExist(string destDir, string backbone, string precision)
    {
        if (string.IsNullOrWhiteSpace(destDir)) return false;
        if (!Directory.Exists(destDir)) return false;
        var fileName = ConstValue.V2.BuildModelName(backbone, precision);
        return File.Exists(Path.Combine(Path.GetFullPath(destDir), fileName));
    }

    // ---- manifest DTO ----
    private sealed class V2Manifest
    {
        [JsonPropertyName("schema_version")]
        public int SchemaVersion { get; set; }

        [JsonPropertyName("artifacts")]
        public List<V2Artifact>? Artifacts { get; set; }
    }

    private sealed class V2Artifact
    {
        [JsonPropertyName("engine")]
        public string Engine { get; set; } = "";

        [JsonPropertyName("backbone")]
        public string Backbone { get; set; } = "";

        [JsonPropertyName("precision")]
        public string Precision { get; set; } = "";

        [JsonPropertyName("format")]
        public string Format { get; set; } = "";

        [JsonPropertyName("files")]
        public List<V2File>? Files { get; set; }
    }

    private sealed class V2File
    {
        [JsonPropertyName("path")]
        public string Path { get; set; } = "";

        [JsonPropertyName("release_asset_name")]
        public string ReleaseAssetName { get; set; } = "";

        [JsonPropertyName("sha256")]
        public string Sha256 { get; set; } = "";
    }
}