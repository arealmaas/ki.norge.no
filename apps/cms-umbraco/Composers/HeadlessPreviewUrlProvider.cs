using Kjac.HeadlessPreview.Models;
using Kjac.HeadlessPreview.Services;
using Umbraco.Cms.Core.Composing;
using Umbraco.Cms.Core.Models;

namespace KiNorge.Cms.Composers;

/// <summary>
/// Implements IDocumentPreviewService for Kjac.HeadlessPreview package.
/// Maps CMS content types to their Astro frontend preview URLs.
/// </summary>
public class HeadlessPreviewComposer : IComposer
{
    public void Compose(IUmbracoBuilder builder)
        => builder.Services.AddUnique<IDocumentPreviewService, KiNorgePreviewService>();
}

public class KiNorgePreviewService : IDocumentPreviewService
{
    private readonly Microsoft.Extensions.Configuration.IConfiguration _config;

    public KiNorgePreviewService(Microsoft.Extensions.Configuration.IConfiguration config)
    {
        _config = config;
    }

    public Task<DocumentPreviewUrlInfo> PreviewUrlInfoAsync(IContent content, string? culture, string? segment)
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
            "kiOrdbok" => "/ki-ordbok",
            _ => null
        };

        if (path == null)
        {
            return Task.FromResult(new DocumentPreviewUrlInfo { Info = $"Forhåndsvisning er ikke tilgjengelig for innholdstypen '{contentType}'." });
        }

        var previewUrl = $"{frontendUrl}{path}?preview=true&secret={previewSecret}";

        return Task.FromResult(new DocumentPreviewUrlInfo { PreviewUrl = previewUrl });
    }
}
