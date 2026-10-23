#!/usr/bin/env bash
# Installs @TITLE@ for the current user: the AppImage (it updates itself), a menu entry and the uflauncher:// handler
# (the browser returns to the launcher after a Discord login). No root needed.
#   curl -fsSL "@BASE_URL@/updateclient/download_file/launcher?file=install.sh" | bash
set -euo pipefail

APP="@APP@"
TITLE="@TITLE@"
BASE_URL="@BASE_URL@"
DIR="${XDG_DATA_HOME:-$HOME/.local/share}/$APP"
APPS="${XDG_DATA_HOME:-$HOME/.local/share}/applications"
IMAGE="$DIR/$APP.AppImage"
DESKTOP_ID="$APP.desktop"

download() {
    local file="$1" target="$2"
    if command -v curl >/dev/null 2>&1; then
        curl -fL --progress-bar -o "$target" "$BASE_URL/updateclient/download_file/launcher?file=$file"
    elif command -v wget >/dev/null 2>&1; then
        wget -O "$target" "$BASE_URL/updateclient/download_file/launcher?file=$file"
    else
        echo "Install curl or wget first." >&2
        exit 1
    fi
}

mkdir -p "$DIR" "$APPS"

echo "Downloading $TITLE..."
download "$APP.AppImage" "$IMAGE.part"
mv -f "$IMAGE.part" "$IMAGE"
chmod +x "$IMAGE"

# The icon comes out of the AppImage itself.
ICON="$DIR/icon.png"
if "$IMAGE" --appimage-extract '*.png' >/dev/null 2>&1; then
    found="$(find squashfs-root -maxdepth 1 -name '*.png' | head -n 1 || true)"
    [ -n "$found" ] && cp -f "$found" "$ICON"
    rm -rf squashfs-root
fi

# An AppImage needs FUSE 2. Without it the launcher still starts, but unpacks itself on every run (a little slower).
EXEC="\"$IMAGE\" %u"
if ! ldconfig -p 2>/dev/null | grep -q 'libfuse.so.2'; then
    echo "libfuse2 is not installed: the launcher will unpack itself on each start."
    echo "For faster starts install it (Debian/Ubuntu: sudo apt install libfuse2)."
    EXEC="env APPIMAGE_EXTRACT_AND_RUN=1 \"$IMAGE\" %u"
fi

cat > "$APPS/$DESKTOP_ID" <<EOF
[Desktop Entry]
Type=Application
Name=$TITLE
Comment=Unreal Frontiers launcher
Exec=$EXEC
Icon=$ICON
Terminal=false
Categories=Game;
MimeType=x-scheme-handler/uflauncher;
StartupWMClass=$APP
EOF

command -v update-desktop-database >/dev/null 2>&1 && update-desktop-database "$APPS" >/dev/null 2>&1 || true
command -v xdg-mime >/dev/null 2>&1 && xdg-mime default "$DESKTOP_ID" x-scheme-handler/uflauncher || true

echo "Done: $TITLE is in your applications menu ($IMAGE)."
