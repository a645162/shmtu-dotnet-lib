using System;
using System.IO;
using System.Text.Json;
using shmtu.captcha.onnx.Backend;
using Xunit;

public class RealManifestTest
{
    [Fact]
    public void ParseRealV204Manifest()
    {
        var json = File.ReadAllText("/tmp/model-assets-v2.0.4.json");
        var manifest = JsonSerializer.Deserialize<ReleaseManifest>(json);
        
        Assert.NotNull(manifest);
        Assert.Equal(2, manifest.SchemaVersion);
        Assert.Equal(4, manifest.Models.Count);
        Assert.Equal(4, manifest.ModelList.Count);
        
        // First model: mobilenet_v3_small
        var first = manifest.Models[0];
        Assert.Equal("mobilenet_v3_small", first.Backbone);
        Assert.Equal(1.48, first.ModelSizeM ?? 0, 0.01);
        Assert.NotNull(first.Metrics);
        Assert.True(first.Metrics!.ValAccExpression > 0.99);
        Assert.True(first.Metrics.TestAccExpression > 0.99);
        
        // Check grouped artifacts
        Assert.NotNull(first.Artifacts);
        Assert.True(first.Artifacts!.ContainsKey("ncnn"));
        Assert.True(first.Artifacts["ncnn"].ContainsKey("fp32"));
        Assert.Equal(2, first.Artifacts["ncnn"]["fp32"].Files.Count); // .param + .bin
        
        Console.WriteLine("✅ C#: real v2.0.4 manifest parsed correctly - 4 models, metrics OK");
    }
}
