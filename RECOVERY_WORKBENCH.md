# Recovery Workbench

## Recommended workflow for an old or damaged PC

1. Shut down the old PC. If possible, connect its HDD/SSD to a working PC using a suitable SATA-to-USB adapter or enclosure. Do not initialize, format, reinstall Windows to, or run a write-mode repair on the source disk.
2. If the disk clicks, disconnects, overheats, stalls, or reports hardware warnings, minimize reads. Consider creating a sector image with a recovery-oriented imager or contacting a professional lab.
3. In DriveMedic's **Find & manage** tab, select the old drive and click **Scan**. Use the filter for all accessible files (including hidden), hidden/system items, Downloads folders, app installers, documents, photos/videos, or recent changes. Check files individually or use **Select all shown**; choose a destination on a separate physical disk and copy. Copy is verified with SHA-256. Move verifies a destination copy before removing the source; Delete sends selected existing files to Recycle Bin where Windows supports it.
4. For permanently deleted files, install nothing on the source. In **Find & manage**, choose **Recover deleted files (GUI)**, select the old physical disk inside QPhotoRec, and save output to a different disk. When the scan finishes, return to DriveMedic, click **Browse recovered output**, select the QPhotoRec output folder and scan it. You can then filter, select, copy or move the recovered files. QPhotoRec carves file signatures and may not restore original filenames/folders. For a lost partition or volume Windows cannot mount, use **Find lost partitions / boot** in the Recovery tools tab.
5. TestDisk is intended for partition analysis/recovery and certain filesystem boot-sector operations. PhotoRec carves supported file types by signatures. Carved output may not keep original filenames/folders, and data overwritten by later writes generally cannot be recovered.
6. Use **Single file** only when useful: hashing reads the entire file and may stress a failing drive. Without a known-good reference hash, SHA-256 does not prove a file is uncorrupted.
7. Use **EFI / Windows / GRUB** for read-only diagnostics and the relevant guide. Diagnostics describe the Windows installation currently running DriveMedic, not automatically the old attached system. For an unbootable Windows PC, boot its Windows recovery media and try Startup Repair. For GRUB, use a matching Linux live USB and the distribution's current instructions.
8. Do not run bcdboot, bootrec, partition-table writes, filesystem repairs, or TestDisk write options until the data is protected and the target/system/EFI partitions are positively identified. The GUI does not run these boot-write operations automatically.

## TestDisk/PhotoRec licensing and source

DriveMedic bundles unmodified TestDisk & PhotoRec 7.2 archives from [the official CGSecurity download page](https://www.cgsecurity.org/wiki/TestDisk_Download) and extracts them automatically. The full Windows package includes the corresponding source archive at `ThirdParty/TestDisk/testdisk-7.2.tar.bz2`. See [THIRD_PARTY_NOTICES.md](THIRD_PARTY_NOTICES.md) for official SHA-256 checksums, GPL-2.0 details, and [upstream source](https://github.com/cgsecurity/testdisk).

## Limits

No product can guarantee all permanently deleted files will return. Overwritten sectors, SSD TRIM, encryption, physical failure, unsupported formats and inaccessible media can prevent recovery. DriveMedic does not bypass BitLocker/EFS or repair arbitrary file types generically. Copy recovered data to a separate physical disk and protect the original when professional recovery is still an option.
