// Prevent running two CMS instances simultaneously — the second one
// would overwrite the SQLite database with an empty file.
// Check both a file lock AND port 5000.
var lockPath = Path.Combine(Directory.GetCurrentDirectory(), ".cms-running.lock");
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

// Also check if port 5000 is already in use (catches processes started from other directories)
try
{
    using var portCheck = new System.Net.Sockets.TcpClient();
    portCheck.Connect("127.0.0.1", 5000);
    portCheck.Close();
    lockFile?.Dispose();
    Console.ForegroundColor = ConsoleColor.Red;
    Console.Error.WriteLine("Port 5000 er allerede i bruk! En annen CMS-instans kjører sannsynligvis.");
    Console.Error.WriteLine("Stopp den med: lsof -ti :5000 | xargs kill");
    Console.ResetColor();
    Environment.Exit(1);
}
catch (System.Net.Sockets.SocketException)
{
    // Port is free — good
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
