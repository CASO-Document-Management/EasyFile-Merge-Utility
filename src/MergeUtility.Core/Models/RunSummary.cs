namespace MergeUtility.Core.Models;

public class RunSummary
{
    public int TotalFilesFound { get; set; }
    public int Skipped { get; set; }
    public int Success { get; set; }
    public int NoIdentifier { get; set; }
    public int NoMatch { get; set; }
    public int MultipleMatches { get; set; }
    public int DownloadFailed { get; set; }
    public int MergeFailed { get; set; }
    public int ReplaceFailed { get; set; }
    public int Error { get; set; }
    public DateTime StartedAt { get; set; }
    public DateTime CompletedAt { get; set; }

    public void Tally(MergeStatus status)
    {
        switch (status)
        {
            case MergeStatus.Success: Success++; break;
            case MergeStatus.NoIdentifier: NoIdentifier++; break;
            case MergeStatus.NoMatch: NoMatch++; break;
            case MergeStatus.MultipleMatches: MultipleMatches++; break;
            case MergeStatus.DownloadFailed: DownloadFailed++; break;
            case MergeStatus.MergeFailed: MergeFailed++; break;
            case MergeStatus.ReplaceFailed: ReplaceFailed++; break;
            case MergeStatus.Error: Error++; break;
        }
    }

    public int TotalProcessed => Success + NoIdentifier + NoMatch + MultipleMatches
        + DownloadFailed + MergeFailed + ReplaceFailed + Error;
}
