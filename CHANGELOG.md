# Changelog

All notable changes to the EasyFile Merge Utility are documented in this file.

## [1.1.0] - 2026-03-26

### Added
- **Batch group merge**: All LF pages for a document group are merged locally into a single PDF and checked in as one new version, instead of creating a separate version per page.
- **Checkout/Checkin workflow**: Documents are locked via `POST /checkout` before modification and unlocked via `POST /checkin` after upload. On failure, `POST /undo-checkout` releases the lock automatically.
- **Summary file**: After each run, a `merge-summary-{timestamp}.txt` is written to the source directory with a per-group results table showing document, DocId, pages added, and status.
- **Batch PDF merge via qpdf**: Uses `ArgumentList` to pass file paths, avoiding Windows drive letter parsing issues with qpdf's `--pages` argument.
- **Dynamic test search**: `--test-search` derives the search value from actual files in the source directory instead of a hardcoded value.
- **Server-side fix documentation**: Added `docs/SERVER-SIDE-FIXES.md` (checkout/checkin endpoint fixes) and `docs/EFREST-SCOPE-FIX.md` (OAuth2 scope enforcement fix).

### Changed
- `MergeOrchestrator` calls `ProcessGroupAsync` instead of per-file `ProcessAsync` for atomic group processing.
- `PdfMergeService` accepts `IReadOnlyList<string>` for batch merge; single-file overload delegates to batch.
- `MergeStatus` enum added `CheckoutFailed` and `CheckinFailed` values.
- `RunSummary` added `CheckoutFailed` and `CheckinFailed` tally fields.
- Version set to `1.1.0` in `MergeUtility.Console.csproj`.

### Removed
- `RetryPolicyOptions` class (unused; retry config is inline in `Program.cs`).
- `PdfSharpCore` and `iText7` package references (replaced by qpdf).
- `ReplaceAsync` from `IDocumentSource` (replaced by checkout/checkin flow).

## [1.0.0] - 2026-03-20

### Added
- Initial release of EasyFile Merge Utility.
- Scans a source directory for PDF files matching a configurable pattern.
- Groups files by identifier extracted via regex.
- Searches EasyFile cabinet for matching documents by key value.
- Downloads base document, merges scanned pages via qpdf, and uploads the result.
- OAuth2 client credentials authentication with automatic token refresh.
- Polly resilience policies with exponential backoff and circuit breaker.
- CSV report generation with per-file processing records and run summary.
- Processed file tracking to prevent duplicate processing across runs.
- `--test-connection` and `--test-search` CLI modes for validation.
- `--encrypt-keys` CLI mode for DPAPI encryption of API credentials.
- Atomic group processing: if any file in a group fails, none are marked as processed.
