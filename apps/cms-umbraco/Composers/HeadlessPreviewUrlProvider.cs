using Umbraco.Cms.Core;
using Umbraco.Cms.Core.Composing;
using Umbraco.Cms.Core.Models;
using Umbraco.Cms.Core.Models.PublishedContent;
using Umbraco.Cms.Core.Routing;
using Umbraco.Cms.Core.Services;
using Umbraco.Cms.Core.Strings;

namespace KiNorge.Cms.Composers;

/// <summary>
/// Provides preview URLs that point to the headless Astro frontend.
/// When an editor clicks "Save and preview" in the backoffice,
/// Umbraco opens the URL returned by GetPreviewUrlAsync.
/// </summary>
public class HeadlessPreviewUrlProviderComposer : IComposer
{
    public void Compose(IUmbracoBuilder builder)
        => builder.AddUrlProvider<HeadlessPreviewUrlProvider>();
}

public class HeadlessPreviewUrlProvider : IUrlProvider
{
    private readonly Microsoft.Extensions.Configuration.IConfiguration _config;

    public HeadlessPreviewUrlProvider(Microsoft.Extensions.Configuration.IConfiguration config)
    {
        _config = config;
    }

    public string Alias => "HeadlessPreviewUrlProvider";

    // Not used for regular URL generation
    public UrlInfo? GetUrl(IPublishedContent content, UrlMode mode, string? culture, Uri current)
        => null;

    public IEnumerable<UrlInfo> GetOtherUrls(int id, Uri current)
        => [];

    /// <summary>
    /// Generates a preview URL pointing to the Astro frontend.
    /// Maps content types to their frontend routes.
    /// </summary>
    public Task<UrlInfo?> GetPreviewUrlAsync(IContent content, string? culture, string? segment)
    {
        var frontendUrl = _config["HeadlessPreview:FrontendUrl"] ?? "http://localhost:4321";
        var previewSecret = _config["HeadlessPreview:PreviewSecret"] ?? "";
        var contentType = content.ContentType.Alias;
        var slug = content.GetValue<string>("slug") ?? "";
        var guideSlug = content.GetValue<string>("guideSlug") ?? "";

        var path = contentType switch
        {
            "artikkel" => $"/artikler/{slug}",
            "eksempel" => $"/eksempler/{slug}",
            "side" => $"/{slug}",
            "faq" => "/faq",
            "forside" => "/",
            "omOss" => "/om-oss",
            "omOssSeksjon" => "/om-oss",
            "sandkasse" => "/sandkasse",
            "veiledningOversikt" => "/veiledning",
            "veiledningGuide" => $"/veiledning/{slug}",
            "veiledningSteg" => $"/veiledning/{guideSlug}/{slug}",
            _ => $"/?contentType={contentType}&id={content.Key}"
        };

        var previewUrl = $"{frontendUrl}{path}?preview=true&secret={previewSecret}";

        return Task.FromResult<UrlInfo?>(new UrlInfo(
            url: new Uri(previewUrl),
            provider: Alias,
            culture: culture,
            message: null,
            isExternal: true
        ));
    }
}
