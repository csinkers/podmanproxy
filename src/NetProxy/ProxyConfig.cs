using System.Text.Json.Serialization;

namespace NetProxy;

public class ProxyConfig
{
    [JsonPropertyName("protocol")]
    public string? Protocol { get; set; }

    [JsonPropertyName("localPort")]
    public ushort? LocalPort { get; set; }

    [JsonPropertyName("localIp")]
    public string? LocalIp { get; set; }

    [JsonPropertyName("forwardIp")]
    public string? ForwardIp { get; set; }

    [JsonPropertyName("forwardPort")]
    public ushort? ForwardPort { get; set; }
}