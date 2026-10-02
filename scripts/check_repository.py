#!/usr/bin/env python3
"""Check the staged source tree for private artifacts and broken documentation links."""

from pathlib import PurePosixPath
import posixpath
import re
import subprocess
import sys
from urllib.parse import unquote, urlsplit


FORBIDDEN_PARTS = {
    ".cache", ".tools", ".godot", "vendor", "upstream", "__pycache__", "bin", "obj", "logs", "android",
    "cloud-downloads", "save-backups",
}
FORBIDDEN_SUFFIXES = {
    ".log", ".jsonl", ".ndjson", ".ips", ".crash", ".save", ".pck", ".dll", ".dylib",
    ".so", ".a", ".apk", ".ipa", ".aar", ".jar", ".pdb", ".pyc", ".mobileprovision",
    ".p12", ".p8", ".key", ".pem", ".keystore", ".zip", ".tpz", ".gz",
}
PRIVATE_PATTERNS = (
    ("absolute home directory", re.compile(r"/(?:Users|home)/[A-Za-z0-9_.-]+/")),
    ("Apple development identity", re.compile(r"Apple Development: [^\n<]+\([A-Z0-9]{10}\)")),
    ("physical device UDID", re.compile(r"\b[0-9A-Fa-f]{8}-[0-9A-Fa-f]{16}\b")),
    ("private key", re.compile(r"-----BEGIN (?:RSA |EC |OPENSSH )?PRIVATE KEY-----")),
)


def git(*args):
    return subprocess.check_output(["git", *args])


def main():
    paths = set(filter(None, git("ls-files", "-z").decode().split("\0")))
    errors = []
    documents = {}
    for path in sorted(paths):
        name = PurePosixPath(path)
        if (set(name.parts) & FORBIDDEN_PARTS or name.suffix.lower() in FORBIDDEN_SUFFIXES
                or ".save." in name.name or name.name.endswith(".local.json")
                or path.startswith(("ios/build/", "ios/prebuilt/", "ios/addons/", "src/STS2Mobile/", "src/stubs/"))
                or any(part.endswith((".framework", ".xcframework", ".dSYM")) for part in name.parts)):
            errors.append(f"{path}: private/generated artifact is staged")
        data = git("show", ":" + path)
        if len(data) > 1_000_000:
            errors.append(f"{path}: source file exceeds 1 MB; review before publication")
        try:
            text = data.decode("utf-8-sig")
        except UnicodeDecodeError:
            # Public image assets can be reviewed separately; executable binaries cannot.
            if name.suffix.lower() not in {".png", ".jpg", ".jpeg", ".webp", ".ico"}:
                errors.append(f"{path}: unexpected binary file")
            continue
        for label, pattern in PRIVATE_PATTERNS:
            if pattern.search(text):
                errors.append(f"{path}: contains {label}")
        if name.suffix.lower() == ".md":
            documents[path] = text

    for path, text in documents.items():
        for target in re.findall(r"!?\[[^\]]*\]\(([^)]+)\)", text):
            target = target.strip().split(' "', 1)[0].strip("<>")
            parsed = urlsplit(target)
            if parsed.scheme or parsed.netloc or not parsed.path:
                continue
            resolved = posixpath.normpath(posixpath.join(posixpath.dirname(path), unquote(parsed.path)))
            if resolved not in paths and not any(p.startswith(resolved.rstrip("/") + "/") for p in paths):
                errors.append(f"{path}: broken local link to {target}")

    if errors:
        print("\n".join(errors), file=sys.stderr)
        return 1
    print(f"Checked {len(paths)} staged files and {len(documents)} Markdown documents; no rejected artifacts or broken file links.")
    return 0


if __name__ == "__main__":
    sys.exit(main())
