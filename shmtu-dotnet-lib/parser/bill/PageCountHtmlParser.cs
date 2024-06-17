using System.Text.RegularExpressions;
using HtmlAgilityPack;
using shmtu.datatype.bill;

namespace shmtu.parser.bill;

public static partial class PageCountHtmlParser
{
    public static int GetTotalPagesCount(HtmlNode classRootNode)
    {
        if (classRootNode == null)
        {
            throw new ArgumentNullException(nameof(classRootNode), "RootNode is null");
        }

        var pageCountTextElement = classRootNode
            .SelectSingleNode($"div/td/table/tr/td");
        if (pageCountTextElement == null)
        {
            throw new InvalidOperationException("Page count text element not found");
        }

        var pageText =
            pageCountTextElement.InnerText
                .ReplaceUnusedHtmlTags()
                .Trim();

        if (string.IsNullOrEmpty(pageText))
        {
            throw new InvalidOperationException("Page text is empty");
        }

        var matches =
            PagePositionRegex().Matches(pageText);

        if (matches.Count != 1)
        {
            throw new InvalidOperationException("Failed to match page number");
        }

        // Get Total Page Count
        var pageCountRegex = TotalPagesCountRegex();
        var pageCountText = pageCountRegex.Match(matches[0].Value).Value;
        // Remove the '/' character
        pageCountText = pageCountText[1..];

        if (!int.TryParse(pageCountText, out var pageCount))
        {
            throw new InvalidOperationException("Failed to parse page number");
        }

        return pageCount;
    }

    [GeneratedRegex(@"当前[\d+]/\d+页")]
    private static partial Regex PagePositionRegex();

    [GeneratedRegex(@"/\d+")]
    private static partial Regex TotalPagesCountRegex();
}