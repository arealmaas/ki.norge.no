using Umbraco.Cms.Core.Composing;
using Umbraco.Cms.Core.DependencyInjection;
using Umbraco.Cms.Core.Events;
using Umbraco.Cms.Core.Notifications;
using Umbraco.Cms.Core.Strings;

namespace KiNorge.Cms.Composers;

/// <summary>
/// Auto-generates the merkelapp.slug field from navn on save.
/// Editor never sees or edits the slug — it's hidden in a "Teknisk (skjult)" tab.
/// Slug is used for stable filter URLs (e.g. /artikler?tag=helse).
/// </summary>
public class MerkelappSlugComposer : IComposer
{
    public void Compose(IUmbracoBuilder builder)
    {
        builder.AddNotificationHandler<ContentSavingNotification, MerkelappSlugHandler>();
    }
}

public class MerkelappSlugHandler : INotificationHandler<ContentSavingNotification>
{
    private readonly IShortStringHelper _shortStringHelper;

    public MerkelappSlugHandler(IShortStringHelper shortStringHelper)
    {
        _shortStringHelper = shortStringHelper;
    }

    public void Handle(ContentSavingNotification notification)
    {
        foreach (var content in notification.SavedEntities)
        {
            if (content.ContentType.Alias != "merkelapp") continue;

            var navn = content.GetValue<string>("navn");
            if (string.IsNullOrWhiteSpace(navn)) continue;

            var slug = _shortStringHelper.CleanStringForUrlSegment(navn);
            content.SetValue("slug", slug);
        }
    }
}
