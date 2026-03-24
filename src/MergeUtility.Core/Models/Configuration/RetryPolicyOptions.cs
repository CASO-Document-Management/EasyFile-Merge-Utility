namespace MergeUtility.Core.Models.Configuration;

public class RetryPolicyOptions
{
    public int MaxRetries { get; set; } = 3;
    public int BaseDelaySeconds { get; set; } = 2;
}
