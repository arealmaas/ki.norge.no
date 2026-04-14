// ── Guard: prevent two instances (protects SQLite from corruption) ──
// Must run BEFORE WebApplication.CreateBuilder, because Umbraco's boot
// sequence overwrites the database file during builder setup.
{
    // 1. File lock (same directory)
    var lockPath = Path.Combine(Directory.GetCurrentDirectory(), ".cms-running.lock");
    try
    {
        var lf = new FileStream(lockPath, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
        // Keep lf open for the lifetime of the process (GC won't collect a rooted static)
        AppDomain.CurrentDomain.ProcessExit += (_, _) => { lf.Dispose(); try { File.Delete(lockPath); } catch {} };
    }
    catch (IOException)
    {
        Console.ForegroundColor = ConsoleColor.Red;
        Console.Error.WriteLine("CMS kjører allerede! Stopp den andre instansen først (Ctrl+C).");
        Console.ResetColor();
        Environment.Exit(1);
    }

    // 2. Port check (catches instances started from other directories)
    try
    {
        using var tcp = new System.Net.Sockets.TcpClient();
        tcp.Connect("127.0.0.1", 5000);
        Console.ForegroundColor = ConsoleColor.Red;
        Console.Error.WriteLine("Port 5000 er allerede i bruk! En annen CMS-instans kjører sannsynligvis.");
        Console.Error.WriteLine("Stopp den med: lsof -ti :5000 | xargs kill");
        Console.ResetColor();
        Environment.Exit(1);
    }
    catch (System.Net.Sockets.SocketException) { /* Port is free */ }
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
