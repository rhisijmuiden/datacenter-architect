#!/bin/sh
# Adds Datacenter Architect to your application menu (for your user only, no root needed).
# Run it from the unpacked game folder: ./install.sh   Remove the menu entry again with: ./install.sh --remove
set -eu
here="$(cd "$(dirname "$0")" && pwd)"
apps="${XDG_DATA_HOME:-$HOME/.local/share}/applications"
icons="${XDG_DATA_HOME:-$HOME/.local/share}/icons/hicolor/scalable/apps"
entry="$apps/datacenter-architect.desktop"
if [ "${1:-}" = "--remove" ]; then
  rm -f "$entry" "$icons/datacenter-architect.svg"
  echo "Removed the menu entry. Your savegames stay in ${XDG_DATA_HOME:-$HOME/.local/share}/Datacenter Architect."
  exit 0
fi
[ -x "$here/DatacenterArchitect.x86_64" ] || chmod +x "$here/DatacenterArchitect.x86_64"
mkdir -p "$apps" "$icons"
cp "$here/icon.svg" "$icons/datacenter-architect.svg"
# the desktop entry points at this folder; the in-game updater replaces the folder in place, so the entry keeps working
esc="$(printf '%s' "$here" | sed 's/[\\"`$]/\\&/g')"
cat > "$entry" <<DESKTOP
[Desktop Entry]
Type=Application
Name=Datacenter Architect
Comment=Build and run your own datacenter
Comment[nl]=Bouw en beheer je eigen datacenter
Exec="$esc/DatacenterArchitect.x86_64"
Path=$here
Icon=datacenter-architect
Terminal=false
Categories=Game;Simulation;
StartupWMClass=Datacenter Architect
DESKTOP
chmod +x "$entry"
command -v update-desktop-database >/dev/null 2>&1 && update-desktop-database "$apps" >/dev/null 2>&1 || true
echo "Datacenter Architect is now in your application menu."
