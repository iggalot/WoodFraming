# Wood Framing: notes for assistants

- **Read first:** `..\DesignHub\docs\SUITE.md`: all seven repos, how they fit, the branches, the build, and **the
  owner's rules** (local commits only, no installs, stay in the project folders, never click the owner's windows).
- **Branch:** `dev/ui-pass-2`.
- **What this is:** a UI-less library for what is not shear-wall design, kept out of the Shear Wall Calculator so it
  does not bloat:
  - wall framing (`WallFraming`);
  - stud design (`StudDesign`: axial loads, every ASD combination, CD);
  - sill anchorage (`SillAnchorage`);
  - an upper story's sill nailing through the floor sheathing (`SillNailing`: TR12 with the sheathing as the gap, the
    owner's choice).
  - a double top plate as a diaphragm collector (`TopPlateCollector`: one ply in tension and compression, the splice
    nails).
  - a floor's rim as the collector where no wall is under the line (`RimCollector`: one ply of the joists' depth,
    `ModelDefaults.FloorJoistSize`; a strap across each butt splice for the whole force), and the roof's continuous
    blocking the same where no wall is under the roof's line or edge (member "blocking", `ModelDefaults.RoofBlockingSize`;
    a strap across every joint; `BlockingCitations`). The strap and the framing plates (LTP4) are picked from the Simpson catalog in
    the Shear Wall Calculator (`SpliceStrap`, `TopClip`): this library holds what was picked (`DiaphragmLine.Strap`,
    `DiaphragmChord.Strap`, `TopConnection.Clip`, `Tie`).
  - the rim or the roof's blocking toe-nailed into a wall's top plates (`RimToPlate`: NDS Figure 12A's geometry for the
    yield equations and Ctn 0.83, the owner's choice).

  Its framing data lives in the building model (stud size and spacing per wall and story, with the defaults in
  `ModelDefaults`). The design values come from `..\NDS_WoodLibrary`.
- `WoodFraming.Connections`: **the connection details, kept out of the building model** (owner 2026-11-07: the model
  is the structure and geometry; the details that hold it together are separate, else it becomes a god class). It
  references only BuildingModel.Core; the model never references it. Two sections of the project file, keyed by the
  model's ids:
  - `ConnectionsSection` ("Connections", written by the Shear Wall Calculator, in its `OwnsData`): each shear wall
    story's sheathing nailing (`WallNailing`, a `FaceNailing` per face in the order of the model's
    `ShearWallDesign.Faces`; the sheathing itself is the model's), its sill (`SillConnection`: the first story's
    `SillAnchors`, an upper story's `SillNails`), its top (`TopConnection`: the rim or blocking toe-nailed into its plates)
    and each diaphragm's
    lines of shear walls (`DiaphragmLine`: line shear, boundary nailing, collector) and chords (`DiaphragmChord`, span by
    span between the lines, with its two field nailing zones; `ChordsOn` a wall's). `Describe` and `Describer` give a
    `ShearPair` its connector in words (for `ShearPairs.Of`).
  - `ConnectionDefaults` ("ConnectionDefaults"): the floor and roof sheathing, nails, blocking, framing width, sill
    nail, splice nail, toe-nail. `Start()` is the only place the starting values are written; a value missing from the file
    keeps its starting value. Edited in `WoodFraming.Wpf.ConnectionDefaultsWindow` (part of the connection details, owner
    2026-11-08), which the Shear Wall Calculator opens (Details > Connection defaults) and saves with its own data.
  - The forces are not here: the hold-downs and pairs stay in the model's `Lateral` section, since they carry the load
    through the building.
  - A new project in an SDK solution here restores offline: `-restore -p:RestoreSources=<an empty folder>` (it has no
    packages).
- `WoodFraming.Wpf`: the 3D framing (`Framing3D`), and **the wall window of every app**: `WallView` (plain data,
  made from the model, its `Lateral` section and the `Connections` (the face nailing, the sill's bolts) by `WallViews.FromModel`), `WallElevation`, `WallModel3D`, `WallPanel`
  (Elevation and 3D, the **Shear wall** toggle, on by default) and `WallWindow`. The Building Model opens `WallWindow`;
  the Shear Wall Calculator's construction window hosts `WallPanel` and adds its own tabs. They draw the data; they
  calculate nothing. A change to how a wall is drawn goes here, for every app.
- **Build:** `MSBuild WoodFraming.sln -restore`.
- **Test:** `vstest WoodFraming.Tests\bin\Debug\net472\WoodFraming.Tests.dll`. Each check is tested against a hand
  calculation.
- **The suite's TODO list** is `..\ShearWallCalculator\ROADMAP.md`.
