# DiskFix / DriveMedic

Windows desktop drive diagnostics and data-recovery helper for repair technicians.

> **Safety notice:** recover or image important data before attempting write-mode filesystem repairs. This tool cannot bypass BitLocker/EFS encryption or guarantee recovery from failing hardware.

## Project

The complete source snapshot is distributed in [DriveMedic-source.zip](./DriveMedic-source.zip). It includes the C# Windows Forms solution, tests, technician workflow, build/publish scripts, and validation tooling.

## Build and test

Use a Windows environment with the required .NET Framework targeting pack and MSBuild/Visual Studio Build Tools. Extract the ZIP, open `DriveMedic/DriveMedic.sln`, then run `DriveMedic/BUILD_WINDOWS.ps1` from a Developer PowerShell.

The Windows CI workflow extracts the source archive, builds the solution, runs the test harness three times, and stores build output as a workflow artifact.

## Core features

- Detects mounted volumes and physical disks, highlighting the current Windows OS volume.
- Displays volume/filesystem information and health indications available through Windows storage APIs.
- Browses old Windows user profiles and common folders.
- Copy-only recovery with no-overwrite behavior, optional SHA-256 verification, and reparse-point safeguards.
- Diagnostics and guarded repair recommendations.
- Technician workflow, activity logs, and report exports.

A drive reported as unknown is not necessarily healthy. SMART data is hardware/driver-dependent. Elevated permissions do not bypass encryption. If a drive is physically failing, prioritize imaging and professional recovery.
