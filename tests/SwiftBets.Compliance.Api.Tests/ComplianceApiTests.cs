using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using SwiftBets.BuildingBlocks.Testing;

namespace SwiftBets.Compliance.Api.Tests;

public sealed class ComplianceApiTests(SqlServerFixture sql)
{
    [Fact]
    public async Task Customer_sets_a_limit_raises_it_and_sees_the_raise_pending()
    {
        await using var host = await ComplianceHost.StartAsync(sql);
        using var client = host.ClientFor(Guid.NewGuid().ToString());

        (await client.PutAsJsonAsync("/me/limits/stake/day", new { amount = 50_000, currency = "ZAR" }, TestContext.Current.CancellationToken)).StatusCode.ShouldBe(HttpStatusCode.OK);
        using var raised = await client.PutAsJsonAsync("/me/limits/stake/day", new { amount = 80_000, currency = "ZAR" }, TestContext.Current.CancellationToken);

        raised.StatusCode.ShouldBe(HttpStatusCode.OK);
        var limit = (await Json(raised)).GetProperty("limits")[0];
        limit.GetProperty("amount").GetInt64().ShouldBe(50_000);
        limit.GetProperty("pendingAmount").GetInt64().ShouldBe(80_000);
        limit.GetProperty("kind").GetString().ShouldBe("stake");
    }

    [Fact]
    public async Task Removing_a_limit_is_pending_and_removing_a_missing_one_is_not_found()
    {
        await using var host = await ComplianceHost.StartAsync(sql);
        using var client = host.ClientFor(Guid.NewGuid().ToString());
        await client.PutAsJsonAsync("/me/limits/deposit/month", new { amount = 1_000_000, currency = "ZAR" }, TestContext.Current.CancellationToken);

        using var removed = await client.DeleteAsync(new Uri("/me/limits/deposit/month", UriKind.Relative), TestContext.Current.CancellationToken);
        using var missing = await client.DeleteAsync(new Uri("/me/limits/loss/week", UriKind.Relative), TestContext.Current.CancellationToken);

        (await Json(removed)).GetProperty("limits")[0].GetProperty("pendingRemoval").GetBoolean().ShouldBeTrue();
        missing.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Theory]
    [InlineData("/me/limits/bonus/day", """{"amount":1,"currency":"ZAR"}""", HttpStatusCode.NotFound)]
    [InlineData("/me/limits/stake/1", """{"amount":1,"currency":"ZAR"}""", HttpStatusCode.NotFound)]
    [InlineData("/me/limits/stake/day", """{"currency":"ZAR"}""", HttpStatusCode.BadRequest)]
    [InlineData("/me/limits/stake/day", """{"amount":1,"currency":"EUR"}""", HttpStatusCode.BadRequest)]
    public async Task Bad_limit_requests_are_refused(string path, string body, HttpStatusCode expected)
    {
        await using var host = await ComplianceHost.StartAsync(sql);
        using var client = host.ClientFor(Guid.NewGuid().ToString());

        using var response = await client.PutAsync(new Uri(path, UriKind.Relative), new StringContent(body, System.Text.Encoding.UTF8, "application/json"), TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(expected);
    }

    [Fact]
    public async Task Self_exclusion_marks_the_account_excluded_and_a_short_one_is_refused()
    {
        await using var host = await ComplianceHost.StartAsync(sql);
        using var client = host.ClientFor(Guid.NewGuid().ToString());

        using var tooShort = await client.PostAsJsonAsync("/me/exclusions", new { kind = "selfExclusion", months = 3 }, TestContext.Current.CancellationToken);
        using var excluded = await client.PostAsJsonAsync("/me/exclusions", new { kind = "selfExclusion", months = 6, reason = "taking a break" }, TestContext.Current.CancellationToken);

        tooShort.StatusCode.ShouldBe(HttpStatusCode.UnprocessableEntity);
        (await Json(excluded)).GetProperty("excluded").GetBoolean().ShouldBeTrue();
    }

    [Fact]
    public async Task Session_settings_round_trip()
    {
        await using var host = await ComplianceHost.StartAsync(sql);
        using var client = host.ClientFor(Guid.NewGuid().ToString());

        (await client.PutAsJsonAsync("/me/session-settings", new { sessionLimitMinutes = 90, realityCheckMinutes = 30 }, TestContext.Current.CancellationToken)).StatusCode.ShouldBe(HttpStatusCode.OK);
        var state = await Json(await client.GetAsync(new Uri("/me/compliance", UriKind.Relative), TestContext.Current.CancellationToken));

        (state.GetProperty("sessionLimitMinutes").GetInt32(), state.GetProperty("realityCheckMinutes").GetInt32()).ShouldBe((90, 30));
    }

    [Fact]
    public async Task Anonymous_callers_and_customers_on_the_operator_route_are_refused()
    {
        await using var host = await ComplianceHost.StartAsync(sql);
        using var anonymous = host.CreateClient();
        using var customer = host.ClientFor(Guid.NewGuid().ToString());

        (await anonymous.GetAsync(new Uri("/me/compliance", UriKind.Relative), TestContext.Current.CancellationToken)).StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        (await customer.GetAsync(new Uri($"/admin/users/{Guid.NewGuid()}/compliance", UriKind.Relative), TestContext.Current.CancellationToken)).StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        (await customer.GetAsync(new Uri("/admin/audit/verify", UriKind.Relative), TestContext.Current.CancellationToken)).StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    private static async Task<JsonElement> Json(HttpResponseMessage response) =>
        JsonDocument.Parse(await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken)).RootElement;
}
