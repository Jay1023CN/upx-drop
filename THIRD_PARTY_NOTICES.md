# Third-party notices

## UPX — the Ultimate Packer for eXecutables

- Authors: Markus Oberhumer, Laszlo Molnar, John Reiser and contributors.
- Official website: https://upx.github.io/
- Official source: https://github.com/upx/upx
- Pinned release: https://github.com/upx/upx/releases/tag/v5.2.1
- License: GNU General Public License version 2 or later; upstream also offers its special exception and restrictions for compressed executables. Full original terms: `tools/UPX-LICENSE` and `LICENSE`.
- Windows x64 archive SHA-256: eabc6792a347d45e945be7748423e7868fd01b0d2bcaa2f4b1031fd71ff69bda
- Source archive SHA-256: a7d457be4ef942e46844ee8f301206b111394cbcbde3599747a6904c54ff116b

UpxDrop embeds the unmodified official Windows x64 archive as an EXE resource, verifies it, and extracts the engine into the current user's local application cache. UPX runs as a separate process. UpxDrop does not modify UPX, its stub, copyright strings, or compressed-file format.

The application's About / Licenses dialog includes these notices, the GPL and UPX's original terms. Every binary Release must also provide the corresponding source ZIP produced by package.ps1, containing application sources, build scripts and the verified complete official upx-5.2.1-src.tar.xz archive. The source ZIP is a distribution artifact; it is not a runtime dependency beside the executable.

The UPX exception applies to compressed programs under upstream's stated conditions. It does not replace the license on UPX itself. See the original agreement for the controlling terms. This project is an independent frontend, not affiliated with upstream UPX.
