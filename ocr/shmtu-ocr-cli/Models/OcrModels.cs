namespace shmtu.captcha.ocr.cli.Models;

/// <summary>
/// RESTful OCR 接口请求模型
/// </summary>
public class OcrRequest
{
    public string? ImageBase64 { get; set; }
}

/// <summary>
/// RESTful OCR 接口响应模型
/// </summary>
public class OcrHttpResponse
{
    public bool Success { get; set; }
    public string Expression { get; set; } = "";
    public int Result { get; set; }
    public int EqualSymbol { get; set; }
    public int Operator { get; set; }
    public int Digit1 { get; set; }
    public int Digit2 { get; set; }
    public string? Error { get; set; }
}

/// <summary>
/// RESTful 健康检查响应模型
/// </summary>
public class HealthResponse
{
    public string Status { get; set; } = "";
    public bool ModelsLoaded { get; set; }
    public int PoolSize { get; set; }
}
