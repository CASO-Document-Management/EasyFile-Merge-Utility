# EasyFile Merge Utility — User Guide

This guide walks you through setting up and running the EasyFile Merge Utility for the first time.

---

## 1. Prerequisites

Before you begin, ensure you have:

- **Windows** operating system
- **.NET 9 Runtime** — download from [dotnet.microsoft.com](https://dotnet.microsoft.com/download/dotnet/9.0)
- **Network access** to the EasyFile REST API endpoint
- **EasyFile API credentials** from your administrator:
  - API Key
  - OAuth2 Client ID
  - OAuth2 Client Secret
  - Profile Key (typically `easyfile`)
- **Read access** to the source directory containing large-format PDFs
- **Write access** to the working directory (default: `C:\ProgramData\CASO Document Management\MergeUtility\WorkingDir`)

---

## 2. Installation

### Option A: Build from source

```bash
dotnet publish src/MergeUtility.Console -c Release -r win-x64 --self-contained false --output ./publish
```

The executable and supporting files will be in the `./publish` folder.

### Option B: Download a release

Download the latest ZIP from the [GitHub Releases](https://github.com/CASO-Document-Management/EasyFile-Merge-Utility/releases) page and extract it to a folder of your choice.

### Verify installation

```bash
MergeUtility.Console.exe --test-connection
```

If .NET is installed correctly, you'll see either a connection success or a credentials error (expected before configuration).

---

## 3. Configuration

Open `appsettings.json` in the same folder as the executable. You need to fill in three sections:

### EasyFileApi

| Field | Description | Example |
|-------|-------------|---------|
| `BaseUrl` | EasyFile REST API base URL | `https://yourserver.com/spwsrest` |
| `AuthBaseUrl` | EasyFile OAuth2 authentication URL | `https://yourserver.com/spwsrestauth` |
| `ApiKey` | API key provided by your EasyFile administrator | *(enter plaintext; will be encrypted in step 4)* |
| `ClientId` | OAuth2 client ID | `merge-utility` |
| `ClientSecret` | OAuth2 client secret | *(enter plaintext; will be encrypted in step 4)* |
| `ProfileKey` | Authentication profile key | `easyfile` |
| `LoggedInUser` | Service account username (used for audit trail) | `merge-svc` |
| `CallingApp` | Application name in audit trail | `MergeUtility` |

### MergeOptions

| Field | Description | Default |
|-------|-------------|---------|
| `SourceDirectory` | Folder containing the large-format PDFs to merge | *(required — no default)* |
| `CabinetName` | Target cabinet name in EasyFile | *(required — no default)* |
| `SearchFieldName` | Cabinet field to match against the extracted identifier | *(required — no default)* |
| `IdentifierRegex` | Regex to extract the identifier from the filename. The first capture group is used. | `^(.+?)_LF\d+` |
| `WorkingDirectory` | Directory for logs, reports, and temporary files | `C:\ProgramData\CASO Document Management\MergeUtility\WorkingDir` |
| `FilePattern` | Glob pattern to match source files | `*LF*.pdf` |

**How the identifier regex works:**

Given the default pattern `^(.+?)_LF\d+`, filenames are matched like this:

| Filename | Extracted Identifier |
|----------|---------------------|
| `01-11234_LF001.pdf` | `01-11234` |
| `01-11234_LF002.pdf` | `01-11234` |
| `PROJ-5678_LF001.pdf` | `PROJ-5678` |
| `report.pdf` | *(no match — file will be skipped)* |

### RetryPolicy

| Field | Description | Default |
|-------|-------------|---------|
| `MaxRetries` | Number of retry attempts for failed API calls | `3` |
| `BaseDelaySeconds` | Initial delay before first retry (doubles each attempt) | `2` |

### Example configuration

```json
{
  "EasyFileApi": {
    "BaseUrl": "https://easyfile.yourcompany.com/spwsrest",
    "AuthBaseUrl": "https://easyfile.yourcompany.com/spwsrestauth",
    "ApiKey": "your-api-key-here",
    "ClientId": "merge-utility",
    "ClientSecret": "your-secret-here",
    "ProfileKey": "easyfile",
    "LoggedInUser": "merge-svc",
    "CallingApp": "MergeUtility"
  },
  "MergeOptions": {
    "SourceDirectory": "\\\\fileserver\\LargeFormat",
    "CabinetName": "FT_MYPROJECT",
    "SearchFieldName": "Key Value",
    "IdentifierRegex": "^(.+?)_LF\\d+",
    "WorkingDirectory": "C:\\ProgramData\\CASO Document Management\\MergeUtility\\WorkingDir",
    "FilePattern": "*LF*.pdf"
  },
  "RetryPolicy": {
    "MaxRetries": 3,
    "BaseDelaySeconds": 2
  }
}
```

---

## 4. First-Run Setup

### Step 1: Encrypt credentials

After entering your `ApiKey` and `ClientSecret` as plaintext in `appsettings.json`, run:

```bash
MergeUtility.Console.exe --encrypt-keys
```

You should see:

```
ApiKey: encrypted
ClientSecret: encrypted
appsettings.json updated.
```

The plaintext values are now replaced with encrypted ciphertext (prefixed with `ENC:`). This uses Windows DPAPI, which means the encrypted values are tied to the machine where you ran the command. If you move the utility to another machine, you'll need to re-encrypt.

### Step 2: Test connectivity

```bash
MergeUtility.Console.exe --test-connection
```

Expected output:

```
Testing connection to EasyFile...
[OK] Authentication successful.
     User   : merge-svc
     Expires: 2026-03-24 02:18:41Z
```

If you see `[FAIL]`, check:
- Are the API URLs correct?
- Were credentials encrypted properly?
- Can you reach the server from this machine?

### Step 3: Verify source directory

Make sure `SourceDirectory` contains your PDF files and they match `FilePattern`:

```bash
dir "\\fileserver\LargeFormat\*LF*.pdf"
```

---

## 5. Running the Utility

### Full merge run (default)

```bash
MergeUtility.Console.exe
```

The utility will:
1. Scan the source directory for matching files
2. For each file: extract identifier, search EasyFile, download target, merge, upload
3. Log every result to console, log file, and CSV report
4. Print a summary at the end

Example output:

```
info: Starting merge run. Source: \\fileserver\LargeFormat
info: Found 150 file(s) to process
info: [Success] 01-11234_LF001.pdf -> DocId: 42857 (2340ms)
info: [NoMatch] UNKNOWN_LF001.pdf -> DocId: N/A (180ms)
warn:   Error: No matching document found
info: [Skipped] 01-11234_LF001.pdf — already processed
...
info: Run complete. Total: 150 | Skipped: 12 | Success: 130 | Failed: 8
```

### Test connection only

```bash
MergeUtility.Console.exe --test-connection
```

Verifies API credentials and connectivity without processing any files.

### Encrypt credentials

```bash
MergeUtility.Console.exe --encrypt-keys
```

Encrypts `ApiKey` and `ClientSecret` in `appsettings.json`. Run this after changing credentials.

### Exit codes

| Code | Meaning |
|------|---------|
| `0` | Completed successfully (some files may have failed individually) |
| `1` | Unhandled exception — check the log file |

---

## 6. Output Files

All output is written to the configured `WorkingDirectory`.

| File | Location | Description |
|------|----------|-------------|
| **Log** | `logs/merge-{yyyyMMdd}.log` | Daily rolling log with timestamped entries for every operation |
| **CSV Report** | `merge-report-{yyyyMMdd-HHmmss}.csv` | Per-file results with status, timing, and errors. Each run creates a new file. |
| **Processed Log** | `processed-files.txt` | List of successfully merged filenames (prevents duplicate processing) |
| **Temp files** | `base_*.pdf`, `merge_*.pdf` | Temporary files during processing. Automatically cleaned up. |

### Reading the CSV report

Open `merge-report-*.csv` in Excel. Columns:

| Column | Description |
|--------|-------------|
| `Timestamp` | When the file was processed |
| `FileName` | Source PDF filename |
| `Identifier` | Key value extracted from filename |
| `TargetDocId` | EasyFile document ID (if found) |
| `Status` | Result — see status codes below |
| `ErrorMessage` | Details if failed |
| `DurationMs` | Processing time in milliseconds |

A run summary is appended at the bottom of the CSV with totals for each status.

### Status codes

| Status | Meaning |
|--------|---------|
| `Success` | File merged and uploaded successfully |
| `NoIdentifier` | Filename didn't match the regex pattern |
| `NoMatch` | No document found in EasyFile with matching identifier |
| `MultipleMatches` | More than one document matched (ambiguous) |
| `DownloadFailed` | Failed to download target from EasyFile (after retries) |
| `MergeFailed` | Failed to merge PDFs locally |
| `ReplaceFailed` | Failed to upload merged PDF to EasyFile (after retries) |
| `Error` | Unexpected error |

---

## 7. Re-Run Safety

The utility tracks which files have been successfully processed in `processed-files.txt`. On subsequent runs, these files are automatically skipped — preventing duplicate pages from being inserted.

**To reprocess specific files:**
1. Open `processed-files.txt` in the WorkingDirectory
2. Delete the line(s) for the file(s) you want to reprocess
3. Run the utility again

**To reprocess all files:**
1. Delete `processed-files.txt` entirely
2. Run the utility again

---

## 8. Scheduling with Windows Task Scheduler

To run the utility on a schedule:

1. Open **Task Scheduler** and create a new task
2. Set **Program/script** to the full path of `MergeUtility.Console.exe`
3. Set **Start in** to the folder containing `appsettings.json`
4. Leave **Arguments** blank (for a normal merge run)
5. Configure the schedule (e.g., daily at 2:00 AM)
6. Under **General**, check "Run whether user is logged on or not"

**Tips:**
- Ensure the service account running the task has read access to the source directory and write access to the working directory
- Check **exit code** in Task Scheduler history: `0` = success, `1` = error
- Review `logs/merge-{date}.log` after each scheduled run

---

## 9. Troubleshooting

### Authentication errors

| Symptom | Fix |
|---------|-----|
| `[FAIL] Authentication failed: HTTP 400` | Verify `ClientId`, `ClientSecret`, `ProfileKey` in appsettings.json |
| `[FAIL] Authentication failed: HTTP 415` | This is a known API quirk — ensure you're using the correct build |
| `HTTP 401` during processing | Token may have expired; the utility auto-refreshes, but check credentials if persistent |

### File processing errors

| Symptom | Fix |
|---------|-----|
| High `NoIdentifier` count | Verify `IdentifierRegex` matches your filename convention |
| High `NoMatch` count | Verify `SearchFieldName` is the correct field name in EasyFile and that values match |
| `MultipleMatches` | The identifier is not unique in the cabinet — coordinate with your EasyFile administrator |
| `DownloadFailed` / `ReplaceFailed` | Network or API issues — check logs for HTTP status codes |
| `MergeFailed` | Source PDF may be corrupted or password-protected |

### General

| Symptom | Fix |
|---------|-----|
| "Source directory not found" | Verify `SourceDirectory` path exists and is accessible |
| No log files created | Verify `WorkingDirectory` exists and is writable |
| Encrypted credentials not working on new machine | Re-run `--encrypt-keys` on the new machine (DPAPI is machine-specific) |

---

## 10. Quick Reference

### First-time setup checklist

- [ ] Install .NET 9 Runtime
- [ ] Place the utility files in a permanent location
- [ ] Edit `appsettings.json` with your API credentials and paths
- [ ] Run `MergeUtility.Console.exe --encrypt-keys`
- [ ] Run `MergeUtility.Console.exe --test-connection`
- [ ] Place source PDFs in the configured `SourceDirectory`
- [ ] Run `MergeUtility.Console.exe` for the first merge

### Key file locations

| What | Where |
|------|-------|
| Configuration | `appsettings.json` (same folder as executable) |
| Daily log | `{WorkingDirectory}/logs/merge-{date}.log` |
| Run report | `{WorkingDirectory}/merge-report-{datetime}.csv` |
| Processed files | `{WorkingDirectory}/processed-files.txt` |
| Temp files | `{WorkingDirectory}/base_*.pdf`, `merge_*.pdf` |

### CLI quick reference

```bash
MergeUtility.Console.exe                    # Run full merge
MergeUtility.Console.exe --test-connection  # Test API connectivity
MergeUtility.Console.exe --encrypt-keys     # Encrypt credentials
```
