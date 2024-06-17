using System.Text.RegularExpressions;
using HtmlAgilityPack;
using shmtu.datatype.bill;

namespace shmtu.parser.bill;

public class BillHtmlParser(string htmlCode)
{
    public string HtmlCode
    {
        get => htmlCode;
        set
        {
            htmlCode = value ?? throw new ArgumentNullException(nameof(value));
            Parse();
        }
    }

    private HtmlDocument? _billHtmlDocument;
    private HtmlNode? _rootNode;
    public int TotalPagesCount = 0;
    public readonly List<BillItemInfo> BillItems = [];

    public bool Parse()
    {
        _billHtmlDocument = new HtmlDocument();
        _billHtmlDocument.LoadHtml(HtmlCode);
        _rootNode = _billHtmlDocument.DocumentNode;

        var classRootNode = GetClassRootNode(_rootNode);

        try
        {
            TotalPagesCount =
                PageCountHtmlParser.GetTotalPagesCount(classRootNode);
        }
        catch (Exception e)
        {
            Console.WriteLine(e);
            TotalPagesCount = 0;
        }

        BillItems.Clear();

        var billItems =
            BillItemHtmlParser.ParseBillItemInfoList(classRootNode);

        if (billItems.Count == 0)
        {
            return false;
        }

        BillItems.AddRange(billItems);

        return true;
    }

    private static HtmlNode GetClassRootNode(
        HtmlNode rootNode,
        BillType billType = BillType.All
    )
    {
        var idName = billType switch
        {
            BillType.All => "zone_show_box_1",
            BillType.NotPaid => "zone_show_box_2",
            BillType.Success => "zone_show_box_3",
            BillType.Failure => "zone_show_box_4",
            _ => throw new ArgumentOutOfRangeException(nameof(billType), billType, null)
        };

        var classRootNode = rootNode
            .SelectSingleNode($"//*[@id=\"aazone.{idName}\"]");

        return classRootNode;
    }
}