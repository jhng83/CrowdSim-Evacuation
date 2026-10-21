# Unity presentation design

User requirement, continued from M7/M8: professional, intuitive presentation with
user-centred colours and typography in subsequent milestones. Keep these choices
consistent through diagnostics, recording and final acceptance.

- Bundle **Atkinson Hyperlegible Regular** (Braille Institute of America, 2020) for
  startup, workspace and world labels. Distinct letter/number shapes aid inspection.
  Body/control text is normally 14–16 px; secondary labels are at least 13 px;
  headings establish hierarchy. Do not depend on a font installed on the user's PC.
- Graphite surfaces reduce visual clutter. Pale ink supplies readable contrast;
  restrained blue marks actions, emerald the Fume workflow, amber event/readiness.
  Primary-button solid text/normal/hover/focus combinations pass at least 4.5:1.
  Static checks cover selected authored colour pairs; local review must check actual
  glyph rendering, wrapping, disabled/focus states, lighting and translucent overlays.
- Keep state meanings stable: green evacuating, amber waiting/reaction, blue injured,
  yellow crawling, red fallen, dark red temporary/permanent incapacity, grey fatal,
  white normal. Panic uses a separate orange ring and numeric selected readout.
  Never tint state colour to represent panic or assistance.
- Legends pair swatches with normal high-contrast words. Selected labels name the
  state and ID; low/kneeling poses convey posture; `! ALARM`, `GOING TO HELP`,
  `+ HELPING` and target IDs add text/symbol cues. Temporary/permanent incapacity
  deliberately share the reference colour/pose and are distinguished by their text.
  Colour alone must not be the only way to interpret a critical state.
- Keep keyboard focus visible, actions labelled, dense information scrollable and
  the inspector optional. Selection retains the user's hidden-panel choice.
  Follow/POV always display the mode and an in-viewport way to leave it.
- Humanoids stay volumetric at every zoom. Added detail is cosmetic; rendered
  picking and animation do not affect physical hitboxes, time, randomness or saves.

Font asset: `UnityProject/Assets/CrowdSim/Resources/CrowdSim/Fonts/AtkinsonHyperlegible-Regular.ttf`.
Source: [Google Fonts mirror](https://github.com/google/fonts/tree/main/ofl/atkinsonhyperlegible).
Unmodified downloaded regular TTF SHA-256:
`7fb917c89019896d0b52ee84b7cbb3304c18cb90b19a62f5e32712bd23e97669`.
The complete SIL Open Font License 1.1 and copyright are bundled beside it in
[OFL.txt](../../UnityProject/Assets/CrowdSim/Resources/CrowdSim/Fonts/OFL.txt).
Procedural human/furniture geometry, surfaces and the outline shader are project
source; no external model, material, font package or new NuGet/UPM dependency is used.
The project does not claim complete accessibility certification from these checks.
