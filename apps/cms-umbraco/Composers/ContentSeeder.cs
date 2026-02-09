using Umbraco.Cms.Core;
using Umbraco.Cms.Core.Composing;
using Umbraco.Cms.Core.Models;
using Umbraco.Cms.Core.Services;

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
            // Create container nodes (folders) at root
            var artiklerFolder = CreateFolder("artikler", "Artikler");
            var siderFolder = CreateFolder("sider", "Sider");
            var eksemplerFolder = CreateFolder("eksempler", "Eksempler");
            var veiledningerFolder = CreateFolder("veiledninger", "Veiledninger");
            var faqFolder = CreateFolder("faqSamling", "FAQ");
            var merkelapperFolder = CreateFolder("merkelapper", "Merkelapper");

            // Seed content under each folder
            SeedArticles(artiklerFolder.Id);
            SeedPages(siderFolder.Id);
            SeedExamples(eksemplerFolder.Id);
            SeedVeiledninger(veiledningerFolder.Id);
            SeedFAQ(faqFolder.Id);
            SeedMerkelapper(merkelapperFolder.Id);

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
        _contentService.Publish(folder, Array.Empty<string>());
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
        _contentService.Publish(content, Array.Empty<string>());
    }

    // ── Artikler ──────────────────────────────────────────────

    private void SeedArticles(int parentId)
    {
        var a1 = Create("artikkel", "Ny nasjonal strategi for kunstig intelligens", parentId);
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

        var a2 = Create("artikkel", "Kommuner tar i bruk KI for bedre innbyggertjenester", parentId);
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

        var a3 = Create("artikkel", "EUs AI Act og konsekvenser for norsk offentlig sektor", parentId);
        a3.SetValue("tittel", "EUs AI Act og konsekvenser for norsk offentlig sektor");
        a3.SetValue("slug", "eus-ai-act-og-konsekvenser-for-norsk-offentlig-sektor");
        a3.SetValue("innhold", @"<p>EU har vedtatt verdens første helhetlige regulering av kunstig intelligens.
AI Act klassifiserer KI-systemer etter risikonivå og stiller krav til åpenhet,
sikkerhet og menneskerettigheter.</p>
<h2>Hva betyr dette for Norge?</h2>
<p>Gjennom EØS-avtalen vil AI Act også gjelde i Norge. Offentlige virksomheter
som bruker KI-systemer til saksbehandling, velferdstjenester eller overvåkning
må forberede seg på nye krav til dokumentasjon og risikovurdering.</p>
<p>KI Norge tilbyr veiledning for virksomheter som trenger hjelp med å
forstå og etterleve de nye reglene.</p>");
        SaveAndPublish(a3);

        var a4 = Create("artikkel", "Åpenhet og tillit i KI-prosjekter", parentId);
        a4.SetValue("tittel", "Åpenhet og tillit i KI-prosjekter");
        a4.SetValue("slug", "apenhet-og-tillit-i-ki-prosjekter");
        a4.SetValue("innhold", @"<p>For at kunstig intelligens skal lykkes i offentlig sektor, er det
avgjørende at innbyggerne har tillit til løsningene. Åpenhet om hvordan
KI-systemer fungerer og hvilke data de bruker, er en forutsetning.</p>
<h2>Prinsipper for åpen KI</h2>
<ul>
<li>Dokumenter beslutningsgrunnlaget for KI-systemer</li>
<li>Gjør algoritmene tilgjengelige for ekstern revisjon</li>
<li>Informer innbyggerne når KI brukes i saksbehandling</li>
<li>Etabler klageadgang for automatiserte beslutninger</li>
</ul>");
        SaveAndPublish(a4);
    }

    // ── Sider ──────────────────────────────────────────────────

    private void SeedPages(int parentId)
    {
        var omOss = Create("side", "Om KI Norge", parentId);
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
        SaveAndPublish(e4);
    }

    // ── Veiledninger ───────────────────────────────────────────

    private void SeedVeiledninger(int parentId)
    {
        var v1 = Create("veiledning", "Kom i gang med KI i din virksomhet", parentId);
        v1.SetValue("tittel", "Kom i gang med KI i din virksomhet");
        v1.SetValue("slug", "kom-i-gang-med-ki");
        v1.SetValue("innhold", @"<p>Denne veiledningen hjelper offentlige virksomheter med å ta de første
stegene mot å bruke kunstig intelligens. Vi dekker alt fra behovsanalyse
til valg av teknologi og leverandør.</p>
<h2>Steg 1: Identifiser behov</h2>
<p>Start med å kartlegge prosesser som kan ha nytte av automatisering
eller forbedring med KI. Fokuser på oppgaver som er repetitive,
datadrevne eller tidkrevende.</p>
<h2>Steg 2: Vurder modenhet</h2>
<p>Kartlegg virksomhetens digitale modenhet. Har dere tilstrekkelig
datakvalitet? Er organisasjonen klar for endring?</p>
<h2>Steg 3: Velg riktig tilnærming</h2>
<p>Vurder om dere skal kjøpe en ferdig løsning, tilpasse en eksisterende,
eller utvikle noe helt nytt.</p>");
        v1.SetValue("rekkefolge", 1);
        SaveAndPublish(v1);

        var v2 = Create("veiledning", "Ansvarlig bruk av KI", parentId);
        v2.SetValue("tittel", "Ansvarlig bruk av KI");
        v2.SetValue("slug", "ansvarlig-bruk-av-ki");
        v2.SetValue("innhold", @"<p>Denne veiledningen gir praktiske råd for å sikre at KI-systemer
brukes på en etisk og ansvarlig måte i offentlig sektor.</p>
<h2>Grunnleggende prinsipper</h2>
<ul>
<li>Transparens: Forklar hvordan KI-systemet tar beslutninger</li>
<li>Rettferdighet: Sikre at systemet ikke diskriminerer</li>
<li>Personvern: Beskytt personopplysninger gjennom hele livssyklusen</li>
<li>Sikkerhet: Beskytt mot misbruk og uønsket bruk</li>
<li>Ansvarlighet: Tydelig ansvarslinje for KI-beslutninger</li>
</ul>
<h2>Risikovurdering</h2>
<p>Gjennomfør en grundig risikovurdering før KI-systemer settes i produksjon.
Vurder risiko for feil, bias, personvernbrudd og sikkerhetssvakheter.</p>");
        v2.SetValue("rekkefolge", 2);
        SaveAndPublish(v2);

        var v3 = Create("veiledning", "Datakvalitet for KI-prosjekter", parentId);
        v3.SetValue("tittel", "Datakvalitet for KI-prosjekter");
        v3.SetValue("slug", "datakvalitet-for-ki-prosjekter");
        v3.SetValue("innhold", @"<p>God datakvalitet er en forutsetning for vellykkede KI-prosjekter.
Denne veiledningen gir praktiske råd for å sikre at dataene dine
er egnet for maskinlæring og andre KI-teknikker.</p>
<h2>Vanlige datakvalitetsproblemer</h2>
<ul>
<li>Manglende verdier og inkonsistente formater</li>
<li>Utdaterte eller feilaktige data</li>
<li>Skjevheter (bias) i treningsdata</li>
<li>Manglende dokumentasjon av datakilder</li>
</ul>
<h2>Beste praksis</h2>
<p>Etabler rutiner for datavask, dokumentasjon og kvalitetskontroll
tidlig i prosjektet. Bruk verktøy for automatisk dataprofilering.</p>");
        v3.SetValue("rekkefolge", 3);
        SaveAndPublish(v3);
    }

    // ── FAQ ────────────────────────────────────────────────────

    private void SeedFAQ(int parentId)
    {
        var q1 = Create("faq", "Hva er kunstig intelligens?", parentId);
        q1.SetValue("sporsmal", "Hva er kunstig intelligens?");
        q1.SetValue("svar", @"<p>Kunstig intelligens (KI) er et samlebegrep for datasystemer som
kan utføre oppgaver som normalt krever menneskelig intelligens. Dette inkluderer
maskinlæring, naturlig språkbehandling, bildegjenkjenning og beslutningstaking.</p>
<p>I offentlig sektor brukes KI typisk til å automatisere rutineoppgaver,
forbedre innbyggertjenester og effektivisere saksbehandling.</p>");
        q1.SetValue("rekkefolge", 1);
        SaveAndPublish(q1);

        var q2 = Create("faq", "Er det trygt å bruke KI i offentlig sektor?", parentId);
        q2.SetValue("sporsmal", "Er det trygt å bruke KI i offentlig sektor?");
        q2.SetValue("svar", @"<p>Ja, men det krever at man følger etablerte retningslinjer for
ansvarlig KI-bruk. Dette innebærer grundig risikovurdering, ivaretakelse
av personvern, og transparent bruk av teknologien.</p>
<p>EUs AI Act setter tydelige krav til KI-systemer som brukes i offentlig
sektor, spesielt for systemer med høy risiko.</p>");
        q2.SetValue("rekkefolge", 2);
        SaveAndPublish(q2);

        var q3 = Create("faq", "Hvordan komme i gang med KI?", parentId);
        q3.SetValue("sporsmal", "Hvordan komme i gang med KI i min virksomhet?");
        q3.SetValue("svar", @"<p>Start med å identifisere konkrete utfordringer eller prosesser
som kan forbedres med KI. Kartlegg datakvalitet og digital modenhet.
Se vår <em>veiledning for å komme i gang</em> for en steg-for-steg-guide.</p>
<p>Vi anbefaler å starte med små pilotprosjekter for å bygge kompetanse
og erfaring før man skalerer opp.</p>");
        q3.SetValue("rekkefolge", 3);
        SaveAndPublish(q3);

        var q4 = Create("faq", "Hva er EUs AI Act?", parentId);
        q4.SetValue("sporsmal", "Hva er EUs AI Act, og gjelder den i Norge?");
        q4.SetValue("svar", @"<p>EUs AI Act er verdens første helhetlige regulering av kunstig intelligens.
Den klassifiserer KI-systemer etter risikonivå og stiller strengere krav
jo høyere risikoen er.</p>
<p>Ja, gjennom EØS-avtalen vil regelverket også gjelde i Norge. Norske
virksomheter bør begynne å forberede seg allerede nå.</p>");
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
        q5.SetValue("rekkefolge", 5);
        SaveAndPublish(q5);
    }

    // ── Merkelapper ────────────────────────────────────────────

    private void SeedMerkelapper(int parentId)
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
        };

        foreach (var (navn, slug, beskrivelse) in tags)
        {
            var m = Create("merkelapp", navn, parentId);
            m.SetValue("navn", navn);
            m.SetValue("slug", slug);
            m.SetValue("beskrivelse", beskrivelse);
            SaveAndPublish(m);
        }
    }
}
