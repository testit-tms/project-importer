using Importer.Client.Implementations;
using Microsoft.Extensions.DependencyInjection;
using TestIT.AdaptersApi.Api;
using LegacyApi = TestIT.ApiClient.Api;

namespace Importer.Client.Extensions;

public static class ServiceCollectionExtensions
{
    public static void RegisterApiServices(this IServiceCollection services)
    {
        services.AddTransient<IApiConfigurationFactory, ApiConfigurationFactory>();

        services.AddTransient<IAttachmentsApi>(AdaptersApiClientFactory<AttachmentsApi>);
        services.AddTransient<IProjectsApi>(AdaptersApiClientFactory<ProjectsApi>);
        services.AddTransient<IProjectAttributesApi>(AdaptersApiClientFactory<ProjectAttributesApi>);
        services.AddTransient<IProjectSectionsApi>(AdaptersApiClientFactory<ProjectSectionsApi>);
        services.AddTransient<ISectionsApi>(AdaptersApiClientFactory<SectionsApi>);
        services.AddTransient<IWorkItemsApi>(AdaptersApiClientFactory<WorkItemsApi>);
        services.AddTransient<IParametersApi>(AdaptersApiClientFactory<ParametersApi>);
        services.AddTransient<LegacyApi.ICustomAttributesApi>(LegacyApiClientFactory<LegacyApi.CustomAttributesApi>);
    }

    private static T AdaptersApiClientFactory<T>(IServiceProvider sp) where T : class
    {
        var httpClientFactory = sp.GetRequiredService<IHttpClientFactory>();
        var client = httpClientFactory.CreateClient("ClientApi");
        var config = sp.GetRequiredService<IApiConfigurationFactory>().CreateAdapters();

        return Activator.CreateInstance(typeof(T), client, config, null) as T
               ?? throw new InvalidOperationException($"Cannot create instance of {typeof(T)}");
    }

    private static T LegacyApiClientFactory<T>(IServiceProvider sp) where T : class
    {
        var httpClientFactory = sp.GetRequiredService<IHttpClientFactory>();
        var client = httpClientFactory.CreateClient("ClientApi");
        var config = sp.GetRequiredService<IApiConfigurationFactory>().Create();

        return Activator.CreateInstance(typeof(T), client, config, null) as T
               ?? throw new InvalidOperationException($"Cannot create instance of {typeof(T)}");
    }
}
