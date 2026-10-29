# Aquarium Civ

A tiny 3D civilization living in a glass terrarium: an idle civ builder for iOS and Android, made with Unity 6 (URP).
The civ grows on its own, even while you're away. You come back, read what happened, and look after the tank.

Early prototyping. Design docs start at [docs/README.md](docs/README.md).

## Building
- Unity **6000.3.27f1** (6.3 LTS). Open `AquariumCiv/`. Binaries are in Git LFS: `git lfs install` before cloning.
- CI and repository security: [docs/ci.md](docs/ci.md). iOS/TestFlight builds: [docs/testflight.md](docs/testflight.md).

## License
Copyleft. Copyright © 2026 Tommaso Ribaudo.

| What | License |
|------|---------|
| Code: C# scripts, shaders, editor tools, workflows, scripts under `Tools/` | [GPL-3.0-or-later](LICENSE) with the [Unity and app store permissions](LICENSE-EXCEPTION.md) |
| Original art, audio and writing: models, textures, icons, sounds, design docs in `docs/` | [CC BY-SA 4.0](LICENSES/CC-BY-SA-4.0.txt) |
| Third-party packs under `AquariumCiv/Assets/ThirdParty/` | Their own licenses (all CC0 so far), see [docs/assets-log.md](docs/assets-log.md) |
| Unity packages and the engine | Unity's terms, not covered by this repository's licenses |

"Aquarium Civ" and the app icon identify the official app. The licenses above cover the code and content, not the
right to publish your build under that name or icon.

Contributions are accepted under the same licenses.

## Security
See [SECURITY.md](SECURITY.md).
