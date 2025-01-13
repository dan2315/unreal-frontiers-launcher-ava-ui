# Base Image
FROM debian:bullseye

# Install Flatpak and Flatpak Builder
RUN apt-get update && apt-get install -y \
    flatpak \
    flatpak-builder \
    wget \
    ca-certificates \
    && apt-get clean

# Add Flathub as a remote repository
RUN flatpak remote-add --if-not-exists flathub https://flathub.org/repo/flathub.flatpakrepo

# Install the Freedesktop SDK and Platform (21.08 version)
RUN flatpak install -y flathub org.freedesktop.Sdk//21.08 org.freedesktop.Platform//21.08

# Set working directory
WORKDIR /app

# Copy project files
COPY . .

# Build the Flatpak and create a bundle
CMD flatpak-builder --force-clean build-dir manifest.json && \
    flatpak build-export repo build-dir && \
    flatpak build-bundle repo myapp.flatpak dod.unrealfrontiers.launcher 1.0 && \
    echo "Flatpak bundle created: myapp.flatpak"
