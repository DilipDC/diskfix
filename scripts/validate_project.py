#!/usr/bin/env python3
"""Offline source-integrity checks; Windows CI also compiles and runs the C# test harness."""
from pathlib import Path
import re
import sys
import xml.etree.ElementTree as ET

ROOT = Path(__file__).resolve().parents[1]


def fail(message):
    raise AssertionError(message)


def strip_csharp_lexical(source):
    """Replace comments and string/char contents so delimiter balance can be checked safely."""
    out = list(source)
    i = 0
    state = "normal"
    while i < len(source):
        c = source[i]
        nxt = source[i + 1] if i + 1 < len(source) else ""
        if state == "normal":
            if c == '"':
                out[i] = ' '
                state = "string"
            elif c == "'":
                out[i] = ' '
                state = "char"
            elif c == "/" and nxt == "/":
                out[i] = out[i + 1] = " "
                i += 1
                state = "line"
            elif c == "/" and nxt == "*":
                out[i] = out[i + 1] = " "
                i += 1
                state = "block"
        elif state in ("string", "char"):
            out[i] = " "
            if c == "\n":
                fail("Unescaped newline inside C# " + state + " literal")
            if c == "\\" and i + 1 < len(source):
                if source[i + 1] == "\n":
                    fail("Line break after escape character inside C# " + state + " literal")
                out[i + 1] = " "
                i += 1
            elif (state == "string" and c == '"') or (state == "char" and c == "'"):
                state = "normal"
        elif state == "line":
            if c == "\n":
                state = "normal"
            else:
                out[i] = " "
        elif state == "block":
            out[i] = " "
            if c == "*" and nxt == "/":
                out[i + 1] = " "
                i += 1
                state = "normal"
        i += 1
    if state in ("string", "char", "block"):
        fail("Unterminated C# lexical construct (state=" + state + ")")
    return "".join(out)


def check_delimiters(path):
    clean = strip_csharp_lexical(path.read_text(encoding="utf-8"))
    pairs = {"}": "{", ")": "(", "]": "["}
    stack = []
    for pos, char in enumerate(clean):
        if char in "{([":
            stack.append((char, pos))
        elif char in "})]":
            if not stack or stack[-1][0] != pairs[char]:
                fail(f"{path.relative_to(ROOT)} has mismatched {char} at offset {pos}")
            stack.pop()
    if stack:
        fail(f"{path.relative_to(ROOT)} has unclosed delimiter(s): {stack[-3:]}")


def main():
    # Regression check for the exact class of C# compile error found during review.
    try:
        strip_csharp_lexical('class Broken { string value = "first\nsecond"; }')
    except AssertionError as error:
        if "Unescaped newline" not in str(error):
            raise
    else:
        fail("C# lexical checker accepted a raw newline inside a quoted string")

    projects = [ROOT / "DriveMedic.csproj", ROOT / "DriveMedic.Tests" / "DriveMedic.Tests.csproj"]
    for project in projects:
        tree = ET.parse(str(project))
        ns = {"m": "http://schemas.microsoft.com/developer/msbuild/2003"}
        version = tree.findtext("m:PropertyGroup/m:TargetFrameworkVersion", namespaces=ns)
        if version != "v4.5.2":
            fail(f"Unexpected target framework in {project}: {version}")
        for entry in tree.findall(".//m:Compile", ns):
            path = (project.parent / entry.attrib["Include"].replace("\\", "/")).resolve()
            if not path.is_file():
                fail(f"Missing Compile item: {path}")
    for source in ROOT.rglob("*.cs"):
        check_delimiters(source)

    solution = (ROOT / "DriveMedic.sln").read_text(encoding="utf-8")
    build_lines = [line.strip() for line in solution.splitlines() if ".Build.0 =" in line]
    keys = [line.split(" =", 1)[0] for line in build_lines]
    if len(keys) != len(set(keys)):
        fail("Duplicate solution build configuration mapping found")
    if len(build_lines) != 4:
        fail(f"Expected four project build configuration mappings, found {len(build_lines)}")

    recovery = (ROOT / "Core" / "RecoveryService.cs").read_text(encoding="utf-8")
    if "File.Copy(source, temporaryPath, false)" not in recovery or "File.Move(temporaryPath, destination)" not in recovery:
        fail("Recovery must stage copies and publish without overwriting an existing destination")
    if re.search(r"File\.(Move|Delete)\s*\(\s*(source|fullSource)\b", recovery):
        fail("Recovery service must not move or delete source files")
    if "IsSameOrChild(fullSource, destinationRoot)" not in recovery:
        fail("Directory copy must block destinations nested inside a selected source")
    if "FileAttributes.ReparsePoint" not in recovery:
        fail("Recovery copy must skip reparse-point files and directories")
    if "SHA256.Create()" not in recovery or "File.Move(temporaryPath, destination)" not in recovery:
        fail("Verified recovery must hash data and publish staged copies only after checks")
    if "public int FilesVerified" not in recovery or "public long BytesCopied" not in recovery:
        fail("Recovery results must report verification and byte counts")

    advisor = (ROOT / "Core" / "RepairAdvisor.cs").read_text(encoding="utf-8")
    if advisor.index("if (hardwareWarning") > advisor.index("switch (kind)"):
        fail("Hardware warnings must override generic repair advice")
    if "if (rawVolume || kind == FindingKind.RawVolume) return RepairAction.StopAndRecoverOrImageDrive;" not in advisor:
        fail("RAW volumes must be directed to recovery/image-first workflow")

    form = (ROOT / "MainForm.cs").read_text(encoding="utf-8")
    repair_block = form[form.index("private void RunChkdskRepair()") : form.index("private void RunSfc(")]
    if repair_block.index("DriveHasHardwareWarning(drive)") > repair_block.index('new ProcessStartInfo("cmd.exe"'):
        fail("Physical hardware-warning guard must run before launching CHKDSK /f")
    if "drive.PhysicalDiskIndex.HasValue" not in form or "_physicalDisks.Any(disk => disk.Health == \"Warning\")" not in form:
        fail("Repair findings must use physical-disk warnings when volume mapping is unavailable")
    if "IsUnsafeFilesystemForChkdsk(drive)" not in repair_block:
        fail("RAW/unknown-filesystem guard is missing from filesystem repair")
    inventory = (ROOT / "Core" / "DriveInventoryService.cs").read_text(encoding="utf-8")
    if "Environment.SpecialFolder.Windows" not in inventory:
        fail("Current Windows volume detection is missing")
    if "record.PhysicalDiskIndex = diskIndex" not in inventory:
        fail("Mapped physical-disk identity must be retained for repair safety decisions")
    if "PhysicalDiskIndex" not in (ROOT / "Models" / "DriveRecord.cs").read_text(encoding="utf-8"):
        fail("Drive records must retain the mapped physical-disk identity")
    assembly_info = (ROOT / "Properties" / "AssemblyInfo.cs").read_text(encoding="utf-8")
    if 'AssemblyVersion("0.3.0.0")' not in assembly_info:
        fail("Assembly version must reflect the current source package version")

    test_source = (ROOT / "DriveMedic.Tests" / "Program.cs").read_text(encoding="utf-8")
    test_cases = len(re.findall(r'Run\("', test_source))
    if test_cases < 17:
        fail(f"Expected at least 17 C# safety tests, found {test_cases}")
    if "SHA-256 verified copy preserves source and reports byte count" not in test_source:
        fail("SHA-256 verification regression test is missing")
    if "CopyPaths(sourcePaths, dialog.SelectedPath, verifySha256)" not in form:
        fail("Browse UI must pass the verification choice to the recovery service")
    if "Destination is on the source volume" not in form or "Path.GetPathRoot(Path.GetFullPath(dialog.SelectedPath))" not in form:
        fail("Recovery UI must warn when a source and destination appear to share a volume")
    workflow = (ROOT / ".github" / "workflows" / "windows-ci.yml").read_text(encoding="utf-8")
    if "1..3" not in workflow or "msbuild DriveMedic.sln" not in workflow:
        fail("Windows CI must build the solution and execute three test passes")
    if "actions/upload-artifact@v4" not in workflow or "DriveMedic-windows.zip" not in workflow:
        fail("Windows CI must package and upload the Release application artifact")
    if "Get-FileHash" not in workflow or "DriveMedic-windows.zip.sha256" not in workflow:
        fail("Windows CI must produce a SHA-256 checksum for the build artifact")
    publisher = (ROOT / "PUBLISH_TO_GITHUB.ps1").read_text(encoding="utf-8")
    if "gh repo create" not in publisher or "Invoke-Git -Arguments @('push'" not in publisher or "Invoke-Git -Arguments @('push', '--force'" in publisher:
        fail("Publishing script must create/push normally and must never force-push")
    local_build = (ROOT / "BUILD_WINDOWS.ps1").read_text(encoding="utf-8")
    if "msbuild DriveMedic.sln" not in local_build or "DriveMedic.Tests\\bin\\Release\\DriveMedic.Tests.exe" not in local_build or "Compress-Archive" not in local_build:
        fail("Local Windows build script must compile, run the safety tests, and package the application")
    guide = (ROOT / "TECHNICIAN_WORKFLOW.md").read_text(encoding="utf-8")
    if "BitLocker" not in guide or "professional" not in guide or "SHA-256" not in guide:
        fail("Technician workflow must describe encryption limitations, recovery escalation, and copy verification")

    print("PASS: malformed multiline C# string regression check")
    print("PASS: project XML and compile-file references")
    print("PASS: C# delimiter integrity")
    print("PASS: solution configuration mappings")
    print("PASS: copy-only recovery safeguards")
    print("PASS: reparse-point and staged-copy safeguards")
    print("PASS: SHA-256 verification and recovery metadata checks")
    print("PASS: same-volume recovery warning")
    print("PASS: physical-disk health warning gates filesystem repair")
    print("PASS: publish script safeguards and technician workflow")
    print("PASS: hardware/RAW repair gating")
    print(f"PASS: {test_cases} C# tests are wired into triple-pass Windows CI")
    return 0


if __name__ == "__main__":
    try:
        sys.exit(main())
    except Exception as error:
        print("FAIL:", error, file=sys.stderr)
        sys.exit(1)