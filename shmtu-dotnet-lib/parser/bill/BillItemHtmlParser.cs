using HtmlAgilityPack;
using shmtu.datatype.bill;

namespace shmtu.parser.bill;

public static class BillItemHtmlParser
{
    public static BillItemInfo ParseBillItemInfo(HtmlNode trElement)
    {
        var children =
            trElement
                .ChildNodes
                .Where(node => node.NodeType == HtmlNodeType.Element)
                .ToList();
        if (children.Count != 7)
        {
            throw new InvalidOperationException("Expected 7 children in each tr element");
        }

        var datetimeChildElement = children[0]
            .ChildNodes
            .Where(node => node.NodeType == HtmlNodeType.Element)
            .ToList();
        if (datetimeChildElement.Count != 2)
        {
            throw new InvalidOperationException("Expected 2 children in each datetime element");
        }

        var itemDateStr =
            datetimeChildElement[0].InnerText
                .ReplaceUnusedHtmlTags()
                .Trim();
        var itemTimeStr =
            datetimeChildElement[1].InnerText
                .ReplaceUnusedHtmlTags()
                .Trim();

        var dealChildElement = children[1]
            .ChildNodes
            .Where(node => node.NodeType == HtmlNodeType.Element)
            .ToList();
        var itemType =
            dealChildElement[0].InnerText
                .ReplaceUnusedHtmlTags()
                .Trim();
        var itemNumber =
            dealChildElement[1].InnerText
                .ReplaceUnusedHtmlTags()
                .Trim();

        var itemTargetUser =
            children[2].InnerText
                .ReplaceUnusedHtmlTags()
                .Trim();

        var itemMoneyStr =
            children[3].InnerText
                .ReplaceUnusedHtmlTags()
                .Trim();

        var itemMethod =
            children[4].InnerText
                .ReplaceUnusedHtmlTags()
                .Trim();
        var itemStatus =
            children[5].InnerText
                .ReplaceUnusedHtmlTags()
                .Trim();

        Console.WriteLine();

        var billItemInfo =
            new BillItemInfo(
                dateStr: itemDateStr,
                timeStr: itemTimeStr,
                itemType: itemType,
                number: itemNumber,
                targetUser: itemTargetUser,
                moneyStr: itemMoneyStr,
                method: itemMethod,
                statusString: itemStatus
            );

        return billItemInfo;
    }

    public static List<BillItemInfo> ParseBillItemInfoList(HtmlNode classRootNode)
    {
        var tbodyElement = classRootNode.SelectSingleNode("table/tbody");
        if (tbodyElement == null)
        {
            throw new ArgumentNullException(nameof(tbodyElement), "tbodyElement is null");
        }

        var trElements = tbodyElement.SelectNodes("tr").ToList();
        if (trElements == null || trElements.Count == 0)
        {
            throw new InvalidOperationException("No tr elements found");
        }

        var billList = new List<BillItemInfo>(trElements.Count);

        foreach (var tr in trElements)
        {
            try
            {
                var billItemInfo = ParseBillItemInfo(tr);
                billList.Add(billItemInfo);
            }
            catch (Exception e)
            {
                Console.WriteLine(e.Message);
            }
        }

        return billList;
    }
}