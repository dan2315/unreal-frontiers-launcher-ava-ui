#!/bin/bash

# Set app details
APP_ID="dod.unrealfrontiers.launcher"
BUNDLE_NAME="unrealfrontiers-launcher.flatpak"
MANIFEST_FILE="dod.unrealfrontiers.launcher.yaml"
BUILD_DIR="flatpak-build"
REPO_DIR="flatpak-repo"
NUGET_SOURCES_DIR="nuget-sources"
PROJECT_FILE="../unreal-frontiers-launcher.csproj"

# Ensure Flatpak and Flatpak Builder are installed
if ! command -v flatpak &>/dev/null || ! command -v flatpak-builder &>/dev/null; then
    echo "Flatpak or Flatpak Builder is missing. Installing..."
    
    if [ -f /etc/debian_version ]; then
        sudo apt update && sudo apt install -y flatpak flatpak-builder python3 dotnet-sdk-8.0
    elif [ -f /etc/redhat-release ]; then
        sudo dnf install -y flatpak flatpak-builder python3 dotnet-sdk-8.0
    else
        echo "Unsupported OS. Install Flatpak manually."
        exit 1
    fi
fi

# Ensure the Flathub repository is added
if ! flatpak remote-list | grep -q "flathub"; then
    echo "Adding Flathub repository..."
    sudo flatpak remote-add --if-not-exists flathub https://flathub.org/repo/flathub.flatpakrepo
fi

# Install dependencies from Flathub
echo "Installing dependencies from Flathub..."
flatpak-builder "$BUILD_DIR" --system --install-deps-from=flathub --download-only "$MANIFEST_FILE"

# Run flatpak-dotnet-generator.py to generate NuGet sources
echo "Generating NuGet sources..."
python3 flatpak-dotnet-generator.py --dotnet 8 --freedesktop 23.08 "$NUGET_SOURCES_DIR/nuget-sources.json" "$PROJECT_FILE"

# Restore NuGet packages inside the NuGet sources directory
echo "Restoring NuGet packages..."
mkdir -p "$NUGET_SOURCES_DIR"
dotnet restore "$PROJECT_FILE" --packages "$NUGET_SOURCES_DIR"

# Clean previous build and build the app
echo "Building Flatpak package..."
sudo flatpak-builder "$BUILD_DIR" --system --force-clean --install --repo="$REPO_DIR" "$MANIFEST_FILE"

# Export the bundle
echo "Creating single Flatpak bundle: $BUNDLE_NAME"
flatpak build-bundle "$REPO_DIR" "$BUNDLE_NAME" "$APP_ID" --runtime-repo=https://flathub.org/repo/flathub.flatpakrepo

# Success message
if [ -f "$BUNDLE_NAME" ]; then
    echo "Flatpak bundle created successfully: $BUNDLE_NAME"
else
    echo "Failed to create Flatpak bundle!"
    exit 1
fi

# Test the installed Flatpak application
echo "Testing installed application..."
flatpak run "$APP_ID"
