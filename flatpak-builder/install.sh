#!/bin/bash

FLATPAK_APP_ID="dod.unrealfrontiers.launcher"
FLATPAK_PACKAGE_URL="https://unrealfrontiers.xyz/updateclient/download_file/launcher?file=unrealfrontiers-launcher.flatpak"
FLATPAK_PACKAGE_NAME="UnrealFrontiersLauncher.flatpak"

if ! command -v flatpak &>/dev/null; then
    echo "Flatpak is not installed. Installing Flatpak..."
    
    if [ -f /etc/debian_version ]; then
        sudo apt update && sudo apt install -y flatpak
    fi

    if [ -f /etc/redhat-release ]; then
        sudo dnf install -y flatpak
    fi
fi

echo "Downloading Flatpak package from $FLATPAK_PACKAGE_URL..."
wget -O "$FLATPAK_PACKAGE_NAME" "$FLATPAK_PACKAGE_URL"

echo "Installing $FLATPAK_PACKAGE_NAME..."
flatpak install -y --user "$FLATPAK_PACKAGE_NAME"

if flatpak list | grep -q "$FLATPAK_APP_ID"; then
    echo "$FLATPAK_APP_ID installed successfully!"
else
    echo "Installation failed!"
    exit 1
fi
