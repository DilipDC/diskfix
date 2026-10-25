# DriveMedic

**Windows drive diagnostics and copy-first file recovery.** A lightweight WinForms utility for repair technicians and home users. Built for Windows 7 SP1, Windows 8/8.1, Windows 10, and Windows 11 using .NET Framework 4.5.2 to retain Windows 8 compatibility.

> **Recovery rule:** copy important files or create a sector-level image before running any repair that writes to a damaged source drive. If a drive is clicking, disconnecting, overheating, or reporting a hardware/SMART warning, stop scans and prioritize recovery or professional help.

## Features

- Lists mounted volumes and highlights the volume containing the currently running Windows installation. It reports volume capacity, filesystem, role, physical-disk mapping when available, and conservative hardware-health status.
- Queries Windows storage status and the legacy SMART failure-prediction WMI provider when the driver exposes it. **“No warning” does not mean “healthy”; USB bridges, NVMe drivers, RAID controllers, and some SSDs do not expose all health data.** It also cross-checks physical-device warnings before allowing a write-mode filesystem repair; if a volume cannot be mapped and any disk reports a warning, the repair is blocked conservatively.
- Browses old Windows user profiles on a secondary drive, including `Desktop`, `Documents`, `Downloads`, `Pictures`, `Music`, and `Videos` folders where present.
- Offers a copy-only recovery workflow. It never moves, deletes, or overwrites source files; filename conflicts are copied to a new ` (recovered N)` name. It skips directory and file reparse points (junctions/symbolic links), stages copies in temporary files before publishing them, compares file sizes, and can optionally verify each copy with SHA-256 without reading source bytes a second time. The UI warns when the selected destination appears to be on the same volume as a source.
- Scans file readability by reading file contents without writing to them. Reports access-denied, directory-enumeration, and read errors; it does not pretend that arbitrary file formats can be validated generically.
- Runs read-only `chkdsk` on a mounted drive and captures its output. A separate, confirmed **Repair filesystem** action opens an interactive elevated command window for `chkdsk X: /f`.
- Offers current-Windows `sfc /verifyonly` and `sfc /scannow` command launches. SFC targets the currently running Windows installation; it is not an offline repair mechanism.
- Shows findings with a recommended next step, activity log, and local TXT/CSV report export. The recovery result includes copied-file counts and bytes, SHA-256 verification counts, skipped-link counts, and per-file errors.
- Windows CI builds the Release solution, runs the safety/recovery test harness three times, and packages `DriveMedic-windows.zip` with a SHA-256 checksum.
- Official TestDisk & PhotoRec 7.2 Windows x64 and x86 archives are downloaded and checksum-verified during the build, embedded into `DriveMedic.exe`, and extracted automatically into the current user's Local AppData folder on first use. Users do not need to manually download or unpack TestDisk.

## Recovery Workbench (version 0.4)

The new start screen adds a copy-first workflow while keeping the original dashboard in **Drive inventory**.

- **Recover files:** queue selected files/folders or the visible top-level contents of a mounted drive, choose a destination, preserve existing destination files, and optionally verify copies with SHA-256. The app warns when Windows maps source and destination to the same physical disk.
- **Single file:** explicitly consent to a full-file read to compute SHA-256, then queue the file for a safe copy. A hash without a trusted reference does not prove that the file is correct.
- **Deleted files and partitions:** TestDisk & PhotoRec 7.2 are bundled inside `DriveMedic.exe`. The app extracts the official archive automatically on first use. Choose **Recover deleted files (GUI)** to open QPhotoRec, or **Find lost partitions / boot** for TestDisk's keyboard/menu interface. The corresponding upstream source archive is included in the full ZIP package under `ThirdParty/TestDisk`.
- **EFI / Windows / GRUB:** read-only boot/volume diagnostics and guided notes for UEFI, legacy MBR, GRUB, RAW filesystems, and offline Windows system files. Boot-write commands are not executed automatically against a guessed disk.

See [RECOVERY_WORKBENCH.md](RECOVERY_WORKBENCH.md) and [THIRD_PARTY_NOTICES.md](THIRD_PARTY_NOTICES.md) for operating steps, upstream links, hashes and license information. The bundled recovery executables are downloaded from [CGSecurity](https://www.cgsecurity.org/wiki/TestDisk_Download) during build and verified against published SHA-256 checksums.

**Recovery is not guaranteed.** Stop using media after accidental deletion, and save recovered files to another physical disk. Data overwritten by new writes generally cannot be reconstructed. A physically unstable disk should be imaged or handled by a professional recovery service.

## Build

Requirements: Visual Studio 2017 or later with the .NET desktop build tools, Python 3, network access to official CGSecurity downloads, and the .NET Framework 4.5.2 targeting pack. The build script downloads and checksum-verifies the two Windows tool archives plus matching source before validation, embedding, tests and packaging. The 4.5.2 target includes legacy Windows versions whose support has ended. On Windows, run:

```powershell
.\BUILD_WINDOWS.ps1
```

The app is intentionally not configured to require elevation at launch. Choose **Restart as admin** only when needed. A source volume does not have to be the currently booted Windows volume to browse and copy files, but encryption still applies: BitLocker volumes must be unlocked, and EFS files require the proper certificate/key. Administrator access is not an encryption bypass or a guarantee that all ACL-protected data can be read.

## Safe use

1. Open DriveMedic from a working Windows installation on another drive when recovering data from an unbootable Windows SSD.
2. Refresh the inventory and inspect the source volume's health status.
3. If the drive reports a warning or may be physically failing, stop repeated scans and recover/image the drive first.
4. Browse the old `Users` directory and copy important folders to a different physical drive. Verify the copy before any repair.
5. Use read-only checks before considering a repair. `CHKDSK /f` modifies filesystem metadata and can make recovery harder on failing media; DriveMedic blocks this action when a hardware warning or RAW filesystem is reported.

## Important limitations

- DriveMedic itself does not perform sector imaging, RAID reconstruction, BitLocker unlocking, EFS key recovery, or NTFS metadata reconstruction. It bundles TestDisk/PhotoRec for partition analysis and deleted-file carving; see RECOVERY_WORKBENCH.md.
- Windows may not expose physical-disk identity/SMART data for a volume. In that case the health state stays `Unknown`; DriveMedic avoids making a fabricated “healthy” claim.
- The OS volume and the firmware/system boot partition can differ. DriveMedic highlights the active Windows OS volume; unmounted EFI/System Reserved partitions may not be browsable in this version.
- A readability scan can stress a weak disk and reads full file contents. Use it selectively and do not run it first on a drive that may be mechanically failing.
- Access errors do not prove corruption, and unreadable data cannot always be repaired in place. Generic file-corruption detection requires format-specific validators.
- Repair commands are launched in visible, separate Windows command windows. DriveMedic does not silently run destructive commands or claim that a separately launched repair succeeded.
- Windows 7/8 are legacy operating systems. This compatibility target uses .NET Framework 4.5.2, whose support ended in April 2022; later .NET Framework 4.x runtimes can run apps targeting older 4.x versions, but validate your build on each target OS. Prefer a supported computer and current recovery tooling when practical.

## Repository layout

- `Core/DriveInventoryService.cs` — volume, WMI, physical device, and SMART status inventory.
- `Core/DiagnosticService.cs` — read-only file readability scan and CHKDSK wrapper.
- `Core/RecoveryService.cs` — copy-only recovery operations.
- `Core/RepairAdvisor.cs` — conservative action policy for findings.
- `DriveMedic.Tests/` — 17 safety and recovery tests without an external test framework, including verified copies, empty files, missing sources, and independent recovery runs.
- `TECHNICIAN_WORKFLOW.md` — repeatable customer-data recovery and repair checklist.
- `BUILD_WINDOWS.ps1` — local Release build, triple test pass, package, and checksum script for a Windows development environment.
- `CHANGELOG.md` — user-visible changes in this source package.
- `.github/workflows/windows-ci.yml` — Windows build, triple test pass, artifact packaging, and SHA-256 checksum.

## Repository and release build

This project is published directly in [DilipDC/diskfix](https://github.com/DilipDC/diskfix). Clone the repository to work on the source. Use a Windows developer environment with Visual Studio Build Tools/MSBuild and the .NET Framework 4.5.2 targeting assemblies.

Run `.\BUILD_WINDOWS.ps1` from a Visual Studio Developer PowerShell to perform three source-validation passes, build Release, run the 17-case safety harness three times, and create a ZIP plus SHA-256 checksum. GitHub Actions runs the same validation/build/test sequence on pushes and pull requests and publishes a downloadable workflow artifact when successful.


## Status and test expectations

The source package contains the application, 17 executable safety/recovery tests, repeatable source validation, and a Windows CI pipeline. A compiled `.exe` is not committed to source control; obtain it from the successful Windows workflow artifact after CI completes. Structural validation can run on any OS, but the WinForms application and C# test executable must be compiled and executed on Windows with MSBuild/.NET Framework targeting assemblies.

## License

MIT. See `LICENSE`.