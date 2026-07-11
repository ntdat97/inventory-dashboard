namespace Inventory.Api.Application.Dtos;

/// <summary>A page of results plus the paging metadata a client needs to render controls.</summary>
public record PagedResult<T>(IReadOnlyList<T> Items, int Page, int PageSize, int TotalCount)
{
    public int TotalPages => PageSize <= 0 ? 0 : (int)Math.Ceiling(TotalCount / (double)PageSize);
}
