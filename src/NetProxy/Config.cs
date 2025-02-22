using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;

namespace NetProxy;

public class Config
{
    // e.g. udp: 192.168.153.1:53 -> 172.25.132.240:53
    static readonly Regex ProxyPattern = new(@"([a-z]+):\s*([0-9.]+):(\d+)\s*->([^:]+):(\d+)");
    static readonly JsonSerializerOptions Options = new()
    {
        AllowTrailingCommas = true,
        ReadCommentHandling = JsonCommentHandling.Skip
    };

    public static Config Parse(string json) =>
        JsonSerializer.Deserialize<Config>(json, Options)
        ?? throw new FormatException("Config could not be parsed");

    [JsonPropertyName("proxies")]
    public List<string> Proxies { get; set; } = new();

    public Config WithRemote(IPAddress address) =>
        new()
        {
            Proxies =
                Proxies
                .Select(p => p.Replace("$remote", address.ToString()))
                .ToList()
        };

    public List<ProxyConfig> ParseProxyConfigs(ILogger log)
    {
        var results = new List<ProxyConfig>();

        foreach (var s in Proxies)
        {
            var m = ProxyPattern.Match(s);
            if (!m.Success)
            {
                log.LogError("Could not parse proxy config \"{s}\"", s);
                continue;
            }

            if (!Enum.TryParse<Protocol>(m.Groups[1].Value, true, out var protocol))
            {
                log.LogError($"Could not parse protocol \"{m.Groups[1].Value}\"");
                continue;
            }

            if (!ushort.TryParse(m.Groups[3].Value, out var localPort))
            {
                log.LogError($"Could not parse port \"{m.Groups[3].Value}\"");
                continue;
            }

            if (!ushort.TryParse(m.Groups[5].Value, out var remotePort))
            {
                log.LogError($"Could not parse port \"{m.Groups[5].Value}\"");
                continue;
            }

            var localIp = m.Groups[2].Value.Trim();
            var remoteIp = m.Groups[4].Value.Trim();

            results.Add(new ProxyConfig(s, protocol, localIp, localPort, remoteIp, remotePort));
        }

        return results;
    }
}
