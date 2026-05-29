using shmtu.cas.auth;
using shmtu.datatype.bill;
using shmtu.parser.bill;

namespace shmtu.sync;

/// <summary>
/// 调用方提供的数据存储抽象。库不关心底层是 JSON / SQLite / 内存。
/// 对齐 Rust 的 <c>sync::BillStore</c> trait。
/// </summary>
public interface IBillStore
{
    /// <summary>判断某条交易号是否已存在于本地。</summary>
    bool Contains(string number);

    /// <summary>将新增条目合并到本地（调用方自行决定持久化策略）。</summary>
    void Merge(List<BillItemInfo> newBills);
}

/// <summary>
/// 同步选项。对齐 Rust 的 <c>sync::SyncOptions</c>。
/// </summary>
public sealed record SyncOptions
{
    /// <summary>从第几页开始（默认 1）。</summary>
    public int StartPage { get; init; } = 1;

    /// <summary>最大翻页数（防止无限翻页）。</summary>
    public int MaxPages { get; init; } = 100;

    /// <summary>账单类型。</summary>
    public BillType BillType { get; init; } = BillType.All;

    /// <summary>连续遇到多少条已存在的交易号就早停。</summary>
    public int EarlyStopThreshold { get; init; } = 5;
}

/// <summary>
/// 页面级同步进度。对齐 Rust 的 <c>sync::SyncProgress</c>。
/// </summary>
public sealed record PageSyncProgress
{
    public int CurrentPage { get; init; }
    public int TotalPages { get; init; }
    public int NewCount { get; init; }
    public int PagesFetched { get; init; }
}

/// <summary>
/// 同步结果。对齐 Rust 的 <c>sync::SyncResult</c>。
/// </summary>
public sealed record SyncResult
{
    /// <summary>本次新增的条目数。</summary>
    public int NewCount { get; init; }

    /// <summary>翻了多少页。</summary>
    public int PagesFetched { get; init; }

    /// <summary>是否因早停条件而终止。</summary>
    public bool EarlyStopped { get; init; }

    /// <summary>所有新增条目。</summary>
    public List<BillItemInfo> NewBills { get; init; } = [];
}

/// <summary>
/// 增量同步：逐页拉取账单，用交易号去重，遇到连续 N 条已知条目则早停。
/// 对齐 Rust 的 <c>sync::incremental_sync</c>。
/// </summary>
public static class BillSync
{
    public static async Task<SyncResult> IncrementalSyncAsync(
        EpayAuth epay,
        IBillStore store,
        SyncOptions? options = null,
        IProgress<PageSyncProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        options ??= new SyncOptions();

        var tabNo = EpayAuth.GetTabNo(options.BillType);
        var newBills = new List<BillItemInfo>();
        var pagesFetched = 0;
        var consecutiveKnown = 0;
        var earlyStopped = false;

        for (var pageOffset = 0; pageOffset < options.MaxPages; pageOffset++)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var pageNo = options.StartPage + pageOffset;
            var html = await epay.GetBillAsync(pageNo, options.BillType, cancellationToken);

            var parser = new BillHtmlParser(html);
            parser.Parse();
            var pageBills = parser.BillItems;

            if (pageBills.Count == 0 && pageOffset == 0)
                break;

            pagesFetched++;

            foreach (var bill in pageBills)
            {
                if (store.Contains(bill.Number))
                {
                    consecutiveKnown++;
                    if (consecutiveKnown >= options.EarlyStopThreshold)
                    {
                        earlyStopped = true;
                        break;
                    }
                }
                else
                {
                    consecutiveKnown = 0;
                    newBills.Add(bill);
                }
            }

            progress?.Report(new PageSyncProgress
            {
                CurrentPage = pageNo,
                TotalPages = parser.TotalPagesCount,
                NewCount = newBills.Count,
                PagesFetched = pagesFetched,
            });

            if (earlyStopped) break;

            // 已到最后一页
            if (pageNo >= parser.TotalPagesCount) break;
        }

        var newCount = newBills.Count;
        store.Merge(newBills);

        return new SyncResult
        {
            NewCount = newCount,
            PagesFetched = pagesFetched,
            EarlyStopped = earlyStopped,
            NewBills = newBills,
        };
    }
}
