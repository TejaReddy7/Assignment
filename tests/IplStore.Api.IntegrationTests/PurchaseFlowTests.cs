using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;

namespace IplStore.Api.IntegrationTests;

[Collection("api")]
public class PurchaseFlowTests
{
    private readonly HttpClient _client;

    public PurchaseFlowTests(IplStoreApiFactory factory) => _client = factory.CreateClient();

    private async Task<AuthView> RegisterAndLoginAsync()
    {
        var email = $"buyer{Guid.NewGuid():N}@test.local";
        var register = await _client.PostAsJsonAsync("/api/v1/auth/register",
            new { email, fullName = "Test Buyer", password = "Buyer#12345" });
        register.StatusCode.Should().Be(HttpStatusCode.OK);
        return (await register.Content.ReadFromJsonAsync<AuthView>())!;
    }

    private void Authorize(string token)
        => _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

    [Fact]
    public async Task Cart_WithoutAuth_Returns401()
    {
        var client = _client;
        client.DefaultRequestHeaders.Authorization = null;
        var response = await client.GetAsync("/api/v1/cart");
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task FullFlow_Register_AddToCart_PlaceOrder_AppearsInHistory()
    {
        var auth = await RegisterAndLoginAsync();
        Authorize(auth.AccessToken);

        // Pick a product + variant.
        var list = await _client.GetFromJsonAsync<PagedResultDto<ProductListItemView>>(
            "/api/v1/products?page=1&pageSize=1");
        var slug = list!.Items[0].Slug;
        var details = await _client.GetFromJsonAsync<ProductDetailsView>($"/api/v1/products/{slug}");
        var variantId = details!.Variants[0].Id;

        // Add to cart.
        var addResponse = await _client.PostAsJsonAsync("/api/v1/cart/items",
            new { productVariantId = variantId, quantity = 2 });
        addResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var cart = await addResponse.Content.ReadFromJsonAsync<CartView>();
        cart!.TotalItems.Should().Be(2);

        // Place order with an idempotency key.
        var idemKey = Guid.NewGuid().ToString();
        var orderRequest = new HttpRequestMessage(HttpMethod.Post, "/api/v1/orders")
        {
            Content = JsonContent.Create(new
            {
                shippingAddress = new
                {
                    line1 = "1 Test St", city = "Mumbai", state = "MH", postalCode = "400001", country = "India"
                },
                paymentMethod = 3,
                couponCode = (string?)null
            })
        };
        orderRequest.Headers.Add("Idempotency-Key", idemKey);
        var orderResponse = await _client.SendAsync(orderRequest);
        orderResponse.StatusCode.Should().Be(HttpStatusCode.Created);
        var order = await orderResponse.Content.ReadFromJsonAsync<OrderView>();
        order!.StatusName.Should().Be("Confirmed");

        // Order appears in history.
        var history = await _client.GetFromJsonAsync<OrderHistoryView>("/api/v1/orders");
        history!.Items.Should().Contain(o => o.OrderNumber == order.OrderNumber);

        // Idempotency replay returns the same order.
        var replay = new HttpRequestMessage(HttpMethod.Post, "/api/v1/orders")
        {
            Content = JsonContent.Create(new
            {
                shippingAddress = new
                {
                    line1 = "1 Test St", city = "Mumbai", state = "MH", postalCode = "400001", country = "India"
                },
                paymentMethod = 3,
                couponCode = (string?)null
            })
        };
        replay.Headers.Add("Idempotency-Key", idemKey);
        var replayResponse = await _client.SendAsync(replay);
        var replayOrder = await replayResponse.Content.ReadFromJsonAsync<OrderView>();
        replayOrder!.OrderNumber.Should().Be(order.OrderNumber);
    }

    [Fact]
    public async Task GetOtherUsersOrder_Returns404()
    {
        // User A places an order.
        var userA = await RegisterAndLoginAsync();
        Authorize(userA.AccessToken);
        var list = await _client.GetFromJsonAsync<PagedResultDto<ProductListItemView>>("/api/v1/products?pageSize=1");
        var details = await _client.GetFromJsonAsync<ProductDetailsView>($"/api/v1/products/{list!.Items[0].Slug}");
        await _client.PostAsJsonAsync("/api/v1/cart/items", new { productVariantId = details!.Variants[0].Id, quantity = 1 });
        var req = new HttpRequestMessage(HttpMethod.Post, "/api/v1/orders")
        {
            Content = JsonContent.Create(new
            {
                shippingAddress = new { line1 = "1 St", city = "M", state = "MH", postalCode = "400001", country = "India" },
                paymentMethod = 3,
                couponCode = (string?)null
            })
        };
        req.Headers.Add("Idempotency-Key", Guid.NewGuid().ToString());
        var orderResponse = await _client.SendAsync(req);
        var order = await orderResponse.Content.ReadFromJsonAsync<OrderView>();

        // User B cannot see User A's order.
        var userB = await RegisterAndLoginAsync();
        Authorize(userB.AccessToken);
        var foreignResponse = await _client.GetAsync($"/api/v1/orders/{order!.OrderNumber}");
        foreignResponse.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task AdminFlow_Ship_Then_Deliver_TransitionsStatus()
    {
        // Customer places an order.
        var customer = await RegisterAndLoginAsync();
        Authorize(customer.AccessToken);
        var list = await _client.GetFromJsonAsync<PagedResultDto<ProductListItemView>>("/api/v1/products?pageSize=1");
        var details = await _client.GetFromJsonAsync<ProductDetailsView>($"/api/v1/products/{list!.Items[0].Slug}");
        await _client.PostAsJsonAsync("/api/v1/cart/items", new { productVariantId = details!.Variants[0].Id, quantity = 1 });
        var orderReq = new HttpRequestMessage(HttpMethod.Post, "/api/v1/orders")
        {
            Content = JsonContent.Create(new
            {
                shippingAddress = new { line1 = "1 Test", city = "Mumbai", state = "MH", postalCode = "400001", country = "India" },
                paymentMethod = 3,
                couponCode = (string?)null
            })
        };
        orderReq.Headers.Add("Idempotency-Key", Guid.NewGuid().ToString());
        var orderResponse = await _client.SendAsync(orderReq);
        var order = await orderResponse.Content.ReadFromJsonAsync<OrderView>();
        order!.StatusName.Should().Be("Confirmed");

        // Non-admin customer cannot ship.
        var customerShip = await _client.PostAsync($"/api/v1/orders/{order.OrderNumber}/ship", null);
        customerShip.StatusCode.Should().Be(HttpStatusCode.Forbidden);

        // Admin logs in and ships the order.
        var adminLogin = await _client.PostAsJsonAsync("/api/v1/auth/login",
            new { email = "admin@iplstore.local", password = "Admin#12345" });
        adminLogin.StatusCode.Should().Be(HttpStatusCode.OK);
        var adminAuth = await adminLogin.Content.ReadFromJsonAsync<AuthView>();
        Authorize(adminAuth!.AccessToken);

        var shipResponse = await _client.PostAsync($"/api/v1/orders/{order.OrderNumber}/ship", null);
        shipResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var shipped = await shipResponse.Content.ReadFromJsonAsync<OrderView>();
        shipped!.StatusName.Should().Be("Shipped");

        // Cannot deliver again from Confirmed (already Shipped now) — wait, deliver requires Shipped.
        var deliverResponse = await _client.PostAsync($"/api/v1/orders/{order.OrderNumber}/deliver", null);
        deliverResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var delivered = await deliverResponse.Content.ReadFromJsonAsync<OrderView>();
        delivered!.StatusName.Should().Be("Delivered");

        // Cannot ship a delivered order (invalid transition → 400).
        var retryShip = await _client.PostAsync($"/api/v1/orders/{order.OrderNumber}/ship", null);
        retryShip.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task AdminListsAllOrders_ButCustomerOnlySeesOwn()
    {
        // Customer A places an order.
        var customer = await RegisterAndLoginAsync();
        Authorize(customer.AccessToken);
        var list = await _client.GetFromJsonAsync<PagedResultDto<ProductListItemView>>("/api/v1/products?pageSize=1");
        var details = await _client.GetFromJsonAsync<ProductDetailsView>($"/api/v1/products/{list!.Items[0].Slug}");
        await _client.PostAsJsonAsync("/api/v1/cart/items", new { productVariantId = details!.Variants[0].Id, quantity = 1 });
        var req = new HttpRequestMessage(HttpMethod.Post, "/api/v1/orders")
        {
            Content = JsonContent.Create(new
            {
                shippingAddress = new { line1 = "1 St", city = "M", state = "MH", postalCode = "400001", country = "India" },
                paymentMethod = 3,
                couponCode = (string?)null
            })
        };
        req.Headers.Add("Idempotency-Key", Guid.NewGuid().ToString());
        var orderResponse = await _client.SendAsync(req);
        var order = await orderResponse.Content.ReadFromJsonAsync<OrderView>();

        // Customer list only shows their own orders.
        var customerHistory = await _client.GetFromJsonAsync<OrderHistoryView>("/api/v1/orders?pageSize=50");
        customerHistory!.Items.Should().Contain(o => o.OrderNumber == order!.OrderNumber);

        // Admin sees all orders.
        var adminLogin = await _client.PostAsJsonAsync("/api/v1/auth/login",
            new { email = "admin@iplstore.local", password = "Admin#12345" });
        var adminAuth = await adminLogin.Content.ReadFromJsonAsync<AuthView>();
        Authorize(adminAuth!.AccessToken);

        var adminHistory = await _client.GetFromJsonAsync<OrderHistoryView>("/api/v1/orders?pageSize=50");
        adminHistory!.Items.Should().Contain(o => o.OrderNumber == order!.OrderNumber);
    }
}

[CollectionDefinition("api")]
public sealed class ApiCollection : ICollectionFixture<IplStoreApiFactory>;
