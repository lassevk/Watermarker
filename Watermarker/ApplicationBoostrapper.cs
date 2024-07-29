using LVK.Core.App.Console;
using LVK.Core.Bootstrapping;
using Microsoft.Extensions.Hosting;

namespace Watermarker;

public class ApplicationBoostrapper : IApplicationBootstrapper<HostApplicationBuilder,IHost>
{
    public void Bootstrap(IHostBootstrapper<HostApplicationBuilder, IHost> bootstrapper, HostApplicationBuilder builder)
    {
        builder.Services.AddMainEntrypoint<MainEntrypoint>();
    }
}