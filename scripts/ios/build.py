#!/usr/bin/env python3
"""Build the physical-device port using a locally installed Steam copy."""

import argparse
import hashlib
import json
import os
from pathlib import Path
import re
import shutil
import subprocess
import sys

from pck_ls import read_dir

ROOT = Path(__file__).resolve().parents[2]
CACHE = ROOT / ".cache"
IOS = ROOT / "ios"
DOTNET = ROOT / ".tools/dotnet/dotnet"
GODOT = ROOT / ".tools/godot/Godot_mono.app/Contents/MacOS/Godot"
DEFAULT_GAME = Path.home() / "Library/Application Support/Steam/steamapps/common/Slay the Spire 2/SlayTheSpire2.app"


def run(*args):
    print("+ " + " ".join(str(a) for a in args), flush=True)
    env = dict(os.environ, DOTNET_ROOT=str(DOTNET.parent))
    env["PATH"] = str(DOTNET.parent) + os.pathsep + env.get("PATH", "")
    subprocess.run([str(a) for a in args], cwd=ROOT, env=env, check=True)


def copy(source, destination):
    destination.parent.mkdir(parents=True, exist_ok=True)
    if source.is_dir():
        shutil.copytree(source, destination, dirs_exist_ok=True)
    else:
        shutil.copy2(source, destination)


def config():
    path = IOS / "config.local.json"
    if not path.exists():
        raise SystemExit("Create ios/config.local.json from ios/config.example.json first")
    value = json.loads(path.read_text())
    for key in ("team_id", "bundle_id"):
        if not re.fullmatch(r"[A-Za-z0-9.-]+", value[key]) or value[key].startswith("YOUR_"):
            raise SystemExit(f"Set a valid {key} in {path}")
    return value


def prepare(game):
    resources = game / "Contents/Resources"
    managed = resources / "data_sts2_macos_arm64"
    if not (managed / "sts2.dll").is_file():
        raise SystemExit(f"No supported macOS ARM64 game installation at {game}")
    framework_names = {p.name for p in (ROOT / ".tools/dotnet/shared/Microsoft.NETCore.App").glob("9.*/*.dll")}
    if not framework_names:
        raise SystemExit("Install the pinned .NET 9 SDK into .tools/dotnet first")
    for directory in (ROOT / "upstream/game-refs", IOS / "prebuilt/deps"):
        directory.mkdir(parents=True, exist_ok=True)
        for old in directory.glob("*.dll"):
            old.unlink()
    for dll in managed.glob("*.dll"):
        if dll.name in framework_names:
            continue
        copy(dll, ROOT / "upstream/game-refs" / dll.name)
        if dll.name not in {"sts2.dll", "GodotSharp.dll", "GodotSharpEditor.dll"}:
            copy(dll, IOS / "prebuilt/deps" / dll.name)
    (IOS / "prebuilt/.gdignore").touch()

    with (resources / "Slay the Spire 2.pck").open("rb") as pack:
        for name, offset, size, flags in read_dir(pack):
            if name not in {"addons/fmod/fmod.gdextension", "addons/spine/spine_godot_extension.gdextension"}:
                continue
            if flags & 1:
                raise SystemExit("Encrypted extensions are unsupported")
            pack.seek(offset)
            path = IOS / name
            path.parent.mkdir(parents=True, exist_ok=True)
            descriptor = pack.read(size).decode("utf-8")
            # Steam ships release frameworks; use those for the export editor too.
            descriptor = descriptor.replace(".macos.editor.framework", ".macos.template_release.framework")
            descriptor = descriptor.replace("libfmodL.dylib", "libfmod.dylib")
            descriptor = descriptor.replace("libfmodstudioL.dylib", "libfmodstudio.dylib")
            path.write_text(descriptor)

    frameworks = game / "Contents/Frameworks"
    for name in ("libGodotFmod.macos.template_release.framework", "libfmod.dylib", "libfmodstudio.dylib"):
        copy(frameworks / name, IOS / "addons/fmod/libs/macos" / name)
    copy(frameworks / "libspine_godot.macos.template_release.framework",
         IOS / "addons/spine/macos/libspine_godot.macos.template_release.framework")
    for name in ("libGodotFmod.ios.template_release.xcframework", "libfmod_iphoneos.a", "libfmodstudio_iphoneos.a"):
        copy(ROOT / "vendor/fmod-release/fmod/libs/iOS" / name, IOS / "addons/fmod/libs/ios" / name)
    name = "libspine_godot.ios.template_release.framework"
    copy(ROOT / "vendor/spine-runtimes/spine-godot/bin/ios" / name, IOS / "addons/spine/ios" / name)


def content(game):
    from pck_add_cs import patch as add_scripts
    from pck_patch_sentry import patch as remove_sentry

    managed = game / "Contents/Resources/data_sts2_macos_arm64/sts2.dll"
    paths = sorted(set(re.findall(rb"res://[A-Za-z0-9_/.]+\.cs", managed.read_bytes())))
    if not paths:
        raise SystemExit("No embedded Godot script paths found; unsupported game version")
    paths_file = CACHE / "cs_paths.txt"
    paths_file.write_bytes(b"\n".join(paths) + b"\n")
    source = game / "Contents/Resources/Slay the Spire 2.pck"
    remove_sentry(source, CACHE / "game-nosentry.pck")
    add_scripts(CACHE / "game-nosentry.pck", paths_file, CACHE / "StS2.pck")
    metadata = {
        "assembly_sha256": hashlib.sha256(managed.read_bytes()).hexdigest(),
        "source_pack_bytes": source.stat().st_size,
        "script_count": len(paths),
    }
    (CACHE / "content-input.json").write_text(json.dumps(metadata, indent=2) + "\n")


def export_host():
    cfg = config()
    # A separate shell prevents Godot's editor from also building both simulators.
    host = CACHE / "ios-host"
    host.mkdir(parents=True, exist_ok=True)
    for name in ("project.godot", "bootstrap.tscn", "icon.svg"):
        copy(IOS / name, host / name)
    copy(IOS / "addons", host / "addons")
    preset = (IOS / "export_presets.template.cfg").read_text()
    for key, value in {
        "TEAM_ID": cfg["team_id"], "BUNDLE_ID": cfg["bundle_id"],
        "TEMPLATE": str(ROOT / ".tools/templates/ios.zip"),
    }.items():
        preset = preset.replace("@" + key + "@", value)
    (host / "export_presets.cfg").write_text(preset)
    (IOS / "build").mkdir(exist_ok=True)
    (IOS / "build/.gdignore").touch()
    # Let extension documentation callbacks drain before editor shutdown.
    run(GODOT, "--headless", "--path", host, "--editor", "--quit-after", "60")
    run(GODOT, "--headless", "--path", host, "--export-release", "iOS", IOS / "build/StS2.ipa")


def build(refresh_host):
    cfg = config()
    run(DOTNET, "build", "src/STS2MobileIos", "-c", "Release")
    run(DOTNET, "build", "src/STS2Weaver", "-c", "Release")
    hooks = ROOT / "src/STS2MobileIos/bin/Release/net9.0/STS2MobileIos.dll"
    run(DOTNET, "src/STS2Weaver/bin/Release/net9.0/STS2Weaver.dll",
        "upstream/game-refs/sts2.dll", hooks, "src/STS2MobileIos/manifest.json", "ios/prebuilt/sts2.dll")
    copy(hooks, IOS / "prebuilt/deps/STS2MobileIos.dll")
    run(DOTNET, "publish", "ios/sts2.csproj", "-c", "ExportRelease", "-r", "ios-arm64",
        "-p:GodotTargetPlatform=ios", "--self-contained")
    if refresh_host or not (IOS / "build/StS2.xcodeproj").exists():
        export_host()
    run(sys.executable, "scripts/ios/prepare_xcode.py")
    run("xcodebuild", "-project", "ios/build/StS2.xcodeproj", "-scheme", "StS2",
        "-configuration", "Release", "-destination", "generic/platform=iOS",
        "-derivedDataPath", ".cache/xcode", "-allowProvisioningUpdates",
        "-allowProvisioningDeviceRegistration", "DEVELOPMENT_TEAM=" + cfg["team_id"], "build")


def install(push_content):
    cfg = config()
    if not cfg.get("device") or cfg["device"].startswith("YOUR_"):
        raise SystemExit("Set device to the iPad identifier from xcrun devicectl list devices")
    app = CACHE / "xcode/Build/Products/Release-iphoneos/StS2.app"
    if not (app / "Info.plist").is_file():
        raise SystemExit("Build the signed app first")
    # Upgrade in place: uninstalling would erase this app's fresh saves and content.
    run("xcrun", "devicectl", "device", "install", "app", "--device", cfg["device"], app)
    if push_content:
        run("xcrun", "devicectl", "device", "copy", "to", "--device", cfg["device"],
            "--source", CACHE / "StS2.pck", "--destination", "Documents/StS2.pck",
            "--domain-type", "appDataContainer", "--domain-identifier", cfg["bundle_id"])
    print("Installed. Launch StS2 iOS on the iPad.")


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("stage", choices=("prepare", "content", "export-host", "build", "install"))
    parser.add_argument("--game", type=Path, default=DEFAULT_GAME)
    parser.add_argument("--export-host", action="store_true", help="Regenerate the Xcode host during build")
    parser.add_argument("--content", action="store_true", help="Transfer the game pack during install (about 2 GB)")
    args = parser.parse_args()
    CACHE.mkdir(exist_ok=True)
    if args.stage == "prepare":
        prepare(args.game.expanduser().resolve())
    elif args.stage == "content":
        content(args.game.expanduser().resolve())
    elif args.stage == "export-host":
        export_host()
    elif args.stage == "build":
        build(args.export_host)
    elif args.stage == "install":
        install(args.content)


if __name__ == "__main__":
    main()
