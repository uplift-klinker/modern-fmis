namespace Fmis.Core.Common;

public record ListResult<TItem>(IReadOnlyList<TItem> Items, int TotalCount);
