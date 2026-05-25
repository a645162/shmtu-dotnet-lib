namespace shmtu.captcha.onnx.server.Models;

public class OcrResponse
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

public class OcrRequest
{
    public string? ImageBase64 { get; set; }
}

public class HealthResponse
{
    public string Status { get; set; } = "healthy";
    public bool ModelsLoaded { get; set; }
    public int PoolSize { get; set; }
}
