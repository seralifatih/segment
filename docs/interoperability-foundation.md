# Interoperability Foundation

## Intent
Establish API-first interoperability groundwork so Segment can augment CAT/TMS workflows without becoming a full CAT replacement.

## Contract Boundaries

### Connector contract
- Interface: `Segment/Services/IInteroperabilityConnector.cs`
- Responsibilities:
  - advertise supported formats (`SupportedFormats`)
  - declare import/export capability (`CanImport`, `CanExport`)
  - map external files to `TermEntry` and back
- Current concrete adapter:
  - `Segment/Services/TmxInteroperabilityConnector.cs` (`ConnectorId = tmx-file`)

### Connector registry
- `Segment/Services/InteroperabilityConnectorRegistry.cs`
- Resolves import/export connector by format (e.g., `tmx`).
- Keeps extension point stable for future connectors (memoQ/Trados/Smartcat adapters).

### Interop orchestration service
- `Segment/Services/InteroperabilityService.cs`
- Format routing:
  - `tmx` -> connector registry
  - `glossary-json` -> `GlossaryJsonInteropService` tooling path
- API surface remains narrow:
  - `ImportTerms(...)`
  - `ExportTerms(...)`
  - `ApplyExternalProjectMapping(...)`

## Import/Export Improvements

### Translation memory-friendly
- Added TMX export service: `Segment/Services/TmxExportService.cs`
- Added TMX connector adapter: `Segment/Services/TmxInteroperabilityConnector.cs`
- TMX import now keeps language metadata in term entries:
  - `TermEntry.SourceLanguage`
  - `TermEntry.TargetLanguage`

### Glossary tooling path
- Existing JSON glossary import/export remains explicit tooling through `GlossaryJsonInteropService`.
- Runtime storage remains SQLite-primary (no change in primary persistence).

## Project Profile External Mapping
- Model extension in `Segment/Models/ProjectNicheConfiguration.cs`:
  - `ExternalProjectProfileMapping` with:
    - `ConnectorId`
    - `ExternalProjectId`
    - `ExternalClientId`
    - `ExternalStyleGuideId`
    - `ExternalTags`
    - arbitrary metadata dictionary
- Persist/update boundary:
  - `INicheTemplateService.SaveProjectConfiguration(...)`
  - implemented in `NicheTemplateService`

This lets Segment map client/style metadata from external systems into project context without introducing shared workspace features.

## Testing Coverage
- Integration mapping integrity:
  - `Segment.Tests/InteroperabilityFoundationIntegrationTests.cs`
    - TMX export/import round-trip integrity
    - external profile mapping persistence integrity
- Regression coverage for existing import behavior:
  - `Segment.Tests/TmxImportRegressionTests.cs`

## Scope Guardrails
- No CAT editor, assignment workflow, or collaboration workspace features added.
- Connector layer is intentionally minimal and file/API driven for safe incremental expansion.
