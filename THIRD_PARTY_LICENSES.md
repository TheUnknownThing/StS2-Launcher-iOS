# Third-Party Licenses

The iOS port uses Godot, .NET NativeAOT, Mono.Cecil and native FMOD/Spine
components. Game files, dependency binaries and SDK downloads are not distributed in this source
tree. The project's MIT license does not replace any dependency's own terms.

## Original Launcher
- **License**: MIT, copyright (c) 2026 Eky
- **Source**: https://github.com/Ekyso/StS2-Launcher
- **Notice**: The root [LICENSE](LICENSE) preserves the original copyright and
  permission notice for the code this port builds on.

## Community iOS Toolchain
- **License**: MIT, copyright (c) 2026 jhaizhou-ops
- **Source**: https://github.com/jhaizhou-ops/sts2-ios
- **Revision**: `be1144212c7d5ac5d6c78fa56f7cb1d969397389`
- **Imported and adapted**: `src/STS2Weaver`, selected patches and helper in
  `src/STS2MobileIos`, the NativeAOT injection project, and PCK utilities in
  `scripts/ios`.
- **License text**: [third-party/licenses/sts2-ios.LICENSE](third-party/licenses/sts2-ios.LICENSE).
  This license covers the community code, not the game or proprietary middleware.

## Mono.Cecil
- **License**: MIT
- **Source**: https://github.com/jbevain/cecil
- **Version**: 0.11.6, used by the iOS static assembly weaver.

## FMOD Godot Extension
- **License**: MIT (extension); FMOD itself has separate proprietary terms.
- **Source**: https://github.com/utopia-rise/fmod-gdextension
- **Version**: 6.1.0-4.5.0 for the iOS build.

## Godot Engine
- **License**: MIT
- **Copyright**: (c) 2014-present Godot Engine contributors, (c) 2007-2014 Juan Linietsky, Ariel Manzur
- **Source**: https://github.com/godotengine/godot

## HarmonyLib (0Harmony)
- **License**: MIT
- **Copyright**: (c) Andreas Pardeike
- **Source**: https://github.com/pardeike/Harmony
- **Note**: A dependency of the locally supplied game assembly. The iOS port
  applies its own hooks statically and does not enable runtime Harmony mods.

## .NET Runtime (NativeAOT)
- **License**: MIT
- **Copyright**: (c) .NET Foundation and Contributors
- **Source**: https://github.com/dotnet/runtime

## Steam protocol references
- **Reference**: https://github.com/SteamDatabase/Protobufs
- **Use**: Field numbers for authentication, client login, and Cloud messages.
  The launcher implements a limited wire reader/writer; it does not bundle
  SteamKit2, generated Steam message classes, or the Steam client.

## FMOD
- **License**: Proprietary (FMOD EULA)
- **Copyright**: (c) Firelight Technologies Pty Ltd
- **Website**: https://www.fmod.com
- **Attribution**: Made using FMOD Studio by Firelight Technologies Pty Ltd.
- **Note**: FMOD binaries are not included in this source repository. The iOS
  bootstrap retrieves dependencies from the FMOD Godot extension release. Review
  the current [FMOD licensing](https://www.fmod.com/licensing) and
  [attribution requirements](https://www.fmod.com/attribution) before building or
  distributing a product. A public download does not grant unrestricted
  redistribution rights.

## Spine Runtimes

Copyright (c) 2013-2025, Esoteric Software LLC

Integration of the Spine Runtimes into software or otherwise creating derivative works of the Spine Runtimes is permitted under the terms and conditions of Section 2 of the Spine Editor License Agreement:
http://esotericsoftware.com/spine-editor-license#s2

Otherwise, it is permitted to integrate the Spine Runtimes into software or otherwise create derivative works of the Spine Runtimes (collectively, "Products"), provided that each user of the Products must obtain their own Spine Editor license and redistribution of the Products in any form must include this license and copyright notice.

THE SPINE RUNTIMES ARE PROVIDED BY ESOTERIC SOFTWARE LLC "AS IS" AND ANY EXPRESS OR IMPLIED WARRANTIES, INCLUDING, BUT NOT LIMITED TO, THE IMPLIED WARRANTIES OF MERCHANTABILITY AND FITNESS FOR A PARTICULAR PURPOSE ARE DISCLAIMED. IN NO EVENT SHALL ESOTERIC SOFTWARE LLC BE LIABLE FOR ANY DIRECT, INDIRECT, INCIDENTAL, SPECIAL, EXEMPLARY, OR CONSEQUENTIAL DAMAGES (INCLUDING, BUT NOT LIMITED TO, PROCUREMENT OF SUBSTITUTE GOODS OR SERVICES, BUSINESS INTERRUPTION, OR LOSS OF USE, DATA, OR PROFITS) HOWEVER CAUSED AND ON ANY THEORY OF LIABILITY, WHETHER IN CONTRACT, STRICT LIABILITY, OR TORT (INCLUDING NEGLIGENCE OR OTHERWISE) ARISING IN ANY WAY OUT OF THE USE OF THE SPINE RUNTIMES, EVEN IF ADVISED OF THE POSSIBILITY OF SUCH DAMAGE.

Spine runtime binaries are built locally and are not included in this source
repository. Obtain a Spine Editor license at https://esotericsoftware.com.
