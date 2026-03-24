namespace MergeUtility.Core.Models;

public enum MergeStatus
{
    Success,
    NoIdentifier,
    NoMatch,
    MultipleMatches,
    DownloadFailed,
    MergeFailed,
    ReplaceFailed,
    Error
}
