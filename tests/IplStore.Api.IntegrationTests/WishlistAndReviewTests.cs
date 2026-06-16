using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;

namespace IplStore.Api.IntegrationTests;

[Collection("api")]
public class WishlistAndReviewTests
{
    private readonly HttpClient _client;

    public WishlistAndReviewTests(IplStoreApiFactory factory) => _client = factory.CreateClient();

    private async Task<AuthView> RegisterAndLoginAsync()
    {
        var email = $"shop{Guid.NewGuid():N}@test.local";
        var register = await _client.PostAsJsonAsync("/api/v1/auth/register",
            new { email, fullName = "Test Shopper", password = "Shop#12345" });
        register.StatusCode.Should().Be(HttpStatusCode.OK);
        return (await register.Content.ReadFromJsonAsync<AuthView>())!;
    }

    private void Authorize(string token)
        => _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

    private async Task<OrderView> PlaceOrderForFirstProductAsync(Guid variantId)
    {
        await _client.PostAsJsonAsync("/api/v1/cart/items", new { productVariantId = variantId, quantity = 1 });
        var req = new HttpRequestMessage(HttpMethod.Post, "/api/v1/orders")
        {
            Content = JsonContent.Create(new
            {
                shippingAddress = new { line1 = "1 Test", city = "Mumbai", state = "MH", postalCode = "400001", country = "India" },
                paymentMethod = 3,
                couponCode = (string?)null
            })
        };
        req.Headers.Add("Idempotency-Key", Guid.NewGuid().ToString());
        var resp = await _client.SendAsync(req);
        return (await resp.Content.ReadFromJsonAsync<OrderView>())!;
    }

    [Fact]
    public async Task Wishlist_Add_Then_Get_Then_Remove_IsIdempotent()
    {
        var auth = await RegisterAndLoginAsync();
        Authorize(auth.AccessToken);

        var list = await _client.GetFromJsonAsync<PagedResultDto<ProductListItemView>>("/api/v1/products?pageSize=1");
        var productId = list!.Items[0].Id;

        // Empty by default.
        var empty = await _client.GetFromJsonAsync<List<object>>("/api/v1/wishlist");
        empty!.Count.Should().Be(0);

        // Add.
        var add = await _client.PostAsync($"/api/v1/wishlist/{productId}", null);
        add.StatusCode.Should().Be(HttpStatusCode.NoContent);

        // Adding twice is idempotent (no error).
        var addAgain = await _client.PostAsync($"/api/v1/wishlist/{productId}", null);
        addAgain.StatusCode.Should().Be(HttpStatusCode.NoContent);

        // Now appears.
        var after = await _client.GetFromJsonAsync<List<WishlistItemView>>("/api/v1/wishlist");
        after!.Should().ContainSingle(w => w.ProductId == productId);

        // Remove.
        var remove = await _client.DeleteAsync($"/api/v1/wishlist/{productId}");
        remove.StatusCode.Should().Be(HttpStatusCode.NoContent);

        // Removing again is also idempotent.
        var removeAgain = await _client.DeleteAsync($"/api/v1/wishlist/{productId}");
        removeAgain.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var final = await _client.GetFromJsonAsync<List<WishlistItemView>>("/api/v1/wishlist");
        final!.Count.Should().Be(0);
    }

    [Fact]
    public async Task Wishlist_Unauthenticated_Returns401()
    {
        _client.DefaultRequestHeaders.Authorization = null;
        var response = await _client.GetAsync("/api/v1/wishlist");
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Review_NonBuyer_Cannot_Submit()
    {
        var auth = await RegisterAndLoginAsync();
        Authorize(auth.AccessToken);

        var list = await _client.GetFromJsonAsync<PagedResultDto<ProductListItemView>>("/api/v1/products?pageSize=1");
        var productId = list!.Items[0].Id;

        var response = await _client.PostAsJsonAsync(
            $"/api/v1/products/{productId}/reviews",
            new { rating = 5, title = "Great", body = "Loved it" });

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Review_Buyer_CanSubmit_And_AppearsInList_And_UpdatesAggregate()
    {
        var auth = await RegisterAndLoginAsync();
        Authorize(auth.AccessToken);

        var list = await _client.GetFromJsonAsync<PagedResultDto<ProductListItemView>>("/api/v1/products?pageSize=1");
        var slug = list!.Items[0].Slug;
        var details = await _client.GetFromJsonAsync<ProductDetailsView>($"/api/v1/products/{slug}");
        await PlaceOrderForFirstProductAsync(details!.Variants[0].Id);

        var productId = details.Id;

        var add = await _client.PostAsJsonAsync(
            $"/api/v1/products/{productId}/reviews",
            new { rating = 4, title = "Solid", body = "Quality fabric and great fit." });
        add.StatusCode.Should().Be(HttpStatusCode.OK);

        // Cannot review twice.
        var second = await _client.PostAsJsonAsync(
            $"/api/v1/products/{productId}/reviews",
            new { rating = 5, title = "Again", body = "I tried again." });
        second.StatusCode.Should().Be(HttpStatusCode.Conflict);

        // Appears in list (anonymous read).
        _client.DefaultRequestHeaders.Authorization = null;
        var reviews = await _client.GetFromJsonAsync<ProductReviewsView>($"/api/v1/products/{productId}/reviews");
        reviews!.ReviewCount.Should().Be(1);
        reviews.AverageRating.Should().Be(4m);
        reviews.Reviews.Should().ContainSingle(r => r.Title == "Solid");
    }
}

public sealed record WishlistItemView(Guid ProductId, string Name, string Slug, decimal Price, bool InStock);

public sealed record ProductReviewsView(
    Guid ProductId,
    decimal AverageRating,
    int ReviewCount,
    IReadOnlyList<ReviewItemView> Reviews);

public sealed record ReviewItemView(Guid Id, int Rating, string Title, string Body);
