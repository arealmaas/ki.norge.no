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

            // Seed merkelapper FIRST so we can reference them from other content
            var merkelappMap = SeedMerkelapper(merkelapperFolder.Id);

            // Seed content under each folder (with merkelapp references)
            SeedArticles(artiklerFolder.Id);
            SeedPages(siderFolder.Id);
            SeedExamples(eksemplerFolder.Id);
            SeedVeiledninger(veiledningerFolder.Id, merkelappMap);
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

        var a5 = Create("artikkel", "EU AI Act: Hva betyr det for norsk offentlig sektor?", parentId);
        a5.SetValue("tittel", "EU AI Act: Hva betyr det for norsk offentlig sektor?");
        a5.SetValue("slug", "eu-ai-act-hva-betyr-det-for-norsk-offentlig-sektor");
        a5.SetValue("innhold", @"<p>EUs forordning om kunstig intelligens (AI Act) trådte i kraft i 2024 og
innføres gradvis frem mot 2026. Gjennom EØS-avtalen vil regelverket også gjelde
i Norge. Hva betyr dette i praksis for offentlige virksomheter?</p>
<h2>Risikobasert tilnærming</h2>
<p>AI Act klassifiserer KI-systemer i fire risikonivåer: uakseptabel risiko,
høy risiko, begrenset risiko og minimal risiko. Systemer brukt i offentlig
saksbehandling — for eksempel velferdstjenester, grensekontroll og
strafferettspleie — faller typisk i kategorien høy risiko.</p>
<h2>Krav til høyrisiko-systemer</h2>
<ul>
<li>Risikovurdering og kvalitetsstyring</li>
<li>Dokumentasjon av treningsdata og algoritmisk logikk</li>
<li>Menneskelig tilsyn og mulighet for overstyring</li>
<li>Logging og sporbarhet av beslutninger</li>
</ul>
<p>Norske virksomheter bør begynne kartleggingen av egne KI-systemer allerede nå,
slik at de er klare når regelverket trer i kraft i EØS.</p>");
        SaveAndPublish(a5);

        var a6 = Create("artikkel", "Slik bruker Nav kunstig intelligens til saksbehandling", parentId);
        a6.SetValue("tittel", "Slik bruker Nav kunstig intelligens til saksbehandling");
        a6.SetValue("slug", "slik-bruker-nav-kunstig-intelligens-til-saksbehandling");
        a6.SetValue("innhold", @"<p>Nav er blant de offentlige virksomhetene i Norge som har kommet lengst
med å ta i bruk kunstig intelligens. Fra automatisert dokumenthåndtering
til prediktive modeller for oppfølging — KI er i ferd med å endre
hvordan Norges største velferdsetat jobber.</p>
<h2>Automatisk dokumentklassifisering</h2>
<p>Nav mottar millioner av dokumenter hvert år. En KI-modell klassifiserer
innkommende dokumenter automatisk og ruter dem til riktig saksbehandler,
noe som har kuttet behandlingstiden betydelig.</p>
<h2>Prediktiv oppfølging</h2>
<p>Ved hjelp av maskinlæring identifiserer Nav brukere som kan ha nytte
av tidlig oppfølging, slik at rådgivere kan prioritere der behovet er størst.</p>
<h2>Erfaringer og utfordringer</h2>
<p>Nav understreker viktigheten av menneskelig kontroll, transparens overfor
brukerne, og løpende evaluering av modellenes treffsikkerhet og rettferdighet.</p>");
        SaveAndPublish(a6);

        var a7 = Create("artikkel", "5 ting du må vite før du anskaffer KI-løsninger", parentId);
        a7.SetValue("tittel", "5 ting du må vite før du anskaffer KI-løsninger");
        a7.SetValue("slug", "5-ting-du-ma-vite-for-du-anskaffer-ki-losninger");
        a7.SetValue("innhold", @"<p>Anskaffelse av KI-løsninger i offentlig sektor krever en annen tilnærming
enn tradisjonelle IT-innkjøp. Her er fem viktige ting å tenke på.</p>
<h2>1. Definer problemet, ikke løsningen</h2>
<p>Start med behovet. Hvilken prosess skal forbedres? Hvilke gevinster forventer
dere? Unngå å bestille «KI» uten et tydelig bruksområde.</p>
<h2>2. Datakvalitet er avgjørende</h2>
<p>En KI-modell er bare så god som dataene den trenes på. Kartlegg tilgjengelige
data og kvaliteten på disse før dere går ut i markedet.</p>
<h2>3. Still krav til åpenhet</h2>
<p>Krev at leverandøren kan forklare hvordan modellen tar beslutninger, og at
dere får innsyn i treningsdata og modellarkitektur.</p>
<h2>4. Tenk livssyklus, ikke bare lansering</h2>
<p>KI-systemer trenger løpende overvåking, oppdatering av modeller og nye
treningsdata. Budsjetter for drift, ikke bare utvikling.</p>
<h2>5. Vurder personvern og etikk tidlig</h2>
<p>Gjennomfør DPIA tidlig i prosessen, og involver personvernombud og
fageksperter fra starten.</p>");
        SaveAndPublish(a7);

        var a8 = Create("artikkel", "Datatilsynets risikovurdering for KI — en gjennomgang", parentId);
        a8.SetValue("tittel", "Datatilsynets risikovurdering for KI — en gjennomgang");
        a8.SetValue("slug", "datatilsynets-risikovurdering-for-ki");
        a8.SetValue("innhold", @"<p>Datatilsynet har publisert en veileder for risikovurdering av
KI-systemer som behandler personopplysninger. Vi oppsummerer de
viktigste punktene og hva det betyr for din virksomhet.</p>
<h2>Hvem gjelder dette?</h2>
<p>Alle virksomheter som bruker KI til å behandle personopplysninger —
enten det er ansiktsgjenkjenning, profilering eller automatisert
saksbehandling — må gjennomføre en risikovurdering.</p>
<h2>Sentrale vurderingspunkter</h2>
<ul>
<li>Nødvendighet og proporsjonalitet: Er KI riktig verktøy?</li>
<li>Dataminimering: Bruker systemet kun nødvendige data?</li>
<li>Rettferdighet: Er det risiko for diskriminering eller skjevhet?</li>
<li>Transparens: Kan de registrerte forstå hvordan beslutninger tas?</li>
<li>Sikkerhet: Er data og modeller tilstrekkelig beskyttet?</li>
</ul>
<p>Datatilsynet anbefaler at risikovurderingen gjøres før systemet settes
i produksjon, og at den oppdateres ved vesentlige endringer.</p>");
        SaveAndPublish(a8);

        var a9 = Create("artikkel", "Generativ KI i kommunene: erfaringer fra pilotprosjekter", parentId);
        a9.SetValue("tittel", "Generativ KI i kommunene: erfaringer fra pilotprosjekter");
        a9.SetValue("slug", "generativ-ki-i-kommunene-erfaringer-fra-pilotprosjekter");
        a9.SetValue("innhold", @"<p>Flere norske kommuner tester nå generativ KI — store språkmodeller som
kan skrive tekst, oppsummere dokumenter og svare på spørsmål. Hva har
de lært så langt?</p>
<h2>Bruksområder som fungerer</h2>
<p>Kommunene rapporterer best resultater for intern bruk: utkast til brev
og vedtak, oppsummering av lange saksdokumenter, og oversettelse til
klart språk. Her sparer saksbehandlere mye tid.</p>
<h2>Utfordringer</h2>
<p>Utadrettet bruk — som chatboter mot innbyggere — krever mer forsiktighet.
Feilaktige svar (hallusinasjoner) kan få alvorlige konsekvenser når det
gjelder rettigheter og tjenester.</p>
<h2>Anbefalinger</h2>
<ul>
<li>Start med intern bruk der feiltoleransen er høyere</li>
<li>Etabler tydelige retningslinjer for hva som kan og ikke kan deles med KI</li>
<li>Sørg for at sensitive personopplysninger ikke sendes til skybaserte tjenester</li>
<li>Mål effekten: Spar dere faktisk tid, eller bruker folk like lang tid på å kvalitetssjekke?</li>
</ul>");
        SaveAndPublish(a9);

        var a10 = Create("artikkel", "Åpenhet og innsyn: Krav til forklarbarhet i KI-systemer", parentId);
        a10.SetValue("tittel", "Åpenhet og innsyn: Krav til forklarbarhet i KI-systemer");
        a10.SetValue("slug", "apenhet-og-innsyn-krav-til-forklarbarhet-i-ki-systemer");
        a10.SetValue("innhold", @"<p>Når offentlige virksomheter bruker KI til å fatte beslutninger som
påvirker innbyggere, stiller både forvaltningsloven og GDPR krav til
forklarbarhet. Men hva betyr egentlig forklarbarhet i praksis?</p>
<h2>Juridiske krav</h2>
<p>Forvaltningsloven krever at vedtak begrunnes. GDPR gir den registrerte
rett til informasjon om automatiserte beslutninger. AI Act stiller
ytterligere krav til dokumentasjon og transparens for høyrisiko-systemer.</p>
<h2>Tekniske tilnærminger</h2>
<p>Forklarbarhet kan implementeres på ulike nivåer: fra enkle
beslutningsregler og featureviktighet til mer avanserte teknikker
som SHAP-verdier og kontrafaktiske forklaringer.</p>
<h2>Praktiske råd</h2>
<ul>
<li>Tilpass forklaringen til mottakeren — innbygger, saksbehandler og revisor trenger ulik detaljeringsgrad</li>
<li>Dokumenter modellens virkemåte ved utvikling, ikke i etterkant</li>
<li>Test forklaringene med reelle brukere — gir de faktisk mening?</li>
</ul>");
        SaveAndPublish(a10);
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
    }

    // ── Veiledninger ───────────────────────────────────────────

    private void SeedVeiledninger(int parentId, Dictionary<string, IContent> merkelapper)
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
        v1.SetValue("kategori", Udi(merkelapper["automatisering"]));
        v1.SetValue("lenker", @"[{""tekst"": ""Digitaliseringsdirektoratets KI-guide"", ""url"": ""https://www.digdir.no/kunstig-intelligens"", ""ekstern"": true}, {""tekst"": ""Nasjonal KI-strategi"", ""url"": ""/artikler/ny-nasjonal-strategi-for-kunstig-intelligens"", ""ekstern"": false}]");
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
        v2.SetValue("kategori", Udi(merkelapper["etikk"]));
        v2.SetValue("lenker", @"[{""tekst"": ""EUs retningslinjer for pålitelig KI"", ""url"": ""https://digital-strategy.ec.europa.eu/en/library/ethics-guidelines-trustworthy-ai"", ""ekstern"": true}]");
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
        v3.SetValue("kategori", Udi(merkelapper["maskinlaering"]));
        v3.SetValue("rekkefolge", 3);
        SaveAndPublish(v3);

        var v4 = Create("veiledning", "Risikovurdering av KI-systemer", parentId);
        v4.SetValue("tittel", "Risikovurdering av KI-systemer");
        v4.SetValue("slug", "risikovurdering-av-ki-systemer");
        v4.SetValue("innhold", @"<p>Denne veiledningen gir en praktisk steg-for-steg-tilnærming til
risikovurdering av KI-systemer i offentlig sektor, i tråd med
kravene i AI Act og Datatilsynets anbefalinger.</p>
<h2>Steg 1: Kartlegg systemet</h2>
<p>Beskriv KI-systemets formål, datakilder, beslutningslogikk og
hvem som påvirkes av systemets output. Inkluder en vurdering
av systemets autonomi — tar det beslutninger alene, eller
støtter det en menneskelig beslutningstaker?</p>
<h2>Steg 2: Klassifiser risikonivå</h2>
<p>Bruk AI Acts risikoklasser (uakseptabel, høy, begrenset, minimal)
som utgangspunkt. Systemer i offentlig saksbehandling vil ofte
falle i kategorien høy risiko.</p>
<h2>Steg 3: Identifiser trusler</h2>
<p>Vurder risiko for feil, bias, personvernbrudd, sikkerhetssårbarheter
og misbruk. Bruk et tverrfaglig team med både teknisk og juridisk
kompetanse.</p>
<h2>Steg 4: Definer tiltak</h2>
<p>For hver identifisert risiko: beskriv risikoreduserende tiltak,
hvem som er ansvarlig, og hvordan effekten av tiltaket måles.</p>
<h2>Steg 5: Overvåk og oppdater</h2>
<p>Risikovurderingen er et levende dokument. Oppdater den ved
vesentlige endringer i systemet, datagrunnlaget eller bruksmønsteret.</p>");
        v4.SetValue("kategori", Udi(merkelapper["sikkerhet"]));
        v4.SetValue("lenker", @"[{""tekst"": ""Datatilsynets veileder for KI og personvern"", ""url"": ""https://www.datatilsynet.no/kunstig-intelligens"", ""ekstern"": true}]");
        v4.SetValue("rekkefolge", 4);
        SaveAndPublish(v4);

        var v5 = Create("veiledning", "Personvernkonsekvensvurdering (DPIA) for KI", parentId);
        v5.SetValue("tittel", "Personvernkonsekvensvurdering (DPIA) for KI");
        v5.SetValue("slug", "personvernkonsekvensvurdering-dpia-for-ki");
        v5.SetValue("innhold", @"<p>Når KI-systemer behandler personopplysninger, er det ofte påkrevd å
gjennomføre en personvernkonsekvensvurdering (DPIA) etter GDPR artikkel 35.
Denne veiledningen forklarer når DPIA er nødvendig og hvordan den gjennomføres.</p>
<h2>Når kreves DPIA?</h2>
<p>DPIA er påkrevd når behandlingen sannsynligvis medfører høy risiko
for personers rettigheter. For KI gjelder dette typisk ved:</p>
<ul>
<li>Profilering eller automatiserte beslutninger med rettsvirkning</li>
<li>Systematisk overvåking av offentlig tilgjengelig område</li>
<li>Behandling av særlige kategorier personopplysninger i stor skala</li>
</ul>
<h2>Gjennomføring</h2>
<p>En DPIA bør inkludere: beskrivelse av behandlingen og formålet,
vurdering av nødvendighet og proporsjonalitet, identifisering av
risikoer, og beskrivelse av tiltak for å håndtere risikoene.</p>
<h2>Involvering av Datatilsynet</h2>
<p>Dersom risikovurderingen viser at risikoen forblir høy etter tiltak,
plikter virksomheten å forhåndsdrøfte behandlingen med Datatilsynet.</p>");
        v5.SetValue("kategori", Udi(merkelapper["personvern"]));
        v5.SetValue("lenker", @"[{""tekst"": ""Datatilsynets DPIA-mal"", ""url"": ""https://www.datatilsynet.no/rettigheter-og-plikter/virksomhetenes-plikter/vurdere-personvernkonsekvenser"", ""ekstern"": true}]");
        v5.SetValue("rekkefolge", 5);
        SaveAndPublish(v5);

        var v6 = Create("veiledning", "Anskaffelse av KI-løsninger i offentlig sektor", parentId);
        v6.SetValue("tittel", "Anskaffelse av KI-løsninger i offentlig sektor");
        v6.SetValue("slug", "anskaffelse-av-ki-losninger-i-offentlig-sektor");
        v6.SetValue("innhold", @"<p>Innkjøp av KI-løsninger skiller seg fra tradisjonelle IT-anskaffelser.
Denne veiledningen hjelper offentlige innkjøpere med å stille de
riktige kravene og velge riktig anskaffelsesform.</p>
<h2>Forberedelse</h2>
<p>Kartlegg behovet grundig. Definer problemet dere ønsker å løse,
ikke teknologien. Involver fageksperter, IT og juridisk kompetanse
fra starten.</p>
<h2>Kravspesifikasjon</h2>
<p>Still krav til:</p>
<ul>
<li>Forklarbarhet: Leverandøren må kunne forklare modellens virkemåte</li>
<li>Dataeierskap: Virksomheten bør eie data og trente modeller</li>
<li>Testbarhet: Mulighet for uavhengig testing av modellen</li>
<li>Driftskostnader: Inkluder kostnader til oppdatering og overvåking</li>
</ul>
<h2>Anskaffelsesform</h2>
<p>Vurder innovasjonspartnerskap eller konkurransepreget dialog for
komplekse KI-anskaffelser der behovet ikke kan spesifiseres presist
på forhånd.</p>
<h2>Evaluering og oppfølging</h2>
<p>Etabler målbare kriterier for suksess. Avtal jevnlige evalueringspunkter
og mulighet for å justere kursen underveis.</p>");
        v6.SetValue("kategori", Udi(merkelapper["innkjop"]));
        v6.SetValue("lenker", @"[{""tekst"": ""Difis veileder for innovative anskaffelser"", ""url"": ""https://anskaffelser.no/innovasjon"", ""ekstern"": true}]");
        v6.SetValue("rekkefolge", 6);
        SaveAndPublish(v6);

        var v7 = Create("veiledning", "Etiske retningslinjer for bruk av KI", parentId);
        v7.SetValue("tittel", "Etiske retningslinjer for bruk av KI");
        v7.SetValue("slug", "etiske-retningslinjer-for-bruk-av-ki");
        v7.SetValue("innhold", @"<p>Etisk bruk av KI i offentlig sektor handler om mer enn å følge loven.
Det handler om å ivareta tillit, rettferdighet og menneskeverd. Denne
veiledningen hjelper virksomheter med å utarbeide egne etiske retningslinjer.</p>
<h2>Grunnleggende etiske prinsipper</h2>
<ul>
<li>Menneskesentrert: KI skal tjene mennesker, ikke omvendt</li>
<li>Rettferdig: Systemene skal ikke diskriminere eller forsterke ulikhet</li>
<li>Transparent: Beslutninger skal kunne forklares og etterprøves</li>
<li>Ansvarlig: Det skal alltid være klart hvem som er ansvarlig</li>
<li>Privat: Personvern skal ivaretas gjennom hele livssyklusen</li>
</ul>
<h2>Fra prinsipper til praksis</h2>
<p>Etiske prinsipper må omsettes til konkrete vurderingspunkter i
prosjektets ulike faser: utvikling, testing, utrulling og drift.
Bruk sjekklister og tverrfaglige vurderingsmøter.</p>
<h2>Etisk råd</h2>
<p>Vurder å opprette et internt etisk råd eller forum som kan gi
veiledning i vanskelige avveininger. Inkluder perspektiver fra
brukere, fageksperter og sivilsamfunn.</p>");
        v7.SetValue("kategori", Udi(merkelapper["etikk"]));
        v7.SetValue("lenker", @"[{""tekst"": ""EUs retningslinjer for pålitelig KI"", ""url"": ""https://digital-strategy.ec.europa.eu/en/library/ethics-guidelines-trustworthy-ai"", ""ekstern"": true}]");
        v7.SetValue("rekkefolge", 7);
        SaveAndPublish(v7);

        var v8 = Create("veiledning", "Krav til transparens og forklarbarhet", parentId);
        v8.SetValue("tittel", "Krav til transparens og forklarbarhet");
        v8.SetValue("slug", "krav-til-transparens-og-forklarbarhet");
        v8.SetValue("innhold", @"<p>Transparens og forklarbarhet er juridiske krav, men også en forutsetning
for tillit til KI i offentlig sektor. Denne veiledningen dekker
gjeldende regelverk og praktiske tilnærminger.</p>
<h2>Juridisk rammeverk</h2>
<p>Forvaltningsloven krever begrunnelse av vedtak. GDPR gir rett til
informasjon om automatisert behandling. AI Act stiller krav til
dokumentasjon og brukerinformasjon for høyrisiko-systemer. Til sammen
skaper dette et sterkt krav om forklarbarhet.</p>
<h2>Nivåer av forklarbarhet</h2>
<ul>
<li>Systemdokumentasjon: Teknisk beskrivelse av modellens virkemåte</li>
<li>Beslutningsforklaring: Hvorfor ble akkurat dette resultatet gitt?</li>
<li>Brukerinformasjon: Forenklet informasjon tilpasset den berørte</li>
</ul>
<h2>Tekniske verktøy</h2>
<p>SHAP-verdier, LIME, kontrafaktiske forklaringer og beslutningstrær
er eksempler på teknikker som kan gjøre KI-modeller mer tolkbare.
Valg av teknikk avhenger av modelltype og brukerkontekst.</p>
<h2>Organisatoriske grep</h2>
<p>Dokumenter modellens virkemåte under utvikling. Etabler rutiner
for å gi forklaringer til berørte parter, og tren saksbehandlere
i å tolke og kommunisere KI-output.</p>");
        v8.SetValue("kategori", Udi(merkelapper["transparens"]));
        v8.SetValue("lenker", @"[{""tekst"": ""Nasjonal strategi for kunstig intelligens"", ""url"": ""/artikler/ny-nasjonal-strategi-for-kunstig-intelligens"", ""ekstern"": false}]");
        v8.SetValue("rekkefolge", 8);
        SaveAndPublish(v8);
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
}
