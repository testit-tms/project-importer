using TestIT.AdaptersApi.Client;

namespace Importer.Client;

public interface IApiConfigurationFactory
{
    Configuration Create();
}
