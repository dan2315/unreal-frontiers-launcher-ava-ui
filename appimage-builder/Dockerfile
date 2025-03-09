FROM ubuntu:latest

# Install dependencies
RUN apt-get update && apt-get install -y \
    wget \
    software-properties-common \
    dotnet-sdk-8.0 \
    libfuse2 \
    && rm -rf /var/lib/apt/lists/*

# Set up working directory
WORKDIR /app

# Copy application files
COPY . /app

# Restore and build the application
RUN dotnet publish -c Release -r linux-x64 --self-contained false -o /app/out

# Download and set up appimagetool
RUN apt-get update && apt-get install -y wget \
    && wget https://github.com/AppImage/AppImageKit/releases/latest/download/appimagetool-x86_64.AppImage \
    && chmod +x appimagetool-x86_64.AppImage \
    && mv appimagetool-x86_64.AppImage /usr/local/bin/appimagetool

RUN apt-get update && apt-get install -y fuse libfuse2

# Ensure AppImage compatibility
RUN chmod +x /usr/local/bin/appimagetool

# Prepare AppDir structure
RUN mkdir -p /app/AppDir/usr/bin && cp -r /app/out/* /app/AppDir/usr/bin/ \
    && ls -lah /app/AppDir && ls -lah /app/AppDir/usr/bin

# Create an AppRun script
RUN echo "#!/bin/bash\nexec \$APPDIR/usr/bin/UnrealFrontiersLauncher" > /app/AppDir/AppRun && chmod +x /app/AppDir/AppRun

# Debugging: Verify structure before running appimagetool
RUN ls -lah /app/AppDir && ls -lah /app/AppDir/usr/bin

# Create the AppImage
RUN appimagetool /app/AppDir /app/UnrealFrontiersLauncher.AppImage

# Set the entry point
CMD ["/app/UnrealFrontiersLauncher.AppImage"]
