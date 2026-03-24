using MergeUtility.Core.Models;

namespace MergeUtility.Core.Interfaces;

public interface IFileScanner
{
    IEnumerable<PdfFileInfo> Scan();
}
