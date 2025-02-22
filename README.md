PodmanProxy
========

This originated as a fork of https://github.com/Stormancer/netproxy

This particular proxy is for the rather specific use-case of running podman on
WSL2 under Windows 10 where the networking configuration only allows an
auto-configured virtual switch which picks a new IP each time it starts, and
podman only seems to allow forwarding TCP ports (and only to the 127.0.0.1
adaptor at that). Under Win11, the WSL2 networking is more flexible and allows
mirroring the adaptors of the host machine so a proxy isn't required.

The proxy server will wait in an idle state until it receives a packet from the
client on the control port (9188 by default). The NetProxy.Client process should
be run on the WSL2 machine in order to advertise its address. It simply
broadcasts a 1-byte UDP packet on the control port every 5 seconds so the server
can discover it.

Once the client address is discovered, the server will start listening on the
configured ports and forwarding traffic to the client machine. Any updates to
the config file, or a message from a new address on the control port (e.g. after
a WSL2 restart) will cause the proxy to reconfigure itself.

# Requirements:
- dotnet 8.0
- podman >= 5.4.0
- podman-desktop >= 1.16.2

