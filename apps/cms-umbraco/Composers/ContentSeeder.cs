using System.Text.Json;
using Umbraco.Cms.Core;
using Umbraco.Cms.Core.Composing;
using Umbraco.Cms.Core.Models;
using Umbraco.Cms.Core.Services;
using Umbraco.Cms.Core.IO;
using Umbraco.Cms.Core.Strings;
using Umbraco.Cms.Core.PropertyEditors;
using Microsoft.AspNetCore.Hosting;

namespace KiNorge.Cms.Composers;

/// <summary>
/// Seeds demo content for development. Only runs once (checks if content exists).
/// Creates container nodes (folders) and populates each with content items.
/// Must run after ContentTypeComposer so document types exist.
/// </summary>
[ComposeAfter(typeof(ContentTypeComposer))]
public class ContentSeederComposer : ComponentComposer<ContentSeeder>
{
}

public class ContentSeeder : IAsyncComponent
{
    private readonly IContentService _contentService;
    private readonly IContentTypeService _contentTypeService;
    private readonly IMediaService _mediaService;
    private readonly IMediaTypeService _mediaTypeService;
    private readonly MediaFileManager _mediaFileManager;
    private readonly IShortStringHelper _shortStringHelper;
    private readonly IContentTypeBaseServiceProvider _contentTypeBaseServiceProvider;
    private readonly MediaUrlGeneratorCollection _mediaUrlGenerators;
    private readonly IRuntimeState _runtimeState;
    private readonly IWebHostEnvironment _webHostEnvironment;

    public ContentSeeder(
        IContentService contentService,
        IContentTypeService contentTypeService,
        IMediaService mediaService,
        IMediaTypeService mediaTypeService,
        MediaFileManager mediaFileManager,
        IShortStringHelper shortStringHelper,
        IContentTypeBaseServiceProvider contentTypeBaseServiceProvider,
        MediaUrlGeneratorCollection mediaUrlGenerators,
        IRuntimeState runtimeState,
        IWebHostEnvironment webHostEnvironment)
    {
        _contentService = contentService;
        _contentTypeService = contentTypeService;
        _mediaService = mediaService;
        _mediaTypeService = mediaTypeService;
        _mediaFileManager = mediaFileManager;
        _shortStringHelper = shortStringHelper;
        _contentTypeBaseServiceProvider = contentTypeBaseServiceProvider;
        _mediaUrlGenerators = mediaUrlGenerators;
        _runtimeState = runtimeState;
        _webHostEnvironment = webHostEnvironment;
    }

    public Task InitializeAsync(bool isRestarting, CancellationToken cancellationToken)
    {
        if (_runtimeState.Level < RuntimeLevel.Run) return Task.CompletedTask;

        // Skip if content already exists
        var existing = _contentService.GetRootContent();
        if (existing.Any()) return Task.CompletedTask;

        try
        {
            // Create container nodes (folders) at root
            var artiklerFolder = CreateFolder("artikler", "Artikler");
            var siderFolder = CreateFolder("sider", "Sider");
            var eksemplerFolder = CreateFolder("eksempler", "Eksempler");
            var veiledningerFolder = CreateFolder("veiledninger", "Veiledninger");
            var faqFolder = CreateFolder("faqSamling", "FAQ");
            var merkelapperFolder = CreateFolder("merkelapper", "Merkelapper");

            // Seed media images
            SeedMedia();

            // Create root-level content nodes
            SeedForside();
            var omOssNode = SeedOmOss();
            SeedSandkasse();
            SeedVeiledningOversikt();

            // Seed merkelapper FIRST so we can reference them from other content
            var merkelappMap = SeedMerkelapper(merkelapperFolder.Id);

            // Seed content under each folder (with merkelapp references)
            SeedArticles(artiklerFolder.Id);
            SeedPages(siderFolder.Id);
            SeedExamples(eksemplerFolder.Id);
            SeedVeiledninger(veiledningerFolder.Id);
            SeedFAQ(faqFolder.Id, merkelappMap);

            Console.WriteLine("ContentSeeder: Seeded all content under folder structure");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"ContentSeeder: {ex.Message}");
        }
        return Task.CompletedTask;
    }

    public Task TerminateAsync(bool isRestarting, CancellationToken cancellationToken) => Task.CompletedTask;

    private IContent CreateFolder(string contentTypeAlias, string name)
    {
        var ct = _contentTypeService.Get(contentTypeAlias)
            ?? throw new InvalidOperationException($"Container type '{contentTypeAlias}' not found");
        var folder = _contentService.Create(name, -1, ct.Alias);
        _contentService.Save(folder);
        _contentService.Publish(folder, new[] { "*" });
        return folder;
    }

    private IContent Create(string contentTypeAlias, string name, int parentId)
    {
        var ct = _contentTypeService.Get(contentTypeAlias)
            ?? throw new InvalidOperationException($"Content type '{contentTypeAlias}' not found");
        return _contentService.Create(name, parentId, ct.Alias);
    }

    private void SaveAndPublish(IContent content)
    {
        _contentService.Save(content);
        _contentService.Publish(content, new[] { "*" });
    }

    // ── Forside ─────────────────────────────────────────────

    private void SeedForside()
    {
        var ct = _contentTypeService.Get("forside")
            ?? throw new InvalidOperationException("Content type 'forside' not found");
        var forside = _contentService.Create("Forside", -1, ct.Alias);
        forside.SetValue("heroOverskrift", "Bruk av kunstig intelligens i Norge");
        forside.SetValue("raadTittel", "Tre råd før du går i gang med KI");
        forside.SetValue("sandkasseTittel", "Regulatorisk sandkasse for KI");
        forside.SetValue("sandkasseTekst", "<p>Den regulatoriske sandkassen gir virksomheter mulighet til å teste KI-løsninger i et kontrollert miljø med veiledning fra relevante tilsynsmyndigheter.</p>");
        forside.SetValue("sandkasseUrl", "/sandkasse");
        forside.SetValue("veiledningOverskrift", "Veiledning");
        forside.SetValue("veiledning1Tittel", "Vi skal ta i bruk KI");
        forside.SetValue("veiledning1Beskrivelse", "For deg som vil ta i bruk ferdig trent KI →");
        forside.SetValue("veiledning1Url", "/veiledning");
        forside.SetValue("veiledning2Tittel", "Vi skal lage et KI-system");
        forside.SetValue("veiledning2Beskrivelse", "For deg som ønsker å bygge en KI-løsning selv →");
        forside.SetValue("veiledning2Url", "/veiledning");
        forside.SetValue("aktueltOverskrift", "Aktuelt");
        forside.SetValue("aktueltLenkeTekst", "Finn inspirasjon og lær av andre");
        forside.SetValue("aktueltLenkeUrl", "/eksempler");
        forside.SetValue("arrangementOverskrift", "Arrangement");
        forside.SetValue("arrangementKommendeTekst", "Se kommende arrangement");
        forside.SetValue("arrangementAvholdteTekst", "Se avholdte arrangement");
        forside.SetValue("seoTittel", "KI Norge – Kunstig intelligens i norsk offentlig sektor");
        forside.SetValue("seoBeskrivelse", "KI Norge er en nasjonal satsing for ansvarlig bruk av kunstig intelligens. Veiledning, regulatorisk sandkasse og gode eksempler for offentlig sektor.");
        SaveAndPublish(forside);
    }

    // ── Om Oss ──────────────────────────────────────────────

    private IContent SeedOmOss()
    {
        var ct = _contentTypeService.Get("omOss")
            ?? throw new InvalidOperationException("Content type 'omOss' not found");
        var omOss = _contentService.Create("Om oss", -1, ct.Alias);
        omOss.SetValue("heroTittel", "KI Norge");
        omOss.SetValue("heroUndertittel", "Verdigrunnlag");
        omOss.SetValue("introTekst", "<p>KI Norge er en nasjonal satsing under Digitaliseringsdirektoratet (Digdir). Formålet er å gjøre det enklere for norske virksomheter å ta i bruk KI på en måte som er trygg, lovlig og verdiskapende, enten du driver en liten privat bedrift eller jobber i en offentlig virksomhet.</p>");
        omOss.SetValue("misjonTekst", "<p>KI Norge kobler virksomheter på tvers av offentlig sektor, næringsliv, akademia og forskning. Vi samler kunnskap gjennom kartlegginger, og gir den tilgjengelig for deg som trenger et solid grunnlag for å ta gode beslutninger.</p>");
        omOss.SetValue("seoTittel", "Om oss – KI Norge");
        omOss.SetValue("seoBeskrivelse", "Om KI Norge – en nasjonal satsing for ansvarlig bruk av kunstig intelligens.");
        SaveAndPublish(omOss);

        // Seed child sections
        var s1 = Create("omOssSeksjon", "Hvorfor KI Norge?", omOss.Id);
        s1.SetValue("tittel", "Hvorfor KI Norge?");
        s1.SetValue("slug", "hvorfor-ki-norge");
        s1.SetValue("tekst", "<p>Mange virksomheter vil ta i bruk kunstig intelligens, men vet ikke helt hvor de skal begynne, eller om de gjør det riktig. Det er der KI Norge kommer inn.</p>");
        s1.SetValue("rekkefolge", 0);
        SaveAndPublish(s1);

        var s2 = Create("omOssSeksjon", "Veiledning", omOss.Id);
        s2.SetValue("tittel", "Veiledning");
        s2.SetValue("slug", "veiledning");
        s2.SetValue("tekst", "<p>Sammen med Datatilsynet og Nasjonal kommunikasjonsmyndighet (Nkom) gir vi praktisk veiledning, særlig for deg som ikke har et eget juridisk team eller KI-eksperter i staben. Vi hjelper deg å forstå hvilke krav som gjelder, identifisere risiko og finne ut hva du faktisk trenger å forholde deg til.</p>");
        s2.SetValue("rekkefolge", 1);
        SaveAndPublish(s2);

        var s3 = Create("omOssSeksjon", "KI-sandkassen", omOss.Id);
        s3.SetValue("tittel", "KI-sandkassen");
        s3.SetValue("slug", "den-regulatoriske-ki-sandkassen");
        s3.SetValue("tekst", "<p>I KI-sandkassen kan du utvikle, teste og trene KI-løsninger i trygge og kontrollerte omgivelser, før du lanserer dem i markedet eller tar dem i bruk internt. Du får juridisk veiledning knyttet til personvern, grunnleggende rettigheter og sikkerhet, og hjelp til å oppfylle kravene i KI-forordningen og annet relevant regelverk.</p>");
        s3.SetValue("rekkefolge", 2);
        SaveAndPublish(s3);

        return omOss;
    }

    // ── Sandkasse ────────────────────────────────────────────

    private void SeedSandkasse()
    {
        var ct = _contentTypeService.Get("sandkasse")
            ?? throw new InvalidOperationException("Content type 'sandkasse' not found");
        var sandkasse = _contentService.Create("Sandkasse", -1, ct.Alias);

        // Hero
        sandkasse.SetValue("heroTittel", "KI-sandkassen");
        sandkasse.SetValue("heroTekst", "<p>KI-sandkassen er et tilbud der virksomheter kan utvikle, teste og trene KI-løsninger i trygge og kontrollerte omgivelser. Du får juridisk veiledning knyttet til personvern, grunnleggende rettigheter og sikkerhet, og hjelp til å oppfylle kravene i KI-forordningen og annet relevant regelverk.</p>");
        sandkasse.SetValue("nedtelling", "120 dager til du kan søke!");

        // Hvem
        sandkasse.SetValue("hvemTittel", "Hvem er det til for?");
        sandkasse.SetValue("hvemTekst", @"<p>Sandkassen er åpen for alle som utvikler eller tar i bruk KI-systemer og ønsker veiledning om regelverket.</p>
<ul>
<li>Offentlige virksomheter som utvikler eller anskaffer KI-løsninger</li>
<li>Private virksomheter som leverer KI-tjenester til offentlig sektor</li>
<li>Forsknings- og utdanningsinstitusjoner som jobber med KI</li>
<li>Startups og scale-ups med innovative KI-løsninger</li>
</ul>");

        // Prosess
        sandkasse.SetValue("prosessTittel", "Slik foregår prosessen");
        sandkasse.SetValue("prosessSteg", BuildSandkasseStegBlockList(
            ("1", "Søknad", "<p>Send inn en søknad som beskriver KI-systemet du ønsker å teste, hvilke data det bruker, og hvilke regulatoriske spørsmål du trenger avklaring på. Vi vurderer søknaden og gir deg svar innen fire uker.</p>"),
            ("2", "Opptak", "<p>Dersom søknaden godkjennes, blir du tatt opp i sandkassen. Du får tildelt et team med juridisk og teknisk ekspertise som følger deg gjennom hele forløpet.</p>"),
            ("3", "Planlegging", "<p>Sammen med teamet ditt lager du en plan for sandkasseforløpet. Planen beskriver hva som skal testes, hvilke risikoer som skal vurderes, og hvilke milepæler som gjelder.</p>"),
            ("4", "Sluttbevis", "<p>Etter gjennomført forløp får du et skriftlig bevis som dokumenterer funnene, vurderingene og anbefalingene fra sandkassen. Dette kan brukes som dokumentasjon overfor tilsynsmyndigheter.</p>")
        ));

        // Resultat
        sandkasse.SetValue("resultatTittel", "Hva får du ut av det?");
        sandkasse.SetValue("resultatTekst", @"<p>Deltakelse i KI-sandkassen gir deg verdifull innsikt og dokumentasjon som hjelper deg videre.</p>
<p>Du får en grundig juridisk vurdering av KI-systemet ditt opp mot gjeldende regelverk, inkludert KI-forordningen, personvernregelverket og sektorspesifikke krav. I tillegg får du praktiske anbefalinger for hvordan du kan tilpasse løsningen din for å oppfylle kravene.</p>
<p>Etter gjennomført forløp mottar du et sluttbevis som dokumenterer vurderingene og kan brukes overfor tilsynsmyndigheter og samarbeidspartnere.</p>");

        // FAQ
        sandkasse.SetValue("faqTittel", "Ofte stilte spørsmål");
        sandkasse.SetValue("faqSeksjoner", BuildSandkasseFaqBlockList(
            ("Hvem kan søke om deltakelse i sandkassen?", "<p>Sandkassen er åpen for alle leverandører og virksomheter som utvikler, tilbyr eller bruker KI-systemer og ønsker veiledning om regelverket. Både offentlige og private aktører kan søke.</p>"),
            ("Hvor lang tid tar et sandkasseforløp?", "<p>Et typisk forløp varer 6-12 måneder, avhengig av kompleksiteten til KI-systemet og omfanget av de regulatoriske spørsmålene som skal avklares.</p>"),
            ("Hva koster det å delta?", "<p>Det er gratis å delta i KI-sandkassen. Deltakerne må selv dekke egne kostnader knyttet til utvikling og tilpasning av KI-systemet.</p>"),
            ("Hvilke krav stilles til KI-systemet?", "<p>KI-systemet bør være innovativt og reise regulatoriske spørsmål som det er behov for å avklare. Det er en fordel om systemet er i en tidlig fase der det fortsatt er mulig å gjøre tilpasninger basert på veiledningen.</p>"),
            ("Hva skjer etter sandkasseforløpet?", "<p>Du får et skriftlig bevis som dokumenterer funnene, vurderingene og anbefalingene fra sandkassen. Dette kan brukes som dokumentasjon overfor tilsynsmyndigheter og samarbeidspartnere.</p>")
        ));

        // SEO
        sandkasse.SetValue("seoTittel", "KI-sandkassen – Test KI-løsninger trygt");
        sandkasse.SetValue("seoBeskrivelse", "KI-sandkassen lar virksomheter teste og utvikle KI-løsninger i et kontrollert miljø med juridisk veiledning og regulatorisk støtte.");
        SaveAndPublish(sandkasse);
    }

    private string BuildSandkasseStegBlockList(params (string nummer, string tittel, string beskrivelse)[] steps)
    {
        var contentData = new List<object>();
        var layoutItems = new List<object>();

        var elementType = _contentTypeService.Get("sandkasseSteg");
        if (elementType == null) return "{}";

        foreach (var (nummer, tittel, beskrivelse) in steps)
        {
            var guid = Guid.NewGuid();
            var udi = $"umb://element/{guid:N}";

            layoutItems.Add(new Dictionary<string, object?>
            {
                ["contentUdi"] = udi,
                ["settingsUdi"] = null
            });

            contentData.Add(new Dictionary<string, object>
            {
                ["contentTypeKey"] = elementType.Key.ToString(),
                ["udi"] = udi,
                ["nummer"] = nummer,
                ["tittel"] = tittel,
                ["beskrivelse"] = beskrivelse
            });
        }

        var blockList = new Dictionary<string, object>
        {
            ["layout"] = new Dictionary<string, object>
            {
                ["Umbraco.BlockList"] = layoutItems
            },
            ["contentData"] = contentData,
            ["settingsData"] = new List<object>()
        };

        return JsonSerializer.Serialize(blockList);
    }

    private string BuildSandkasseFaqBlockList(params (string sporsmal, string svar)[] items)
    {
        var contentData = new List<object>();
        var layoutItems = new List<object>();

        var elementType = _contentTypeService.Get("sandkasseFaq");
        if (elementType == null) return "{}";

        foreach (var (sporsmal, svar) in items)
        {
            var guid = Guid.NewGuid();
            var udi = $"umb://element/{guid:N}";

            layoutItems.Add(new Dictionary<string, object?>
            {
                ["contentUdi"] = udi,
                ["settingsUdi"] = null
            });

            contentData.Add(new Dictionary<string, object>
            {
                ["contentTypeKey"] = elementType.Key.ToString(),
                ["udi"] = udi,
                ["sporsmal"] = sporsmal,
                ["svar"] = svar
            });
        }

        var blockList = new Dictionary<string, object>
        {
            ["layout"] = new Dictionary<string, object>
            {
                ["Umbraco.BlockList"] = layoutItems
            },
            ["contentData"] = contentData,
            ["settingsData"] = new List<object>()
        };

        return JsonSerializer.Serialize(blockList);
    }

    // ── Veiledning Oversikt ─────────────────────────────────

    private void SeedVeiledningOversikt()
    {
        var ct = _contentTypeService.Get("veiledningOversikt")
            ?? throw new InvalidOperationException("Content type 'veiledningOversikt' not found");
        var vo = _contentService.Create("Veiledning Oversikt", -1, ct.Alias);

        // Hero
        vo.SetValue("heroLabel", "Veiledning");
        vo.SetValue("heroTittel", "Lag et KI-system");
        vo.SetValue("heroTekst", "Vi veileder deg gjennom regler, krav og beste praksis.");

        // Seksjon 1
        vo.SetValue("seksjon1Tittel", "Før du går i gang");
        vo.SetValue("seksjon1Kort", BuildVeiledningKortBlockList(
            ("Definer behovet og hva KI skal løse", "", "#"),
            ("Finn ut hvilket risikonivå løsningen din har", "Hvis det du skal lage har høy risiko, må du få det godkjent før du kan sette det i drift.", "#"),
            ("Forstå KI-loven og GDPR", "Det er nytt at loven stiller krav både til leverandøren og de som setter KI i drift.", "#"),
            ("Forstå krav til data og hva du må gjøre", "", "/veiledning/bruk-data-rett")
        ));

        // Seksjon 2
        vo.SetValue("seksjon2Tittel", "Utvikle KI-systemet");
        vo.SetValue("seksjon2Kort", BuildVeiledningKortBlockList(
            ("Dette er kravene du må følge for utforming", "", "#"),
            ("Valg av språkmodell – utvikle noe eget eller bruke en på markedet", "", "#"),
            ("Dokumentasjon og testing", "", "#"),
            ("Tiltak for sikkerhet og hindre misbruk", "", "#")
        ));

        // Verktøy
        vo.SetValue("verktoyTittel", "Verktøy");
        vo.SetValue("verktoyKort", BuildVerktoyKortBlockList(
            ("Bias explorer", "Utforsk hvordan dataskjevheter blir til modellskjevheter", "#"),
            ("Risikovurdering", "Modellen til Marie", "#")
        ));

        // SEO
        vo.SetValue("seoTittel", "Veiledning – Lag et KI-system");
        vo.SetValue("seoBeskrivelse", "Vi veileder deg gjennom regler, krav og beste praksis for å lage et KI-system.");
        SaveAndPublish(vo);
    }

    private string BuildVeiledningKortBlockList(params (string tittel, string beskrivelse, string url)[] cards)
    {
        var contentData = new List<object>();
        var layoutItems = new List<object>();

        var elementType = _contentTypeService.Get("veiledningKort");
        if (elementType == null) return "{}";

        foreach (var (tittel, beskrivelse, url) in cards)
        {
            var guid = Guid.NewGuid();
            var udi = $"umb://element/{guid:N}";

            layoutItems.Add(new Dictionary<string, object?>
            {
                ["contentUdi"] = udi,
                ["settingsUdi"] = null
            });

            contentData.Add(new Dictionary<string, object>
            {
                ["contentTypeKey"] = elementType.Key.ToString(),
                ["udi"] = udi,
                ["tittel"] = tittel,
                ["beskrivelse"] = beskrivelse,
                ["url"] = url
            });
        }

        var blockList = new Dictionary<string, object>
        {
            ["layout"] = new Dictionary<string, object>
            {
                ["Umbraco.BlockList"] = layoutItems
            },
            ["contentData"] = contentData,
            ["settingsData"] = new List<object>()
        };

        return JsonSerializer.Serialize(blockList);
    }

    private string BuildVerktoyKortBlockList(params (string tittel, string beskrivelse, string url)[] cards)
    {
        var contentData = new List<object>();
        var layoutItems = new List<object>();

        var elementType = _contentTypeService.Get("verktoyKort");
        if (elementType == null) return "{}";

        foreach (var (tittel, beskrivelse, url) in cards)
        {
            var guid = Guid.NewGuid();
            var udi = $"umb://element/{guid:N}";

            layoutItems.Add(new Dictionary<string, object?>
            {
                ["contentUdi"] = udi,
                ["settingsUdi"] = null
            });

            contentData.Add(new Dictionary<string, object>
            {
                ["contentTypeKey"] = elementType.Key.ToString(),
                ["udi"] = udi,
                ["tittel"] = tittel,
                ["beskrivelse"] = beskrivelse,
                ["url"] = url
            });
        }

        var blockList = new Dictionary<string, object>
        {
            ["layout"] = new Dictionary<string, object>
            {
                ["Umbraco.BlockList"] = layoutItems
            },
            ["contentData"] = contentData,
            ["settingsData"] = new List<object>()
        };

        return JsonSerializer.Serialize(blockList);
    }

    // ── Artikler ──────────────────────────────────────────────

    // ── Block List helpers ──────────────────────────────────

    private string BuildArticleBlockList(params (string elementAlias, Dictionary<string, object> properties)[] blocks)
    {
        var contentData = new List<object>();
        var layoutItems = new List<object>();

        foreach (var (alias, props) in blocks)
        {
            var elementType = _contentTypeService.Get(alias);
            if (elementType == null) continue;

            var guid = Guid.NewGuid();
            var udi = $"umb://element/{guid:N}";

            layoutItems.Add(new Dictionary<string, object?>
            {
                ["contentUdi"] = udi,
                ["settingsUdi"] = null
            });

            var data = new Dictionary<string, object>
            {
                ["contentTypeKey"] = elementType.Key.ToString(),
                ["udi"] = udi
            };
            foreach (var (key, value) in props)
            {
                data[key] = value;
            }
            contentData.Add(data);
        }

        var blockList = new Dictionary<string, object>
        {
            ["layout"] = new Dictionary<string, object>
            {
                ["Umbraco.BlockList"] = layoutItems
            },
            ["contentData"] = contentData,
            ["settingsData"] = new List<object>()
        };

        return JsonSerializer.Serialize(blockList);
    }

    private (string, Dictionary<string, object>) TextBlock(string html) =>
        ("artikkelTekst", new Dictionary<string, object> { ["innhold"] = html });

    private (string, Dictionary<string, object>) InfoBox(string title, string html) =>
        ("artikkelInfoBoks", new Dictionary<string, object> { ["tittel"] = title, ["innhold"] = html });

    private (string, Dictionary<string, object>) DarkPanel(string title, string html) =>
        ("artikkelMorkPanel", new Dictionary<string, object> { ["tittel"] = title, ["innhold"] = html });

    // ── Artikler ──────────────────────────────────────────────

    private void SeedArticles(int parentId)
    {
        // ── Short articles (simple, 1-2 text blocks) ──

        var a1 = Create("artikkel", "Ny nasjonal strategi for kunstig intelligens", parentId);
        a1.SetValue("tittel", "Ny nasjonal strategi for kunstig intelligens");
        a1.SetValue("slug", "ny-nasjonal-strategi-for-kunstig-intelligens");
        a1.SetValue("innhold", BuildArticleBlockList(
            TextBlock(@"<p>Regjeringen har lansert en oppdatert nasjonal strategi for kunstig intelligens. Strategien legger vekt på ansvarlig bruk av KI i offentlig sektor, med fokus på åpenhet, personvern og tillit.</p>
<p>Strategien følger opp EUs AI Act og setter rammer for hvordan norske virksomheter kan ta i bruk KI på en trygg og tillitvekkende måte.</p>"),
            TextBlock(@"<h2>Hovedpunkter i strategien</h2>
<ul>
<li>Styrket satsing på KI-kompetanse i offentlig forvaltning</li>
<li>Felles retningslinjer for ansvarlig KI-bruk</li>
<li>Økt deling av data mellom offentlige virksomheter</li>
<li>Etablering av nasjonalt KI-senter for offentlig sektor</li>
</ul>")
        ));
        a1.SetValue("seoTittel", "Ny nasjonal strategi for kunstig intelligens");
        a1.SetValue("seoBeskrivelse", "Regjeringens oppdaterte strategi for ansvarlig bruk av KI i offentlig sektor med fokus på åpenhet og tillit.");
        SaveAndPublish(a1);

        var a2 = Create("artikkel", "Kommuner tar i bruk KI for bedre innbyggertjenester", parentId);
        a2.SetValue("tittel", "Kommuner tar i bruk KI for bedre innbyggertjenester");
        a2.SetValue("slug", "kommuner-tar-i-bruk-ki-for-bedre-innbyggertjenester");
        a2.SetValue("innhold", BuildArticleBlockList(
            TextBlock(@"<p>Flere norske kommuner har begynt å eksperimentere med kunstig intelligens for å forbedre tjenestene til innbyggerne. Fra automatisert saksbehandling til chatboter for innbyggerdialog — mulighetene er mange.</p>
<p>Stavanger kommune bruker maskinlæring for å predikere vedlikeholdsbehov på kommunale bygg, mens Trondheim har utviklet en KI-basert chatbot som hjelper innbyggere med å finne riktig tjeneste. Bergen kommune tester automatisk klassifisering av innkommende henvendelser, noe som har redusert svartiden med 40 prosent.</p>")
        ));
        a2.SetValue("seoTittel", "Kommuner tar i bruk KI for bedre innbyggertjenester");
        a2.SetValue("seoBeskrivelse", "Norske kommuner eksperimenterer med KI for automatisert saksbehandling, chatboter og prediktivt vedlikehold.");
        SaveAndPublish(a2);

        var a3 = Create("artikkel", "EUs AI Act og konsekvenser for norsk offentlig sektor", parentId);
        a3.SetValue("tittel", "EUs AI Act og konsekvenser for norsk offentlig sektor");
        a3.SetValue("slug", "eus-ai-act-og-konsekvenser-for-norsk-offentlig-sektor");
        a3.SetValue("innhold", BuildArticleBlockList(
            TextBlock(@"<p>EU har vedtatt verdens første helhetlige regulering av kunstig intelligens. AI Act klassifiserer KI-systemer etter risikonivå og stiller krav til åpenhet, sikkerhet og menneskerettigheter.</p>
<p>Gjennom EØS-avtalen vil AI Act også gjelde i Norge. Offentlige virksomheter som bruker KI-systemer til saksbehandling, velferdstjenester eller overvåkning må forberede seg på nye krav til dokumentasjon og risikovurdering.</p>
<p>KI Norge tilbyr veiledning for virksomheter som trenger hjelp med å forstå og etterleve de nye reglene.</p>")
        ));
        a3.SetValue("seoTittel", "EUs AI Act og konsekvenser for norsk offentlig sektor");
        a3.SetValue("seoBeskrivelse", "Hvordan EUs AI Act påvirker norske offentlige virksomheter gjennom EØS-avtalen.");
        SaveAndPublish(a3);

        var a4 = Create("artikkel", "Åpenhet og tillit i KI-prosjekter", parentId);
        a4.SetValue("tittel", "Åpenhet og tillit i KI-prosjekter");
        a4.SetValue("slug", "apenhet-og-tillit-i-ki-prosjekter");
        a4.SetValue("innhold", BuildArticleBlockList(
            TextBlock(@"<p>For at kunstig intelligens skal lykkes i offentlig sektor, er det avgjørende at innbyggerne har tillit til løsningene. Åpenhet om hvordan KI-systemer fungerer og hvilke data de bruker, er en forutsetning.</p>")
        ));
        a4.SetValue("seoTittel", "Åpenhet og tillit i KI-prosjekter");
        a4.SetValue("seoBeskrivelse", "Hvorfor åpenhet og tillit er avgjørende for vellykkede KI-prosjekter i offentlig sektor.");
        SaveAndPublish(a4);

        // ── Medium articles (text + info box) ──

        var a5 = Create("artikkel", "EU AI Act: Hva betyr det for norsk offentlig sektor?", parentId);
        a5.SetValue("tittel", "EU AI Act: Hva betyr det for norsk offentlig sektor?");
        a5.SetValue("slug", "eu-ai-act-hva-betyr-det-for-norsk-offentlig-sektor");
        a5.SetValue("innhold", BuildArticleBlockList(
            TextBlock(@"<p>EUs forordning om kunstig intelligens (AI Act) trådte i kraft i 2024 og innføres gradvis frem mot 2026. Gjennom EØS-avtalen vil regelverket også gjelde i Norge. Hva betyr dette i praksis for offentlige virksomheter?</p>
<h2>Risikobasert tilnærming</h2>
<p>AI Act klassifiserer KI-systemer i fire risikonivåer: uakseptabel risiko, høy risiko, begrenset risiko og minimal risiko. Systemer brukt i offentlig saksbehandling — for eksempel velferdstjenester, grensekontroll og strafferettspleie — faller typisk i kategorien høy risiko.</p>"),
            InfoBox("Krav til høyrisiko-systemer", @"<ul>
<li>Risikovurdering og kvalitetsstyring</li>
<li>Dokumentasjon av treningsdata og algoritmisk logikk</li>
<li>Menneskelig tilsyn og mulighet for overstyring</li>
<li>Logging og sporbarhet av beslutninger</li>
</ul>"),
            TextBlock(@"<p>Norske virksomheter bør begynne kartleggingen av egne KI-systemer allerede nå, slik at de er klare når regelverket trer i kraft i EØS.</p>")
        ));
        a5.SetValue("seoTittel", "EU AI Act: Hva betyr det for norsk offentlig sektor?");
        a5.SetValue("seoBeskrivelse", "En praktisk gjennomgang av EUs AI Act og hva den betyr for norske offentlige virksomheter.");
        SaveAndPublish(a5);

        var a6 = Create("artikkel", "Slik bruker Nav kunstig intelligens til saksbehandling", parentId);
        a6.SetValue("tittel", "Slik bruker Nav kunstig intelligens til saksbehandling");
        a6.SetValue("slug", "slik-bruker-nav-kunstig-intelligens-til-saksbehandling");
        a6.SetValue("innhold", BuildArticleBlockList(
            TextBlock(@"<p>Nav er blant de offentlige virksomhetene i Norge som har kommet lengst med å ta i bruk kunstig intelligens. Fra automatisert dokumenthåndtering til prediktive modeller for oppfølging — KI er i ferd med å endre hvordan Norges største velferdsetat jobber.</p>
<h2>Automatisk dokumentklassifisering</h2>
<p>Nav mottar millioner av dokumenter hvert år. En KI-modell klassifiserer innkommende dokumenter automatisk og ruter dem til riktig saksbehandler, noe som har kuttet behandlingstiden betydelig.</p>
<h2>Prediktiv oppfølging</h2>
<p>Ved hjelp av maskinlæring identifiserer Nav brukere som kan ha nytte av tidlig oppfølging, slik at rådgivere kan prioritere der behovet er størst.</p>"),
            InfoBox("Navs erfaringer", @"<p>Nav understreker viktigheten av menneskelig kontroll, transparens overfor brukerne, og løpende evaluering av modellenes treffsikkerhet og rettferdighet. Alle automatiserte beslutninger kan overstyres av en saksbehandler.</p>")
        ));
        a6.SetValue("seoTittel", "Slik bruker Nav kunstig intelligens til saksbehandling");
        a6.SetValue("seoBeskrivelse", "Hvordan Nav bruker KI til dokumentklassifisering, prediktiv oppfølging og effektivisering av saksbehandling.");
        SaveAndPublish(a6);

        var a7 = Create("artikkel", "5 ting du må vite før du anskaffer KI-løsninger", parentId);
        a7.SetValue("tittel", "5 ting du må vite før du anskaffer KI-løsninger");
        a7.SetValue("slug", "5-ting-du-ma-vite-for-du-anskaffer-ki-losninger");
        a7.SetValue("innhold", BuildArticleBlockList(
            TextBlock(@"<p>Anskaffelse av KI-løsninger i offentlig sektor krever en annen tilnærming enn tradisjonelle IT-innkjøp. Her er fem viktige ting å tenke på.</p>
<h2>1. Definer problemet, ikke løsningen</h2>
<p>Start med behovet. Hvilken prosess skal forbedres? Hvilke gevinster forventer dere? Unngå å bestille «KI» uten et tydelig bruksområde.</p>
<h2>2. Datakvalitet er avgjørende</h2>
<p>En KI-modell er bare så god som dataene den trenes på. Kartlegg tilgjengelige data og kvaliteten på disse før dere går ut i markedet.</p>
<h2>3. Still krav til åpenhet</h2>
<p>Krev at leverandøren kan forklare hvordan modellen tar beslutninger, og at dere får innsyn i treningsdata og modellarkitektur.</p>"),
            InfoBox("Husk livssyklus og etikk", @"<p><strong>4. Tenk livssyklus, ikke bare lansering.</strong> KI-systemer trenger løpende overvåking, oppdatering av modeller og nye treningsdata. Budsjetter for drift, ikke bare utvikling.</p>
<p><strong>5. Vurder personvern og etikk tidlig.</strong> Gjennomfør DPIA tidlig i prosessen, og involver personvernombud og fageksperter fra starten.</p>")
        ));
        a7.SetValue("seoTittel", "5 ting du må vite før du anskaffer KI-løsninger");
        a7.SetValue("seoBeskrivelse", "Fem viktige råd for offentlige virksomheter som skal anskaffe KI-løsninger.");
        SaveAndPublish(a7);

        // ── Long/rich articles (text + info box + dark panel) ──

        var a8 = Create("artikkel", "Datatilsynets risikovurdering for KI — en gjennomgang", parentId);
        a8.SetValue("tittel", "Datatilsynets risikovurdering for KI — en gjennomgang");
        a8.SetValue("slug", "datatilsynets-risikovurdering-for-ki");
        a8.SetValue("innhold", BuildArticleBlockList(
            TextBlock(@"<p>Datatilsynet har publisert en veileder for risikovurdering av KI-systemer som behandler personopplysninger. Vi oppsummerer de viktigste punktene og hva det betyr for din virksomhet.</p>
<h2>Hvem gjelder dette?</h2>
<p>Alle virksomheter som bruker KI til å behandle personopplysninger — enten det er ansiktsgjenkjenning, profilering eller automatisert saksbehandling — må gjennomføre en risikovurdering.</p>"),
            InfoBox("Sentrale vurderingspunkter", @"<ul>
<li>Nødvendighet og proporsjonalitet: Er KI riktig verktøy?</li>
<li>Dataminimering: Bruker systemet kun nødvendige data?</li>
<li>Rettferdighet: Er det risiko for diskriminering eller skjevhet?</li>
<li>Transparens: Kan de registrerte forstå hvordan beslutninger tas?</li>
<li>Sikkerhet: Er data og modeller tilstrekkelig beskyttet?</li>
</ul>"),
            DarkPanel("Når skal risikovurderingen gjøres?", @"<p>Datatilsynet anbefaler at risikovurderingen gjøres <strong>før</strong> systemet settes i produksjon, og at den oppdateres ved vesentlige endringer i modell, data eller bruksområde. Virksomheter som allerede har KI i drift bør gjennomføre en vurdering så snart som mulig.</p>")
        ));
        a8.SetValue("seoTittel", "Datatilsynets risikovurdering for KI — en gjennomgang");
        a8.SetValue("seoBeskrivelse", "Oppsummering av Datatilsynets veileder for risikovurdering av KI-systemer som behandler personopplysninger.");
        SaveAndPublish(a8);

        var a9 = Create("artikkel", "Generativ KI i kommunene: erfaringer fra pilotprosjekter", parentId);
        a9.SetValue("tittel", "Generativ KI i kommunene: erfaringer fra pilotprosjekter");
        a9.SetValue("slug", "generativ-ki-i-kommunene-erfaringer-fra-pilotprosjekter");
        a9.SetValue("innhold", BuildArticleBlockList(
            TextBlock(@"<p>Flere norske kommuner tester nå generativ KI — store språkmodeller som kan skrive tekst, oppsummere dokumenter og svare på spørsmål. Hva har de lært så langt?</p>
<h2>Bruksområder som fungerer</h2>
<p>Kommunene rapporterer best resultater for intern bruk: utkast til brev og vedtak, oppsummering av lange saksdokumenter, og oversettelse til klart språk. Her sparer saksbehandlere mye tid.</p>"),
            DarkPanel("Utfordringer med utadrettet bruk", @"<p>Utadrettet bruk — som chatboter mot innbyggere — krever mer forsiktighet. Feilaktige svar (hallusinasjoner) kan få alvorlige konsekvenser når det gjelder rettigheter og tjenester. Kommunene anbefaler å starte internt før man vurderer innbyggerrettede løsninger.</p>"),
            InfoBox("Anbefalinger fra pilotene", @"<ul>
<li>Start med intern bruk der feiltoleransen er høyere</li>
<li>Etabler tydelige retningslinjer for hva som kan og ikke kan deles med KI</li>
<li>Sørg for at sensitive personopplysninger ikke sendes til skybaserte tjenester</li>
<li>Mål effekten: Spar dere faktisk tid, eller bruker folk like lang tid på å kvalitetssjekke?</li>
</ul>")
        ));
        a9.SetValue("seoTittel", "Generativ KI i kommunene: erfaringer fra pilotprosjekter");
        a9.SetValue("seoBeskrivelse", "Erfaringer og anbefalinger fra norske kommuner som tester generativ KI i offentlig forvaltning.");
        SaveAndPublish(a9);

        var a10 = Create("artikkel", "Åpenhet og innsyn: Krav til forklarbarhet i KI-systemer", parentId);
        a10.SetValue("tittel", "Åpenhet og innsyn: Krav til forklarbarhet i KI-systemer");
        a10.SetValue("slug", "apenhet-og-innsyn-krav-til-forklarbarhet-i-ki-systemer");
        a10.SetValue("innhold", BuildArticleBlockList(
            TextBlock(@"<p>Når offentlige virksomheter bruker KI til å fatte beslutninger som påvirker innbyggere, stiller både forvaltningsloven og GDPR krav til forklarbarhet. Men hva betyr egentlig forklarbarhet i praksis?</p>
<h2>Juridiske krav</h2>
<p>Forvaltningsloven krever at vedtak begrunnes. GDPR gir den registrerte rett til informasjon om automatiserte beslutninger. AI Act stiller ytterligere krav til dokumentasjon og transparens for høyrisiko-systemer.</p>"),
            InfoBox("Tekniske tilnærminger", @"<p>Forklarbarhet kan implementeres på ulike nivåer: fra enkle beslutningsregler og featureviktighet til mer avanserte teknikker som SHAP-verdier og kontrafaktiske forklaringer.</p>"),
            DarkPanel("Praktiske råd for forklarbarhet", @"<ul>
<li>Tilpass forklaringen til mottakeren — innbygger, saksbehandler og revisor trenger ulik detaljeringsgrad</li>
<li>Dokumenter modellens virkemåte ved utvikling, ikke i etterkant</li>
<li>Test forklaringene med reelle brukere — gir de faktisk mening?</li>
</ul>")
        ));
        a10.SetValue("seoTittel", "Åpenhet og innsyn: Krav til forklarbarhet i KI-systemer");
        a10.SetValue("seoBeskrivelse", "Juridiske og tekniske krav til forklarbarhet når offentlige virksomheter bruker KI til beslutninger.");
        SaveAndPublish(a10);

        // ── Full showcase article: KI-regnekraft ──

        var aFull = Create("artikkel", "KI-regnekraft i Norge: Status, utvikling og behov fremover", parentId);
        aFull.SetValue("tittel", "KI-regnekraft i Norge: Status, utvikling og behov fremover");
        aFull.SetValue("slug", "ki-regnekraft-i-norge");
        aFull.SetValue("innhold", BuildArticleBlockList(
            TextBlock(@"<p>Regnekraft er en grunnleggende forutsetning for utvikling, tilpasning og bruk av moderne kunstig intelligens. Etter hvert som avanserte KI-modeller blir større, mer komplekse og mer datakrevende, øker også behovet for nasjonal kapasitet til å trene, kjøre og videreutvikle dem.</p>"),
            TextBlock(@"<h2>Hva menes med KI-infrastruktur?</h2>
<p>KI-infrastruktur omfatter både teknologiske og organisatoriske ressurser som gjør det mulig å utvikle og anvende kunstig intelligens på en trygg og effektiv måte. En sentral komponent er tungregning (High Performance Computing, HPC), hvor CPU- og GPU-ressurser brukes til tungregneoppgaver.</p>"),
            InfoBox("Komponenter i KI-infrastruktur", @"<ul>
<li>Dataressurser, inkludert tilgjengelige datasett og ordnede prosesser for datadeling.</li>
<li>Programvare og verktøy, som rammeverk og plattformer for modelltrening og drift.</li>
<li>Organisatoriske strukturer, som sikrer kompetanseutvikling, forvaltning og sikker drift.</li>
<li>Regulatoriske mekanismer, inkludert tilsyn, sandkasser og ansvarlig bruk av KI.</li>
</ul>
<p>Samlet skal infrastrukturen støtte forskning, innovasjon og bruk av KI i Norge.</p>"),
            DarkPanel("Status for KI-infrastruktur i Norge", @"<p>I statsbudsjettet for 2026 har regjeringen bevilget 380 millioner kroner over to år til første fase av tiltaket for å styrke nasjonal infrastruktur for tungregning. Dette er en del av den økte satsingen regjeringen har gjort de siste årene for å styrke nasjonal KI-infrastruktur gjennom investeringer i superdatamaskiner, språkmodeller og støtteordninger for forskning og utvikling.</p>
<p>Sigma2 åpnet i 2025 en ny nasjonal KI-fabrikk som huser Norges kraftigste superdatamaskin, <strong>Olivia</strong>. Maskinen inngår i det europeiske LUMI AI Factory-nettverket og er tilgjengelig for forskningsmiljøer, offentlig sektor og deler av næringslivet.</p>"),
            TextBlock(@"<h2>Nasjonale språkmodeller og datagrunnlag</h2>
<p>Nasjonalbiblioteket har fått et utvidet mandat til å klargjøre norske og samiske data for KI-trening. Dette inkluderer blant annet en nasjonal lisensordning for bruk av avisinnhold, inngått i samarbeid med Kopinor. Målet er å sikre tilgang til kvalitetsdata som gjenspeiler norske forhold.</p>
<h2>Behovsvurderinger og kapasitetsutfordringer</h2>
<p>Utredningene fra Forskningsrådet peker på at dagens kapasitet ikke er tilstrekkelig for behovene i forskning, forvaltning og næringsliv. Arbeidet med en konseptvalgutredning i 2025 anslo at behovet for GPU-kapasitet vil øke med 40–50 % årlig frem mot 2030.</p>"),
            TextBlock(@"<h2>Internasjonalt samarbeid: EuroHPC og nordisk kapasitet</h2>
<p>Som deltaker i EuroHPC får Norge tilgang til europeisk toppkapasitet, deriblant LUMI-superdatamaskinen i Finland. Deltakelsen gir også mulighet for å påvirke europeiske investeringer og delta i forsknings- og innovasjonsprosjekter.</p>
<p>Flere europeiske land, inkludert nordiske naboer, investerer tungt i KI-infrastruktur. Dette bidrar til økt samlet kapasitet, men illustrerer også viktigheten av at Norge selv bygger og opprettholder nasjonalt kontrollert regnekraft.</p>
<h2>Hvorfor nasjonal kapasitet er viktig</h2>
<p>Uten tilstrekkelig nasjonal kapasitet blir Norge i større grad avhengig av globale skyleverandører, hvor reguleringsmuligheter, tilgangskontroll og databehandling foregår utenfor landets jurisdiksjon.</p>")
        ));
        aFull.SetValue("seoTittel", "KI-regnekraft i Norge: Status, utvikling og behov fremover");
        aFull.SetValue("seoBeskrivelse", "En oversikt over norsk KI-infrastruktur, regnekraft og kapasitetsbehov frem mot 2030.");
        SaveAndPublish(aFull);
    }

    // ── Sider ──────────────────────────────────────────────────

    private void SeedPages(int parentId)
    {
        var kontakt = Create("side", "Kontakt", parentId);
        kontakt.SetValue("tittel", "Kontakt oss");
        kontakt.SetValue("slug", "kontakt");
        kontakt.SetValue("innhold", @"<p>Har du spørsmål om kunstig intelligens i offentlig sektor?
Ta gjerne kontakt med oss.</p>
<h3>E-post</h3>
<p>post@ki.norge.no</p>
<h3>Besøksadresse</h3>
<p>Digitaliseringsdirektoratet<br>Brattørkaia 15B<br>7010 Trondheim</p>");
        SaveAndPublish(kontakt);

        var sandkasse = Create("side", "Sandkasse", parentId);
        sandkasse.SetValue("tittel", "Regulatorisk sandkasse for KI");
        sandkasse.SetValue("slug", "sandkasse");
        sandkasse.SetValue("innhold", @"<p>Den regulatoriske sandkassen gir virksomheter mulighet til å teste
KI-løsninger i et kontrollert miljø med veiledning fra relevante tilsynsmyndigheter.</p>
<h2>Hva er en regulatorisk sandkasse?</h2>
<p>En regulatorisk sandkasse er et rammeverk der virksomheter kan teste innovative
løsninger under tilsyn, uten å bryte med gjeldende regelverk. Dette gir mulighet
for å utvikle og teste nye KI-tjenester på en trygg måte.</p>
<h2>Hvem kan søke?</h2>
<p>Alle offentlige virksomheter som ønsker å utvikle KI-løsninger kan søke om
deltakelse i sandkassen.</p>");
        sandkasse.SetValue("seoBeskrivelse", "Regulatorisk sandkasse for utprøving av KI-løsninger i offentlig sektor.");
        SaveAndPublish(sandkasse);
    }

    // ── Eksempler ──────────────────────────────────────────────

    private void SeedExamples(int parentId)
    {
        var e1 = Create("eksempel", "KI-chatbot for innbyggerdialog", parentId);
        e1.SetValue("tittel", "KI-chatbot for innbyggerdialog");
        e1.SetValue("slug", "ki-chatbot-for-innbyggerdialog");
        e1.SetValue("organisasjon", "Trondheim kommune");
        e1.SetValue("beskrivelse", @"<p>Trondheim kommune har utviklet en KI-basert chatbot som hjelper
innbyggere med å finne riktig kommunal tjeneste. Chatboten forstår naturlig språk
og kan svare på vanlige spørsmål om åpningstider, søknadsprosesser og tjenestetilbud.</p>
<p>Løsningen er bygget på en stor språkmodell som er finjustert på kommunens
egne data, med strenge personvernregler og full sporbarhet.</p>");
        e1.SetValue("verktoy", "[\"Azure OpenAI\", \"LangChain\", \"Pinecone\"]");
        e1.SetValue("resultater", "40% reduksjon i henvendelser til servicekontoret. 85% av innbyggerne oppgir at de fikk svar på spørsmålet sitt.");
        e1.SetValue("status", "i_drift");
        e1.SetValue("merkelapper", "[\"chatbot\", \"naturlig-sprak\", \"kommune\"]");
        SaveAndPublish(e1);

        var e2 = Create("eksempel", "Prediktivt vedlikehold av kommunale bygg", parentId);
        e2.SetValue("tittel", "Prediktivt vedlikehold av kommunale bygg");
        e2.SetValue("slug", "prediktivt-vedlikehold-kommunale-bygg");
        e2.SetValue("organisasjon", "Stavanger kommune");
        e2.SetValue("beskrivelse", @"<p>Stavanger kommune bruker maskinlæring for å forutsi når kommunale bygg
trenger vedlikehold. Systemet analyserer sensordata fra bygningene — temperatur,
fuktighet, energiforbruk — og varsler før problemer oppstår.</p>
<p>Prosjektet har spart kommunen for betydelige kostnader ved å unngå
akutte reparasjoner og forlenge levetiden på tekniske installasjoner.</p>");
        e2.SetValue("verktoy", "[\"Python\", \"scikit-learn\", \"Azure IoT Hub\"]");
        e2.SetValue("resultater", "25% reduksjon i vedlikeholdskostnader. 60% færre akutte reparasjoner.");
        e2.SetValue("status", "pilot");
        e2.SetValue("merkelapper", "[\"maskinlaering\", \"automatisering\", \"kommune\"]");
        SaveAndPublish(e2);

        var e3 = Create("eksempel", "Automatisk klassifisering av henvendelser", parentId);
        e3.SetValue("tittel", "Automatisk klassifisering av henvendelser");
        e3.SetValue("slug", "automatisk-klassifisering-av-henvendelser");
        e3.SetValue("organisasjon", "Bergen kommune");
        e3.SetValue("beskrivelse", @"<p>Bergen kommune har tatt i bruk maskinlæring for automatisk klassifisering
av innkommende henvendelser fra innbyggere. Systemet sorterer e-post, skjemaer
og meldinger til riktig avdeling basert på innholdet.</p>
<p>Dette har redusert behandlingstiden betydelig og sikrer at henvendelser
raskt kommer til rett saksbehandler.</p>");
        e3.SetValue("verktoy", "[\"Python\", \"spaCy\", \"Azure ML\"]");
        e3.SetValue("resultater", "40% reduksjon i svartid. 92% korrekt klassifisering.");
        e3.SetValue("status", "i_drift");
        e3.SetValue("merkelapper", "[\"maskinlaering\", \"automatisering\", \"kommune\"]");
        SaveAndPublish(e3);

        var e4 = Create("eksempel", "KI-assistert oversettelse av offentlige dokumenter", parentId);
        e4.SetValue("tittel", "KI-assistert oversettelse av offentlige dokumenter");
        e4.SetValue("slug", "ki-assistert-oversettelse");
        e4.SetValue("organisasjon", "Digitaliseringsdirektoratet");
        e4.SetValue("beskrivelse", @"<p>Digitaliseringsdirektoratet tester KI-basert oversettelse for å gjøre
offentlig informasjon tilgjengelig på flere språk. Løsningen kombinerer
maskinoversettelse med menneskelig kvalitetskontroll.</p>
<p>Målet er at viktig offentlig informasjon skal være tilgjengelig på
norsk, samisk, engelsk og de mest utbredte innvandrerspråkene.</p>");
        e4.SetValue("verktoy", "[\"Azure Translator\", \"GPT-4\", \"Custom glossary\"]");
        e4.SetValue("resultater", "70% raskere oversettelsesprosess. Tilgjengelig på 8 språk.");
        e4.SetValue("status", "i_utvikling");
        e4.SetValue("merkelapper", "[\"naturlig-sprak\", \"automatisering\"]");
        SaveAndPublish(e4);

        // Full-featured example using all CMS fields
        var eFull = Create("eksempel", "Kunnskapsassistenten", parentId);
        eFull.SetValue("tittel", "Kunnskapsassistenten");
        eFull.SetValue("slug", "kunnskapsassistenten");
        eFull.SetValue("organisasjon", "Digitaliseringsdirektoratet");
        eFull.SetValue("beskrivelse", @"<p>Kunnskapsassistenten skal styrke – ikke erstatte – faglige vurderinger i staten. Piloten viser at den har størst verdi i starten av en kunnskapsprosess, og at vi må øke presisjonen, kunnskapsforberedelsen og kontrolltiltakene videre når oppgavene krever flere steg.</p>

<h2>Utfordringen vi skulle løse</h2>
<p>Målet har vært å undersøke hvordan KI kan støtte raske utredningsprosesser – på en trygg, åpen og faglig forsvarlig måte.</p>
<p>Kunnskapsproduksjon i staten er krevende og tidkrevende. Informasjon er fragmentert, spredt på tvers av mange kilder og i stadig endring. I tillegg utvikler vi ikke kunnskapsgrunnlaget godt nok, og det øker risikoen for feilaktige beslutninger.</p>

<h2>Løsning</h2>
<p>Kunnskapsassistenten er et spesialisert KI-verktøy for kunnskapsarbeid i offentlig sektor. Den hjelper brukerne med å finne, sammenstille og vurdere informasjon fra store mengder kilder, og har innebygde mekanismer for kontroll og etterprøvelighet.</p>
<p>Kunnskapsassistenten skal støtte utforskende analyse, styrke menneskelig vurdering og faglig forankring slik at utredningsarbeidet og verifisering av informasjon blir bedre.</p>

<h2>Resultat</h2>
<p>Kunnskapsassistenten:</p>
<ul>
<li>Gir økt kunnskapstilgjengelighet for alle ansatte</li>
<li>Reduserer tid brukt på informasjonsinnhenting og databehandling</li>
<li>Økt kvalitet ved å presentere flere relevante datakilder</li>
<li>Redusert behov for manuell koordinering på tvers av virksomheter</li>
<li>Teknisk system som demonstrerer en ansvarlig, etterprøvelig og transparent bruk av KI</li>
</ul>");
        eFull.SetValue("verktoy", "[\"Azure OpenAI\", \"RAG\", \"Kudos-databasen\", \"LangChain\"]");
        eFull.SetValue("resultater", @"<p>Den største utfordringen er ikke teknologisk, men institusjonell: KI må oppleves som trygg, etterprøvbar og tillitsvekkende.</p>
<p>Piloten viste at kunnskapsassistenten gir størst verdi i tidlige faser av arbeidet – når brukeren skal orientere seg, oppsummere og finne relevante dokumenter.</p>");
        eFull.SetValue("status", "pilot");
        eFull.SetValue("merkelapper", "[\"naturlig-sprak\", \"automatisering\", \"etikk\"]");
        eFull.SetValue("seoTittel", "Kunnskapsassistenten – KI for kunnskapsarbeid i staten");
        eFull.SetValue("seoBeskrivelse", "Kunnskapsassistenten er et KI-verktøy som støtter faglige vurderinger og utredningsprosesser i offentlig sektor.");
        SaveAndPublish(eFull);
    }

    // ── Veiledninger ───────────────────────────────────────────

    private void SeedVeiledninger(int parentId)
    {
        // Create guide
        var guide = Create("veiledningGuide", "Bruk data rett når du lager KI", parentId);
        guide.SetValue("tittel", "Bruk data rett når du lager KI");
        guide.SetValue("slug", "bruk-data-rett");
        guide.SetValue("introTekst", "<p>God dataforvaltning er avgjørende for at KI-systemer skal fungere ordentlig og bidra til at du når målet. KI-loven har krav om hvordan vi skal forvalte data både når vi bruker KI og utvikler KI-systemer.</p>");
        guide.SetValue("seoTittel", "Bruk data rett når du lager KI – Veiledning");
        guide.SetValue("seoBeskrivelse", "Lær hvordan du bruker data riktig når du utvikler KI-systemer. Steg-for-steg veiledning.");
        SaveAndPublish(guide);

        // Step 1.1
        var s = Create("veiledningSteg", "Forstå informasjonskrav", parentId);
        s.SetValue("tittel", "Finn ut hvilken informasjon du trenger");
        s.SetValue("slug", "forsta-informasjonskrav");
        s.SetValue("guideSlug", "bruk-data-rett");
        s.SetValue("steg", 1);
        s.SetValue("understeg", 1);
        s.SetValue("innhold", "<p>Når vi lager et KI-system, har vi et mål for hva vi skal oppnå med det. Å forstå informasjonskravet til KI-systemet handler om å finne ut hvilken informasjon det trenger for å nå målet.</p><p>Ta utgangspunkt i problemet KI-systemet skal løse eller behovet det skal dekke. Hvilke data trenger du for å at det du lager når målet?</p>");
        s.SetValue("eksempelTittel", "Eksempel: Den smarte insulinpumpen");
        s.SetValue("eksempelTekst", "<p>Først må vi analysere løsningen vi vil lage og målet vi vil nå – i dette tilfellet å bestemme hvor mye insulin en diabetiker trenger til enhver tid. Da må vi samle inn data som blodsukkernivå, puls og oksygennivå i blodet.</p>");
        SaveAndPublish(s);

        // Step 1.2
        s = Create("veiledningSteg", "Sensitive personopplysninger", parentId);
        s.SetValue("tittel", "Forstå behandling av spesielle kategorier av personopplysninger");
        s.SetValue("slug", "sensitive-personopplysninger");
        s.SetValue("guideSlug", "bruk-data-rett");
        s.SetValue("steg", 1);
        s.SetValue("understeg", 2);
        s.SetValue("innhold", "<p>Når vi utvikler KI-systemer må vi følge regelverket om vern av personopplysninger. Noen kategorier av opplysninger kan innebære en særlig risiko for enkeltpersoners rettigheter og friheter. Det er regler for når vi kan bruke slik data i KI-systemer?</p>");
        s.SetValue("infoKortTittel", "Slik kan du verne om personopplysninger");
        s.SetValue("infoKortInnhold", "<p>Vurder hvilke tiltak som er relevante for ditt prosjekt.</p>");
        SaveAndPublish(s);

        // Step 2.1
        s = Create("veiledningSteg", "Finn datakilder", parentId);
        s.SetValue("tittel", "Finn datakilder");
        s.SetValue("slug", "finn-datakilder");
        s.SetValue("guideSlug", "bruk-data-rett");
        s.SetValue("steg", 2);
        s.SetValue("understeg", 1);
        s.SetValue("innhold", "<ol><li>Finn ut hvilke kilder du kan hente data fra.</li><li>Finn ut hvilke metoder du skal bruke til å hente data</li></ol><p>Les om ulike metoder for å samle inn data (lenke til ny side)</p>");
        s.SetValue("eksempelTittel", "Eksempel: Registrere fremmøte på jobb");
        s.SetValue("eksempelTekst", "<p>Ta et AI-system for registrering av frammøte på jobb med biometrisk gjenkjenning som eksempel. Hvis du trener systemet med bilder som er skjeve når det gjelder kjønn og rase, er det stor risiko for at systemet også blir skjevt og diskriminerende.</p><p>Hvis du for eksempel hovedsakelig bruker bilder av hvite menn for å trene ansiktsgjenkjenningssystemet, vil systemet trolig slite med å gjenkjenne og klassifisere personer av andre kjønn og raser. Dette kan føre til at systemet gjør feil når det skal identifisere personer av visse raser eller kjønn, og dermed diskriminerer.</p>");
        SaveAndPublish(s);

        // Step 2.2
        s = Create("veiledningSteg", "Samle inn data", parentId);
        s.SetValue("tittel", "Samle inn data");
        s.SetValue("slug", "samle-inn-data");
        s.SetValue("guideSlug", "bruk-data-rett");
        s.SetValue("steg", 2);
        s.SetValue("understeg", 2);
        s.SetValue("innhold", "<ol><li>Hent data fra de identifiserte kildene</li><li>Dokumenter hvor dataene kommer fra</li></ol>");
        SaveAndPublish(s);

        // Step 3.1
        s = Create("veiledningSteg", "Måle og forbedre datakvalitet", parentId);
        s.SetValue("tittel", "Måle og forbedre datakvalitet");
        s.SetValue("slug", "male-og-forbedre-datakvalitet");
        s.SetValue("guideSlug", "bruk-data-rett");
        s.SetValue("steg", 3);
        s.SetValue("understeg", 1);
        s.SetValue("innhold", "<p>Å vurdere kvaliteten på dataene handler om å finne ut hvor godt dataene passer til formålet. Det gjør vi ved å analysere aspekter ved dataene. Analysen forteller oss hva vi må justere for å forbedre dataene.</p><p>Dette må du gjøre for hvert datasett.</p>");
        s.SetValue("infoKortTittel", "Slik måler du datakvalitet");
        s.SetValue("infoKortInnhold", "<ol><li>Velg hvilke aspekter av datane du skal måle kvaliteten på.</li><li>Finn ut hvordan du skal måle kvaliteten.</li><li>Implementer kontrollen teknisk.</li><li>Lag en rapport med resultatene fra kontrollen.</li><li>Lag tiltak og plan for å forbedre dataene.</li></ol>");
        s.SetValue("eksempelTittel", "Eksempel: Implementere kontrollen teknisk");
        s.SetValue("eksempelTekst", "<p>La oss anta at vi har definert kvalitetskontrollene fra forrige eksempel for de tre punktene i datalivssyklusen. Nå må vi implementere disse kontrollene, og som vi har forklart vil hvordan vi gjør det avhenge av hver plattform.</p>");
        SaveAndPublish(s);

        // Step 3.2
        s = Create("veiledningSteg", "Datatransformasjon", parentId);
        s.SetValue("tittel", "Datatransformasjon");
        s.SetValue("slug", "datatransformasjon");
        s.SetValue("guideSlug", "bruk-data-rett");
        s.SetValue("steg", 3);
        s.SetValue("understeg", 2);
        s.SetValue("innhold", "<p>Å endre dataene slik at de har likt format, er ekstra viktig når dataene kommer fra ulike kilder. Ulikt format kan for eksempel være at dataene bruker ulike måleenheter eller har indekser på ulike skalaer.</p>");
        s.SetValue("infoKortTittel", "Slik gir du datene likt format");
        s.SetValue("infoKortInnhold", "<p>1. Identifiser data som ikke er ensartede, og finn ut hvorfor de er ulike. Sjekk om data</p><ul><li>har ulike formater, for eksempel datoformater</li><li>bruker ulike måleenheter</li><li>har ulike skalaer eller indekser</li></ul><p>2. Gjør data ensartede.</p><ul><li>Normalisere - justere verdier til en felles skala</li><li>Skalere - tilpasse størrelsesorden</li><li>Konvertere - endre format eller enhet</li></ul>");
        s.SetValue("eksempelTittel", "Konkret eksempel på transformasjon");
        s.SetValue("eksempelTekst", "<ul><li>Konverter valuta (f.eks. Yen → Euro)</li><li>Konverter måleenheter (f.eks. miles → kilometer)</li><li>Standardiser datoformater (f.eks. DD/MM/ÅÅÅÅ → ÅÅÅÅ-MM-DD)</li><li>Konverter kategoriske verdier (f.eks. \"Ja/Nei\" → 1/0)</li></ul>");
        SaveAndPublish(s);

        // Step 3.3
        s = Create("veiledningSteg", "Aggregere data", parentId);
        s.SetValue("tittel", "Aggregere data");
        s.SetValue("slug", "aggregere-data");
        s.SetValue("guideSlug", "bruk-data-rett");
        s.SetValue("steg", 3);
        s.SetValue("understeg", 3);
        s.SetValue("innhold", "<p>Hvis du trenger å vite egenskaper ved grupper av dataene, må du gruppere dem for å kunne analysere dem og trekke konklusjoner.</p>");
        s.SetValue("infoKortTittel", "Slik går du frem");
        s.SetValue("infoKortInnhold", "<ol><li>Finn ut om du trenger å analysere data på gruppenivå.</li><li>Velg egenskaper du vil gruppere data etter (f.eks. per ansatt, per avdeling, per dato)</li><li>Bestem hvordan du vil oppsummere dataene, for eksempel gjennomsnitt, sum eller antall.</li><li>Endre de originale dataene: grupper dem etter karakteristikkene du har valgt, beregn aggregeringsfunksjon for hver gruppe, lag nytt aggregert datasett.</li></ol>");
        s.SetValue("eksempelTittel", "Konkret eksempel på transformasjon");
        s.SetValue("eksempelTekst", "<ul><li>Konverter valuta (f.eks. Yen → Euro)</li><li>Konverter måleenheter (f.eks. miles → kilometer)</li><li>Standardiser datoformater (f.eks. DD/MM/ÅÅÅÅ → ÅÅÅÅ-MM-DD)</li><li>Konverter kategoriske verdier (f.eks. \"Ja/Nei\" → 1/0)</li></ul>");
        SaveAndPublish(s);

        // Step 3.4
        s = Create("veiledningSteg", "Trekke ut data", parentId);
        s.SetValue("tittel", "Trekke ut data");
        s.SetValue("slug", "trekke-ut-data");
        s.SetValue("guideSlug", "bruk-data-rett");
        s.SetValue("steg", 3);
        s.SetValue("understeg", 4);
        s.SetValue("innhold", "<p>Det kan i noen tilfeller være nødvendig å trekke ut data fra et datasett. Det kan for eksempel være hvis du vil teste AI-systemet raskt uten å bruke hele datasettet eller skal dele data i treningssett, valideringssett og testsett.</p>");
        s.SetValue("infoKortTittel", "Velg metode for å trekke ut data");
        s.SetValue("infoKortInnhold", "<p>Forskjellen på tilfeldig og stratifisert uttrekk er at ved tilfeldig utvalg har alle datapunkter like stor sjanse for å bli valgt, mens ved stratifisert utvalg deler du først dataene i undergrupper og velger deretter fra hver gruppe for å sikre at alle grupper er representert.</p>");
        s.SetValue("eksempelTittel", "Konkret eksempel på transformasjon");
        s.SetValue("eksempelTekst", "<ul><li>Konverter valuta (f.eks. Yen → Euro)</li><li>Konverter måleenheter (f.eks. miles → kilometer)</li><li>Standardiser datoformater (f.eks. DD/MM/ÅÅÅÅ → ÅÅÅÅ-MM-DD)</li><li>Konverter kategoriske verdier (f.eks. \"Ja/Nei\" → 1/0)</li></ul>");
        SaveAndPublish(s);

        // Steps 4.1-4.3 (placeholder)
        s = Create("veiledningSteg", "Tilgang", parentId);
        s.SetValue("tittel", "Tilgang");
        s.SetValue("slug", "tilgang");
        s.SetValue("guideSlug", "bruk-data-rett");
        s.SetValue("steg", 4);
        s.SetValue("understeg", 1);
        s.SetValue("innhold", "<p>Innhold kommer snart.</p>");
        SaveAndPublish(s);

        s = Create("veiledningSteg", "Dokumentasjon", parentId);
        s.SetValue("tittel", "Dokumentasjon");
        s.SetValue("slug", "dokumentasjon");
        s.SetValue("guideSlug", "bruk-data-rett");
        s.SetValue("steg", 4);
        s.SetValue("understeg", 2);
        s.SetValue("innhold", "<p>Innhold kommer snart.</p>");
        SaveAndPublish(s);

        s = Create("veiledningSteg", "Personvern og sikkerhet", parentId);
        s.SetValue("tittel", "Personvern og sikkerhet");
        s.SetValue("slug", "personvern-og-sikkerhet");
        s.SetValue("guideSlug", "bruk-data-rett");
        s.SetValue("steg", 4);
        s.SetValue("understeg", 3);
        s.SetValue("innhold", "<p>Innhold kommer snart.</p>");
        SaveAndPublish(s);

        // Steps 5.1-5.4 (placeholder)
        s = Create("veiledningSteg", "Før du sletter", parentId);
        s.SetValue("tittel", "Før du sletter");
        s.SetValue("slug", "for-du-sletter");
        s.SetValue("guideSlug", "bruk-data-rett");
        s.SetValue("steg", 5);
        s.SetValue("understeg", 1);
        s.SetValue("innhold", "<p>Innhold kommer snart.</p>");
        SaveAndPublish(s);

        s = Create("veiledningSteg", "Når du sletter", parentId);
        s.SetValue("tittel", "Når du sletter");
        s.SetValue("slug", "nar-du-sletter");
        s.SetValue("guideSlug", "bruk-data-rett");
        s.SetValue("steg", 5);
        s.SetValue("understeg", 2);
        s.SetValue("innhold", "<p>Innhold kommer snart.</p>");
        SaveAndPublish(s);

        s = Create("veiledningSteg", "Dokumentasjon av sletting", parentId);
        s.SetValue("tittel", "Dokumentasjon av sletting");
        s.SetValue("slug", "dokumentasjon-sletting");
        s.SetValue("guideSlug", "bruk-data-rett");
        s.SetValue("steg", 5);
        s.SetValue("understeg", 3);
        s.SetValue("innhold", "<p>Innhold kommer snart.</p>");
        SaveAndPublish(s);

        s = Create("veiledningSteg", "Slett deler av dataen", parentId);
        s.SetValue("tittel", "Slett deler av dataen");
        s.SetValue("slug", "slett-deler-av-dataen");
        s.SetValue("guideSlug", "bruk-data-rett");
        s.SetValue("steg", 5);
        s.SetValue("understeg", 4);
        s.SetValue("innhold", "<p>Innhold kommer snart.</p>");
        SaveAndPublish(s);

        // Merke data (step 3 sub-step)
        s = Create("veiledningSteg", "Merke data", parentId);
        s.SetValue("tittel", "Merke data");
        s.SetValue("slug", "merke-data");
        s.SetValue("guideSlug", "bruk-data-rett");
        s.SetValue("steg", 3);
        s.SetValue("understeg", 5);
        s.SetValue("innhold", "<p>Innhold kommer snart.</p>");
        SaveAndPublish(s);
    }

    // ── FAQ ────────────────────────────────────────────────────

    private void SeedFAQ(int parentId, Dictionary<string, IContent> merkelapper)
    {
        var q1 = Create("faq", "Hva er kunstig intelligens?", parentId);
        q1.SetValue("sporsmal", "Hva er kunstig intelligens?");
        q1.SetValue("svar", @"<p>Kunstig intelligens (KI) er et samlebegrep for datasystemer som
kan utføre oppgaver som normalt krever menneskelig intelligens. Dette inkluderer
maskinlæring, naturlig språkbehandling, bildegjenkjenning og beslutningstaking.</p>
<p>I offentlig sektor brukes KI typisk til å automatisere rutineoppgaver,
forbedre innbyggertjenester og effektivisere saksbehandling.</p>");
        q1.SetValue("kategori", Udi(merkelapper["maskinlaering"]));
        q1.SetValue("rekkefolge", 1);
        SaveAndPublish(q1);

        var q2 = Create("faq", "Er det trygt å bruke KI i offentlig sektor?", parentId);
        q2.SetValue("sporsmal", "Er det trygt å bruke KI i offentlig sektor?");
        q2.SetValue("svar", @"<p>Ja, men det krever at man følger etablerte retningslinjer for
ansvarlig KI-bruk. Dette innebærer grundig risikovurdering, ivaretakelse
av personvern, og transparent bruk av teknologien.</p>
<p>EUs AI Act setter tydelige krav til KI-systemer som brukes i offentlig
sektor, spesielt for systemer med høy risiko.</p>");
        q2.SetValue("kategori", Udi(merkelapper["personvern"]));
        q2.SetValue("rekkefolge", 2);
        SaveAndPublish(q2);

        var q3 = Create("faq", "Hvordan komme i gang med KI?", parentId);
        q3.SetValue("sporsmal", "Hvordan komme i gang med KI i min virksomhet?");
        q3.SetValue("svar", @"<p>Start med å identifisere konkrete utfordringer eller prosesser
som kan forbedres med KI. Kartlegg datakvalitet og digital modenhet.
Se vår <em>veiledning for å komme i gang</em> for en steg-for-steg-guide.</p>
<p>Vi anbefaler å starte med små pilotprosjekter for å bygge kompetanse
og erfaring før man skalerer opp.</p>");
        q3.SetValue("kategori", Udi(merkelapper["automatisering"]));
        q3.SetValue("rekkefolge", 3);
        SaveAndPublish(q3);

        var q4 = Create("faq", "Hva er EUs AI Act?", parentId);
        q4.SetValue("sporsmal", "Hva er EUs AI Act, og gjelder den i Norge?");
        q4.SetValue("svar", @"<p>EUs AI Act er verdens første helhetlige regulering av kunstig intelligens.
Den klassifiserer KI-systemer etter risikonivå og stiller strengere krav
jo høyere risikoen er.</p>
<p>Ja, gjennom EØS-avtalen vil regelverket også gjelde i Norge. Norske
virksomheter bør begynne å forberede seg allerede nå.</p>");
        q4.SetValue("kategori", Udi(merkelapper["etikk"]));
        q4.SetValue("rekkefolge", 4);
        SaveAndPublish(q4);

        var q5 = Create("faq", "Kan KI erstatte saksbehandlere?", parentId);
        q5.SetValue("sporsmal", "Kan KI erstatte saksbehandlere?");
        q5.SetValue("svar", @"<p>KI kan automatisere deler av saksbehandlingsprosessen, men bør
ikke erstatte menneskelig vurdering i beslutninger som har stor
betydning for enkeltpersoner.</p>
<p>I praksis fungerer KI best som et verktøy som støtter saksbehandlere —
for eksempel ved å sortere henvendelser, foreslå vedtak basert på
tidligere praksis, eller kvalitetssikre dokumenter.</p>");
        q5.SetValue("kategori", Udi(merkelapper["automatisering"]));
        q5.SetValue("rekkefolge", 5);
        SaveAndPublish(q5);
    }

    // ── Merkelapper ────────────────────────────────────────────

    private Dictionary<string, IContent> SeedMerkelapper(int parentId)
    {
        var tags = new[]
        {
            ("Maskinlæring", "maskinlaering", "Maskinlæring og nevrale nettverk"),
            ("Naturlig språk", "naturlig-sprak", "Naturlig språkbehandling (NLP)"),
            ("Chatbot", "chatbot", "Chatboter og konversasjonsgrensesnitt"),
            ("Personvern", "personvern", "Personvern og GDPR i KI-systemer"),
            ("Helse", "helse", "KI i helsesektoren"),
            ("Kommune", "kommune", "KI i kommunal sektor"),
            ("Automatisering", "automatisering", "Prosessautomatisering med KI"),
            ("Etikk", "etikk", "Etiske problemstillinger rundt KI"),
            ("Sikkerhet", "sikkerhet", "Informasjonssikkerhet og KI"),
            ("Innkjøp", "innkjop", "Anskaffelse av KI-løsninger"),
            ("Transparens", "transparens", "Åpenhet og forklarbarhet i KI-systemer"),
        };

        var map = new Dictionary<string, IContent>();
        foreach (var (navn, slug, beskrivelse) in tags)
        {
            var m = Create("merkelapp", navn, parentId);
            m.SetValue("navn", navn);
            m.SetValue("slug", slug);
            m.SetValue("beskrivelse", beskrivelse);
            SaveAndPublish(m);
            map[slug] = m;
        }
        return map;
    }

    private string Udi(IContent content) => $"umb://document/{content.Key:N}";

    // ── Media ──────────────────────────────────────────────────

    private void SeedMedia()
    {
        // Check if media already exists
        var existing = _mediaService.GetRootMedia();
        if (existing != null && existing.Any()) return;

        var seedMediaPath = Path.Combine(_webHostEnvironment.WebRootPath ?? _webHostEnvironment.ContentRootPath, "seed-media");
        if (!Directory.Exists(seedMediaPath))
        {
            Console.WriteLine($"ContentSeeder: No seed-media folder found at {seedMediaPath}");
            return;
        }

        // Create a folder in the media library
        var folder = _mediaService.CreateMediaWithIdentity("Seed bilder", -1, "Folder");

        foreach (var filePath in Directory.GetFiles(seedMediaPath, "*.png"))
        {
            var fileName = Path.GetFileName(filePath);
            try
            {
                var media = _mediaService.CreateMedia(fileName, folder.Id, "Image");
                var fileBytes = System.IO.File.ReadAllBytes(filePath);
                var stream = new MemoryStream(fileBytes);
                media.SetValue(_mediaFileManager, _mediaUrlGenerators, _shortStringHelper, _contentTypeBaseServiceProvider, "umbracoFile", fileName, stream);
                _mediaService.Save(media);
                Console.WriteLine($"ContentSeeder: Uploaded media '{fileName}'");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"ContentSeeder: Failed to upload '{fileName}': {ex.Message}");
            }
        }
    }
}
