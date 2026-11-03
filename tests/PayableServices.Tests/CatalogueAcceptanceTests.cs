using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using PayableServices.Domain.Entities;

namespace PayableServices.Tests;

public sealed class CatalogueAcceptanceTests
{
    [Theory]
    [InlineData(null, 401)]
    [InlineData("client", 403)]
    [InlineData("support", 403)]
    [InlineData("admin", 200)]
    public async Task Maintenance_enforces_real_token_policies(string? role, int status)
    {
        await using var rig = await TestRig.CreateAsync();
        using var client = rig.Client(role);
        Assert.Equal((HttpStatusCode)status, (await client.PostAsJsonAsync("/api/catalogue/maintenance/sync",
            new { sourceName = "MockCompany", countryId = TestRig.CountryId })).StatusCode);
        Assert.Equal((HttpStatusCode)status, (await client.PostAsJsonAsync("/api/payment-routes/activate",
            new { paymentRouteId = rig.RouteId })).StatusCode);
        if (role != "client")
            Assert.Equal(role is null ? HttpStatusCode.Unauthorized : HttpStatusCode.Forbidden, (await rig.Inquire(client)).StatusCode);
    }

    [Fact]
    public async Task Catalogue_browses_locally_and_details_expose_only_form_data()
    {
        await using var rig = await TestRig.CreateAsync();
        using var publicClient = rig.Client(null);
        rig.Settings["CompanyProviders:MockCompany:CataloguePath"] = "catalogue?scenario=unavailable";
        var services = await TestRig.Data(await publicClient.GetAsync("/api/catalogue"));
        Assert.Contains(services.EnumerateArray(), x => x.GetProperty("id").GetGuid() == rig.ServiceId);
        var response = await publicClient.GetAsync("/api/catalogue/" + rig.ServiceId);
        var text = await response.Content.ReadAsStringAsync();
        Assert.DoesNotContain("endpoint", text, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("credential", text, StringComparison.OrdinalIgnoreCase);
        var details = await TestRig.Data(response);
        Assert.True(details.GetProperty("inquiryAvailable").GetBoolean());
        var field = details.GetProperty("references")[0];
        Assert.Equal("accountNumber", field.GetProperty("fieldKey").GetString());
        Assert.Equal(8, field.GetProperty("minLength").GetInt32());
        Assert.Equal(20, field.GetProperty("maxLength").GetInt32());
        await rig.InDb(async db => { (await db.PaymentRoutes.SingleAsync(x => x.Id == rig.RouteId)).IsActive = false; await db.SaveChangesAsync(); });
        details = await TestRig.Data(await publicClient.GetAsync("/api/catalogue/" + rig.ServiceId));
        Assert.False(details.GetProperty("inquiryAvailable").GetBoolean());
        await rig.InDb(async db => { (await db.PayableServices.SingleAsync(x => x.Id == rig.ServiceId)).IsActive = false; await db.SaveChangesAsync(); });
        Assert.Equal(HttpStatusCode.NotFound, (await publicClient.GetAsync("/api/catalogue/" + rig.ServiceId)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await publicClient.GetAsync("/api/catalogue/" + Guid.NewGuid())).StatusCode);
    }

    [Fact]
    public async Task Every_filter_AND_search_and_bounded_pagination_work()
    {
        await using var rig = await TestRig.CreateAsync();
        Guid[] routes = [];
        await rig.InDb(async db => routes = await db.PaymentRoutes.Where(x => x.ExternalSourceCode == "MockCompany").Select(x => x.Id).ToArrayAsync());
        foreach (var id in routes) await TestRig.Data(await rig.Admin.PostAsJsonAsync("/api/payment-routes/activate", new { paymentRouteId = id }));
        using var client = rig.Client(null);
        foreach (var filter in new[] { "search=eLeCtRiCiTy", "provider=Quito%20Electricity", "country=EC", "category=Utilities", "externalServiceCode=ELEC-EC",
            "search=Electricity&provider=Quito%20Electricity&country=EC&category=Utilities&externalServiceCode=ELEC-EC" })
        {
            var data = await TestRig.Data(await client.GetAsync("/api/catalogue?" + filter));
            Assert.Contains(data.EnumerateArray(), x => x.GetProperty("id").GetGuid() == rig.ServiceId);
        }
        var noMatch = await TestRig.Data(await client.GetAsync("/api/catalogue?country=US&externalServiceCode=ELEC-EC"));
        Assert.Equal(0, noMatch.GetArrayLength());
        Assert.Equal(0, (await TestRig.Data(await client.GetAsync("/api/catalogue?search=does-not-exist"))).GetArrayLength());
        var all = await TestRig.Data(await client.GetAsync("/api/catalogue?pageSize=100"));
        var pages = new List<Guid>();
        for (var page = 1; page <= all.GetArrayLength(); page++)
            pages.Add((await TestRig.Data(await client.GetAsync("/api/catalogue?pageSize=1&page=" + page)))[0].GetProperty("id").GetGuid());
        Assert.Equal(all.EnumerateArray().Select(x => x.GetProperty("id").GetGuid()), pages);
        foreach (var invalid in new[] { "page=0", "page=-1", "pageSize=101", "pageSize=0", "country=USA", "category=%20", "page=not-a-number" })
            Assert.Equal(HttpStatusCode.BadRequest, (await client.GetAsync("/api/catalogue?" + invalid)).StatusCode);
    }

    [Fact]
    public async Task Sync_is_atomic_versioned_repeatable_preserves_approval_and_rejects_bad_snapshots()
    {
        await using var rig = await TestRig.CreateAsync();
        var firstId = rig.ServiceId;
        async Task<HttpResponseMessage> Sync(string scenario)
        {
            rig.Settings["CompanyProviders:MockCompany:CataloguePath"] = "catalogue?scenario=" + scenario;
            return await rig.Admin.PostAsJsonAsync("/api/catalogue/maintenance/sync", new { sourceName = "MockCompany", countryId = TestRig.CountryId });
        }
        await TestRig.Data(await Sync("initial"));
        Assert.Equal(HttpStatusCode.BadRequest, (await Sync("same-version")).StatusCode);
        foreach (var invalid in new[] { "wrongCurrency", "invalidReferences", "malformed" })
            Assert.Equal(HttpStatusCode.BadRequest, (await Sync(invalid)).StatusCode);
        await TestRig.Data(await Sync("updated"));
        await rig.InDb(async db =>
        {
            Assert.Equal(3, await db.ExternalCatalogueItems.CountAsync());
            Assert.True((await db.PayableServices.SingleAsync(x => x.Id == firstId)).IsActive);
            Assert.Equal("Electricity Quito Updated", (await db.PayableServices.SingleAsync(x => x.Id == firstId)).Name);
            Assert.True((await db.PaymentRoutes.SingleAsync(x => x.Id == rig.RouteId)).IsActive);
        });
        foreach (var invalid in new[] { "invalid", "partial", "unavailable", "initial" })
        {
            Assert.Equal(HttpStatusCode.BadRequest, (await Sync(invalid)).StatusCode);
            await rig.InDb(async db =>
            {
                Assert.Equal(3, await db.ExternalCatalogueItems.CountAsync());
                Assert.True((await db.PayableServices.SingleAsync(x => x.Id == firstId)).IsActive);
                Assert.Equal(2L, (await db.ExternalCatalogueSources.SingleAsync()).SnapshotVersion);
            });
        }
        await TestRig.Data(await Sync("removed"));
        await rig.InDb(async db =>
        {
            var missing = await db.ExternalCatalogueItems.SingleAsync(x => x.ExternalCode == "WATER-EC");
            Assert.False(missing.IsAvailable);
            var mapping = await db.ExternalCatalogueMappings.SingleAsync(x => x.ExternalCatalogueItemId == missing.Id);
            Assert.False((await db.PayableServices.SingleAsync(x => x.Id == mapping.PayableServiceId)).IsActive);
        });
        var overlap = await Task.WhenAll(Enumerable.Range(0, 4).Select(_ => rig.Admin.PostAsJsonAsync("/api/catalogue/maintenance/sync",
            new { sourceName = "MockCompany", countryId = TestRig.CountryId })));
        Assert.All(overlap, response => Assert.Equal(HttpStatusCode.OK, response.StatusCode));
        await rig.InDb(async db => Assert.Equal(3, await db.ExternalCatalogueItems.CountAsync()));
    }

    [Fact]
    public async Task New_imports_are_inactive_and_failed_activation_does_not_change_route()
    {
        await using var rig = await TestRig.CreateAsync(false);
        var sync = await TestRig.Data(await rig.Admin.PostAsJsonAsync("/api/catalogue/maintenance/sync",
            new { sourceName = "MockCompany", countryId = TestRig.CountryId }));
        Assert.Equal(3, sync.GetProperty("paymentRouteIds").GetArrayLength());
        Guid routeId = default;
        await rig.InDb(async db =>
        {
            var routes = await db.PaymentRoutes.Where(x => x.ExternalSourceCode == "MockCompany").ToArrayAsync();
            Assert.All(routes, route => Assert.False(route.IsActive));
            routeId = routes[0].Id;
            var service = await db.PayableServices.SingleAsync(x => x.Id == routes[0].PayableServiceId);
            Assert.False(service.IsActive);
            service.Currency = "EUR"; await db.SaveChangesAsync();
        });
        Assert.Equal(HttpStatusCode.BadRequest, (await rig.Admin.PostAsJsonAsync("/api/payment-routes/activate", new { paymentRouteId = routeId })).StatusCode);
        await rig.InDb(async db =>
        {
            var route = await db.PaymentRoutes.SingleAsync(x => x.Id == routeId);
            Assert.False(route.IsActive); Assert.Equal("", route.CompanyPaymentEndpoint);
        });
    }
    [Fact]
    public async Task Activation_rejects_missing_mapping_invalid_references_and_missing_configuration_atomically()
    {
        await using var rig = await TestRig.CreateAsync(false);
        var sync = await TestRig.Data(await rig.Admin.PostAsJsonAsync("/api/catalogue/maintenance/sync",
            new { sourceName = "MockCompany", countryId = TestRig.CountryId }));
        var id = sync.GetProperty("paymentRouteIds")[0].GetGuid();
        ExternalCatalogueMapping mapping = null!;
        async Task Reject()
        {
            Assert.Equal(HttpStatusCode.BadRequest, (await rig.Admin.PostAsJsonAsync("/api/payment-routes/activate", new { paymentRouteId = id })).StatusCode);
            await rig.InDb(async db =>
            {
                var route = await db.PaymentRoutes.SingleAsync(x => x.Id == id);
                Assert.False(route.IsActive); Assert.Equal("", route.CompanyPaymentEndpoint);
            });
        }
        await rig.InDb(async db =>
        {
            mapping = await db.ExternalCatalogueMappings.SingleAsync(x => x.PaymentRouteId == id);
            db.ExternalCatalogueMappings.Remove(mapping); await db.SaveChangesAsync();
        });
        await Reject();
        await rig.InDb(async db =>
        {
            db.ExternalCatalogueMappings.Add(mapping); await db.SaveChangesAsync();
            var field = await db.ReferenceFieldDefinitions.SingleAsync(x => x.PayableServiceId == mapping.PayableServiceId);
            field.ValidationRule = "[invalid"; await db.SaveChangesAsync();
        });
        await Reject();
        await rig.InDb(async db =>
        {
            var field = await db.ReferenceFieldDefinitions.SingleAsync(x => x.PayableServiceId == mapping.PayableServiceId);
            field.ValidationRule = "^[0-9]{8,20}$"; await db.SaveChangesAsync();
        });
        rig.Settings["CompanyProviders:MockCompany:Enabled"] = "false";
        await Reject();
        rig.Settings["CompanyProviders:MockCompany:Enabled"] = "true";
        await TestRig.Data(await rig.Admin.PostAsJsonAsync("/api/payment-routes/activate", new { paymentRouteId = id }));
        await TestRig.Data(await rig.Admin.PostAsJsonAsync("/api/payment-routes/activate", new { paymentRouteId = id }));
    }
}
