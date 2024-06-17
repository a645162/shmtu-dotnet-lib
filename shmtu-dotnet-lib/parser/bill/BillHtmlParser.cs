using System.Text.RegularExpressions;
using HtmlAgilityPack;
using shmtu.datatype.bill;

namespace shmtu.parser.bill;

public class BillHtmlParser(string htmlCode = "")
{
    public string HtmlCode { get; set; } = htmlCode;
    public HtmlDocument? BillHtmlDocument;
    public HtmlNode? RootNode;
    public int TotalPagesCount = 0;

    public bool Parse()
    {
        BillHtmlDocument = new HtmlDocument();
        BillHtmlDocument.LoadHtml(HtmlCode);
        RootNode = BillHtmlDocument.DocumentNode;

        try
        {
            TotalPagesCount =
                PageCountHtmlParser.GetTotalPagesCount(RootNode);
        }
        catch (Exception e)
        {
            Console.WriteLine(e);
            TotalPagesCount = 0;
        }

        return true;
    }
}