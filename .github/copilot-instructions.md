# Copilot Instructions

## Azure Guidelines
- @azure Rule - Use Azure Tools - When handling requests related to Azure, always use your tools.
- @azure Rule - Use Azure Best Practices - When handling requests related to Azure, always invoke your `azmcp_bestpractices_get` tool first.
- @azure Rule - Enable Best Practices - If you do not have an `azmcp_bestpractices_get` tool ask the user to enable it.

## NX-SWITE Guidelines
- Prioritize autonomy and low maintenance over exhaustive security hardening.
- The application is a tool for updating the CFW and preparing microSD; prefer simple and low-cost solutions.
- Keep all source files in UTF-8 (see `.editorconfig`); never rewrite files with another encoding or duplicate the BOM.
- Keep the build free of CS warnings and run `dotnet test` (NX-Suite.Tests, xUnit) after changes to `Core/` logic.
- `catalogo/catalogo.legacy.json` is a frozen reference copy of the Gist; the app still reads the remote Gist. Do not edit it by hand.
- Update `CODEBASE_INDEX.md` when adding, moving or removing relevant files.
