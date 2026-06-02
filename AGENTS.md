# Project Rules for AI Agents

## File Encoding

All file read/write operations **must** use UTF-8 encoding.

- For `.cs` files in a Unity project: use **UTF-8 with BOM** (`-Encoding UTF8` in PowerShell).
- For `.md`, `.json`, `.xml`, `.yml`, `.shader`, `.asset`, `.meta` files: use **UTF-8 without BOM** unless the file already has a BOM.
- When reading files with `Get-Content`, always specify `-Encoding UTF8`.
- When writing files with `Set-Content`, always specify `-Encoding UTF8` (PowerShell 7) or `-Encoding UTF8BOM` (PowerShell 7) for `.cs` files.

**Rationale**: Unity's C# compiler and the editor expect `.cs` files to be UTF-8 with BOM. Using the wrong encoding corrupts Chinese characters and other non-ASCII text in comments and string literals, producing garbled text that is irreversible.

## General

- Default to ASCII when editing or creating files. Only introduce non-ASCII or Unicode characters when there is a clear reason and the file already uses that character set.
