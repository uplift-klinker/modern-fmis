namespace Fmis.Models.Common;

public record ListResultModel<TModel>(IReadOnlyList<TModel> Items, int TotalCount);
