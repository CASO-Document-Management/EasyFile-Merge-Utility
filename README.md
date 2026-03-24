# EasyFile Merge Utility

A .NET 9 console application that scans a local directory for files and merges (uploads/replaces) them into the EasyFile document management system via its REST API.

## Prerequisites

- [.NET 9 SDK](https://dotnet.microsoft.com/download/dotnet/9.0) (Windows)
- Access to an EasyFile REST API instance
- Valid API credentials (API key + OAuth2 client credentials)

## Getting Started

1. Clone the repository
2. Copy `src/MergeUtility.Console/appsettings.json` and create an `appsettings.Development.json` with your EasyFile API credentials
3. Build and run:

```bash
dotnet build
dotnet run --project src/MergeUtility.Console
```

## Running Tests

```bash
dotnet test
```

## Project Structure

```
src/
  MergeUtility.Console/       # Entry point, hosted service, configuration
  MergeUtility.Core/           # Business logic, file scanning, merge orchestration
  MergeUtility.EasyFileREST/   # EasyFile REST API client (auth, cabinets, documents)
tests/
  MergeUtility.Tests/          # Unit tests
docs/
  TECHNICAL-DESIGN-DOCUMENT.md # Detailed technical design
```

## Documentation

See [docs/TECHNICAL-DESIGN-DOCUMENT.md](docs/TECHNICAL-DESIGN-DOCUMENT.md) for the full technical design document.
