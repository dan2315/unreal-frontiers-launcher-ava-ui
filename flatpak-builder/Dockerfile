FROM debian:bullseye

RUN apt-get update && apt-get install -y \
    flatpak \
    flatpak-builder \
    git \
    python3 \
    build-essential \
    && apt-get clean

# Set up Flatpak repository
RUN flatpak remote-add --if-not-exists flathub https://flathub.org/repo/flathub.flatpakrepo
RUN flatpak install -y flathub org.freedesktop.Sdk//21.08 org.freedesktop.Platform//21.08

# Set working directory
WORKDIR /app

# Copy your Flatpak manifest and application code
COPY ./manifest.json ./manifest.json
COPY ./publish/ ./publish/

# Command to build the Flatpak
CMD ["flatpak-builder", "--force-clean", "build-dir", "manifest.json"]