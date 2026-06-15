using System.Net;
using System.Net.Http.Json;

namespace IplStore.Api.IntegrationTests;

[Collection("api")]
public class CatalogEndpointsTests
{
    private readonly HttpClient _client;

    public CatalogEndpointsTests(IplStoreApiFactory factory) => _client = factory.CreateClient();

    [Fact]
    public async Task GetProducts_ReturnsSeededCatalog()
    {
        var response = await _client.GetAsync("/api/v1/products?page=1&pageSize=5");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<PagedResultDto<ProductListItemView>>();
        body!.TotalCount.Should().BeGreaterThan(0);
        body.Items.Should().HaveCount(5);
    }

    [Fact]
    public async Task GetProductBySlug_UnknownSlug_Returns404()
    {
        var response = await _client.GetAsync("/api/v1/products/does-not-exist");
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Search_ByFranchiseAndType_FiltersResults()
    {
        var response = await _client.GetAsync("/api/v1/products/search?franchise=MI&type=Jersey");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<SearchResultView>();
        body!.Items.Should().OnlyContain(i => i.FranchiseShortCode == "MI");
        body.Facets.Franchises.Should().NotBeEmpty();
    }

    [Fact]
    public async Task GetFranchises_ReturnsTenFranchises()
    {
        var response = await _client.GetAsync("/api/v1/franchises");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var franchises = await response.Content.ReadFromJsonAsync<List<FranchiseView>>();
        franchises!.Count.Should().Be(10);
    }

    [Fact]
    public async Task CreateProduct_WithoutAuth_Returns401()
    {
        var response = await _client.PostAsJsonAsync("/api/v1/products", new { });
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }
}
