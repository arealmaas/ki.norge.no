using Umbraco.Cms.Core;
using Umbraco.Cms.Core.Composing;
using Umbraco.Cms.Core.Models;
using Umbraco.Cms.Core.Services;
using Umbraco.Cms.Core.Strings;

namespace KiNorge.Cms.Composers;

/// <summary>
/// Creates a "HeadlessPreview" template and assigns it to all content types.
/// When Umbraco's "Save and preview" is clicked, the Razor view redirects
/// to the Astro frontend's /api/preview endpoint instead of trying to render locally.
/// </summary>
[ComposeAfter(typeof(ContentTypeComposer))]
public class HeadlessPreviewComposer : ComponentComposer<HeadlessPreviewSetup>
{
}

public class HeadlessPreviewSetup : IAsyncComponent
{
    private readonly IContentTypeService _contentTypeService;
    private readonly IContentService _contentService;
    private readonly IFileService _fileService;
    private readonly IRuntimeState _runtimeState;
    private readonly IShortStringHelper _shortStringHelper;

    private const string TemplateAlias = "headlessPreview";
    private const string TemplateName = "Headless Preview";

    // Content types that should use the preview redirect
    private static readonly string[] PreviewableTypes =
        ["artikkel", "side", "eksempel", "faq", "veiledningGuide", "veiledningSteg", "omOss", "omOssSeksjon", "forside", "sandkasse", "veiledningOversikt"];

    public HeadlessPreviewSetup(
        IContentTypeService contentTypeService,
        IContentService contentService,
        IFileService fileService,
        IRuntimeState runtimeState,
        IShortStringHelper shortStringHelper)
    {
        _contentTypeService = contentTypeService;
        _contentService = contentService;
        _fileService = fileService;
        _runtimeState = runtimeState;
        _shortStringHelper = shortStringHelper;
    }

    public Task InitializeAsync(bool isRestarting, CancellationToken cancellationToken)
    {
        if (_runtimeState.Level < RuntimeLevel.Run) return Task.CompletedTask;

        try
        {
            // Create template if it doesn't exist
            var template = _fileService.GetTemplate(TemplateAlias);
            if (template == null)
            {
                template = new Template(_shortStringHelper, TemplateName, TemplateAlias);
                // The actual rendering is in Views/HeadlessPreview.cshtml
                _fileService.SaveTemplate(template);
                Console.WriteLine("HeadlessPreviewSetup: Created HeadlessPreview template");
            }

            // Assign template to all previewable content types
            foreach (var alias in PreviewableTypes)
            {
                var ct = _contentTypeService.Get(alias);
                if (ct == null) continue;

                // Add template to allowed list if missing
                if (ct.AllowedTemplates?.Any(t => t.Alias == TemplateAlias) != true)
                {
                    var allowed = ct.AllowedTemplates?.ToList() ?? [];
                    allowed.Add(template);
                    ct.AllowedTemplates = allowed;
                    ct.SetDefaultTemplate(template);
                    _contentTypeService.Save(ct);
                }
            }

            // Also assign the template to all existing content items that lack one,
            // since setting a default on the type doesn't retroactively apply to
            // already-published items.
            AssignTemplateToExistingContent(template);

            Console.WriteLine("HeadlessPreviewSetup: Preview template ready");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"HeadlessPreviewSetup: {ex.Message}");
        }

        return Task.CompletedTask;
    }

    private void AssignTemplateToExistingContent(ITemplate template)
    {
        foreach (var alias in PreviewableTypes)
        {
            var ct = _contentTypeService.Get(alias);
            if (ct == null) continue;

            // Get all content of this type
            var items = _contentService.GetPagedOfType(ct.Id, 0, 1000, out _, null);
            foreach (var item in items)
            {
                if (item.TemplateId != null) continue; // already has a template

                item.TemplateId = template.Id;
                _contentService.Save(item);
                _contentService.Publish(item, new[] { "*" });
            }
        }
    }

    public Task TerminateAsync(bool isRestarting, CancellationToken cancellationToken) => Task.CompletedTask;
}
