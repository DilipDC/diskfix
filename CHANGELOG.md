# Changelog

## 0.3.0.0 — Recovery Workbench

- Added a new recovery-first start screen while retaining the existing inventory/diagnostics dashboard in an embedded tab.
- Added whole-mounted-drive queueing, multi-file/folder selection, physical-disk destination warnings, and verified copy-only recovery.
- Added single-file read/hash and queue-for-copy actions with explicit full-read acknowledgement.
- Added optional launchers for separately downloaded TestDisk, PhotoRec, and QPhotoRec; third-party binaries are not bundled.
- Added read-only BCD, volume, WinRE and partition diagnostics plus EFI, MBR, GRUB, RAW-filesystem and offline-Windows guidance.
- Added a recovery workbench guide and updated CI/local build packaging.


## 0.2.0.0 — Source package update

- Added optional SHA-256 verification for recovered copies. Source bytes are hashed during the copy read; the staged destination is hashed before it is published.
- Copies are staged as temporary destination files and moved into their final name only after size checks and optional hash verification complete.
- Added file-level reparse-point skipping, complementing directory junction/symlink skipping.
- Added copied-byte and verified-file counts to recovery results.
- Warns technicians when the selected destination appears to be on the same volume as a selected source.
- Blocks write-mode filesystem repair if physical-disk health reports a warning, even when Windows cannot map the selected volume to a physical disk.
- Added regression tests for verified copy, empty files, missing sources, and separate recovery calls.
- Added a technician workflow checklist, checksum generation for the CI artifact, and a safer no-force-push GitHub publishing script.

## 0.1.0.0 — Initial source package

- Initial WinForms drive inventory, physical disk status, Browse & Recover, read-only diagnostics, guarded repair suggestions, activity log, and report export.