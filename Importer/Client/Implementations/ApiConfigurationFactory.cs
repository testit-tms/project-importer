using Importer.Models;
using Microsoft.Extensions.Options;
using TestIT.AdaptersApi.Client;

namespace Importer.Client.Implementations;

public class ApiConfigurationFactory(
    IOptions<AppConfig> config)
    : IApiConfigurationFactory
{
    public Configuration Create()
    {
        var configV = config.Value;
        var cfg = new Configuration { BasePath = configV.Tms.Url.TrimEnd('/') };
        cfg.AddApiKeyPrefix("Authorization", "PrivateToken");
        cfg.AddApiKey("Authorization", configV.Tms.PrivateToken);
        cfg.Timeout = TimeSpan.FromSeconds(configV.Tms.Timeout);
        return cfg;
    }
}
