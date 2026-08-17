using TestIT.ApiClient.Client;
using AdaptersConfiguration = TestIT.AdaptersApi.Client.Configuration;

namespace Importer.Client;

public interface IApiConfigurationFactory
{
    Configuration Create();
    AdaptersConfiguration CreateAdapters();
}
