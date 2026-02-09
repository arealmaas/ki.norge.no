using Umbraco.Cms.Core;
using Umbraco.Cms.Core.Composing;
using Umbraco.Cms.Core.Models;
using Umbraco.Cms.Core.Services;

namespace KiNorge.Cms.Composers;

/// <summary>
/// Seeds dummy content for development. Only runs once (checks if content exists).
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
    private readonly IRuntimeState _runtimeState;

    public ContentSeeder(
        IContentService contentService,
        IContentTypeService contentTypeService,
        IRuntimeState runtimeState)
    {
        _contentService = contentService;
        _contentTypeService = contentTypeService;
        _runtimeState = runtimeState;
    }

    public Task InitializeAsync(bool isRestarting, CancellationToken cancellationToken)
    {
        if (_runtimeState.Level < RuntimeLevel.Run) return Task.CompletedTask;

        // Skip if content already exists
        var existing = _contentService.GetRootContent();
        if (existing.Any()) return Task.CompletedTask;

        try
        {
            SeedArticles();
            SeedPages();
            SeedExamples();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"ContentSeeder: {ex.Message}");
        }
        return Task.CompletedTask;
    }

    public Task TerminateAsync(bool isRestarting, CancellationToken cancellationToken) => Task.CompletedTask;

    private IContent Create(string contentTypeAlias, string name)
    {
        var ct = _contentTypeService.Get(contentTypeAlias)
            ?? throw new InvalidOperationException($"Content type '{contentTypeAlias}' not found");
        return _contentService.Create(name, -1, ct.Alias);
    }

    private void SaveAndPublish(IContent content)
    {
        _contentService.Save(content);
        _contentService.Publish(content, Array.Empty<string>());
    }

    private void SeedArticles()
    {
        // Article 1
        var a1 = Create("artikkel", "Ny nasjonal strategi for kunstig intelligens");
        a1.SetValue("tittel", "Ny nasjonal strategi for kunstig intelligens");
        a1.SetValue("slug", "ny-nasjonal-strategi-for-kunstig-intelligens");
        a1.SetValue("innhold", @"<p>Regjeringen har lansert en oppdatert nasjonal strategi for kunstig intelligens.
Strategien legger vekt på ansvarlig bruk av KI i offentlig sektor, med fokus på åpenhet,
personvern og tillit.</p>
<h2>Hovedpunkter i strategien</h2>
<ul>
<li>Styrket satsing på KI-kompetanse i offentlig forvaltning</li>
<li>Felles retningslinjer for ansvarlig KI-bruk</li>
<li>Økt deling av data mellom offentlige virksomheter</li>
<li>Etablering av nasjonalt KI-senter for offentlig sektor</li>
</ul>
<p>Strategien følger opp EUs AI Act og setter rammer for hvordan norske
virksomheter kan ta i bruk KI på en trygg og tillitvekkende måte.</p>");
        SaveAndPublish(a1);

        // Article 2
        var a2 = Create("artikkel", "Kommuner tar i bruk KI for bedre innbyggertjenester");
        a2.SetValue("tittel", "Kommuner tar i bruk KI for bedre innbyggertjenester");
        a2.SetValue("slug", "kommuner-tar-i-bruk-ki-for-bedre-innbyggertjenester");
        a2.SetValue("innhold", @"<p>Flere norske kommuner har begynt å eksperimentere med kunstig intelligens
for å forbedre tjenestene til innbyggerne. Fra automatisert saksbehandling til
chatboter for innbyggerdialog — mulighetene er mange.</p>
<h2>Eksempler fra kommunene</h2>
<p>Stavanger kommune bruker maskinlæring for å predikere vedlikeholdsbehov
på kommunale bygg, mens Trondheim har utviklet en KI-basert chatbot som
hjelper innbyggere med å finne riktig tjeneste.</p>
<p>Bergen kommune tester automatisk klassifisering av innkommende henvendelser,
noe som har redusert svartiden med 40 prosent.</p>");
        SaveAndPublish(a2);
    }

    private void SeedPages()
    {
        // Om oss
        var omOss = Create("side", "Om KI Norge");
        omOss.SetValue("tittel", "Om KI Norge");
        omOss.SetValue("slug", "om-oss");
        omOss.SetValue("innhold", @"<p>KI Norge er en nasjonal ressurs for kunstig intelligens i offentlig sektor.
Vi jobber for at norske virksomheter skal ta i bruk KI på en ansvarlig og
verdiskapende måte.</p>
<h2>Vår rolle</h2>
<p>Vi tilbyr veiledning, deler gode eksempler og fasiliterer samarbeid mellom
offentlige virksomheter som ønsker å utforske og ta i bruk kunstig intelligens.</p>
<h2>Kontakt oss</h2>
<p>E-post: post@ki.norge.no</p>");
        omOss.SetValue("seoBeskrivelse", "KI Norge er en nasjonal ressurs for kunstig intelligens i offentlig sektor.");
        SaveAndPublish(omOss);

        // Kontakt
        var kontakt = Create("side", "Kontakt");
        kontakt.SetValue("tittel", "Kontakt oss");
        kontakt.SetValue("slug", "kontakt");
        kontakt.SetValue("innhold", @"<p>Har du spørsmål om kunstig intelligens i offentlig sektor?
Ta gjerne kontakt med oss.</p>
<h3>E-post</h3>
<p>post@ki.norge.no</p>
<h3>Besøksadresse</h3>
<p>Digitaliseringsdirektoratet<br>Brattørkaia 15B<br>7010 Trondheim</p>");
        SaveAndPublish(kontakt);
    }

    private void SeedExamples()
    {
        // Example 1
        var e1 = Create("eksempel", "KI-chatbot for innbyggerdialog");
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
        SaveAndPublish(e1);

        // Example 2
        var e2 = Create("eksempel", "Prediktivt vedlikehold av kommunale bygg");
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
        SaveAndPublish(e2);
    }
}
