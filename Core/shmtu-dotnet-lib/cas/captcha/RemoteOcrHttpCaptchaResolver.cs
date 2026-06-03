namespace shmtu.cas.captcha;

public sealed class RemoteOcrHttpCaptchaResolver : ICaptchaResolver, IDisposable
{
    private readonly string _baseUrl;
    private readonly int _retryTimes;
    private readonly HttpClient _httpClient;

    public RemoteOcrHttpCaptchaResolver(string baseUrl, int retryTimes = 3)
    {
        _baseUrl = baseUrl.TrimEnd('/');
        _retryTimes = retryTimes;
        _httpClient = new HttpClient { Timeout = TimeSpan.FromSeconds(10) };
    }

    public async Task<CaptchaAnswer> ResolveAsync(byte[] imageData, CancellationToken cancellationToken = default)
    {
        Exception? lastException = null;
        for (var attempt = 0; attempt < _retryTimes; attempt++)
        {
            try
            {
                var base64Image = Convert.ToBase64String(imageData);
                var requestBody = $"{{\"imageBase64\":\"{base64Image}\"}}";
                var content = new StringContent(requestBody, System.Text.Encoding.UTF8, "application/json");
                var response = await _httpClient.PostAsync($"{_baseUrl}/api/ocr", content, cancellationToken);
                response.EnsureSuccessStatusCode();
                var responseBody = await response.Content.ReadAsStringAsync(cancellationToken);
                var json = System.Text.Json.JsonDocument.Parse(responseBody);
                var root = json.RootElement;

                if (!root.TryGetProperty("success", out var successProp) || !successProp.GetBoolean())
                {
                    var errorMsg = root.TryGetProperty("error", out var errorProp) ? errorProp.GetString() : "OCR 识别失败";
                    throw new Exception(errorMsg);
                }

                var expression = root.TryGetProperty("expression", out var exprProp) ? exprProp.GetString() : "";
                if (string.IsNullOrEmpty(expression))
                    throw new Exception("OCR 返回空表达式");

                return new CaptchaAnswer(expression, CaptchaAnswerKind.Expression);
            }
            catch (Exception ex)
            {
                lastException = ex;
            }
        }
        throw lastException ?? new Exception("OCR 识别失败");
    }

    public async Task<bool> HealthCheckAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var response = await _httpClient.GetAsync($"{_baseUrl}/api/health", cancellationToken);
            return response.IsSuccessStatusCode;
        }
        catch { return false; }
    }

    public void Dispose()
    {
        _httpClient.Dispose();
    }
}
