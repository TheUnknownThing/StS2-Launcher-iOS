#!/usr/bin/env python3
"""Attach the device-only NativeAOT framework to Godot's generated Xcode host."""

import json
from pathlib import Path
import plistlib
import re
import shutil
import subprocess

ROOT = Path(__file__).resolve().parents[2]


def main():
    config = json.loads((ROOT / "ios/config.local.json").read_text())
    build = ROOT / "ios/build"
    source = ROOT / "ios/.godot/mono/temp/bin/ExportRelease/ios-arm64/publish/sts2.dylib"
    if not source.is_file() or source.stat().st_size == 0:
        raise SystemExit("Publish the iOS NativeAOT library first")
    framework = build / "StS2/dylibs/sts2.framework"
    framework.mkdir(parents=True, exist_ok=True)
    shutil.copy2(source, framework / "sts2")
    subprocess.run([
        "xcrun", "install_name_tool", "-id", "@rpath/sts2.framework/sts2",
        str(framework / "sts2"),
    ], check=True)
    (framework / "Info.plist").write_bytes(plistlib.dumps({
        "CFBundleExecutable": "sts2",
        "CFBundleIdentifier": config["bundle_id"] + ".runtime",
        "CFBundleName": "sts2",
        "CFBundlePackageType": "FMWK",
        "CFBundleShortVersionString": "1.0",
        "CFBundleVersion": "1",
        "MinimumOSVersion": "14.0",
    }))

    project = build / "StS2.xcodeproj/project.pbxproj"
    text = project.read_text()
    file_id, copy_id = "535453320000000000000001", "535453320000000000000002"
    if file_id not in text:
        text = text.replace("/* End PBXFileReference section */", f'''
        {file_id} = {{isa = PBXFileReference; lastKnownFileType = wrapper.framework;
            path = "StS2/dylibs/sts2.framework"; sourceTree = "<group>"; }};
/* End PBXFileReference section */''')
        text = text.replace("/* End PBXBuildFile section */", f'''
        {copy_id} = {{isa = PBXBuildFile; fileRef = {file_id};
            settings = {{ATTRIBUTES = (CodeSignOnCopy, ); }}; }};
/* End PBXBuildFile section */''')
        start = text.index("/* Begin PBXCopyFilesBuildPhase section */")
        position = text.index("files = (", start) + len("files = (")
        text = text[:position] + f"\n{copy_id}," + text[position:]
    # The native bridge supplies the playback audio session and device-only Keychain storage.
    text = text.replace('lastKnownFileType = sourcecode.cpp.cpp; path = dummy.cpp;',
                        'lastKnownFileType = sourcecode.cpp.objcpp; path = dummy.cpp;')
    if "-Wl,-export_dynamic" not in text:
        text = re.sub(r'(OTHER_LDFLAGS = ")([^"]*)(";)',
                      lambda match: match[1] + match[2] + ' -framework AVFoundation -framework Security -Wl,-export_dynamic' + match[3], text)
    if "-framework CoreImage" not in text:
        text = text.replace("-Wl,-export_dynamic", "-Wl,-export_dynamic -framework CoreImage")
    project.write_text(text)
    shutil.copy2(ROOT / "ios/Sts2Native.mm", build / "StS2/Sts2Native.mm")

    info_path = build / "StS2/StS2-Info.plist"
    info = plistlib.loads(info_path.read_bytes())
    info["godot_cmdline"] = ["--main-pack", "user://StS2.pck"]
    if config.get("profile", False):
        info["godot_cmdline"] += ["--", "--sts2-profile"]
    info["UIFileSharingEnabled"] = True
    info["LSSupportsOpeningDocumentsInPlace"] = True
    info["NSLocalNetworkUsageDescription"] = "Find and join Slay the Spire 2 games on your Wi-Fi network."
    info["NSBonjourServices"] = ["_sts2lan._udp"]
    orientations = ["UIInterfaceOrientationLandscapeLeft", "UIInterfaceOrientationLandscapeRight"]
    info["UISupportedInterfaceOrientations"] = orientations
    info["UISupportedInterfaceOrientations~ipad"] = orientations
    info_path.write_bytes(plistlib.dumps(info))

    dummy = build / "StS2/dummy.cpp"
    content = dummy.read_text()
    if "load_all_fmod_plugins" not in content:
        content += '''
// The game has no custom FMOD DSP plugins to register.
extern "C" __attribute__((visibility("default"))) __attribute__((used))
unsigned int *load_all_fmod_plugins(void *, unsigned int *count) {
    if (count) *count = 0;
    return nullptr;
}
'''
    if '#include "Sts2Native.mm"' not in content:
        content += '\n#include "Sts2Native.mm"\n'
    dummy.write_text(content)
    print("Prepared device-only framework, FMOD registration, and isolated game container")


if __name__ == "__main__":
    main()
