using Umbraco.Cms.Core.Composing;
using Umbraco.Cms.Core.DependencyInjection;
using Umbraco.Cms.Core.Events;
using Umbraco.Cms.Core.Notifications;
using Umbraco.Cms.Core.Services;

namespace KiNorge.Cms.Composers;

/// <summary>
/// Refuses to save a new sandkasse-node if one already exists anywhere in the tree.
/// Sandkasse is a single-instance content type (like Forside) — having two would
/// confuse the frontend, which assumes one canonical /sandkasse page.
/// </summary>
public class SandkasseSingleInstanceComposer : IComposer
{
    public void Compose(IUmbracoBuilder builder)
    {
        builder.AddNotificationHandler<ContentSavingNotification, SandkasseSingleInstanceHandler>();
    }
}

public class SandkasseSingleInstanceHandler : INotificationHandler<ContentSavingNotification>
{
    private readonly IContentService _contentService;

    public SandkasseSingleInstanceHandler(IContentService contentService)
    {
        _contentService = contentService;
    }

    public void Handle(ContentSavingNotification notification)
    {
        foreach (var content in notification.SavedEntities)
        {
            if (content.ContentType.Alias != "sandkasse") continue;

            // Only check on first save (HasIdentity = false means new node)
            if (content.HasIdentity) continue;

            // Look for existing sandkasse anywhere in published content (excluding recycle bin)
            var existing = FindAnySandkasse();
            if (existing != null && existing.Id != content.Id)
            {
                notification.CancelOperation(new EventMessage(
                    "Kan ikke opprette",
                    $"Det finnes allerede en Sandkasse-side ({existing.Name}). Det skal kun være én Sandkasse-side på nettstedet.",
                    EventMessageType.Error));
                return;
            }
        }
    }

    private Umbraco.Cms.Core.Models.IContent? FindAnySandkasse()
    {
        // Walk root content, then descendants — sandkasse is allowed only under Sider
        // but we check anywhere defensively in case a future structure migration moves things.
        foreach (var root in _contentService.GetRootContent())
        {
            if (root.ContentType.Alias == "sandkasse" && !root.Trashed) return root;
            var descendants = _contentService.GetPagedDescendants(root.Id, 0, int.MaxValue, out _);
            var hit = descendants.FirstOrDefault(d => d.ContentType.Alias == "sandkasse" && !d.Trashed);
            if (hit != null) return hit;
        }
        return null;
    }
}
