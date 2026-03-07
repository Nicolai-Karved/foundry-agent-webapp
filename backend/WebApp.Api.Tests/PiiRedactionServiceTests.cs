using Microsoft.Extensions.Configuration;
using WebApp.Api.Services;
using Xunit;

namespace WebApp.Api.Tests;

public class PiiRedactionServiceTests
{
	[Fact]
	public void Redact_ShouldReturnOriginal_WhenRedactionDisabled()
	{
		var config = new ConfigurationBuilder()
			.AddInMemoryCollection(new Dictionary<string, string?>
			{
				["Privacy:EnablePiiRedaction"] = "false"
			})
			.Build();

		var service = new PiiRedactionService(config);
		var input = "Contact jane.doe@example.com or +46 70 123 45 67";

		var result = service.Redact(input);

		Assert.Equal(input, result);
	}

	[Fact]
	public void Redact_ShouldMaskEmailAndPhone_WhenRedactionEnabled()
	{
		var config = new ConfigurationBuilder()
			.AddInMemoryCollection(new Dictionary<string, string?>
			{
				["Privacy:EnablePiiRedaction"] = "true"
			})
			.Build();

		var service = new PiiRedactionService(config);
		var input = "Contact jane.doe@example.com or +46 70 123 45 67";

		var result = service.Redact(input);

		Assert.NotNull(result);
		Assert.Contains("[REDACTED_EMAIL]", result);
		Assert.Contains("[REDACTED_PHONE]", result);
		Assert.DoesNotContain("jane.doe@example.com", result, StringComparison.OrdinalIgnoreCase);
	}
}
