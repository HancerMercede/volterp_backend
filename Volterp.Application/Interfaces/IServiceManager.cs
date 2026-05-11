namespace Volterp.Application.Interfaces;

public interface IServiceManager
{
    IProductService Products { get; }
    ICategoryService Categories { get; }
    IUserService Users { get; }
    ICompanyService Companies { get; }
}