# Third-party notices

## TestDisk & PhotoRec 7.2 (Windows binaries)

DriveMedic includes unmodified official TestDisk & PhotoRec 7.2 Windows archives from CGSecurity as embedded resources. It extracts the matching architecture to the current user's Local AppData folder when a recovery tool is first launched.

- Upstream project: https://github.com/cgsecurity/testdisk
- Official downloads: https://www.cgsecurity.org/wiki/TestDisk_Download
- License: GNU General Public License, version 2 (GPL-2.0); the COPYING license text is included in the corresponding source archive.
- Bundled x64 archive: testdisk-7.2.win64.zip — SHA-256 e97e203ce77b6b1a3a37d01beccf069dc6c4632b579ffbb82ae739cdda229f38
- Bundled x86 archive: testdisk-7.2.win.zip — SHA-256 0718a4529611e5f8b6d5fcb873279d23a0b212b18bcf72291f3b47351bfc7126
- Corresponding source archive distributed in ThirdParty/TestDisk/testdisk-7.2.tar.bz2 — SHA-256 f8343be20cb4001c5d91a2e3bcd918398f00ae6d8310894a5a9f2feb813c283f

The TestDisk archives are fetched from the official CGSecurity site during build and the build fails if their SHA-256 differs from the published checksums. The matching versioned source archive is packaged alongside each Windows release ZIP.

DriveMedic does not modify or link against the TestDisk executable. It extracts the upstream archive and starts the original program as a separate process. QPhotoRec provides the graphical file-recovery interface. The original TestDisk partition/boot tool has a keyboard/menu interface; it is not a graphical program upstream.

## Separate-program and recovery warning

TestDisk and PhotoRec are Copyright © Christophe GRENIER and contributors. Refer to the upstream GPL-2.0 text in the source archive and the upstream project for full license terms. DriveMedic is not affiliated with or endorsed by CGSecurity.

DriveMedic cannot guarantee recovery of every deleted file. Stop using a source drive after accidental deletion where possible, save recovered files to a different physical disk, and avoid write/repair operations until the data is secured.
