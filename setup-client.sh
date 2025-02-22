#!/bin/sh
# This script is used to setup the client service on the WSL2 podman machine.

# Error if we're not root or sudo
if [ "$(id -u)" -ne 0 ]; then
	echo "Please run as root or sudo"
	exit 1
fi

# Detect if dotnet is installed
if ! command -v dotnet &> /dev/null
then
	echo "dotnet could not be found"
	exit 1
fi

dotnet publish

cp build/PodmanProxy.Client/bin/Release/net8.0/linux-x64/publish/PodmanProxy.Client /usr/sbin/podmanproxy-client
cp src/PodmanProxy.Client/podmanproxy-client.service /etc/systemd/system/podmanproxy-client.service

systemctl daemon-reload
systemctl start podmanproxy-client.service
systemctl enable podmanproxy-client.service
systemctl status podmanproxy-client

