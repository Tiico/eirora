# BouncyCastle.Cryptography

The assembly the shared package `Eirora.CloudSaving` seals the cloud save's contribution part with (XChaCha20-Poly1305, ADR-0008).
.NET gets the same version from NuGet through `Directory.Packages.props`; Unity compiles against this copy, which only `Eirora.CloudSaving.asmdef` references (the import is not auto-referenced).

| | |
|---|---|
| Package | [BouncyCastle.Cryptography](https://www.nuget.org/packages/BouncyCastle.Cryptography) |
| Version | 2.7.0 |
| File | `lib/netstandard2.0/BouncyCastle.Cryptography.dll` from the package |
| SHA-256 of the DLL | `d61c1f2ba929a230a58e101ccd850e21f2675fa6b9814ec279633e8a089c3495` |
| SHA-256 of the package | `f091ffccab4d03993e660bace277659a79dee0972f54d7f1f4bd46d680966241` |
| License | MIT, see `LICENSE.md` |

Update it with `scripts/update-bouncycastle.sh <version>`, which fetches the package from NuGet, replaces the DLL and the license and prints the hashes for this table.
Bump `Directory.Packages.props` to the same version.
