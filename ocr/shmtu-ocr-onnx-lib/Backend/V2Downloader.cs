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
    /// <summary>
    /// 从 GitHub releases API 自动解析 v{maxMajor}.{≤maxMinor}.x 范围内最新 tag。
    /// 任何异常（无网、解析失败、无匹配）均 fallback 到 <paramref name="fallback"/>。
    /// </summary>
    public static async Task<string> ResolveLatestTagAsync(
        uint maxMajor = ConstValue.V2.MaxSupportedMajor,
        uint maxMinor = ConstValue.V2.MaxSupportedMinor,
        string fallback = ConstValue.V2.DefaultTag,
        HttpClient? httpClient = null,
        Action<string>? log = null)
    {
        var ownClient = httpClient is null;
        var client = httpClient ?? new HttpClient();
        try
        {
            client.DefaultRequestHeaders.UserAgent.ParseAdd("shmtu-cas-ocr-dotnet/1.0");
            client.DefaultRequestHeaders.Accept.ParseAdd("application/vnd.github+json");

            var url = $"{ConstValue.V2.GithubReleasesApi}?per_page=100";
            var json = await client.GetStringAsync(url);

            using var doc = JsonDocument.Parse(json);
            var candidates = new List<(uint M, uint N, uint P, string Tag)>();

            foreach (var rel in doc.RootElement.EnumerateArray())
            {
                if (rel.TryGetProperty("draft", out var d) && d.GetBoolean()) continue;
                if (rel.TryGetProperty("prerelease", out var p) && p.GetBoolean()) continue;
                if (!rel.TryGetProperty("tag_name", out var t)) continue;

                var tag = t.GetString() ?? "";
                if (!TryParseSemverTag(tag, out var major, out var minor, out var patch)) continue;
                if (major != maxMajor) continue;
                // uint.MaxValue 表示无上界
                if (maxMinor < uint.MaxValue && minor > maxMinor) continue;

                candidates.Add((major, minor, patch, tag));
            }

            if (candidates.Count == 0)
            {
                var filter = maxMinor == uint.MaxValue
                    ? $"v{maxMajor}.x.x"
                    : $"v{maxMajor}.{maxMinor}.x";
                log?.Invoke($"[v2] no release matched {filter}; fallback to {fallback}");
                return fallback;
            }

            var chosen = candidates
                .OrderByDescending(c => c.M)
                .ThenByDescending(c => c.N)
                .ThenByDescending(c => c.P)
                .First().Tag;

            log?.Invoke($"[v2] resolved latest tag: {chosen} ({candidates.Count} candidates)");
            return chosen;
        }
        catch (Exception ex)
        {
            log?.Invoke($"[v2] cannot list releases ({ex.Message}); fallback to {fallback}");
            return fallback;
        }
        finally
        {
            if (ownClient) client.Dispose();
        }
    }

    private static bool TryParseSemverTag(string tag, out uint major, out uint minor, out uint patch)
    {
        major = minor = patch = 0;
        if (!tag.StartsWith("v")) return false;
        var parts = tag[1..].Split('.');
        if (parts.Length != 3) return false;
        return uint.TryParse(parts[0], out major)
            && uint.TryParse(parts[1], out minor)
            && uint.TryParse(parts[2], out patch);
    }
    /// <summary>
    /// 下载 v2 模型（自动解析 tag）。当 <paramref name="tag"/> 为 null 时，
    /// 从 GitHub releases API 解析 v{MAX_SUPPORTED_MAJOR}.{≤MAX_SUPPORTED_MINOR}.x 范围内最新 tag，
    /// 失败则 fallback 到 <see cref="ConstValue.V2.DefaultTag"/>。
    /// </summary>
    public static Task<bool> DownloadAsync(
        string destDir,
        string? tag,
        string backbone,
        string precision,
        IProgress<float>? progress = null,
        HttpClient? httpClient = null,
        Action<string>? log = null,
        string primaryMirror = "github",
        string fallbackMirror = "gitee")
    {
        return DownloadInternalAsync(destDir, tag, backbone, precision, progress, httpClient, log,
            primaryMirror, fallbackMirror, autoResolve: true);
    }

    private static async Task<bool> DownloadInternalAsync(
        string destDir,
        string? tag,
        string backbone,
        string precision,
        IProgress<float>? progress,
        HttpClient? httpClient,
        Action<string>? log,
        string primaryMirror,
        string fallbackMirror,
        bool autoResolve)
    {
        if (autoResolve && string.IsNullOrWhiteSpace(tag))
        {
            tag = await ResolveLatestTagAsync(
                ConstValue.V2.MaxSupportedMajor,
                ConstValue.V2.MaxSupportedMinor,
                ConstValue.V2.DefaultTag,
                httpClient,
                log);
        }

        if (string.IsNullOrWhiteSpace(tag))
        {
            log?.Invoke("v2 下载：tag 为空且未启用自动解析");
            return false;
        }
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