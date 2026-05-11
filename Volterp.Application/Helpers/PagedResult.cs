namespace Volterp.Application.Helpers;

public class PagedResult<T>
{
    public int  RowCount { get; set; }
    public int PageNumber { get; set; } = 1;
    public int PageSize { get; set; } = 10;
    public int PageCount { get; set; }
    public List<T> Items { get; set; } = new  List<T>();
}