// Prevent running two CMS instances simultaneously — the second one
// would overwrite the SQLite database with an empty file.
var lockPath = Path.Combine(AppContext.BaseDirectory, ".cms-running.lock");
FileStream? lockFile = null;
try
{
    lockFile = new FileStream(lockPath, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
}
catch (IOException)
{
    Console.ForegroundColor = ConsoleColor.Red;
    Console.Error.WriteLine("CMS kjører allerede! Stopp den andre instansen først (Ctrl+C).");
    Console.ResetColor();
    Environment.Exit(1);
}

WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

builder.CreateUmbracoBuilder()
    .AddBackOffice()
    .AddWebsite()
    .AddDeliveryApi()
    .AddComposers()
    .Build();

// Allow OpenIddict (backoffice auth) to work over HTTP in development
if (builder.Environment.IsDevelopment())
{
    builder.Services.Configure<Microsoft.AspNetCore.Authentication.AuthenticationOptions>(options => { });
    builder.Services.AddOpenIddict()
        .AddServer(options =>
        {
            options.UseAspNetCore().DisableTransportSecurityRequirement();
        });
}

WebApplication app = builder.Build();

await app.BootUmbracoAsync();

app.UseUmbraco()
    .WithMiddleware(u =>
    {
        u.UseBackOffice();
        u.UseWebsite();
    })
    .WithEndpoints(u =>
    {
        u.UseBackOfficeEndpoints();
        u.UseWebsiteEndpoints();
    });

await app.RunAsync();
