# Use Debian as the base image
FROM debian:bullseye

# Install necessary dependencies
RUN apt-get update
RUN apt-get install -y \
    flatpak \
    flatpak-builder \
    python3 \
    wget \
    ca-certificates \
    openssh-client \
    dbus-x11 \
    && apt-get clean

# Add Flathub as a remote repository
RUN flatpak remote-add --if-not-exists flathub https://flathub.org/repo/flathub.flatpakrepo

# Install the Freedesktop SDK and Platform (23.08 version for .NET 8 support)
RUN flatpak install -y --system flathub org.freedesktop.Sdk//23.08 org.freedesktop.Platform//23.08
RUN flatpak install -y --system flathub org.freedesktop.Sdk.Extension.dotnet8//23.08

# Download the Flatpak dotnet dependency generator script

# Create SSH directory and copy the private key
RUN mkdir -p /root/.ssh
COPY keys/key /root/.ssh/id_ed25519
COPY keys/key.pub /root/.ssh/id_ed25519.pub

# Set correct permissions
RUN chmod 600 /root/.ssh/id_ed25519 && \
    chmod 644 /root/.ssh/id_ed25519.pub

RUN ssh-keyscan github.com >> ~/.ssh/known_hosts

# Set the working directory
WORKDIR /app

# Copy project files into the container
COPY . .

RUN git clone git@github.com:dan2315/unreal-frontiers-launcher-ava-ui.git

RUN wget https://raw.githubusercontent.com/flatpak/flatpak-builder-tools/master/dotnet/flatpak-dotnet-generator.py -O flatpak-dotnet-generator.py && chmod +x flatpak-dotnet-generator.py

CMD ["/bin/bash"]