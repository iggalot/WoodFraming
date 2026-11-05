# Wood Framing: notes for assistants

- **Read first:** `..\DesignHub\docs\SUITE.md`: all seven repos, how they fit, the branches, the build, and **the
  owner's rules** (local commits only, no installs, stay in the project folders, never click the owner's windows).
- **Branch:** `dev/ui-pass-2`.
- **What this is:** a UI-less library for what is not shear-wall design, kept out of the Shear Wall Calculator so it
  does not bloat:
  - wall framing (`WallFraming`);
  - stud design (`StudDesign`: axial loads, every ASD combination, CD);
  - sill anchorage (`SillAnchorage`).

  Its data lives in the building model (stud size and spacing per wall and story, with the defaults in
  `ModelDefaults`). The design values come from `..\NDS_WoodLibrary`.
- `WoodFraming.Wpf` (Framing3D) is a work in progress.
- **Build:** `MSBuild WoodFraming.sln -restore`.
- **Test:** `vstest WoodFraming.Tests\bin\Debug\net472\WoodFraming.Tests.dll`. Each check is tested against a hand
  calculation.
- **The suite's TODO list** is `..\ShearWallCalculator\ROADMAP.md`.
