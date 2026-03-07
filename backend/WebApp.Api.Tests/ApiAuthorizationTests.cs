using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;

namespace WebApp.Api.Tests;

public class ApiAuthorizationTests : IClassFixture<WebApplicationFactory<Program>>
{
	private readonly WebApplicationFactory<Program> _factory;

	public ApiAuthorizationTests(WebApplicationFactory<Program> factory)
	{
		_factory = factory.WithWebHostBuilder(_ =>
		{
			Environment.SetEnvironmentVariable("ENTRA_TENANT_ID", "00000000-0000-0000-0000-000000000000");
			Environment.SetEnvironmentVariable("ENTRA_SPA_CLIENT_ID", "00000000-0000-0000-0000-000000000001");
		});
	}

	[Theory]
	[InlineData("/api/tasks?documentId=test-doc")]
	[InlineData("/api/verification/rerun")]
	[InlineData("/api/telemetry/events")]
	[InlineData("/api/tasks/task-1/citation-context?documentId=test-doc")]
	public async Task ProtectedEndpoints_ShouldReturnUnauthorized_WhenNoBearerToken(string url)
	{
		var client = _factory.CreateClient();
		HttpResponseMessage response;

		if (url.Equals("/api/verification/rerun", StringComparison.OrdinalIgnoreCase))
		{
			response = await client.PostAsJsonAsync(url, new { documentId = "test-doc", includeSuggestions = true });
		}
		else if (url.Equals("/api/telemetry/events", StringComparison.OrdinalIgnoreCase))
		{
			response = await client.PostAsJsonAsync(url, new
			{
				eventName = "api_request_succeeded",
				occurredAtUtc = DateTimeOffset.UtcNow,
				properties = new { source = "test" }
			});
		}
		else
		{
			response = await client.GetAsync(url);
		}

		Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
	}
}
