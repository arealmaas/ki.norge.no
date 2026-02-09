using Umbraco.Cms.Core;
using Umbraco.Cms.Core.Composing;
using Umbraco.Cms.Core.Models;
using Umbraco.Cms.Core.Services;
using Umbraco.Cms.Core.Strings;

namespace KiNorge.Cms.Composers;

/// <summary>
/// Creates document types for KI Norge CMS on first boot.
/// Uses RichText for content fields; BlockList can be configured
/// via the backoffice once element types are registered.
/// </summary>
public class ContentTypeComposer : ComponentComposer<ContentTypeComponent>
{
}

public class ContentTypeComponent : IAsyncComponent
{
    private readonly IContentTypeService _contentTypeService;
    private readonly IDataTypeService _dataTypeService;
    private readonly IShortStringHelper _shortStringHelper;
    private readonly IRuntimeState _runtimeState;

    // Data types (resolved at init time)
    private IDataType _textStringDt = null!;
    private IDataType _textAreaDt = null!;
    private IDataType _richTextDt = null!;
    private IDataType _numericDt = null!;
    private IDataType _mediaPickerDt = null!;
    private IDataType _contentPickerDt = null!;

    public ContentTypeComponent(
        IContentTypeService contentTypeService,
        IDataTypeService dataTypeService,
        IShortStringHelper shortStringHelper,
        IRuntimeState runtimeState)
    {
        _contentTypeService = contentTypeService;
        _dataTypeService = dataTypeService;
        _shortStringHelper = shortStringHelper;
        _runtimeState = runtimeState;
    }

    public Task InitializeAsync(bool isRestarting, CancellationToken cancellationToken)
    {
        if (_runtimeState.Level < RuntimeLevel.Run) return Task.CompletedTask;
        if (_contentTypeService.Get("artikkel") != null) return Task.CompletedTask;

        try
        {
            ResolveDataTypes();
            CreateDocumentTypes();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"ContentTypeComposer: {ex.Message}");
        }
        return Task.CompletedTask;
    }

    public Task TerminateAsync(bool isRestarting, CancellationToken cancellationToken) => Task.CompletedTask;

    private void ResolveDataTypes()
    {
        _textStringDt = FindDataType(Constants.PropertyEditors.Aliases.TextBox);
        _textAreaDt = FindDataType(Constants.PropertyEditors.Aliases.TextArea);
        _richTextDt = FindDataType(Constants.PropertyEditors.Aliases.RichText);
        _numericDt = FindDataType(Constants.PropertyEditors.Aliases.Integer);
        _mediaPickerDt = FindDataType(Constants.PropertyEditors.Aliases.MediaPicker3);
        _contentPickerDt = FindDataType(Constants.PropertyEditors.Aliases.ContentPicker);
    }

    private IDataType FindDataType(string editorAlias)
    {
        var dts = _dataTypeService.GetByEditorAlias(editorAlias);
        var dt = dts.FirstOrDefault();
        if (dt == null) throw new InvalidOperationException($"No DataType found for editor {editorAlias}");
        return dt;
    }

    private PropertyType Prop(string alias, string name, IDataType dataType,
        bool mandatory = false, string? description = null)
    {
        return new PropertyType(_shortStringHelper, dataType)
        {
            Alias = alias,
            Name = name,
            Description = description,
            Mandatory = mandatory,
        };
    }

    private void CreateDocumentTypes()
    {
        CreateMerkelapp();
        CreateArtikkel();
        CreateSide();
        CreateEksempel();
        CreateVeiledning();
        CreateFAQ();
    }

    private void CreateMerkelapp()
    {
        var ct = new ContentType(_shortStringHelper, -1)
        {
            Alias = "merkelapp",
            Name = "Merkelapp",
            Description = "Merkelapp/tag for kategorisering",
            Icon = "icon-tag",
            AllowedAsRoot = true,
        };
        ct.AddPropertyGroup("innhold", "Innhold");
        ct.AddPropertyType(Prop("navn", "Navn", _textStringDt, mandatory: true), "innhold");
        ct.AddPropertyType(Prop("slug", "Slug", _textStringDt, mandatory: true), "innhold");
        ct.AddPropertyType(Prop("beskrivelse", "Beskrivelse", _textAreaDt), "innhold");
        _contentTypeService.Save(ct);
    }

    private void CreateArtikkel()
    {
        var ct = new ContentType(_shortStringHelper, -1)
        {
            Alias = "artikkel",
            Name = "Artikkel",
            Description = "Artikler og nyheter",
            Icon = "icon-newspaper-alt",
            AllowedAsRoot = true,
        };
        ct.AddPropertyGroup("innhold", "Innhold");
        ct.AddPropertyType(Prop("tittel", "Tittel", _textStringDt, mandatory: true), "innhold");
        ct.AddPropertyType(Prop("slug", "Slug", _textStringDt, mandatory: true), "innhold");
        ct.AddPropertyType(Prop("innhold", "Innhold", _richTextDt, description: "Hovedinnhold"), "innhold");
        _contentTypeService.Save(ct);
    }

    private void CreateSide()
    {
        var ct = new ContentType(_shortStringHelper, -1)
        {
            Alias = "side",
            Name = "Side",
            Description = "Generelle sider",
            Icon = "icon-document",
            AllowedAsRoot = true,
        };
        ct.AddPropertyGroup("innhold", "Innhold");
        ct.AddPropertyType(Prop("tittel", "Tittel", _textStringDt, mandatory: true), "innhold");
        ct.AddPropertyType(Prop("slug", "Slug", _textStringDt, mandatory: true), "innhold");
        ct.AddPropertyType(Prop("innhold", "Innhold", _richTextDt), "innhold");

        ct.AddPropertyGroup("seo", "SEO");
        ct.AddPropertyType(Prop("template", "Mal", _textStringDt, description: "standard, bred, landingsside"), "seo");
        ct.AddPropertyType(Prop("seoTittel", "SEO-tittel", _textStringDt), "seo");
        ct.AddPropertyType(Prop("seoBeskrivelse", "SEO-beskrivelse", _textAreaDt), "seo");
        _contentTypeService.Save(ct);
    }

    private void CreateEksempel()
    {
        var ct = new ContentType(_shortStringHelper, -1)
        {
            Alias = "eksempel",
            Name = "Eksempel",
            Description = "Gode eksempler / caser",
            Icon = "icon-science",
            AllowedAsRoot = true,
        };
        ct.AddPropertyGroup("innhold", "Innhold");
        ct.AddPropertyType(Prop("tittel", "Tittel", _textStringDt, mandatory: true), "innhold");
        ct.AddPropertyType(Prop("slug", "Slug", _textStringDt, mandatory: true), "innhold");
        ct.AddPropertyType(Prop("organisasjon", "Organisasjon", _textStringDt), "innhold");
        ct.AddPropertyType(Prop("beskrivelse", "Beskrivelse", _richTextDt), "innhold");
        ct.AddPropertyType(Prop("verktoy", "Verktøy", _textAreaDt, description: "JSON array med verktøynavn"), "innhold");
        ct.AddPropertyType(Prop("resultater", "Resultater", _textAreaDt), "innhold");
        ct.AddPropertyType(Prop("status", "Status", _textStringDt, description: "i_utvikling, pilot, i_drift, avsluttet"), "innhold");
        ct.AddPropertyType(Prop("bilde", "Bilde", _mediaPickerDt), "innhold");
        _contentTypeService.Save(ct);
    }

    private void CreateVeiledning()
    {
        var ct = new ContentType(_shortStringHelper, -1)
        {
            Alias = "veiledning",
            Name = "Veiledning",
            Description = "Veiledningsressurser",
            Icon = "icon-book-alt",
            AllowedAsRoot = true,
        };
        ct.AddPropertyGroup("innhold", "Innhold");
        ct.AddPropertyType(Prop("tittel", "Tittel", _textStringDt, mandatory: true), "innhold");
        ct.AddPropertyType(Prop("slug", "Slug", _textStringDt, mandatory: true), "innhold");
        ct.AddPropertyType(Prop("innhold", "Innhold", _richTextDt), "innhold");
        ct.AddPropertyType(Prop("kategori", "Kategori", _contentPickerDt, description: "Velg merkelapp-kategori"), "innhold");
        ct.AddPropertyType(Prop("rekkefolge", "Rekkefølge", _numericDt), "innhold");
        _contentTypeService.Save(ct);
    }

    private void CreateFAQ()
    {
        var ct = new ContentType(_shortStringHelper, -1)
        {
            Alias = "faq",
            Name = "FAQ",
            Description = "Ofte stilte spørsmål",
            Icon = "icon-help-alt",
            AllowedAsRoot = true,
        };
        ct.AddPropertyGroup("innhold", "Innhold");
        ct.AddPropertyType(Prop("sporsmal", "Spørsmål", _textStringDt, mandatory: true), "innhold");
        ct.AddPropertyType(Prop("svar", "Svar", _richTextDt), "innhold");
        ct.AddPropertyType(Prop("kategori", "Kategori", _contentPickerDt), "innhold");
        ct.AddPropertyType(Prop("rekkefolge", "Rekkefølge", _numericDt), "innhold");
        _contentTypeService.Save(ct);
    }
}
