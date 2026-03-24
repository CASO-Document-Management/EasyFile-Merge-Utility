using System.Text.RegularExpressions;
using Microsoft.Extensions.Options;
using MergeUtility.Core.Interfaces;
using MergeUtility.Core.Models.Configuration;

namespace MergeUtility.Core.Services;

public class RegexIdentifierExtractor : IIdentifierExtractor
{
    private readonly Regex _regex;

    public RegexIdentifierExtractor(IOptions<MergeOptions> options)
    {
        _regex = new Regex(options.Value.IdentifierRegex, RegexOptions.Compiled | RegexOptions.IgnoreCase);
    }

    public string? Extract(string fileName)
    {
        var match = _regex.Match(fileName);
        if (!match.Success) return null;

        var value = match.Groups[1].Value;
        return string.IsNullOrEmpty(value) ? null : value;
    }
}
