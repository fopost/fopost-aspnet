using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Xunit;

namespace FoPost.AspNetCore.Tests;

public class ServiceCollectionTests
{
    [Fact]
    public void AddFoPost_registers_a_resolvable_client()
    {
        var services = new ServiceCollection();
        services.AddFoPost(options => options.ApiKey = "fp_test");

        using var provider = services.BuildServiceProvider();
        var client = provider.GetRequiredService<FoPostClient>();

        Assert.Equal(FoPostClientOptions.DefaultBaseUrl, client.BaseUrl);
    }

    [Fact]
    public void The_client_is_a_singleton()
    {
        var services = new ServiceCollection();
        services.AddFoPost(options => options.ApiKey = "fp_test");

        using var provider = services.BuildServiceProvider();

        Assert.Same(provider.GetRequiredService<FoPostClient>(), provider.GetRequiredService<FoPostClient>());
    }

    [Fact]
    public void The_client_is_built_on_the_named_http_client()
    {
        var services = new ServiceCollection();
        services.AddFoPost(options =>
        {
            options.ApiKey = "fp_test";
            options.Timeout = TimeSpan.FromSeconds(7);
        });

        using var provider = services.BuildServiceProvider();
        var factory = provider.GetRequiredService<IHttpClientFactory>();

        Assert.Equal(TimeSpan.FromSeconds(7), factory.CreateClient(FoPostDefaults.HttpClientName).Timeout);
    }

    [Fact]
    public void Options_bind_from_configuration()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["FoPost:ApiKey"] = "fp_from_config",
                ["FoPost:BaseUrl"] = "https://staging.example.test",
                ["FoPost:Timeout"] = "00:00:05",
                ["FoPost:MaxRetries"] = "2",
                ["FoPost:WebhookSecret"] = "whsec_from_config",
            })
            .Build();

        var services = new ServiceCollection();
        services.AddFoPost(configuration);

        using var provider = services.BuildServiceProvider();
        var options = provider.GetRequiredService<IOptions<FoPostOptions>>().Value;

        Assert.Equal("fp_from_config", options.ApiKey);
        Assert.Equal("https://staging.example.test", options.BaseUrl);
        Assert.Equal(TimeSpan.FromSeconds(5), options.Timeout);
        Assert.Equal(2, options.MaxRetries);
        Assert.Equal("whsec_from_config", options.WebhookSecret);
    }

    [Fact]
    public void An_inline_action_wins_over_configuration()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["FoPost:ApiKey"] = "fp_from_config" })
            .Build();

        var services = new ServiceCollection();
        services.AddFoPost(configuration, options => options.ApiKey = "fp_inline");

        using var provider = services.BuildServiceProvider();

        Assert.Equal("fp_inline", provider.GetRequiredService<IOptions<FoPostOptions>>().Value.ApiKey);
    }

    [Fact]
    public void A_missing_api_key_fails_validation()
    {
        using var cleared = EnvironmentVariableScope.Cleared(FoPostOptions.ApiKeyEnvironmentVariable);

        var services = new ServiceCollection();
        services.AddFoPost(options => options.BaseUrl = FoPostClientOptions.DefaultBaseUrl);

        using var provider = services.BuildServiceProvider();
        var error = Assert.Throws<OptionsValidationException>(
            () => provider.GetRequiredService<IOptions<FoPostOptions>>().Value);

        Assert.Contains(nameof(FoPostOptions.ApiKey), string.Join(' ', error.Failures), StringComparison.Ordinal);
    }

    [Fact]
    public void The_api_key_falls_back_to_the_environment()
    {
        using var set = new EnvironmentVariableScope(FoPostOptions.ApiKeyEnvironmentVariable, "fp_from_env");

        var services = new ServiceCollection();
        services.AddFoPost(options => options.MaxRetries = 1);

        using var provider = services.BuildServiceProvider();

        Assert.Equal("fp_from_env", provider.GetRequiredService<IOptions<FoPostOptions>>().Value.ApiKey);
    }

    [Fact]
    public void A_base_url_that_is_not_absolute_fails_validation()
    {
        var services = new ServiceCollection();
        services.AddFoPost(options =>
        {
            options.ApiKey = "fp_test";
            options.BaseUrl = "not-a-url";
        });

        using var provider = services.BuildServiceProvider();

        Assert.Throws<OptionsValidationException>(() => provider.GetRequiredService<IOptions<FoPostOptions>>().Value);
    }
}
