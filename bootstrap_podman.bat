@echo off
wsl -d podman-machine-default sudo dnf install -y git dotnet-sdk-8.0 htop vim
wsl -d podman-machine-default cd ~ ^&^& git clone https://github.com/csinkers/podmanproxy
wsl -d podman-machine-default cd ~/podmanproxy/src/PodmanProxy.Client ^&^& sudo ./setup-client.sh

