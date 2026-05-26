using System.Net.Http.Json;
using shmtu.captcha.ocr.cli.Models;

namespace shmtu.captcha.ocr.cli.Services;

/// <summary>
/// RESTful HTTP 协议 OCR 客户端，连接远端 RESTful 服务器进行验证码识别。
/// </summary>
public class OcrHttpClient : IDisposable
{
    private readonly HttpClient _httpClient;
    private readonly string _baseUrl;

    public OcrHttpClient(string baseUrl)
    {
        _baseUrl = baseUrl.TrimEnd('/');
        _httpClient = new HttpClient
        {
            Timeout = TimeSpan.FromSeconds(30)
        };
    }

    /// <summary>
    /// 通过 RESTful API 识别验证码图片
    /// </summary>
    public async Task<OcrHttpResponse> RecognizeAsync(byte[] imageData)
    {
        var base64 = Convert.ToBase64String(imageData);
        var request = new OcrRequest { ImageBase64 = base64 };
        var response = await _httpClient.PostAsJsonAsync($"{_baseUrl}/api/ocr", request);
        response.EnsureSuccessStatusCode();
        var result = await response.Content.ReadFromJsonAsync<OcrHttpResponse>();
        return result ?? throw new InvalidOperationException("服务器返回空响应");
    }

    /// <summary>
    /// 健康检查
    /// </summary>
    public async Task<HealthResponse> HealthCheckAsync()
    {
        var result = await _httpClient.GetFromJsonAsync<HealthResponse>($"{_baseUrl}/api/health");
        return result ?? throw new InvalidOperationException("服务器返回空响应");
    }

    public void Dispose()
    {
        _httpClient.Dispose();
    }
}
