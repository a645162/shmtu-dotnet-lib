namespace shmtu.parser;

public static class StringExtensions
{
    public static string OnlyDigit(this string input)
    {
        return new string(input.Where(char.IsDigit).ToArray());
    }

    public static string ReplaceUnusedHtmlTags(this string input)
    {
        return input
            .Replace("&nbsp;", "")
            .Replace("&amp;", "&")
            .Replace("&lt;", "<")
            .Replace("&gt;", ">")
            .Replace("&quot;", "\"")
            .Replace("&apos;", "'");
    }

    public static string RemoveUnusedHtmlTags(this string input)
    {
        return input
            .Replace("&nbsp;", "")
            .Replace("&amp;", "")
            .Replace("&lt;", "")
            .Replace("&gt;", "")
            .Replace("&quot;", "")
            .Replace("&apos;", "");
    }
}