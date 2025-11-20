using Amazon.Lambda.AspNetCoreServer;

namespace AuthAPIwithController;

public class LambdaEntryPoint : APIGatewayHttpApiV2ProxyFunction
{
    // Initialize the ASP.NET Core application using the Startup class
    protected override void Init(IWebHostBuilder builder)
    {
        builder.UseStartup<Startup>();
    }
}
