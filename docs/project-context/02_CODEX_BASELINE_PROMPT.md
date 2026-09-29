# Prompt اولیه برای Codex — MakanApp Baseline

این متن را بعد از قرار دادن پوشه `docs/codex-context` در Repository به Codex بده.

---

You are working on the existing MakanApp frontend repository.

Repository:
https://github.com/RastinaxAI/MakanApp-Client.git

Before changing any code, read:

1. AGENTS.md and repository instructions, if present.
2. docs/codex-context/00_CODEX_START_HERE.md
3. docs/codex-context/01_SQLSERVER_ARCHITECTURE_OVERRIDE.md
4. docs/codex-context/02_Makan_Architecture_v3_Original_MySQL_Reference.pdf
5. docs/codex-context/03_Makan_User_Flows_and_Screens_UX_v1.0.pdf
6. docs/codex-context/04_Flow.png
7. docs/codex-context/05_Makan_Project_Status.pdf
8. The repository README, package manifests, lockfiles, current OpenAPI contract, sandbox, tests, and actual source code.

Important source-of-truth rule:

- SQL Server is the required primary database.
- All MySQL-specific decisions in the architecture PDF are superseded by `01_SQLSERVER_ARCHITECTURE_OVERRIDE.md`.
- Do not install or configure MySQL.
- Do not rewrite the existing frontend from scratch.
- Do not change .NET/React/React Native/Expo/Tailwind major versions without explicit approval.
- If actual code conflicts with documentation, report the mismatch before changing architecture.
- Sandbox/mock behavior is not production backend behavior.

For this first pass, DO NOT implement the production backend yet.

Your task is baseline inspection only:

1. Map the actual repository structure.
2. Identify package manager/workspace layout.
3. Inspect apps/web, apps/mobile, packages/core, apps/sandbox and tests if they exist.
4. Find the OpenAPI contract and describe its current scope.
5. Run the documented install/build/typecheck/test commands that are safe and non-destructive.
6. Report the exact results; do not claim tests passed unless they actually ran.
7. Map existing frontend routes/screens to the UX identifiers from the UX document:
   - C-*
   - MSG-*
   - AI-*
   - ED-*
   - ST-*
   - TE-*
   - PA-*
   - MG-*
   - KN-*
   - WEB-*
   - BO-*
8. Separate each screen into:
   - Implemented
   - Partially implemented
   - Mock/static only
   - Missing
9. Identify where current frontend business rules are only client-side and therefore must move to or be revalidated by the production backend.
10. Identify current sandbox endpoints that should become real ASP.NET Core endpoints.
11. Identify all assumptions in the frontend that conflict with SQL Server or the corrected backend architecture.
12. Do not delete, move or mass-rename existing files.
13. Do not commit, push, publish or deploy.

At the end, produce:

- Current repository map
- Build/test results
- UX screen coverage matrix
- Current API/sandbox contract summary
- Security/business rules currently trusted to the client
- Architecture mismatches
- Backend integration gaps
- Blocking decisions
- A proposed first backend vertical slice
- Files you would change in the next step

Then STOP and wait for approval.
