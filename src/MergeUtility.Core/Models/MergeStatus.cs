namespace MergeUtility.Core.Models;

public enum MergeStatus
{
    Success,
    NoIdentifier,
    NoMatch,
    MultipleMatches,
    CheckoutFailed,
    DownloadFailed,
    MergeFailed,
    CheckinFailed,
    Error
}
