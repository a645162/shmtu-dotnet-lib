using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace shmtu.captcha.onnx.Backend;

/// <summary>
/// model-assets.json 根结构（schema v2）：
/// 同时支持"按模型分组"（models[*].artifacts[engine][precision]）与
/// "扁平回退"（FlatArtifacts: List&lt;ArtifactInfo&gt;）两种 schema。
/// </summary>
public class ReleaseManifest
{
    [JsonPropertyName("schema_version")]
    public int SchemaVersion { get; set; }

    [JsonPropertyName("model_count")]
    public int ModelCount { get; set; }

    [JsonPropertyName("modellist")]
    public List<string> ModelList { get; set; } = new();

    /// <summary>v2 新增：按模型分组的多模型信息。</summary>
    [JsonPropertyName("models")]
    public List<ModelInfo> Models { get; set; } = new();

    /// <summary>
    /// v1 兼容：扁平 artifacts 列表。
    /// 当 <see cref="Models"/> 为空时，作为回退源继续支持旧的单模型 / 列表式 manifest。
    /// </summary>
    [JsonPropertyName("artifacts")]
    public List<ArtifactInfo> FlatArtifacts { get; set; } = new();

    [JsonPropertyName("digests")]
    public List<object>? Digests { get; set; }
}

/// <summary>
/// 多模型 manifest 中的单个模型条目。
/// 包含 display_name、backbone、metrics、artifacts（按 engine+precision 嵌套字典）等。
/// </summary>
public class ModelInfo
{
    [JsonPropertyName("asset_stem")]
    public string AssetStem { get; set; } = "";

    [JsonPropertyName("display_name")]
    public string DisplayName { get; set; } = "";

    [JsonPropertyName("backbone")]
    public string Backbone { get; set; } = "";

    [JsonPropertyName("version")]
    public string Version { get; set; } = "";

    [JsonPropertyName("family")]
    public string Family { get; set; } = "";

    [JsonPropertyName("model_size_m")]
    public double? ModelSizeM { get; set; }

    [JsonPropertyName("metrics")]
    public ModelMetrics? Metrics { get; set; }

    [JsonPropertyName("supported_backbones")]
    public List<string> SupportedBackbones { get; set; } = new();

    /// <summary>
    /// 按 engine → precision 嵌套的 artifact 字典。
    /// 例: <c>artifacts["onnx"]["fp16"]</c>。
    /// </summary>
    [JsonPropertyName("artifacts")]
    public Dictionary<string, Dictionary<string, ArtifactInfo>>? Artifacts { get; set; }
}

/// <summary>模型指标。所有字段均为可空，允许 manifest 中省略部分指标。</summary>
public class ModelMetrics
{
    [JsonPropertyName("val_acc_expression")]
    public double? ValAccExpression { get; set; }

    [JsonPropertyName("val_loss")]
    public double? ValLoss { get; set; }

    [JsonPropertyName("test_acc_expression")]
    public double? TestAccExpression { get; set; }

    [JsonPropertyName("test_loss")]
    public double? TestLoss { get; set; }
}

/// <summary>
/// 通用 artifact 描述。引擎（onnx / pytorch / ncnn）+ 精度（fp16 / fp32 / int8）
/// + 文件列表（含 sha256）。
/// </summary>
public class ArtifactInfo
{
    [JsonPropertyName("engine")]
    public string Engine { get; set; } = "";

    [JsonPropertyName("precision")]
    public string Precision { get; set; } = "";

    [JsonPropertyName("format")]
    public string? Format { get; set; }

    [JsonPropertyName("files")]
    public List<AssetFile> Files { get; set; } = new();
}

/// <summary>artifact 内的单个文件（带 sha256）。</summary>
public class AssetFile
{
    [JsonPropertyName("path")]
    public string Path { get; set; } = "";

    [JsonPropertyName("release_asset_name")]
    public string ReleaseAssetName { get; set; } = "";

    [JsonPropertyName("sha256")]
    public string? Sha256 { get; set; }
}
