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

systemctl stop podmanproxy-client.service

cp PodmanProxy.Client /usr/sbin/podmanproxy-client
cp podmanproxy-client.service /etc/systemd/system/podmanproxy-client.service

systemctl daemon-reload
systemctl start podmanproxy-client.service
systemctl enable podmanproxy-client.service
systemctl status podmanproxy-client

