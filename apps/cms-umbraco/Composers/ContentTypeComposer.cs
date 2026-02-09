using Umbraco.Cms.Core;
using Umbraco.Cms.Core.Composing;
using Umbraco.Cms.Core.Models;
using Umbraco.Cms.Core.PropertyEditors;
using Umbraco.Cms.Core.Serialization;
using Umbraco.Cms.Core.Services;
using Umbraco.Cms.Core.Strings;
using Umbraco.Cms.Infrastructure.Migrations;
using Umbraco.Cms.Infrastructure.Scoping;

namespace KiNorge.Cms.Composers;

/// <summary>
/// Creates the document types and element types for the KI Norge CMS.
/// Runs as a migration so types are created once on first boot.
/// </summary>
public class ContentTypeComposer : ComponentComposer<ContentTypeComponent>
{
}

public class ContentTypeComponent : IComponent
{
    private readonly IContentTypeService _contentTypeService;
    private readonly IDataTypeService _dataTypeService;
    private readonly IShortStringHelper _shortStringHelper;
    private readonly IConfigurationEditorJsonSerializer _serializer;

    public ContentTypeComponent(
        IContentTypeService contentTypeService,
        IDataTypeService dataTypeService,
        IShortStringHelper shortStringHelper,
        IConfigurationEditorJsonSerializer serializer)
    {
        _contentTypeService = contentTypeService;
        _dataTypeService = dataTypeService;
        _shortStringHelper = shortStringHelper;
        _serializer = serializer;
    }

    public void Initialize()
    {
        // Only create types if they don't already exist
        if (_contentTypeService.Get("artikkel") != null) return;

        CreateElementTypes();
        CreateDocumentTypes();
        CreateContainerStructure();
    }

    public void Terminate() { }

    // ── Element Types (for Block List) ──────────────────────────────

    private void CreateElementTypes()
    {
        // Tekst block
        var tekst = new ContentType(_shortStringHelper, -1)
        {
            Alias = "tekst",
            Name = "Tekst",
            Description = "Generell tekst-blokk med rik-tekst redigering",
            Icon = "icon-edit",
            IsElement = true,
        };
        tekst.AddPropertyGroup("innhold", "Innhold");
        tekst.AddPropertyType(new PropertyType(_shortStringHelper, "innhold", ValueStorageType.Ntext)
        {
            Alias = "innhold",
            Name = "Innhold",
            Description = "Rik-tekst innhold",
            PropertyEditorAlias = Constants.PropertyEditors.Aliases.TinyMce,
            Mandatory = false,
        }, "innhold");
        _contentTypeService.Save(tekst);

        // Advarsel block
        var advarsel = new ContentType(_shortStringHelper, -1)
        {
            Alias = "advarsel",
            Name = "Advarsel",
            Description = "Varselboks med type (info/advarsel/viktig/suksess)",
            Icon = "icon-alert",
            IsElement = true,
        };
        advarsel.AddPropertyGroup("innhold", "Innhold");
        advarsel.AddPropertyType(new PropertyType(_shortStringHelper, "tittel", ValueStorageType.Nvarchar)
        {
            Alias = "tittel",
            Name = "Tittel",
            PropertyEditorAlias = Constants.PropertyEditors.Aliases.TextBox,
            Mandatory = false,
        }, "innhold");
        advarsel.AddPropertyType(new PropertyType(_shortStringHelper, "innhold", ValueStorageType.Ntext)
        {
            Alias = "innhold",
            Name = "Innhold",
            PropertyEditorAlias = Constants.PropertyEditors.Aliases.TinyMce,
            Mandatory = false,
        }, "innhold");
        advarsel.AddPropertyType(new PropertyType(_shortStringHelper, "type", ValueStorageType.Nvarchar)
        {
            Alias = "type",
            Name = "Type",
            Description = "Velg type varsel",
            PropertyEditorAlias = Constants.PropertyEditors.Aliases.DropDownListFlexible,
            Mandatory = false,
        }, "innhold");
        _contentTypeService.Save(advarsel);

        // Lenkeliste block
        var lenkeliste = new ContentType(_shortStringHelper, -1)
        {
            Alias = "lenkeliste",
            Name = "Lenkeliste",
            Description = "Liste med lenker",
            Icon = "icon-link",
            IsElement = true,
        };
        lenkeliste.AddPropertyGroup("innhold", "Innhold");
        lenkeliste.AddPropertyType(new PropertyType(_shortStringHelper, "tittel", ValueStorageType.Nvarchar)
        {
            Alias = "tittel",
            Name = "Tittel",
            PropertyEditorAlias = Constants.PropertyEditors.Aliases.TextBox,
            Mandatory = false,
        }, "innhold");
        // lenker stored as repeatable text fields (JSON in textarea for simplicity)
        lenkeliste.AddPropertyType(new PropertyType(_shortStringHelper, "lenker", ValueStorageType.Ntext)
        {
            Alias = "lenker",
            Name = "Lenker",
            Description = "JSON array: [{\"tekst\": \"...\", \"url\": \"...\", \"ekstern\": true}]",
            PropertyEditorAlias = Constants.PropertyEditors.Aliases.TextArea,
            Mandatory = false,
        }, "innhold");
        _contentTypeService.Save(lenkeliste);

        // FAQ-Innhold block
        var faqInnhold = new ContentType(_shortStringHelper, -1)
        {
            Alias = "faqInnhold",
            Name = "FAQ-Innhold",
            Description = "Spørsmål og svar blokk",
            Icon = "icon-help-alt",
            IsElement = true,
        };
        faqInnhold.AddPropertyGroup("innhold", "Innhold");
        faqInnhold.AddPropertyType(new PropertyType(_shortStringHelper, "sporsmal", ValueStorageType.Nvarchar)
        {
            Alias = "sporsmal",
            Name = "Spørsmål",
            PropertyEditorAlias = Constants.PropertyEditors.Aliases.TextBox,
            Mandatory = true,
        }, "innhold");
        faqInnhold.AddPropertyType(new PropertyType(_shortStringHelper, "svar", ValueStorageType.Ntext)
        {
            Alias = "svar",
            Name = "Svar",
            PropertyEditorAlias = Constants.PropertyEditors.Aliases.TinyMce,
            Mandatory = false,
        }, "innhold");
        _contentTypeService.Save(faqInnhold);
    }

    // ── Document Types ──────────────────────────────────────────────

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
            AllowedAsRoot = false,
            Variations = ContentVariation.Culture,
        };
        ct.AddPropertyGroup("innhold", "Innhold");
        ct.AddPropertyType(new PropertyType(_shortStringHelper, "navn", ValueStorageType.Nvarchar)
        {
            Alias = "navn",
            Name = "Navn",
            PropertyEditorAlias = Constants.PropertyEditors.Aliases.TextBox,
            Mandatory = true,
            Variations = ContentVariation.Culture,
        }, "innhold");
        ct.AddPropertyType(new PropertyType(_shortStringHelper, "slug", ValueStorageType.Nvarchar)
        {
            Alias = "slug",
            Name = "Slug",
            PropertyEditorAlias = Constants.PropertyEditors.Aliases.TextBox,
            Mandatory = true,
        }, "innhold");
        ct.AddPropertyType(new PropertyType(_shortStringHelper, "beskrivelse", ValueStorageType.Ntext)
        {
            Alias = "beskrivelse",
            Name = "Beskrivelse",
            PropertyEditorAlias = Constants.PropertyEditors.Aliases.TextArea,
            Mandatory = false,
        }, "innhold");
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
            AllowedAsRoot = false,
            Variations = ContentVariation.Culture,
        };
        ct.AddPropertyGroup("innhold", "Innhold");
        ct.AddPropertyType(new PropertyType(_shortStringHelper, "tittel", ValueStorageType.Nvarchar)
        {
            Alias = "tittel",
            Name = "Tittel",
            PropertyEditorAlias = Constants.PropertyEditors.Aliases.TextBox,
            Mandatory = true,
            Variations = ContentVariation.Culture,
        }, "innhold");
        ct.AddPropertyType(new PropertyType(_shortStringHelper, "slug", ValueStorageType.Nvarchar)
        {
            Alias = "slug",
            Name = "Slug",
            PropertyEditorAlias = Constants.PropertyEditors.Aliases.TextBox,
            Mandatory = true,
        }, "innhold");
        ct.AddPropertyType(new PropertyType(_shortStringHelper, "innhold", ValueStorageType.Ntext)
        {
            Alias = "innhold",
            Name = "Innhold",
            Description = "Hovedinnhold (Block List)",
            PropertyEditorAlias = Constants.PropertyEditors.Aliases.BlockList,
            Mandatory = false,
        }, "innhold");
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
            AllowedAsRoot = false,
            Variations = ContentVariation.Culture,
        };
        ct.AddPropertyGroup("innhold", "Innhold");
        ct.AddPropertyType(new PropertyType(_shortStringHelper, "tittel", ValueStorageType.Nvarchar)
        {
            Alias = "tittel",
            Name = "Tittel",
            PropertyEditorAlias = Constants.PropertyEditors.Aliases.TextBox,
            Mandatory = true,
            Variations = ContentVariation.Culture,
        }, "innhold");
        ct.AddPropertyType(new PropertyType(_shortStringHelper, "slug", ValueStorageType.Nvarchar)
        {
            Alias = "slug",
            Name = "Slug",
            PropertyEditorAlias = Constants.PropertyEditors.Aliases.TextBox,
            Mandatory = true,
        }, "innhold");
        ct.AddPropertyType(new PropertyType(_shortStringHelper, "innhold", ValueStorageType.Ntext)
        {
            Alias = "innhold",
            Name = "Innhold",
            PropertyEditorAlias = Constants.PropertyEditors.Aliases.BlockList,
            Mandatory = false,
        }, "innhold");

        ct.AddPropertyGroup("seo", "SEO");
        ct.AddPropertyType(new PropertyType(_shortStringHelper, "template", ValueStorageType.Nvarchar)
        {
            Alias = "template",
            Name = "Mal",
            Description = "Velg sidemal: standard, bred, landingsside",
            PropertyEditorAlias = Constants.PropertyEditors.Aliases.DropDownListFlexible,
            Mandatory = false,
        }, "seo");
        ct.AddPropertyType(new PropertyType(_shortStringHelper, "seoTittel", ValueStorageType.Nvarchar)
        {
            Alias = "seoTittel",
            Name = "SEO-tittel",
            PropertyEditorAlias = Constants.PropertyEditors.Aliases.TextBox,
            Mandatory = false,
        }, "seo");
        ct.AddPropertyType(new PropertyType(_shortStringHelper, "seoBeskrivelse", ValueStorageType.Ntext)
        {
            Alias = "seoBeskrivelse",
            Name = "SEO-beskrivelse",
            PropertyEditorAlias = Constants.PropertyEditors.Aliases.TextArea,
            Mandatory = false,
        }, "seo");
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
            AllowedAsRoot = false,
            Variations = ContentVariation.Culture,
        };
        ct.AddPropertyGroup("innhold", "Innhold");
        ct.AddPropertyType(new PropertyType(_shortStringHelper, "tittel", ValueStorageType.Nvarchar)
        {
            Alias = "tittel",
            Name = "Tittel",
            PropertyEditorAlias = Constants.PropertyEditors.Aliases.TextBox,
            Mandatory = true,
            Variations = ContentVariation.Culture,
        }, "innhold");
        ct.AddPropertyType(new PropertyType(_shortStringHelper, "slug", ValueStorageType.Nvarchar)
        {
            Alias = "slug",
            Name = "Slug",
            PropertyEditorAlias = Constants.PropertyEditors.Aliases.TextBox,
            Mandatory = true,
        }, "innhold");
        ct.AddPropertyType(new PropertyType(_shortStringHelper, "organisasjon", ValueStorageType.Nvarchar)
        {
            Alias = "organisasjon",
            Name = "Organisasjon",
            PropertyEditorAlias = Constants.PropertyEditors.Aliases.TextBox,
            Mandatory = false,
        }, "innhold");
        ct.AddPropertyType(new PropertyType(_shortStringHelper, "beskrivelse", ValueStorageType.Ntext)
        {
            Alias = "beskrivelse",
            Name = "Beskrivelse",
            PropertyEditorAlias = Constants.PropertyEditors.Aliases.BlockList,
            Mandatory = false,
        }, "innhold");
        ct.AddPropertyType(new PropertyType(_shortStringHelper, "verktoy", ValueStorageType.Ntext)
        {
            Alias = "verktoy",
            Name = "Verktøy",
            Description = "JSON array med verktøynavn",
            PropertyEditorAlias = Constants.PropertyEditors.Aliases.TextArea,
            Mandatory = false,
        }, "innhold");
        ct.AddPropertyType(new PropertyType(_shortStringHelper, "resultater", ValueStorageType.Ntext)
        {
            Alias = "resultater",
            Name = "Resultater",
            PropertyEditorAlias = Constants.PropertyEditors.Aliases.TextArea,
            Mandatory = false,
        }, "innhold");
        ct.AddPropertyType(new PropertyType(_shortStringHelper, "status", ValueStorageType.Nvarchar)
        {
            Alias = "status",
            Name = "Status",
            Description = "i_utvikling, pilot, i_drift, avsluttet",
            PropertyEditorAlias = Constants.PropertyEditors.Aliases.DropDownListFlexible,
            Mandatory = false,
        }, "innhold");
        ct.AddPropertyType(new PropertyType(_shortStringHelper, "bilde", ValueStorageType.Nvarchar)
        {
            Alias = "bilde",
            Name = "Bilde",
            PropertyEditorAlias = Constants.PropertyEditors.Aliases.MediaPicker3,
            Mandatory = false,
        }, "innhold");
        ct.AddPropertyType(new PropertyType(_shortStringHelper, "merkelapper", ValueStorageType.Ntext)
        {
            Alias = "merkelapper",
            Name = "Merkelapper",
            PropertyEditorAlias = Constants.PropertyEditors.Aliases.MultiNodeTreePicker,
            Mandatory = false,
        }, "innhold");
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
            AllowedAsRoot = false,
            Variations = ContentVariation.Culture,
        };
        ct.AddPropertyGroup("innhold", "Innhold");
        ct.AddPropertyType(new PropertyType(_shortStringHelper, "tittel", ValueStorageType.Nvarchar)
        {
            Alias = "tittel",
            Name = "Tittel",
            PropertyEditorAlias = Constants.PropertyEditors.Aliases.TextBox,
            Mandatory = true,
            Variations = ContentVariation.Culture,
        }, "innhold");
        ct.AddPropertyType(new PropertyType(_shortStringHelper, "slug", ValueStorageType.Nvarchar)
        {
            Alias = "slug",
            Name = "Slug",
            PropertyEditorAlias = Constants.PropertyEditors.Aliases.TextBox,
            Mandatory = true,
        }, "innhold");
        ct.AddPropertyType(new PropertyType(_shortStringHelper, "innhold", ValueStorageType.Ntext)
        {
            Alias = "innhold",
            Name = "Innhold",
            PropertyEditorAlias = Constants.PropertyEditors.Aliases.BlockList,
            Mandatory = false,
        }, "innhold");
        ct.AddPropertyType(new PropertyType(_shortStringHelper, "kategori", ValueStorageType.Nvarchar)
        {
            Alias = "kategori",
            Name = "Kategori",
            Description = "Velg merkelapp-kategori",
            PropertyEditorAlias = Constants.PropertyEditors.Aliases.ContentPicker,
            Mandatory = false,
        }, "innhold");
        ct.AddPropertyType(new PropertyType(_shortStringHelper, "lenker", ValueStorageType.Ntext)
        {
            Alias = "lenker",
            Name = "Lenker",
            Description = "JSON: [{\"tekst\": \"...\", \"url\": \"...\", \"ekstern\": true}]",
            PropertyEditorAlias = Constants.PropertyEditors.Aliases.BlockList,
            Mandatory = false,
        }, "innhold");
        ct.AddPropertyType(new PropertyType(_shortStringHelper, "rekkefolge", ValueStorageType.Integer)
        {
            Alias = "rekkefolge",
            Name = "Rekkefølge",
            PropertyEditorAlias = Constants.PropertyEditors.Aliases.Integer,
            Mandatory = false,
        }, "innhold");
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
            AllowedAsRoot = false,
            Variations = ContentVariation.Culture,
        };
        ct.AddPropertyGroup("innhold", "Innhold");
        ct.AddPropertyType(new PropertyType(_shortStringHelper, "sporsmal", ValueStorageType.Nvarchar)
        {
            Alias = "sporsmal",
            Name = "Spørsmål",
            PropertyEditorAlias = Constants.PropertyEditors.Aliases.TextBox,
            Mandatory = true,
            Variations = ContentVariation.Culture,
        }, "innhold");
        ct.AddPropertyType(new PropertyType(_shortStringHelper, "svar", ValueStorageType.Ntext)
        {
            Alias = "svar",
            Name = "Svar",
            PropertyEditorAlias = Constants.PropertyEditors.Aliases.BlockList,
            Mandatory = false,
        }, "innhold");
        ct.AddPropertyType(new PropertyType(_shortStringHelper, "kategori", ValueStorageType.Nvarchar)
        {
            Alias = "kategori",
            Name = "Kategori",
            PropertyEditorAlias = Constants.PropertyEditors.Aliases.ContentPicker,
            Mandatory = false,
        }, "innhold");
        ct.AddPropertyType(new PropertyType(_shortStringHelper, "rekkefolge", ValueStorageType.Integer)
        {
            Alias = "rekkefolge",
            Name = "Rekkefølge",
            PropertyEditorAlias = Constants.PropertyEditors.Aliases.Integer,
            Mandatory = false,
        }, "innhold");
        _contentTypeService.Save(ct);
    }

    // ── Content Tree Structure ──────────────────────────────────────

    private void CreateContainerStructure()
    {
        // Container types are created by Umbraco when using list view containers.
        // The content tree containers (Artikler/, Sider/, etc.) will be created
        // in the backoffice on first run, or via a separate content seed migration.
        // This keeps the composer focused on type definitions only.
    }
}
