using Umbraco.Cms.Core;
using Umbraco.Cms.Core.Composing;
using Umbraco.Cms.Core.Models;
using Umbraco.Cms.Core.PropertyEditors;
using Umbraco.Cms.Core.Serialization;
using Umbraco.Cms.Core.Services;
using Umbraco.Cms.Core.Strings;

namespace KiNorge.Cms.Composers;

/// <summary>
/// Creates document types for KI Norge CMS on first boot.
/// Creates both content types and container types (folders with list views).
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
    private readonly IConfigurationEditorJsonSerializer _configSerializer;
    private readonly PropertyEditorCollection _propertyEditors;

    // Data types (resolved at init time)
    private IDataType _textStringDt = null!;
    private IDataType _textAreaDt = null!;
    private IDataType _richTextDt = null!;            // Standard RichText (full toolbar)
    private IDataType _richTextDtRestricted = null!;  // Restricted RichText (no headings, no source editor)
    private IDataType _numericDt = null!;
    private IDataType _mediaPickerDt = null!;
    private IDataType _contentPickerDt = null!;
    private IDataType _calloutVariantDt = null!;
    private IDataType _bakgrunnDropdownDt = null!;    // Hvit / Lys blå dropdown for Artikkelhode
    private IDataType _trueFalseDt = null!;           // Boolean checkbox

    // Block List data types (created at init time)
    private IDataType _blockListAccordionDt = null!;
    private IDataType _blockListTipsDt = null!;
    private IDataType _blockListEventsDt = null!;
    private IDataType _blockListArtikkelDt = null!;
    private IDataType _blockListSandkasseStegDt = null!;
    private IDataType _blockListSandkasseFaqDt = null!;
    private IDataType _blockListVeiledningKortDt = null!;
    private IDataType _blockListVerktoyKortDt = null!;

    public ContentTypeComponent(
        IContentTypeService contentTypeService,
        IDataTypeService dataTypeService,
        IShortStringHelper shortStringHelper,
        IRuntimeState runtimeState,
        IConfigurationEditorJsonSerializer configSerializer,
        PropertyEditorCollection propertyEditors)
    {
        _contentTypeService = contentTypeService;
        _dataTypeService = dataTypeService;
        _shortStringHelper = shortStringHelper;
        _runtimeState = runtimeState;
        _configSerializer = configSerializer;
        _propertyEditors = propertyEditors;
    }

    public Task InitializeAsync(bool isRestarting, CancellationToken cancellationToken)
    {
        if (_runtimeState.Level < RuntimeLevel.Run) return Task.CompletedTask;

        try
        {
            ResolveDataTypes();

            // Create each type only if it doesn't already exist
            if (_contentTypeService.Get("accordionSection") == null)
                CreateAccordionSectionElement();
            if (_contentTypeService.Get("tipItem") == null)
                CreateTipItemElement();
            else
                MigrateTipItemElement();
            // Arrangement deaktivert for MVP
            // if (_contentTypeService.Get("eventItem") == null)
            //     CreateEventItemElement();

            // Article element types
            if (_contentTypeService.Get("artikkelTekst") == null)
                CreateArtikkelTekstElement();
            else
                MigrateArtikkelTekst();
            if (_contentTypeService.Get("artikkelInfoBoks") == null)
                CreateArtikkelInfoBoksElement();
            // artikkelHero element type is deprecated (replaced by Artikkelhode top-level fields).
            // Existing seed data may still reference it; renderer handles missing type gracefully.
            if (_contentTypeService.Get("artikkelBildeSeksjon") == null)
                CreateArtikkelBildeSeksjonElement();
            else
                MigrateArtikkelBildeSeksjon();
            if (_contentTypeService.Get("artikkelTrekkspill") == null)
                CreateArtikkelTrekkspillElement();
            if (_contentTypeService.Get("artikkelSitat") == null)
                CreateArtikkelSitatElement();
            if (_contentTypeService.Get("artikkelCallout") == null)
                CreateArtikkelCalloutElement();
            if (_contentTypeService.Get("artikkelFremheving") == null)
                CreateArtikkelFremhevingElement();

            // Sandkasse element types
            if (_contentTypeService.Get("sandkasseSteg") == null)
                CreateSandkasseStegElement();
            if (_contentTypeService.Get("sandkasseFaq") == null)
                CreateSandkasseFaqElement();

            // Veiledning Oversikt element types
            if (_contentTypeService.Get("veiledningKort") == null)
                CreateVeiledningKortElement();
            if (_contentTypeService.Get("verktoyKort") == null)
                CreateVerktoyKortElement();

            MigrateVeiledningKort();
            MigrateVerktoyKort();

            CreateBlockListDataTypes();

            if (_contentTypeService.Get("merkelapp") == null)
                CreateMerkelapp();
            if (_contentTypeService.Get("artikkel") == null)
                CreateArtikkel();
            MigrateArtikkelType();
            if (_contentTypeService.Get("side") == null)
                CreateSide();

            IContentType? eksempel;
            if (_contentTypeService.Get("eksempel") == null)
                eksempel = CreateEksempel();
            else
                eksempel = _contentTypeService.Get("eksempel");

            if (_contentTypeService.Get("veiledningGuide") == null)
                CreateVeiledningGuide();
            if (_contentTypeService.Get("veiledningSteg") == null)
                CreateVeiledningSteg();
            if (_contentTypeService.Get("faq") == null)
                CreateFAQ();
            if (_contentTypeService.Get("forside") == null)
                CreateForside();
            else
                MigrateForside();
            if (_contentTypeService.Get("omOssSeksjon") == null)
                CreateOmOssSeksjon();
            if (_contentTypeService.Get("omOss") == null)
                CreateOmOss();
            else
                MigrateOmOss();
            if (_contentTypeService.Get("sandkasse") == null)
                CreateSandkasse();
            if (_contentTypeService.Get("veiledningOversikt") == null)
                CreateVeiledningOversikt();

            // Create container types if missing
            CreateContainerIfMissing("artikler", "Artikler", "icon-newspaper-alt", "artikkel");
            CreateContainerIfMissing("sider", "Sider", "icon-document", "side");
            CreateContainerIfMissing("eksempler", "Eksempler", "icon-science", "eksempel");
            if (_contentTypeService.Get("veiledninger") == null)
            {
                var guideType = _contentTypeService.Get("veiledningGuide");
                var stegType = _contentTypeService.Get("veiledningSteg");
                if (guideType != null && stegType != null)
                {
                    var ct = new ContentType(_shortStringHelper, -1)
                    {
                        Alias = "veiledninger",
                        Name = "Veiledninger",
                        Icon = "icon-book-alt",
                        AllowedAsRoot = true,
                    };
                    ct.AllowedContentTypes = new[]
                    {
                        new ContentTypeSort(guideType.Key, 0, guideType.Alias),
                        new ContentTypeSort(stegType.Key, 1, stegType.Alias)
                    };
                    _contentTypeService.Save(ct);
                }
            }
            CreateContainerIfMissing("faqSamling", "FAQ", "icon-help-alt", "faq");
            CreateContainerIfMissing("merkelapper", "Merkelapper", "icon-tags", "merkelapp");
            // Ikonvelger deaktivert — ble ikke bra nok for redaktørene
            // if (_contentTypeService.Get("tilgjengeligIkon") == null)
            //     CreateTilgjengeligIkon();
            // CreateContainerIfMissing("tilgjengeligeIkoner", "Tilgjengelige ikoner", "icon-picture", "tilgjengeligIkon");

            if (_contentTypeService.Get("ordbokOppslag") == null)
                CreateOrdbokOppslag();
            CreateContainerIfMissing("ordbokSamling", "KI-ordbok", "icon-book-alt", "ordbokOppslag");

            // RichText data types are ensured by ResolveDataTypes() at the very start of this method.
            // Standard + Restricted variants get correct toolbar+extensions config every startup.
            // No need to call again here.
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
        // RichText: ensure both standard and restricted variants exist with correct config.
        // Toolbar/extension configs live in StandardToolbar/StandardExtensions and
        // RestrictedToolbar/RestrictedExtensions constants near EnsureRichTextDataTypes().
        EnsureRichTextDataTypes();
        _richTextDt = FindRichTextByName(StandardRichTextName);
        _richTextDtRestricted = FindRichTextByName(RestrictedRichTextName);
        _numericDt = FindDataType(Constants.PropertyEditors.Aliases.Integer);
        _mediaPickerDt = FindDataType(Constants.PropertyEditors.Aliases.MediaPicker3);
        _contentPickerDt = FindDataType(Constants.PropertyEditors.Aliases.ContentPicker);
        _calloutVariantDt = CreateOrGetCalloutVariantDropdown();
        _bakgrunnDropdownDt = CreateOrGetBakgrunnDropdown();
        _trueFalseDt = FindDataType(Constants.PropertyEditors.Aliases.Boolean);
    }

    private IDataType FindRichTextByName(string name)
    {
        return _dataTypeService.GetByEditorAlias(Constants.PropertyEditors.Aliases.RichText)
            .FirstOrDefault(dt => dt.Name == name)
            ?? throw new InvalidOperationException($"RichText data type '{name}' not found after EnsureRichTextDataTypes");
    }

    private IDataType CreateOrGetBakgrunnDropdown()
    {
        var existing = _dataTypeService.GetByEditorAlias(Constants.PropertyEditors.Aliases.DropDownListFlexible)
            .FirstOrDefault(dt => dt.Name == "Artikkelhode Bakgrunn");
        if (existing != null) return existing;

        var editor = _propertyEditors[Constants.PropertyEditors.Aliases.DropDownListFlexible]
            ?? throw new InvalidOperationException("DropDownListFlexible editor not found");

        var dt = new DataType(editor, _configSerializer)
        {
            Name = "Artikkelhode Bakgrunn",
            DatabaseType = ValueStorageType.Nvarchar,
            EditorUiAlias = "Umb.PropertyEditorUi.Dropdown",
            ConfigurationData = new Dictionary<string, object>
            {
                ["items"] = new[] { "hvit", "lyseblaa" },
            },
        };
        _dataTypeService.Save(dt);
        return dt;
    }

    private IDataType CreateOrGetCalloutVariantDropdown()
    {
        var existing = _dataTypeService.GetByEditorAlias(Constants.PropertyEditors.Aliases.DropDownListFlexible)
            .FirstOrDefault(dt => dt.Name == "Callout Variant");
        if (existing != null) return existing;

        var editor = _propertyEditors[Constants.PropertyEditors.Aliases.DropDownListFlexible]
            ?? throw new InvalidOperationException("DropDownListFlexible editor not found");

        var dt = new DataType(editor, _configSerializer)
        {
            Name = "Callout Variant",
            DatabaseType = ValueStorageType.Nvarchar,
            EditorUiAlias = "Umb.PropertyEditorUi.Dropdown",
            ConfigurationData = new Dictionary<string, object>
            {
                ["items"] = new[] { "info", "obs", "advarsel", "suksess" },
            },
        };
        _dataTypeService.Save(dt);
        return dt;
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

    // --- Element types for Block Lists ---

    private IContentType CreateAccordionSectionElement()
    {
        var ct = new ContentType(_shortStringHelper, -1)
        {
            Alias = "accordionSection",
            Name = "Accordion Section",
            Description = "En seksjon i en trekkspill-liste",
            Icon = "icon-list",
            IsElement = true,
        };
        ct.AddPropertyGroup("innhold", "Innhold");
        ct.AddPropertyType(Prop("title", "Tittel", _textStringDt, mandatory: true), "innhold");
        ct.AddPropertyType(Prop("body", "Innhold", _richTextDt), "innhold");
        _contentTypeService.Save(ct);
        return ct;
    }

    private IContentType CreateTipItemElement()
    {
        var ct = new ContentType(_shortStringHelper, -1)
        {
            Alias = "tipItem",
            Name = "Tips",
            Description = "Et tips-element",
            Icon = "icon-lightbulb",
            IsElement = true,
        };
        ct.AddPropertyGroup("innhold", "Innhold");
        ct.AddPropertyType(Prop("tipsTitle", "Tittel", _textStringDt, mandatory: true), "innhold");
        ct.AddPropertyType(Prop("tipsTekst", "Tekst", _richTextDt), "innhold");
        ct.AddPropertyType(Prop("tipsBilde", "Bilde", _mediaPickerDt), "innhold");
        _contentTypeService.Save(ct);
        return ct;
    }

    private void MigrateTipItemElement()
    {
        var ct = _contentTypeService.Get("tipItem");
        if (ct == null) return;
        if (ct.PropertyTypeExists("tipsBilde")) return;
        ct.AddPropertyType(Prop("tipsBilde", "Bilde", _mediaPickerDt), "innhold");
        _contentTypeService.Save(ct);
    }

    private IContentType CreateEventItemElement()
    {
        var ct = new ContentType(_shortStringHelper, -1)
        {
            Alias = "eventItem",
            Name = "Arrangement",
            Description = "Et arrangement-element",
            Icon = "icon-calendar",
            IsElement = true,
        };
        ct.AddPropertyGroup("innhold", "Innhold");
        ct.AddPropertyType(Prop("eventTittel", "Tittel", _textStringDt, mandatory: true), "innhold");
        ct.AddPropertyType(Prop("eventDato", "Dato", _textStringDt), "innhold");
        ct.AddPropertyType(Prop("eventSted", "Sted", _textStringDt), "innhold");
        ct.AddPropertyType(Prop("eventUrl", "URL", _textStringDt), "innhold");
        _contentTypeService.Save(ct);
        return ct;
    }

    // --- Article element types ---

    private IContentType CreateArtikkelTekstElement()
    {
        var ct = new ContentType(_shortStringHelper, -1)
        {
            Alias = "artikkelTekst",
            Name = "Brødtekst",
            Description = "Rik tekstblokk med overskrifter (H2/H3/H4), lister, lenker, fet, kursiv og blockquote",
            Icon = "icon-edit",
            IsElement = true,
        };
        ct.AddPropertyGroup("innhold", "Innhold");
        ct.AddPropertyType(Prop("innhold", "Innhold", _richTextDt), "innhold");
        _contentTypeService.Save(ct);
        return ct;
    }

    private void MigrateArtikkelTekst()
    {
        var ct = _contentTypeService.Get("artikkelTekst");
        if (ct == null) return;
        if (ct.Name == "Brødtekst") return;

        ct.Name = "Brødtekst";
        ct.Description = "Rik tekstblokk med overskrifter (H2/H3/H4), lister, lenker, fet, kursiv og blockquote";
        _contentTypeService.Save(ct);
    }

    private IContentType CreateArtikkelInfoBoksElement()
    {
        var ct = new ContentType(_shortStringHelper, -1)
        {
            Alias = "artikkelInfoBoks",
            Name = "Artikkel Infoboks",
            Description = "Blå infoboks (#e5f2f7 bakgrunn)",
            Icon = "icon-info",
            IsElement = true,
        };
        ct.AddPropertyGroup("innhold", "Innhold");
        ct.AddPropertyType(Prop("tittel", "Tittel", _textStringDt), "innhold");
        ct.AddPropertyType(Prop("innhold", "Innhold", _richTextDt, mandatory: true), "innhold");
        _contentTypeService.Save(ct);
        return ct;
    }

    // CreateArtikkelHeroElement removed — replaced by Artikkelhode top-level fields on artikkel/case.

    private IContentType CreateArtikkelTrekkspillElement()
    {
        var ct = new ContentType(_shortStringHelper, -1)
        {
            Alias = "artikkelTrekkspill",
            Name = "Artikkel Trekkspill",
            Description = "Accordion med tittel og innhold som kan utvides",
            Icon = "icon-list",
            IsElement = true,
        };
        ct.AddPropertyGroup("innhold", "Innhold");
        ct.AddPropertyType(Prop("tittel", "Tittel", _textStringDt, mandatory: true), "innhold");
        ct.AddPropertyType(Prop("innhold", "Innhold", _richTextDt, mandatory: true), "innhold");
        _contentTypeService.Save(ct);
        return ct;
    }

    private IContentType CreateArtikkelSitatElement()
    {
        var ct = new ContentType(_shortStringHelper, -1)
        {
            Alias = "artikkelSitat",
            Name = "Artikkel Sitat",
            Description = "Uthevet sitat med valgfri kilde",
            Icon = "icon-quote",
            IsElement = true,
        };
        ct.AddPropertyGroup("innhold", "Innhold");
        ct.AddPropertyType(Prop("sitat", "Sitat", _textAreaDt, mandatory: true), "innhold");
        ct.AddPropertyType(Prop("kilde", "Kilde", _textStringDt), "innhold");
        _contentTypeService.Save(ct);
        return ct;
    }

    private IContentType CreateArtikkelCalloutElement()
    {
        var ct = new ContentType(_shortStringHelper, -1)
        {
            Alias = "artikkelCallout",
            Name = "Artikkel Callout",
            Description = "Varselboks (info, obs, advarsel, suksess)",
            Icon = "icon-alert",
            IsElement = true,
        };
        ct.AddPropertyGroup("innhold", "Innhold");
        ct.AddPropertyType(Prop("tittel", "Tittel", _textStringDt), "innhold");
        ct.AddPropertyType(Prop("innhold", "Innhold", _richTextDt, mandatory: true), "innhold");
        ct.AddPropertyType(Prop("variant", "Variant", _calloutVariantDt, description: "Velg type varselboks"), "innhold");
        _contentTypeService.Save(ct);
        return ct;
    }

    /// <summary>
    /// Unified highlight block. Toggles control whether it shows as a colored fact box,
    /// a quote with « », or includes an image. Replaces artikkelInfoBoks, artikkelCallout,
    /// and artikkelSitat.
    /// </summary>
    private IContentType CreateArtikkelFremhevingElement()
    {
        var ct = new ContentType(_shortStringHelper, -1)
        {
            Alias = "artikkelFremheving",
            Name = "Fremheving",
            Description = "Uthevet boks med valgfritt bilde, bakgrunnsfarge og sitat-tegn. Brukes for fakta, høydepunkter og sitater.",
            Icon = "icon-favorite",
            IsElement = true,
        };
        ct.AddPropertyGroup("innhold", "Innhold");
        ct.AddPropertyType(Prop("tittel", "Tittel", _textStringDt, description: "Valgfri overskrift over teksten"), "innhold");
        ct.AddPropertyType(Prop("tekst", "Tekst", _richTextDtRestricted, mandatory: true, description: "Hovedteksten i fremhevingen. Bare grunnleggende formatering tillatt (fet, kursiv, lister, lenker)."), "innhold");
        ct.AddPropertyType(Prop("bilde", "Bilde", _mediaPickerDt, description: "Valgfritt bilde til venstre for teksten på desktop, over på mobil"), "innhold");
        ct.AddPropertyType(Prop("bildeAlt", "Alternativ tekst for bilde", _textStringDt, description: "Beskriver bildet for skjermlesere. La stå tom hvis bildet kun er dekorativt."), "innhold");
        ct.AddPropertyType(Prop("visBakgrunn", "Vis bakgrunnsfarge", _trueFalseDt, description: "Slå på lyseblå bakgrunn (Faktaboks-stil). Standard på."), "innhold");
        ct.AddPropertyType(Prop("visAnforselstegn", "Vis anførselstegn", _trueFalseDt, description: "Slå på «...» rundt teksten (Sitat-stil). Standard av."), "innhold");
        ct.AddPropertyType(Prop("kilde", "Kilde", _textStringDt, description: "Valgfri kilde/citat-attribusjon, vises under teksten"), "innhold");
        _contentTypeService.Save(ct);
        return ct;
    }

    private IContentType CreateArtikkelBildeSeksjonElement()
    {
        var ct = new ContentType(_shortStringHelper, -1)
        {
            Alias = "artikkelBildeSeksjon",
            Name = "Bilde",
            Description = "Bilde med valgfri bildetekst og fotokreditering",
            Icon = "icon-picture",
            IsElement = true,
        };
        ct.AddPropertyGroup("innhold", "Innhold");
        ct.AddPropertyType(Prop("bilde", "Bilde", _mediaPickerDt, mandatory: true), "innhold");
        ct.AddPropertyType(Prop("bildeAlt", "Alternativ tekst", _textStringDt, description: "Beskriver bildet for skjermlesere. La stå tom hvis bildet kun er dekorativt."), "innhold");
        ct.AddPropertyType(Prop("bildetekst", "Bildetekst", _textStringDt, description: "Bildetekst og evt. fotograf/kilde, f.eks. 'Foto: Dag Alveng'"), "innhold");
        _contentTypeService.Save(ct);
        return ct;
    }

    private void MigrateArtikkelBildeSeksjon()
    {
        var ct = _contentTypeService.Get("artikkelBildeSeksjon");
        if (ct == null) return;

        bool changed = false;

        // Rename to "Bilde" and update description
        if (ct.Name != "Bilde")
        {
            ct.Name = "Bilde";
            ct.Description = "Bilde med valgfri bildetekst og fotokreditering";
            changed = true;
        }

        // Add bildeAlt if missing
        if (!ct.PropertyTypes.Any(p => p.Alias == "bildeAlt"))
        {
            ct.AddPropertyType(Prop("bildeAlt", "Alternativ tekst", _textStringDt, description: "Beskriver bildet for skjermlesere. La stå tom hvis bildet kun er dekorativt."), "innhold");
            changed = true;
        }

        // Make bilde mandatory
        var bildeProp = ct.PropertyTypes.FirstOrDefault(p => p.Alias == "bilde");
        if (bildeProp != null && !bildeProp.Mandatory)
        {
            bildeProp.Mandatory = true;
            changed = true;
        }

        if (changed)
            _contentTypeService.Save(ct);
    }

    // --- Sandkasse element types ---

    private IContentType CreateSandkasseStegElement()
    {
        var ct = new ContentType(_shortStringHelper, -1)
        {
            Alias = "sandkasseSteg",
            Name = "Sandkasse Steg",
            Description = "Et steg i sandkasse-prosessen",
            Icon = "icon-ordered-list",
            IsElement = true,
        };
        ct.AddPropertyGroup("innhold", "Innhold");
        ct.AddPropertyType(Prop("nummer", "Nummer", _textStringDt), "innhold");
        ct.AddPropertyType(Prop("tittel", "Tittel", _textStringDt, mandatory: true), "innhold");
        ct.AddPropertyType(Prop("beskrivelse", "Beskrivelse", _richTextDt), "innhold");
        _contentTypeService.Save(ct);
        return ct;
    }

    private IContentType CreateSandkasseFaqElement()
    {
        var ct = new ContentType(_shortStringHelper, -1)
        {
            Alias = "sandkasseFaq",
            Name = "Sandkasse FAQ",
            Description = "Et spørsmål og svar i sandkasse-FAQ",
            Icon = "icon-help-alt",
            IsElement = true,
        };
        ct.AddPropertyGroup("innhold", "Innhold");
        ct.AddPropertyType(Prop("sporsmal", "Spørsmål", _textStringDt, mandatory: true), "innhold");
        ct.AddPropertyType(Prop("svar", "Svar", _richTextDt), "innhold");
        _contentTypeService.Save(ct);
        return ct;
    }

    // --- Block List DataTypes ---

    private void CreateBlockListDataTypes()
    {
        _blockListAccordionDt = CreateOrGetBlockListDataType(
            "Block List - Accordion Sections", "accordionSection");
        _blockListTipsDt = CreateOrGetBlockListDataType(
            "Block List - Tips", "tipItem");
        // Arrangement deaktivert for MVP — eventItem er ikke opprettet
        // _blockListEventsDt = CreateOrGetBlockListDataType(
        //     "Block List - Events", "eventItem");
        _blockListArtikkelDt = CreateOrGetMultiBlockListDataType(
            "Block List - Artikkel Innhold",
            BaseArticleModules);
        _blockListSandkasseStegDt = CreateOrGetBlockListDataType(
            "Block List - Sandkasse Steg", "sandkasseSteg");
        _blockListSandkasseFaqDt = CreateOrGetBlockListDataType(
            "Block List - Sandkasse FAQ", "sandkasseFaq");
        _blockListVeiledningKortDt = CreateOrGetBlockListDataType(
            "Block List - Veiledning Kort", "veiledningKort");
        _blockListVerktoyKortDt = CreateOrGetBlockListDataType(
            "Block List - Verktøy Kort", "verktoyKort");
    }

    private IDataType CreateOrGetBlockListDataType(string name, string elementTypeAlias)
    {
        // Check if it already exists by name
        var existing = _dataTypeService.GetByEditorAlias(Constants.PropertyEditors.Aliases.BlockList)
            .FirstOrDefault(dt => dt.Name == name);
        if (existing != null)
        {
            if (string.IsNullOrEmpty(existing.EditorUiAlias))
            {
                existing.EditorUiAlias = "Umb.PropertyEditorUi.BlockList";
                _dataTypeService.Save(existing);
            }
            return existing;
        }

        var elementType = _contentTypeService.Get(elementTypeAlias)
            ?? throw new InvalidOperationException($"Element type '{elementTypeAlias}' not found");

        var blockListEditor = _propertyEditors[Constants.PropertyEditors.Aliases.BlockList]
            ?? throw new InvalidOperationException("Block List property editor not found");

        var dt = new DataType(blockListEditor, _configSerializer)
        {
            Name = name,
            DatabaseType = ValueStorageType.Ntext,
            EditorUiAlias = "Umb.PropertyEditorUi.BlockList",
            ConfigurationData = new Dictionary<string, object>
            {
                ["blocks"] = new object[]
                {
                    new { contentElementTypeKey = elementType.Key }
                }
            },
        };
        _dataTypeService.Save(dt);
        return dt;
    }

    private IDataType CreateOrGetMultiBlockListDataType(string name, string[] elementTypeAliases)
    {
        // Check if it already exists by name
        var existing = _dataTypeService.GetByEditorAlias(Constants.PropertyEditors.Aliases.BlockList)
            .FirstOrDefault(dt => dt.Name == name);
        if (existing != null)
        {
            if (string.IsNullOrEmpty(existing.EditorUiAlias))
            {
                existing.EditorUiAlias = "Umb.PropertyEditorUi.BlockList";
                _dataTypeService.Save(existing);
            }
            return existing;
        }

        // Skip missing element types so the block list can be created during incremental
        // development. Missing types are logged so they can be tracked down.
        var blocks = elementTypeAliases.Select(alias =>
        {
            var elementType = _contentTypeService.Get(alias);
            if (elementType == null)
            {
                Console.WriteLine($"ContentTypeComposer: Element type '{alias}' not found, skipping in block list '{name}'");
                return null;
            }
            return new { contentElementTypeKey = elementType.Key };
        }).Where(b => b != null).ToArray();

        var blockListEditor = _propertyEditors[Constants.PropertyEditors.Aliases.BlockList]
            ?? throw new InvalidOperationException("Block List property editor not found");

        var dt = new DataType(blockListEditor, _configSerializer)
        {
            Name = name,
            DatabaseType = ValueStorageType.Ntext,
            EditorUiAlias = "Umb.PropertyEditorUi.BlockList",
            ConfigurationData = new Dictionary<string, object>
            {
                ["blocks"] = blocks
            },
        };
        _dataTypeService.Save(dt);
        return dt;
    }

    // ── Module lists ───────────────────────────────────────────────────
    // Single source of truth for which element types are allowed in each
    // content type's body Block List. To add a new module everywhere:
    // add it to BaseArticleModules. To diverge case from artikkel later:
    // build CaseModules as BaseArticleModules.Concat(...).ToArray().

    private static readonly string[] BaseArticleModules =
    {
        "artikkelTekst",
        "artikkelBildeSeksjon",
        "artikkelTrekkspill",
        // New unified module replacing InfoBoks + Callout + Sitat
        "artikkelFremheving",
        // Process steps (container + nested items)
        "artikkelProsessteg",
        // Author/contact variants
        "artikkelByline",
        "artikkelInnholdFra",
        "artikkelKontaktkort",
        // Legacy (still in DB, will be removed once no content references them):
        // "artikkelHero", "artikkelInfoBoks", "artikkelCallout", "artikkelSitat"
    };

    private static readonly string[] CaseModules = BaseArticleModules;

    // ── RichText configurations ────────────────────────────────────────
    // Single source of truth for ALL RichText editors in the CMS.
    // To change a toolbar or add/remove a Tiptap feature: edit the lists below.
    // To add a new RichText variant: add a new pair of constants and one EnsureRichTextDataType call.
    //
    // Removing an extension blocks the feature entirely (no paste, no drag-drop, no shortcut).
    // Removing a toolbar button only hides it in UI — the extension must also be removed to fully block.

    private const string StandardRichTextName = "Richtext editor";
    private const string RestrictedRichTextName = "Richtext editor (begrenset)";

    private static readonly List<List<List<string>>> StandardToolbar = new()
    {
        new()
        {
            new() { "Umb.Tiptap.Toolbar.Heading2", "Umb.Tiptap.Toolbar.Heading3", "Umb.Tiptap.Toolbar.Heading4" },
            new() { "Umb.Tiptap.Toolbar.SourceEditor" },
            new() { "Umb.Tiptap.Toolbar.Bold", "Umb.Tiptap.Toolbar.Italic", "Umb.Tiptap.Toolbar.Underline" },
            new() { "Umb.Tiptap.Toolbar.TextAlignLeft", "Umb.Tiptap.Toolbar.TextAlignCenter", "Umb.Tiptap.Toolbar.TextAlignRight" },
            new() { "Umb.Tiptap.Toolbar.BulletList", "Umb.Tiptap.Toolbar.OrderedList" },
            new() { "Umb.Tiptap.Toolbar.Blockquote" },
            new() { "Umb.Tiptap.Toolbar.Link", "Umb.Tiptap.Toolbar.Unlink" },
        }
    };

    private static readonly List<string> StandardExtensions = new()
    {
        "Umb.Tiptap.RichTextEssentials",
        "Umb.Tiptap.Anchor",
        "Umb.Tiptap.Block",
        "Umb.Tiptap.Blockquote",
        "Umb.Tiptap.Bold",
        "Umb.Tiptap.BulletList",
        "Umb.Tiptap.CodeBlock",
        "Umb.Tiptap.Heading",
        "Umb.Tiptap.HtmlAttributeClass",
        "Umb.Tiptap.HtmlAttributeDataset",
        "Umb.Tiptap.HtmlAttributeId",
        "Umb.Tiptap.HtmlAttributeStyle",
        "Umb.Tiptap.HtmlTagDiv",
        "Umb.Tiptap.HtmlTagSpan",
        "Umb.Tiptap.Italic",
        "Umb.Tiptap.Link",
        "Umb.Tiptap.OrderedList",
        "Umb.Tiptap.Strike",
        "Umb.Tiptap.Subscript",
        "Umb.Tiptap.Superscript",
        "Umb.Tiptap.Table",
        "Umb.Tiptap.TextAlign",
        "Umb.Tiptap.TextDirection",
        "Umb.Tiptap.TextIndent",
        "Umb.Tiptap.TrailingNode",
        "Umb.Tiptap.Underline",
        // Excluded: Image, Embed, HorizontalRule, Figure, MediaUpload
    };

    // Restricted: for highlight blocks (Fremheving) and process step descriptions.
    // No headings (block is itself a highlight, no nested sections), no source editor,
    // no alignment, no blockquote (handled by visAnforselstegn toggle).
    private static readonly List<List<List<string>>> RestrictedToolbar = new()
    {
        new()
        {
            new() { "Umb.Tiptap.Toolbar.Bold", "Umb.Tiptap.Toolbar.Italic" },
            new() { "Umb.Tiptap.Toolbar.BulletList", "Umb.Tiptap.Toolbar.OrderedList" },
            new() { "Umb.Tiptap.Toolbar.Link", "Umb.Tiptap.Toolbar.Unlink" },
        }
    };

    private static readonly List<string> RestrictedExtensions = new()
    {
        "Umb.Tiptap.RichTextEssentials",
        "Umb.Tiptap.Bold",
        "Umb.Tiptap.BulletList",
        "Umb.Tiptap.HtmlAttributeClass",
        "Umb.Tiptap.HtmlAttributeId",
        "Umb.Tiptap.Italic",
        "Umb.Tiptap.Link",
        "Umb.Tiptap.OrderedList",
        "Umb.Tiptap.TrailingNode",
    };

    /// <summary>
    /// Ensures all named RichText data types exist with correct toolbar+extensions config.
    /// Runs every startup and overwrites the config to match constants above.
    /// </summary>
    private void EnsureRichTextDataTypes()
    {
        EnsureRichTextDataType(StandardRichTextName, StandardToolbar, StandardExtensions);
        EnsureRichTextDataType(RestrictedRichTextName, RestrictedToolbar, RestrictedExtensions);
    }

    private IDataType EnsureRichTextDataType(string name, List<List<List<string>>> toolbar, List<string> extensions)
    {
        var existing = _dataTypeService.GetByEditorAlias(Constants.PropertyEditors.Aliases.RichText)
            .FirstOrDefault(dt => dt.Name == name);

        if (existing == null)
        {
            // Create new variant by cloning the editor reference from any existing RichText DT
            var template = _dataTypeService.GetByEditorAlias(Constants.PropertyEditors.Aliases.RichText).First();
            var editor = _propertyEditors[Constants.PropertyEditors.Aliases.RichText]
                ?? throw new InvalidOperationException("RichText editor not found");
            existing = new DataType(editor, _configSerializer)
            {
                Name = name,
                DatabaseType = ValueStorageType.Ntext,
                EditorUiAlias = template.EditorUiAlias,
                ConfigurationData = new Dictionary<string, object>(),
            };
            _dataTypeService.Save(existing);
            Console.WriteLine($"ContentTypeComposer: Created RichText data type '{name}'");
        }

        var config = existing.ConfigurationData ?? new Dictionary<string, object>();
        config["toolbar"] = toolbar;
        config["extensions"] = extensions;
        existing.ConfigurationData = config;
        _dataTypeService.Save(existing);
        return existing;
    }

    // --- Container helper ---

    private void CreateContainerIfMissing(string alias, string name, string icon, string childAlias)
    {
        if (_contentTypeService.Get(alias) != null) return;
        var childType = _contentTypeService.Get(childAlias);
        if (childType == null) return;
        CreateContainer(alias, name, icon, childType);
    }

    private void CreateContainer(string alias, string name, string icon, IContentType childType)
    {
        var ct = new ContentType(_shortStringHelper, -1)
        {
            Alias = alias,
            Name = name,
            Icon = icon,
            AllowedAsRoot = true,
        };
        ct.AllowedContentTypes = new[]
        {
            new ContentTypeSort(childType.Key, 0, childType.Alias)
        };
        _contentTypeService.Save(ct);
    }

    // --- Document types ---

    private IContentType CreateTilgjengeligIkon()
    {
        var ct = new ContentType(_shortStringHelper, -1)
        {
            Alias = "tilgjengeligIkon",
            Name = "Tilgjengelig ikon",
            Description = "Et ikon fra Aksel-ikonpakken som er tilgjengelig for redaktører",
            Icon = "icon-picture",
            AllowedAsRoot = false,
        };
        ct.AddPropertyGroup("innhold", "Innhold");
        ct.AddPropertyType(Prop("navn", "Ikonnavn", _textStringDt, mandatory: true, description: "Det engelske navnet på ikonet fra aksel.nav.no/ikoner (f.eks. HandHeart, Package, Calculator)"), "innhold");
        ct.AddPropertyType(Prop("beskrivelse", "Beskrivelse", _textStringDt, description: "Kort norsk beskrivelse av ikonet (f.eks. Hjerte i hånd, Pakke, Kalkulator)"), "innhold");
        _contentTypeService.Save(ct);
        return ct;
    }

    private IContentType CreateMerkelapp()
    {
        var ct = new ContentType(_shortStringHelper, -1)
        {
            Alias = "merkelapp",
            Name = "Merkelapp",
            Description = "Merkelapp/tag for kategorisering",
            Icon = "icon-tag",
            AllowedAsRoot = false,
        };
        ct.AddPropertyGroup("innhold", "Innhold");
        ct.AddPropertyType(Prop("navn", "Navn", _textStringDt, mandatory: true), "innhold");
        ct.AddPropertyType(Prop("slug", "Slug", _textStringDt, mandatory: true), "innhold");
        ct.AddPropertyType(Prop("beskrivelse", "Beskrivelse", _textAreaDt), "innhold");
        _contentTypeService.Save(ct);
        return ct;
    }

    private IContentType CreateArtikkel()
    {
        var ct = new ContentType(_shortStringHelper, -1)
        {
            Alias = "artikkel",
            Name = "Artikkel",
            Description = "Artikler og nyheter",
            Icon = "icon-newspaper-alt",
            AllowedAsRoot = false,
        };
        ct.AddPropertyGroup("innhold", "Innhold");
        AddArtikkelhodeFields(ct);
        ct.AddPropertyType(Prop("innhold", "Innhold", _blockListArtikkelDt, description: "Hovedinnhold"), "innhold");

        ct.AddPropertyGroup("seo", "SEO");
        ct.AddPropertyType(Prop("seoTittel", "SEO-tittel", _textStringDt, description: "Overstyr tittel i søkeresultater og sosiale medier"), "seo");
        ct.AddPropertyType(Prop("seoBeskrivelse", "SEO-beskrivelse", _textAreaDt, description: "Overstyr beskrivelse i søkeresultater og sosiale medier"), "seo");
        ct.AddPropertyType(Prop("seoBilde", "SEO-bilde", _mediaPickerDt, description: "Bilde som vises ved deling på sosiale medier"), "seo");
        _contentTypeService.Save(ct);
        return ct;
    }

    /// <summary>
    /// Adds the standard Artikkelhode field set (title, slug, ingress, image, alt, background)
    /// to a content type. Used by both artikkel and case so the editor experience is identical.
    /// </summary>
    private void AddArtikkelhodeFields(IContentType ct)
    {
        ct.AddPropertyType(Prop("tittel", "Tittel", _textStringDt, mandatory: true), "innhold");
        ct.AddPropertyType(Prop("slug", "Slug", _textStringDt, mandatory: true, description: "URL-vennlig identifikator. Genereres automatisk fra tittel hvis tom."), "innhold");
        ct.AddPropertyType(Prop("ingress", "Ingress", _textAreaDt, mandatory: true, description: "Kort introduksjonstekst som vises under tittelen."), "innhold");
        ct.AddPropertyType(Prop("artikkelBilde", "Hovedbilde", _mediaPickerDt, description: "Hovedbilde som vises ved siden av tittelen (eller under på mobil)."), "innhold");
        ct.AddPropertyType(Prop("bildeAlt", "Alternativ tekst for bilde", _textStringDt, description: "Beskriver bildet for skjermlesere. La stå tom hvis bildet kun er dekorativt."), "innhold");
        ct.AddPropertyType(Prop("bakgrunn", "Bakgrunn", _bakgrunnDropdownDt, description: "Velg bakgrunnsfarge for artikkelhodet. Standard er hvit."), "innhold");
    }

    private void MigrateArtikkelType()
    {
        var ct = _contentTypeService.Get("artikkel");
        if (ct == null) return;

        // Migrate block list data type
        var prop = ct.PropertyTypes.FirstOrDefault(p => p.Alias == "innhold");
        if (prop != null && prop.DataTypeId != _blockListArtikkelDt.Id)
        {
            prop.DataTypeId = _blockListArtikkelDt.Id;
        }

        bool changed = false;

        // Add Artikkelhode fields if missing (idempotent)
        if (!ct.PropertyTypes.Any(p => p.Alias == "ingress"))
        {
            ct.AddPropertyType(Prop("ingress", "Ingress", _textAreaDt, mandatory: true, description: "Kort introduksjonstekst som vises under tittelen."), "innhold");
            changed = true;
        }
        if (!ct.PropertyTypes.Any(p => p.Alias == "artikkelBilde"))
        {
            ct.AddPropertyType(Prop("artikkelBilde", "Hovedbilde", _mediaPickerDt, description: "Hovedbilde som vises ved siden av tittelen (eller under på mobil)."), "innhold");
            changed = true;
        }
        if (!ct.PropertyTypes.Any(p => p.Alias == "bildeAlt"))
        {
            ct.AddPropertyType(Prop("bildeAlt", "Alternativ tekst for bilde", _textStringDt, description: "Beskriver bildet for skjermlesere. La stå tom hvis bildet kun er dekorativt."), "innhold");
            changed = true;
        }
        if (!ct.PropertyTypes.Any(p => p.Alias == "bakgrunn"))
        {
            ct.AddPropertyType(Prop("bakgrunn", "Bakgrunn", _bakgrunnDropdownDt, description: "Velg bakgrunnsfarge for artikkelhodet. Standard er hvit."), "innhold");
            changed = true;
        }

        if (changed)
            _contentTypeService.Save(ct);
    }

    private IContentType CreateSide()
    {
        var ct = new ContentType(_shortStringHelper, -1)
        {
            Alias = "side",
            Name = "Side",
            Description = "Generelle sider",
            Icon = "icon-document",
            AllowedAsRoot = false,
        };
        ct.AddPropertyGroup("innhold", "Innhold");
        ct.AddPropertyType(Prop("tittel", "Tittel", _textStringDt, mandatory: true), "innhold");
        ct.AddPropertyType(Prop("slug", "Slug", _textStringDt, mandatory: true), "innhold");
        ct.AddPropertyType(Prop("innhold", "Innhold", _richTextDt), "innhold");

        ct.AddPropertyGroup("seo", "SEO");
        ct.AddPropertyType(Prop("template", "Mal", _textStringDt, description: "standard, bred, landingsside"), "seo");
        ct.AddPropertyType(Prop("seoTittel", "SEO-tittel", _textStringDt, description: "Overstyr tittel i søkeresultater og sosiale medier"), "seo");
        ct.AddPropertyType(Prop("seoBeskrivelse", "SEO-beskrivelse", _textAreaDt, description: "Overstyr beskrivelse i søkeresultater og sosiale medier"), "seo");
        ct.AddPropertyType(Prop("seoBilde", "SEO-bilde", _mediaPickerDt, description: "Bilde som vises ved deling på sosiale medier"), "seo");
        _contentTypeService.Save(ct);
        return ct;
    }

    private IContentType CreateEksempel()
    {
        var ct = new ContentType(_shortStringHelper, -1)
        {
            Alias = "eksempel",
            Name = "Eksempel",
            Description = "Gode eksempler / caser",
            Icon = "icon-science",
            AllowedAsRoot = false,
        };
        ct.AddPropertyGroup("innhold", "Innhold");
        ct.AddPropertyType(Prop("tittel", "Tittel", _textStringDt, mandatory: true), "innhold");
        ct.AddPropertyType(Prop("slug", "Slug", _textStringDt, mandatory: true), "innhold");
        ct.AddPropertyType(Prop("organisasjon", "Organisasjon", _textStringDt), "innhold");
        ct.AddPropertyType(Prop("beskrivelse", "Beskrivelse", _richTextDt), "innhold");
        ct.AddPropertyType(Prop("verktoy", "Verktøy", _textAreaDt, description: "JSON array med verktøynavn"), "innhold");
        ct.AddPropertyType(Prop("resultater", "Resultater", _richTextDt), "innhold");
        ct.AddPropertyType(Prop("status", "Status", _textStringDt, description: "i_utvikling, pilot, i_drift, avsluttet"), "innhold");
        ct.AddPropertyType(Prop("bilde", "Bilde", _mediaPickerDt), "innhold");
        ct.AddPropertyType(Prop("merkelapper", "Merkelapper", _textAreaDt, description: "JSON array med merkelapp-slugs"), "innhold");
        ct.AddPropertyType(Prop("accordionSeksjoner", "Accordion-seksjoner", _blockListAccordionDt, description: "Trekkspill-seksjoner"), "innhold");

        ct.AddPropertyGroup("seo", "SEO");
        ct.AddPropertyType(Prop("seoTittel", "SEO-tittel", _textStringDt, description: "Overstyr tittel i søkeresultater og sosiale medier"), "seo");
        ct.AddPropertyType(Prop("seoBeskrivelse", "SEO-beskrivelse", _textAreaDt, description: "Overstyr beskrivelse i søkeresultater og sosiale medier"), "seo");
        ct.AddPropertyType(Prop("seoBilde", "SEO-bilde", _mediaPickerDt, description: "Bilde som vises ved deling på sosiale medier"), "seo");
        _contentTypeService.Save(ct);
        return ct;
    }

    private IContentType CreateVeiledningGuide()
    {
        var ct = new ContentType(_shortStringHelper, -1)
        {
            Alias = "veiledningGuide",
            Name = "Veiledning Guide",
            Description = "Oversiktsside for en veiledningsguide",
            Icon = "icon-book-alt",
            AllowedAsRoot = false,
        };
        ct.AddPropertyGroup("innhold", "Innhold");
        ct.AddPropertyType(Prop("tittel", "Tittel", _textStringDt, mandatory: true), "innhold");
        ct.AddPropertyType(Prop("slug", "Slug", _textStringDt, mandatory: true), "innhold");
        ct.AddPropertyType(Prop("introTekst", "Intro-tekst", _richTextDt), "innhold");

        ct.AddPropertyGroup("seo", "SEO");
        ct.AddPropertyType(Prop("seoTittel", "SEO-tittel", _textStringDt), "seo");
        ct.AddPropertyType(Prop("seoBeskrivelse", "SEO-beskrivelse", _textAreaDt), "seo");
        ct.AddPropertyType(Prop("seoBilde", "SEO-bilde", _mediaPickerDt), "seo");
        _contentTypeService.Save(ct);
        return ct;
    }

    private IContentType CreateVeiledningSteg()
    {
        var ct = new ContentType(_shortStringHelper, -1)
        {
            Alias = "veiledningSteg",
            Name = "Veiledning Steg",
            Description = "Et steg i en veiledningsguide",
            Icon = "icon-document",
            AllowedAsRoot = false,
        };
        ct.AddPropertyGroup("innhold", "Innhold");
        ct.AddPropertyType(Prop("tittel", "Tittel", _textStringDt, mandatory: true), "innhold");
        ct.AddPropertyType(Prop("slug", "Slug", _textStringDt, mandatory: true), "innhold");
        ct.AddPropertyType(Prop("guideSlug", "Guide-slug", _textStringDt, mandatory: true, description: "Slug til overordnet guide"), "innhold");
        ct.AddPropertyType(Prop("steg", "Steg", _numericDt, mandatory: true, description: "Stegnummer (1, 2, 3...)"), "innhold");
        ct.AddPropertyType(Prop("understeg", "Understeg", _numericDt, mandatory: true, description: "Understeg-nummer (1, 2, 3...)"), "innhold");
        ct.AddPropertyType(Prop("innhold", "Innhold", _richTextDt, description: "Hovedinnhold"), "innhold");
        ct.AddPropertyType(Prop("infoKortTittel", "Infokort-tittel", _textStringDt, description: "Tittel på informasjonskort (valgfritt)"), "innhold");
        ct.AddPropertyType(Prop("infoKortInnhold", "Infokort-innhold", _richTextDt, description: "Innhold i informasjonskort (valgfritt)"), "innhold");
        ct.AddPropertyType(Prop("accordionSeksjoner", "Accordion-seksjoner", _blockListAccordionDt, description: "Trekkspill-seksjoner (valgfritt)"), "innhold");
        ct.AddPropertyType(Prop("eksempelTittel", "Eksempel-tittel", _textStringDt, description: "Tittel på eksempelkort (valgfritt)"), "innhold");
        ct.AddPropertyType(Prop("eksempelTekst", "Eksempel-tekst", _richTextDt, description: "Tekst i eksempelkort (valgfritt)"), "innhold");
        _contentTypeService.Save(ct);
        return ct;
    }

    private IContentType CreateFAQ()
    {
        var ct = new ContentType(_shortStringHelper, -1)
        {
            Alias = "faq",
            Name = "FAQ",
            Description = "Ofte stilte spørsmål",
            Icon = "icon-help-alt",
            AllowedAsRoot = false,
        };
        ct.AddPropertyGroup("innhold", "Innhold");
        ct.AddPropertyType(Prop("sporsmal", "Spørsmål", _textStringDt, mandatory: true), "innhold");
        ct.AddPropertyType(Prop("svar", "Svar", _richTextDt), "innhold");
        ct.AddPropertyType(Prop("kategori", "Kategori", _contentPickerDt), "innhold");
        ct.AddPropertyType(Prop("rekkefolge", "Rekkefølge", _numericDt), "innhold");
        _contentTypeService.Save(ct);
        return ct;
    }

    private IContentType CreateOrdbokOppslag()
    {
        var ct = new ContentType(_shortStringHelper, -1)
        {
            Alias = "ordbokOppslag",
            Name = "Ordbok-oppslag",
            Description = "Et begrep i KI-ordboka",
            Icon = "icon-book-alt",
            AllowedAsRoot = false,
        };
        ct.AddPropertyGroup("innhold", "Innhold");
        ct.AddPropertyType(Prop("term", "Term", _textStringDt, mandatory: true), "innhold");
        ct.AddPropertyType(Prop("alternativTerm", "Alternativt term (engelsk/alias)", _textStringDt), "innhold");
        ct.AddPropertyType(Prop("definisjon", "Definisjon", _textAreaDt, mandatory: true), "innhold");
        _contentTypeService.Save(ct);
        return ct;
    }

    private IContentType CreateOmOssSeksjon()
    {
        var ct = new ContentType(_shortStringHelper, -1)
        {
            Alias = "omOssSeksjon",
            Name = "Om Oss Seksjon",
            Description = "En seksjon på Om Oss-siden",
            Icon = "icon-document",
            AllowedAsRoot = false,
        };
        ct.AddPropertyGroup("innhold", "Innhold");
        ct.AddPropertyType(Prop("tittel", "Tittel", _textStringDt, mandatory: true), "innhold");
        ct.AddPropertyType(Prop("slug", "Slug", _textStringDt, mandatory: true), "innhold");
        ct.AddPropertyType(Prop("tekst", "Tekst", _richTextDt, mandatory: true), "innhold");
        ct.AddPropertyType(Prop("bilde", "Bilde", _mediaPickerDt, mandatory: true), "innhold");
        ct.AddPropertyType(Prop("rekkefolge", "Rekkefølge", _numericDt), "innhold");
        _contentTypeService.Save(ct);
        return ct;
    }

    private IContentType CreateOmOss()
    {
        var ct = new ContentType(_shortStringHelper, -1)
        {
            Alias = "omOss",
            Name = "Om Oss",
            Description = "Om Oss-siden",
            Icon = "icon-umb-members",
            AllowedAsRoot = true,
        };

        ct.AddPropertyGroup("innhold", "Innhold");
        ct.AddPropertyType(Prop("heroTittel", "Hero-tittel", _textStringDt), "innhold");
        ct.AddPropertyType(Prop("heroUndertittel", "Hero-undertittel", _textStringDt), "innhold");
        ct.AddPropertyType(Prop("introTekst", "Intro-tekst", _richTextDt), "innhold");
        ct.AddPropertyType(Prop("misjonTekst", "Misjonstekst", _richTextDt, description: "Tekst i den blå misjonsbanneren"), "innhold");

        ct.AddPropertyGroup("seo", "SEO");
        ct.AddPropertyType(Prop("seoTittel", "SEO-tittel", _textStringDt, description: "Overstyr tittel i søkeresultater og sosiale medier"), "seo");
        ct.AddPropertyType(Prop("seoBeskrivelse", "SEO-beskrivelse", _textAreaDt, description: "Overstyr beskrivelse i søkeresultater og sosiale medier"), "seo");
        ct.AddPropertyType(Prop("seoBilde", "SEO-bilde", _mediaPickerDt, description: "Bilde som vises ved deling på sosiale medier"), "seo");

        // Allow omOssSeksjon as child
        var seksjonType = _contentTypeService.Get("omOssSeksjon");
        if (seksjonType != null)
        {
            ct.AllowedContentTypes = new[]
            {
                new ContentTypeSort(seksjonType.Key, 0, seksjonType.Alias)
            };
        }

        _contentTypeService.Save(ct);
        return ct;
    }

    private void MigrateOmOss()
    {
        var ct = _contentTypeService.Get("omOss");
        if (ct == null) return;
        if (ct.PropertyTypeExists("misjonTekst")) return;
        ct.AddPropertyType(Prop("misjonTekst", "Misjonstekst", _richTextDt, description: "Tekst i den blå misjonsbanneren"), "innhold");
        _contentTypeService.Save(ct);
    }

    private IContentType CreateSandkasse()
    {
        var ct = new ContentType(_shortStringHelper, -1)
        {
            Alias = "sandkasse",
            Name = "Sandkasse",
            Description = "Sandkasse-siden",
            Icon = "icon-science",
            AllowedAsRoot = true,
        };

        // Tab: Hero
        ct.AddPropertyGroup("hero", "Hero");
        ct.AddPropertyType(Prop("heroTittel", "Hero-tittel", _textStringDt), "hero");
        ct.AddPropertyType(Prop("heroTekst", "Hero-tekst", _richTextDt), "hero");
        ct.AddPropertyType(Prop("nedtelling", "Nedtelling", _textStringDt), "hero");

        // Tab: Hvem
        ct.AddPropertyGroup("hvem", "Hvem");
        ct.AddPropertyType(Prop("hvemTittel", "Hvem-tittel", _textStringDt), "hvem");
        ct.AddPropertyType(Prop("hvemTekst", "Hvem-tekst", _richTextDt), "hvem");
        ct.AddPropertyType(Prop("hvemBilde", "Hvem-bilde", _mediaPickerDt), "hvem");

        // Tab: Prosess
        ct.AddPropertyGroup("prosess", "Prosess");
        ct.AddPropertyType(Prop("prosessTittel", "Prosess-tittel", _textStringDt), "prosess");
        ct.AddPropertyType(Prop("prosessSteg", "Prosess-steg", _blockListSandkasseStegDt), "prosess");

        // Tab: Resultat
        ct.AddPropertyGroup("resultat", "Resultat");
        ct.AddPropertyType(Prop("resultatTittel", "Resultat-tittel", _textStringDt), "resultat");
        ct.AddPropertyType(Prop("resultatTekst", "Resultat-tekst", _richTextDt), "resultat");
        ct.AddPropertyType(Prop("resultatBilde", "Resultat-bilde", _mediaPickerDt), "resultat");

        // Tab: FAQ
        ct.AddPropertyGroup("faq", "FAQ");
        ct.AddPropertyType(Prop("faqTittel", "FAQ-tittel", _textStringDt), "faq");
        ct.AddPropertyType(Prop("faqSeksjoner", "FAQ-seksjoner", _blockListSandkasseFaqDt), "faq");

        // Tab: SEO
        ct.AddPropertyGroup("seo", "SEO");
        ct.AddPropertyType(Prop("seoTittel", "SEO-tittel", _textStringDt, description: "Overstyr tittel i søkeresultater og sosiale medier"), "seo");
        ct.AddPropertyType(Prop("seoBeskrivelse", "SEO-beskrivelse", _textAreaDt, description: "Overstyr beskrivelse i søkeresultater og sosiale medier"), "seo");
        ct.AddPropertyType(Prop("seoBilde", "SEO-bilde", _mediaPickerDt, description: "Bilde som vises ved deling på sosiale medier"), "seo");

        _contentTypeService.Save(ct);
        return ct;
    }

    private IContentType CreateForside()
    {
        var ct = new ContentType(_shortStringHelper, -1)
        {
            Alias = "forside",
            Name = "Forside",
            Description = "Forsiden av nettstedet",
            Icon = "icon-home",
            AllowedAsRoot = true,
        };

        // Tab: Hero
        ct.AddPropertyGroup("hero", "Hero");
        ct.AddPropertyType(Prop("heroOverskrift", "Overskrift", _textStringDt), "hero");
        ct.AddPropertyType(Prop("heroTekst", "Tekst", _richTextDt), "hero");
        ct.AddPropertyType(Prop("heroBilde", "Bilde", _mediaPickerDt), "hero");

        // Tab: Tre råd
        ct.AddPropertyGroup("treRaad", "Tre råd");
        ct.AddPropertyType(Prop("raadTittel", "Tittel", _textStringDt), "treRaad");
        ct.AddPropertyType(Prop("tips", "Tips", _blockListTipsDt), "treRaad");

        // Tab: Sandkassen
        ct.AddPropertyGroup("sandkassen", "Sandkassen");
        ct.AddPropertyType(Prop("sandkasseTittel", "Tittel", _textStringDt), "sandkassen");
        ct.AddPropertyType(Prop("sandkasseTekst", "Tekst", _richTextDt), "sandkassen");
        ct.AddPropertyType(Prop("sandkasseUrl", "URL", _textStringDt), "sandkassen");

        // Tab: Arrangementer — deaktivert for MVP
        // ct.AddPropertyGroup("arrangementer", "Arrangementer");
        // ct.AddPropertyType(Prop("arrangementer", "Arrangementer", _blockListEventsDt), "arrangementer");

        // Tab: Veiledning
        ct.AddPropertyGroup("veiledning", "Veiledning");
        ct.AddPropertyType(Prop("veiledningOverskrift", "Overskrift", _textStringDt), "veiledning");
        ct.AddPropertyType(Prop("veiledning1Tittel", "Veiledning 1 Tittel", _textStringDt), "veiledning");
        ct.AddPropertyType(Prop("veiledning1Beskrivelse", "Veiledning 1 Beskrivelse", _textStringDt), "veiledning");
        ct.AddPropertyType(Prop("veiledning1Url", "Veiledning 1 URL", _textStringDt), "veiledning");
        ct.AddPropertyType(Prop("veiledning2Tittel", "Veiledning 2 Tittel", _textStringDt), "veiledning");
        ct.AddPropertyType(Prop("veiledning2Beskrivelse", "Veiledning 2 Beskrivelse", _textStringDt), "veiledning");
        ct.AddPropertyType(Prop("veiledning2Url", "Veiledning 2 URL", _textStringDt), "veiledning");

        // Tab: Aktuelt
        ct.AddPropertyGroup("aktuelt", "Aktuelt");
        ct.AddPropertyType(Prop("aktueltOverskrift", "Overskrift", _textStringDt), "aktuelt");
        ct.AddPropertyType(Prop("aktueltLenkeTekst", "Lenketekst", _textStringDt), "aktuelt");
        ct.AddPropertyType(Prop("aktueltLenkeUrl", "Lenke-URL", _textStringDt), "aktuelt");

        // Tab: Arrangement
        ct.AddPropertyGroup("arrangement", "Arrangement");
        ct.AddPropertyType(Prop("arrangementOverskrift", "Overskrift", _textStringDt), "arrangement");
        ct.AddPropertyType(Prop("arrangementKommendeTekst", "Kommende tekst", _textStringDt), "arrangement");
        ct.AddPropertyType(Prop("arrangementAvholdteTekst", "Avholdte tekst", _textStringDt), "arrangement");

        // Tab: Bunn (Footer)
        ct.AddPropertyGroup("bunn", "Bunn (Footer)");
        ct.AddPropertyType(Prop("footerTittel", "Merkenavn", _textStringDt), "bunn");
        ct.AddPropertyType(Prop("footerBeskrivelse", "Beskrivelse", _textAreaDt), "bunn");
        ct.AddPropertyType(Prop("footerSosialInstagram", "Instagram", _textStringDt, description: "URL til Instagram-profil"), "bunn");
        ct.AddPropertyType(Prop("footerSosialLinkedin", "LinkedIn", _textStringDt, description: "URL til LinkedIn-profil"), "bunn");
        ct.AddPropertyType(Prop("footerSosialX", "X", _textStringDt, description: "URL til X-profil"), "bunn");
        ct.AddPropertyType(Prop("footerLenke1Tekst", "Lenke 1 tekst", _textStringDt), "bunn");
        ct.AddPropertyType(Prop("footerLenke1Url", "Lenke 1 URL", _textStringDt), "bunn");
        ct.AddPropertyType(Prop("footerLenke2Tekst", "Lenke 2 tekst", _textStringDt), "bunn");
        ct.AddPropertyType(Prop("footerLenke2Url", "Lenke 2 URL", _textStringDt), "bunn");
        ct.AddPropertyType(Prop("footerLenke3Tekst", "Lenke 3 tekst", _textStringDt), "bunn");
        ct.AddPropertyType(Prop("footerLenke3Url", "Lenke 3 URL", _textStringDt), "bunn");
        ct.AddPropertyType(Prop("footerLenke4Tekst", "Lenke 4 tekst", _textStringDt), "bunn");
        ct.AddPropertyType(Prop("footerLenke4Url", "Lenke 4 URL", _textStringDt), "bunn");
        ct.AddPropertyType(Prop("footerLenke5Tekst", "Lenke 5 tekst", _textStringDt), "bunn");
        ct.AddPropertyType(Prop("footerLenke5Url", "Lenke 5 URL", _textStringDt), "bunn");

        // Tab: Rekkefølge (Order)
        ct.AddPropertyGroup("rekkefolge", "Rekkefølge");
        ct.AddPropertyType(Prop("rekkefolgeVeiledning", "Veiledning", _numericDt, description: "Rekkefølge for Veiledning-seksjonen (1-5)"), "rekkefolge");
        ct.AddPropertyType(Prop("rekkefolgeAktuelt", "Aktuelt", _numericDt, description: "Rekkefølge for Aktuelt-seksjonen (1-5)"), "rekkefolge");
        ct.AddPropertyType(Prop("rekkefolgeTreRaad", "Tre råd", _numericDt, description: "Rekkefølge for Tre råd-seksjonen (1-5)"), "rekkefolge");
        ct.AddPropertyType(Prop("rekkefolgeSandkasse", "Sandkasse", _numericDt, description: "Rekkefølge for Sandkasse-seksjonen (1-5)"), "rekkefolge");
        ct.AddPropertyType(Prop("rekkefolgeArrangement", "Arrangement", _numericDt, description: "Rekkefølge for Arrangement-seksjonen (1-5)"), "rekkefolge");

        // Tab: SEO
        ct.AddPropertyGroup("seo", "SEO");
        ct.AddPropertyType(Prop("seoTittel", "SEO-tittel", _textStringDt, description: "Overstyr tittel i søkeresultater og sosiale medier"), "seo");
        ct.AddPropertyType(Prop("seoBeskrivelse", "SEO-beskrivelse", _textAreaDt, description: "Overstyr beskrivelse i søkeresultater og sosiale medier"), "seo");
        ct.AddPropertyType(Prop("seoBilde", "SEO-bilde", _mediaPickerDt, description: "Bilde som vises ved deling på sosiale medier"), "seo");

        _contentTypeService.Save(ct);
        return ct;
    }

    // --- Veiledning Oversikt element types ---

    private IContentType CreateVeiledningKortElement()
    {
        var ct = new ContentType(_shortStringHelper, -1)
        {
            Alias = "veiledningKort",
            Name = "Veiledning Kort",
            Description = "Et kort i veiledningsoversikten",
            Icon = "icon-thumbnail-list",
            IsElement = true,
        };
        ct.AddPropertyGroup("innhold", "Innhold");
        ct.AddPropertyType(Prop("tittel", "Tittel", _textStringDt, mandatory: true), "innhold");
        ct.AddPropertyType(Prop("beskrivelse", "Beskrivelse", _textStringDt), "innhold");
        ct.AddPropertyType(Prop("url", "URL", _textStringDt), "innhold");
        ct.AddPropertyType(Prop("ikon", "Ikon", _textStringDt, description: "Ikonnavn fra Aksel (f.eks. HandHeart, Package)"), "innhold");
        _contentTypeService.Save(ct);
        return ct;
    }

    private void MigrateVeiledningKort()
    {
        var ct = _contentTypeService.Get("veiledningKort");
        if (ct == null) return;
        if (ct.PropertyTypeExists("ikon")) return;
        ct.AddPropertyType(Prop("ikon", "Ikon", _textStringDt, description: "Ikonnavn fra Aksel (f.eks. HandHeart, Package)"), "innhold");
        _contentTypeService.Save(ct);
    }

    private IContentType CreateVerktoyKortElement()
    {
        var ct = new ContentType(_shortStringHelper, -1)
        {
            Alias = "verktoyKort",
            Name = "Verktøy Kort",
            Description = "Et verktøy-kort i veiledningsoversikten",
            Icon = "icon-wrench",
            IsElement = true,
        };
        ct.AddPropertyGroup("innhold", "Innhold");
        ct.AddPropertyType(Prop("tittel", "Tittel", _textStringDt, mandatory: true), "innhold");
        ct.AddPropertyType(Prop("beskrivelse", "Beskrivelse", _textStringDt), "innhold");
        ct.AddPropertyType(Prop("url", "URL", _textStringDt), "innhold");
        ct.AddPropertyType(Prop("bilde", "Bilde", _mediaPickerDt), "innhold");
        ct.AddPropertyType(Prop("ikon", "Ikon", _textStringDt, description: "Ikonnavn fra Aksel (f.eks. HandHeart, Package)"), "innhold");
        _contentTypeService.Save(ct);
        return ct;
    }

    private void MigrateVerktoyKort()
    {
        var ct = _contentTypeService.Get("verktoyKort");
        if (ct == null) return;
        if (ct.PropertyTypeExists("ikon")) return;
        ct.AddPropertyType(Prop("ikon", "Ikon", _textStringDt, description: "Ikonnavn fra Aksel (f.eks. HandHeart, Package)"), "innhold");
        _contentTypeService.Save(ct);
    }

    private IContentType CreateVeiledningOversikt()
    {
        var ct = new ContentType(_shortStringHelper, -1)
        {
            Alias = "veiledningOversikt",
            Name = "Veiledning Oversikt",
            Description = "Oversiktsside for veiledning",
            Icon = "icon-book-alt",
            AllowedAsRoot = true,
        };

        // Tab: Hero
        ct.AddPropertyGroup("hero", "Hero");
        ct.AddPropertyType(Prop("heroLabel", "Hero-label", _textStringDt), "hero");
        ct.AddPropertyType(Prop("heroTittel", "Hero-tittel", _textStringDt), "hero");
        ct.AddPropertyType(Prop("heroTekst", "Hero-tekst", _textStringDt), "hero");
        ct.AddPropertyType(Prop("heroBilde", "Hero-bilde", _mediaPickerDt), "hero");

        // Tab: Seksjon 1
        ct.AddPropertyGroup("seksjon1", "Seksjon 1");
        ct.AddPropertyType(Prop("seksjon1Tittel", "Seksjon 1 tittel", _textStringDt), "seksjon1");
        ct.AddPropertyType(Prop("seksjon1Kort", "Seksjon 1 kort", _blockListVeiledningKortDt), "seksjon1");

        // Tab: Seksjon 2
        ct.AddPropertyGroup("seksjon2", "Seksjon 2");
        ct.AddPropertyType(Prop("seksjon2Tittel", "Seksjon 2 tittel", _textStringDt), "seksjon2");
        ct.AddPropertyType(Prop("seksjon2Kort", "Seksjon 2 kort", _blockListVeiledningKortDt), "seksjon2");

        // Tab: Verktøy
        ct.AddPropertyGroup("verktoy", "Verktøy");
        ct.AddPropertyType(Prop("verktoyTittel", "Verktøy tittel", _textStringDt), "verktoy");
        ct.AddPropertyType(Prop("verktoyKort", "Verktøy kort", _blockListVerktoyKortDt), "verktoy");

        // Tab: SEO
        ct.AddPropertyGroup("seo", "SEO");
        ct.AddPropertyType(Prop("seoTittel", "SEO-tittel", _textStringDt, description: "Overstyr tittel i søkeresultater og sosiale medier"), "seo");
        ct.AddPropertyType(Prop("seoBeskrivelse", "SEO-beskrivelse", _textAreaDt, description: "Overstyr beskrivelse i søkeresultater og sosiale medier"), "seo");
        ct.AddPropertyType(Prop("seoBilde", "SEO-bilde", _mediaPickerDt, description: "Bilde som vises ved deling på sosiale medier"), "seo");

        _contentTypeService.Save(ct);
        return ct;
    }

    private void MigrateForside()
    {
        var ct = _contentTypeService.Get("forside");
        if (ct == null) return;
        if (ct.PropertyTypeExists("veiledningOverskrift")) return; // already migrated

        // Tab: Veiledning
        ct.AddPropertyGroup("veiledning", "Veiledning");
        ct.AddPropertyType(Prop("veiledningOverskrift", "Overskrift", _textStringDt), "veiledning");
        ct.AddPropertyType(Prop("veiledning1Tittel", "Veiledning 1 Tittel", _textStringDt), "veiledning");
        ct.AddPropertyType(Prop("veiledning1Beskrivelse", "Veiledning 1 Beskrivelse", _textStringDt), "veiledning");
        ct.AddPropertyType(Prop("veiledning1Url", "Veiledning 1 URL", _textStringDt), "veiledning");
        ct.AddPropertyType(Prop("veiledning2Tittel", "Veiledning 2 Tittel", _textStringDt), "veiledning");
        ct.AddPropertyType(Prop("veiledning2Beskrivelse", "Veiledning 2 Beskrivelse", _textStringDt), "veiledning");
        ct.AddPropertyType(Prop("veiledning2Url", "Veiledning 2 URL", _textStringDt), "veiledning");

        // Tab: Aktuelt
        ct.AddPropertyGroup("aktuelt", "Aktuelt");
        ct.AddPropertyType(Prop("aktueltOverskrift", "Overskrift", _textStringDt), "aktuelt");
        ct.AddPropertyType(Prop("aktueltLenkeTekst", "Lenketekst", _textStringDt), "aktuelt");
        ct.AddPropertyType(Prop("aktueltLenkeUrl", "Lenke-URL", _textStringDt), "aktuelt");

        // Tab: Arrangement
        ct.AddPropertyGroup("arrangement", "Arrangement");
        ct.AddPropertyType(Prop("arrangementOverskrift", "Overskrift", _textStringDt), "arrangement");
        ct.AddPropertyType(Prop("arrangementKommendeTekst", "Kommende tekst", _textStringDt), "arrangement");
        ct.AddPropertyType(Prop("arrangementAvholdteTekst", "Avholdte tekst", _textStringDt), "arrangement");

        _contentTypeService.Save(ct);

        // Migrate footer fields
        if (!ct.PropertyTypeExists("footerTittel"))
        {
            ct.AddPropertyGroup("bunn", "Bunn (Footer)");
            ct.AddPropertyType(Prop("footerTittel", "Merkenavn", _textStringDt), "bunn");
            ct.AddPropertyType(Prop("footerBeskrivelse", "Beskrivelse", _textAreaDt), "bunn");
            ct.AddPropertyType(Prop("footerSosialInstagram", "Instagram", _textStringDt, description: "URL til Instagram-profil"), "bunn");
            ct.AddPropertyType(Prop("footerSosialLinkedin", "LinkedIn", _textStringDt, description: "URL til LinkedIn-profil"), "bunn");
            ct.AddPropertyType(Prop("footerSosialX", "X", _textStringDt, description: "URL til X-profil"), "bunn");
            ct.AddPropertyType(Prop("footerLenke1Tekst", "Lenke 1 tekst", _textStringDt), "bunn");
            ct.AddPropertyType(Prop("footerLenke1Url", "Lenke 1 URL", _textStringDt), "bunn");
            ct.AddPropertyType(Prop("footerLenke2Tekst", "Lenke 2 tekst", _textStringDt), "bunn");
            ct.AddPropertyType(Prop("footerLenke2Url", "Lenke 2 URL", _textStringDt), "bunn");
            ct.AddPropertyType(Prop("footerLenke3Tekst", "Lenke 3 tekst", _textStringDt), "bunn");
            ct.AddPropertyType(Prop("footerLenke3Url", "Lenke 3 URL", _textStringDt), "bunn");
            ct.AddPropertyType(Prop("footerLenke4Tekst", "Lenke 4 tekst", _textStringDt), "bunn");
            ct.AddPropertyType(Prop("footerLenke4Url", "Lenke 4 URL", _textStringDt), "bunn");
            ct.AddPropertyType(Prop("footerLenke5Tekst", "Lenke 5 tekst", _textStringDt), "bunn");
            ct.AddPropertyType(Prop("footerLenke5Url", "Lenke 5 URL", _textStringDt), "bunn");
            _contentTypeService.Save(ct);
        }

        // Migrate reorder fields
        if (!ct.PropertyTypeExists("rekkefolgeVeiledning"))
        {
            ct.AddPropertyGroup("rekkefolge", "Rekkefølge");
            ct.AddPropertyType(Prop("rekkefolgeVeiledning", "Veiledning", _numericDt, description: "Rekkefølge for Veiledning-seksjonen (1-5)"), "rekkefolge");
            ct.AddPropertyType(Prop("rekkefolgeAktuelt", "Aktuelt", _numericDt, description: "Rekkefølge for Aktuelt-seksjonen (1-5)"), "rekkefolge");
            ct.AddPropertyType(Prop("rekkefolgeTreRaad", "Tre råd", _numericDt, description: "Rekkefølge for Tre råd-seksjonen (1-5)"), "rekkefolge");
            ct.AddPropertyType(Prop("rekkefolgeSandkasse", "Sandkasse", _numericDt, description: "Rekkefølge for Sandkasse-seksjonen (1-5)"), "rekkefolge");
            ct.AddPropertyType(Prop("rekkefolgeArrangement", "Arrangement", _numericDt, description: "Rekkefølge for Arrangement-seksjonen (1-5)"), "rekkefolge");
            _contentTypeService.Save(ct);
        }
    }
}
