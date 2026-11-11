using Laborator8_PSSC.Data.Queries;
using Laborator8_PSSC.Data.Repositories;
using Laborator8_PSSC.Domain.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Laborator8_PSSC.Data;

// Registers the DbContext and the adapters for the domain ports. Everything is scoped:
// one DbContext per scope, so a failed save leaves nothing tracked for the next action.
public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddShoppingCartData(this IServiceCollection services, string connectionString)
    {
        services.AddDbContext<ShoppingCartContext>(options => options.UseSqlServer(connectionString));
        services.AddScoped<IProductsRepository, ProductsRepository>();
        services.AddScoped<IOrdersRepository, OrdersRepository>();
        services.AddScoped<ICartsRepository, CartsRepository>();
        services.AddScoped<IShopQueries, ShopQueries>();
        return services;
    }
}
