namespace MergeUtility.Core.Models.Configuration;

public class MergeOptions
{
    public string SourceDirectory { get; set; } = string.Empty;
    public string CabinetName { get; set; } = string.Empty;
    public string SearchFieldName { get; set; } = string.Empty;
    public string IdentifierRegex { get; set; } = @"^(.+?)_LF\d+";
    public string WorkingDirectory { get; set; } =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
            "CASO Document Management", "MergeUtility", "WorkingDir");
    public string FilePattern { get; set; } = "*_LF*.pdf";
}
