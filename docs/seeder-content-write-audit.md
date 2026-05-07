# Seeder content-write audit

Inventory of every place CMS code writes content NODES (not just types/schema). Goal: by launch, all of these go away or are clearly fenced off so editor changes can never be silently overwritten.

Generated 2026-05-04.

## Categories

- **A. Schema-only**: writes content TYPES, property groups, allowed children. These mostly live in `ContentTypeComposer.cs`. Keeping these is fine; they don't touch user content.
- **B. Structure migration on existing nodes**: modifies/moves/deletes EXISTING content nodes. These run on every startup and have caused incidents. Each should self-disable once its job is done.
- **C. Content node creation**: creates NEW content nodes. These should only run on fresh dev installs, never on prod.

## Category B — structure migrations that touch existing nodes

All in `ContentSeeder.RunStructureMigrations()`, run unconditionally on every CMS startup (including prod).

| Method | What it does | Status / risk |
|---|---|---|
| `EnsureCaserFolderExists` | Creates "Caser" folder at root if missing | **Writes content.** Conceptually a one-time migration; should self-disable once Caser exists everywhere. |
| `ForceForsideToTop` | Re-sorts root content so Forside is first | Reorders existing nodes only. Idempotent. Low risk. |
| `RemoveIkonerContent` | Moves Ikoner container to recycle bin if it exists | One-time migration. Safe to remove now (no Ikoner exists anywhere). |
| `RenameVeiledningerToVeiledning` | Renames container | One-time. Safe to remove now. |
| `RenameFaqContainerNode` | Renames "FAQ" → "Ofte stilte spørsmål" | One-time. Safe to remove now. |
| `MoveOmOssIntoSider` | Moves Om Oss node from root into Sider container | One-time. Safe to remove. |
| `ClearFaqKategoriReferences` | Empties faq.kategori property values to allow merkelapp deletion | One-time. Safe to remove. |
| `MoveVeiledningOversiktUnderVeiledning` | Moves a node | One-time. |
| `FlattenVeiledningOversiktIntoContainer` | Moves Oversikt fields onto Veiledning container, then `MoveToRecycleBin(oversikt)` | One-time. Has been safer since recycle-bin switch. |
| `NestVeiledningStegUnderGuide` | Moves veiledningSteg nodes under their parent guide | One-time. |
| `FlattenOmOssSeksjonerToBlocks` | Reads omOssSeksjon child nodes, packs them as blocks on parent, recycles originals | One-time. Recycle bin switch makes safer. |
| `MigrateEksempelToCase` | Reads each eksempel, creates an equivalent case under Caser, recycles eksempel container | One-time. **Has the same shape as the Sandkasse-deletion bug** but is gated correctly (only deletes the eksempler container itself, not editor-moved content). |
| `FixBakgrunnDropdownValues` | Walks every artikkel + case, fixes `bakgrunn` property if it's a plain string | One-time data fix. Safe to remove once we know all content is JSON-formatted. |
| `EnsureSandkasseExistsForDev` | Creates Sandkasse under Sider if missing AND `LAUNCH_MODE != production` | **Dev only by gating.** Doesn't run on prod. Still: violates the new principle. |

**Recommendation pre-launch**: confirm each one-time migration has run successfully on prod, then DELETE them. The seeder file should shrink to almost nothing.

## Category C — content node creation (initial seed)

In `ContentSeeder.InitializeAsync`, gated by `existing.Any(c => c.ContentType.Alias == "forside")`. Skipped on prod via `LAUNCH_MODE=production`. Only runs on **fresh local dev install**.

These methods all create demo content from scratch:

- `SeedForside` — creates root Forside with hero text, sections
- `SeedOmOss` — creates Om Oss page with 3 seksjon blocks
- `SeedSandkasse(siderFolderId)` — creates Sandkasse under Sider with placeholder blocks
- `SeedVeiledningOversikt` — creates Veiledning Oversikt
- `SeedMerkelapper(parentId)` — creates ~12 merkelapper
- `SeedArticles(parentId)` — creates 11 artikkel nodes
- `SeedPages(parentId)` — creates "Kontakt" side
- `SeedExamples(parentId)` — creates eksempel nodes (legacy, since migrated)
- `SeedCases(parentId)` — creates 4 case nodes under Caser
- `SeedVeiledninger(parentId)` — creates the veiledning guide + ~16 steg
- `SeedFAQ(parentId, merkelapper)` — creates ~10 FAQ items
- `SeedOrdbokOppslag(parentId)` — creates ~190 ordbok entries
- `SeedMedia` — creates a Media folder + uploads N seed images

Plus `MigrateOrdbokOppslag` — runs ALWAYS (even on prod), creates the ordbokSamling folder + all 190 entries IF ordbokSamling doesn't exist at root. **This violates the principle.** It was added so the ordbok content would land on prod without re-running the full seeder. Should now be considered "one-time migration done" and removed.

## What to do

### Now (this PR)

- [ ] Audit document committed (this file)
- [ ] No code changes yet — just visibility

### Before launch

- [ ] Verify on prod: Caser folder exists, no eksempler, no Ikoner, faqSamling renamed, all artikler have `bakgrunn` JSON-formatted
- [ ] Once verified, delete:
  - `EnsureCaserFolderExists`
  - `RemoveIkonerContent`
  - `RenameVeiledningerToVeiledning`, `RenameFaqContainerNode`
  - `MoveOmOssIntoSider`, `ClearFaqKategoriReferences`
  - `MoveVeiledningOversiktUnderVeiledning`, `FlattenVeiledningOversiktIntoContainer`, `NestVeiledningStegUnderGuide`
  - `FlattenOmOssSeksjonerToBlocks`
  - `MigrateEksempelToCase`
  - `FixBakgrunnDropdownValues`
  - `EnsureSandkasseExistsForDev`
  - `MigrateOrdbokOppslag`
- [ ] Consider deleting all `SeedXxx` methods + the `InitializeAsync` seed flow entirely. Fresh local installs would then start with an empty Umbraco, and the developer would create test content via the editor (the same way Sara would). Cuts ~1500 lines of code and eliminates the entire class of "seeder broke prod" bugs.

### Going forward

- New "data" needs go through the editor, not code
- Schema changes (content types, property groups, allowed children) stay in `ContentTypeComposer.cs` — they're necessary
- Content type schema migrations (rename a property, change a data type) are OK as code, but DON'T let them touch content nodes — let editors fix any data issues manually post-migration

## Why this matters

Both Sandkasse incidents and the original Caser-folder-missing problem came from this code path. The pattern is always: "I'll write a clever migration that detects and fixes X" → assumption is wrong → live editor content gets modified or deleted → no recycle bin recovery (until we fixed that) → data loss.

Not having any code that writes content nodes makes that whole class of bug impossible.
