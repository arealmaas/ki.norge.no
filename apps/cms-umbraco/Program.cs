// ── Guard: prevent two instances (protects SQLite from corruption) ──
// Must run BEFORE WebApplication.CreateBuilder, because Umbraco's boot
// sequence can overwrite the database file.
{
    // 1. Port check — the most reliable guard
    foreach (var port in new[] { 5000, 44391 })
    {
        try
        {
            using var tcp = new System.Net.Sockets.TcpClient();
            tcp.Connect("127.0.0.1", port);
            Console.ForegroundColor = ConsoleColor.Red;
            Console.Error.WriteLine($"Port {port} er allerede i bruk! En annen CMS-instans kjører sannsynligvis.");
            Console.Error.WriteLine("Stopp den med: pkill -f KiNorge.Cms");
            Console.ResetColor();
            Environment.Exit(1);
        }
        catch (System.Net.Sockets.SocketException) { /* Port is free */ }
    }

    // 2. DB file lock — keeps the DB file locked while running
    var dbPath = Path.Combine(Directory.GetCurrentDirectory(), "umbraco", "Data", "Umbraco.sqlite.db");
    if (File.Exists(dbPath) && new FileInfo(dbPath).Length > 8192)
    {
        try
        {
            var dbLock = new FileStream(dbPath + ".lock", FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
            AppDomain.CurrentDomain.ProcessExit += (_, _) => { dbLock.Dispose(); try { File.Delete(dbPath + ".lock"); } catch {} };
        }
        catch (IOException)
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.Error.WriteLine("CMS kjører allerede! Stopp den andre instansen først (Ctrl+C / pkill -f KiNorge.Cms).");
            Console.ResetColor();
            Environment.Exit(1);
        }
    }
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
