using System.Net;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;

namespace PodmanProxy;

public class PodmanProxyOptions
{
    // e.g. udp: 192.168.153.1:53 -> 172.25.132.240:53
    static readonly Regex ProxyPattern = new(@"([a-z]+):\s*([0-9.]+):(\d+)\s*->([^:]+):(\d+)");
    public ushort ControlPort { get; set; } = 9188;
    public List<string> Proxies { get; set; } = new();

    public List<ProxyConfig> ParseProxyConfigs(ILogger log, IPAddress remoteAddress)
    {
        var results = new List<ProxyConfig>();

        foreach (var s in Proxies)
        {
            var withRemote = s.Replace("$remote", remoteAddress.ToString());

            var m = ProxyPattern.Match(withRemote);
            if (!m.Success)
            {
                log.LogError("Could not parse proxy config \"{line}\" with $remote = \"{remoteAddress}\"", s, remoteAddress);
                continue;
            }

            if (!Enum.TryParse<Protocol>(m.Groups[1].Value, true, out var protocol))
            {
                log.LogError("Could not parse protocol \"{protocol}\"", m.Groups[1].Value);
                continue;
            }

            if (!ushort.TryParse(m.Groups[3].Value, out var localPort))
            {
                log.LogError("Could not parse port \"{port}\"", m.Groups[3].Value);
                continue;
            }

            if (!ushort.TryParse(m.Groups[5].Value, out var remotePort))
            {
                log.LogError("Could not parse port \"{port}\"", m.Groups[5].Value);
                continue;
            }

            var localIp = m.Groups[2].Value.Trim();
            var remoteIp = m.Groups[4].Value.Trim();

            results.Add(new ProxyConfig(s, protocol, localIp, localPort, remoteIp, remotePort));
        }

        return results;
    }
}
