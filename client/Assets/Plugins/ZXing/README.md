# ZXing.Net

The assembly the client makes and reads QR codes with: the key transfer ticket shown on a new phone and scanned by the old one (ADR-0008, TASK-023b).
Only `Eirora.Client.asmdef` and the EditMode tests reference it; the import is not auto-referenced.

| | |
|---|---|
| Package | [ZXing.Net](https://www.nuget.org/packages/ZXing.Net) |
| Version | 0.16.11 |
| File | `lib/netstandard2.0/zxing.dll` from the package |
| SHA-256 of the DLL | `f3b823b6fd6492525a7547989056883def5d43be1e12c4f63fa54df73e3c5cfc` |
| SHA-256 of the package | `7d39234d668e558b3d374d18116ed57021d7c0df482801f1e1db4f1db9314ec3` |
| License | Apache 2.0, see `LICENSE.md` |

Update it with `scripts/update-zxing.sh <version>`, which fetches the package from NuGet, replaces the DLL and prints the hashes for this table.
