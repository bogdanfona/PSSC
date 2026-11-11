using Laborator8_PSSC.Data.Queries;
using Microsoft.AspNetCore.Http.HttpResults;

namespace Laborator8_PSSC.Api.Endpoints;

public static class ProductsEndpoints
{
    public static IEndpointRouteBuilder MapProducts(this IEndpointRouteBuilder app)
    {
        app.MapGroup("/products").WithTags("Products")
            .MapGet("/", GetAllAsync)
            .WithName("GetProducts")
            .WithSummary("Catalogul de produse, cu stocul curent.");
        return app;
    }

    private static async Task<Ok<IReadOnlyList<ProductListItem>>> GetAllAsync(IShopQueries queries, CancellationToken cancellationToken) =>
        TypedResults.Ok(await queries.GetProductsAsync(cancellationToken));
}
