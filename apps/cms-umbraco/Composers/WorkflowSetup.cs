using Umbraco.Cms.Core;
using Umbraco.Cms.Core.Composing;
using Umbraco.Cms.Core.Services;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Configuration;
using Microsoft.AspNetCore.Hosting;

namespace KiNorge.Cms.Composers;

/// <summary>
/// Sets up Workflow approval groups and demo users for the 3-org workflow.
/// Only runs once (checks if groups already exist).
/// </summary>
[ComposeAfter(typeof(ContentSeederComposer))]
public class WorkflowSetupComposer : ComponentComposer<WorkflowSetup>
{
}

public class WorkflowSetup : IAsyncComponent
{
    private readonly IRuntimeState _runtimeState;
    private readonly IUserService _userService;
    private readonly IUserGroupService _userGroupService;
    private readonly IConfiguration _configuration;
    private readonly IWebHostEnvironment _hostEnvironment;

    public WorkflowSetup(
        IRuntimeState runtimeState,
        IUserService userService,
        IUserGroupService userGroupService,
        IConfiguration configuration,
        IWebHostEnvironment hostEnvironment)
    {
        _runtimeState = runtimeState;
        _userService = userService;
        _userGroupService = userGroupService;
        _configuration = configuration;
        _hostEnvironment = hostEnvironment;
    }

    public async Task InitializeAsync(bool isRestarting, CancellationToken cancellationToken)
    {
        if (_runtimeState.Level < RuntimeLevel.Run) return;

        try
        {
            var connStr = _configuration.GetConnectionString("umbracoDbDSN");
            if (string.IsNullOrWhiteSpace(connStr)) return;

            // Resolve |DataDirectory| token to actual path
            var dataDir = AppDomain.CurrentDomain.GetData("DataDirectory") as string
                ?? Path.Combine(_hostEnvironment.ContentRootPath, "umbraco", "Data");
            connStr = connStr.Replace("|DataDirectory|", dataDir);

            using var conn = new SqliteConnection(connStr);
            conn.Open();

            // Check if WorkflowUserGroups table exists (Workflow package may not have run migrations yet)
            using var tableCheck = conn.CreateCommand();
            tableCheck.CommandText = "SELECT COUNT(*) FROM sqlite_master WHERE type='table' AND name='WorkflowUserGroups'";
            if (Convert.ToInt32(tableCheck.ExecuteScalar()) == 0)
            {
                Console.WriteLine("WorkflowSetup: WorkflowUserGroups table not found, skipping (Workflow migrations may not have run yet)");
                return;
            }

            // Check if groups already exist
            using var check = conn.CreateCommand();
            check.CommandText = "SELECT COUNT(*) FROM WorkflowUserGroups";
            var count = Convert.ToInt32(check.ExecuteScalar());
            if (count > 0) return;

            // Create 3 workflow approval groups
            CreateWorkflowGroup(conn, 1, "Intern redaktør", "intern-redaktor",
                "Intern redaksjonell gjennomgang innen organisasjonen",
                "icon-users");
            CreateWorkflowGroup(conn, 2, "Faglig gjennomgang", "faglig-gjennomgang",
                "Tverr-organisatorisk faglig gjennomgang",
                "icon-eye");
            CreateWorkflowGroup(conn, 3, "Publisering", "publisering",
                "Endelig godkjenning og publisering",
                "icon-check");

            // Set up global workflow permissions (all content goes through all 3 stages)
            SetGlobalPermissions(conn);

            // Enable workflow in settings
            EnableWorkflow(conn);

            // Create demo users for each org
            await CreateDemoUsersAsync();

            Console.WriteLine("WorkflowSetup: Created 3 approval groups and demo users");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"WorkflowSetup: {ex.Message}");
        }
    }

    public Task TerminateAsync(bool isRestarting, CancellationToken cancellationToken) => Task.CompletedTask;

    private void CreateWorkflowGroup(SqliteConnection conn, int id, string name, string alias,
        string description, string icon)
    {
        using var cmd = conn.CreateCommand();
        cmd.CommandText = @"INSERT INTO WorkflowUserGroups
            (GroupId, [Key], Name, Alias, Description, Icon, GroupEmail, OfflineApproval, Deleted)
            VALUES ($id, $key, $name, $alias, $desc, $icon, '', 0, 0)";
        cmd.Parameters.AddWithValue("$id", id);
        cmd.Parameters.AddWithValue("$key", Guid.NewGuid().ToString("D"));
        cmd.Parameters.AddWithValue("$name", name);
        cmd.Parameters.AddWithValue("$alias", alias);
        cmd.Parameters.AddWithValue("$desc", description);
        cmd.Parameters.AddWithValue("$icon", icon);
        cmd.ExecuteNonQuery();
    }

    private void SetGlobalPermissions(SqliteConnection conn)
    {
        // Check if permissions table exists
        using var tableCheck = conn.CreateCommand();
        tableCheck.CommandText = "SELECT COUNT(*) FROM sqlite_master WHERE type='table' AND name='WorkflowUserGroupPermissions'";
        if (Convert.ToInt32(tableCheck.ExecuteScalar()) == 0) return;

        for (int stage = 0; stage < 3; stage++)
        {
            using var cmd = conn.CreateCommand();
            cmd.CommandText = @"INSERT INTO WorkflowUserGroupPermissions
                (GroupId, NodeId, ContentTypeId, Permission, Condition, Variant, ApprovalThreshold, EntityType)
                VALUES ($groupId, 0, NULL, $perm, NULL, NULL, NULL, NULL)";
            cmd.Parameters.AddWithValue("$groupId", (stage + 1).ToString());
            cmd.Parameters.AddWithValue("$perm", stage);
            cmd.ExecuteNonQuery();
        }
    }

    private void EnableWorkflow(SqliteConnection conn)
    {
        // Check if settings table exists
        using var tableCheck = conn.CreateCommand();
        tableCheck.CommandText = "SELECT COUNT(*) FROM sqlite_master WHERE type='table' AND name='WorkflowSettings'";
        if (Convert.ToInt32(tableCheck.ExecuteScalar()) == 0) return;

        using var check = conn.CreateCommand();
        check.CommandText = "SELECT COUNT(*) FROM WorkflowSettings";
        var count = Convert.ToInt32(check.ExecuteScalar());
        if (count > 0) return;

        using var cmd = conn.CreateCommand();
        cmd.CommandText = @"INSERT INTO WorkflowSettings (Type, Value)
            VALUES (0, $value)";
        cmd.Parameters.AddWithValue("$value", """
        {
            "flowType": 0,
            "sendNotifications": true,
            "reminderDelay": 0,
            "editUrl": "",
            "siteUrl": "",
            "email": "",
            "defaultApprover": "",
            "excludeNodes": ""
        }
        """);
        cmd.ExecuteNonQuery();
    }

    private async Task CreateDemoUsersAsync()
    {
        // Create demo users for each organization
        // Digdir
        await CreateUserIfNotExistsAsync("Kari Nordmann", "kari@digdir.no", "writer");
        await CreateUserIfNotExistsAsync("Ola Hansen", "ola@digdir.no", "editor");
        // Nkom
        await CreateUserIfNotExistsAsync("Per Johansen", "per@nkom.no", "writer");
        await CreateUserIfNotExistsAsync("Lisa Berg", "lisa@nkom.no", "editor");
        // KS (Kommunesektorens organisasjon)
        await CreateUserIfNotExistsAsync("Erik Dahl", "erik@ks.no", "writer");
        await CreateUserIfNotExistsAsync("Marte Vik", "marte@ks.no", "editor");
    }

    private async Task CreateUserIfNotExistsAsync(string name, string email, string userGroupAlias)
    {
        var existing = _userService.GetByEmail(email);
        if (existing != null) return;

        var userGroup = await _userGroupService.GetAsync(userGroupAlias);
        if (userGroup == null)
        {
            userGroup = await _userGroupService.GetAsync("writer");
            if (userGroup == null) return;
        }

        var user = _userService.CreateUserWithIdentity(email, email);
        user.Name = name;
        user.AddGroup(userGroup.ToReadOnlyGroup());
        _userService.Save(user);
    }
}
