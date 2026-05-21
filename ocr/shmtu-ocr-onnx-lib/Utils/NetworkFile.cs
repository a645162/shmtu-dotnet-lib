namespace shmtu.captcha.onnx.Utils;

public static class NetworkFile
{
    public static async Task DownloadFileAsync(
        HttpClient client,
        string url,
        string outputPath,
        IProgress<float>? progress)
    {
        using var response = await client.GetAsync(url, HttpCompletionOption.ResponseHeadersRead);
        response.EnsureSuccessStatusCode();

        var totalBytes = response.Content.Headers.ContentLength ?? -1L;
        var downloadedBytes = 0L;

        await using var contentStream = await response.Content.ReadAsStreamAsync();
        await using var fileStream = new FileStream(
            outputPath,
            FileMode.Create, FileAccess.Write, FileShare.None,
            8192, true);

        var buffer = new byte[8192];
        int bytesRead;

        while ((bytesRead = await contentStream.ReadAsync(buffer, 0, buffer.Length)) > 0)
        {
            await fileStream.WriteAsync(buffer, 0, bytesRead);
            downloadedBytes += bytesRead;

            if (totalBytes != -1 && progress != null)
            {
                var progressValue = (float)downloadedBytes / totalBytes * 100;
                progress.Report(progressValue);
            }
        }
    }
}
