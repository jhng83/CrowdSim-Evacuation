# Unity presentation design

User requirement, continued from M7–M9: professional, intuitive presentation with
user-centred colours and typography in subsequent milestones. Keep these choices
consistent through diagnostics, recording and final acceptance.

- Bundle **Atkinson Hyperlegible Regular** (Braille Institute of America, 2020) for
  startup, workspace and world labels. Distinct letter/number shapes aid inspection.
  Body/control text is normally 14–16 px; secondary labels are at least 13 px;
  headings establish hierarchy. Do not depend on a font installed on the user's PC.
- M9 explicitly changes text to **black**, including hazard/agent legends, world
  labels, input/hover/focus states and validation panels. Warm pale surfaces support
  readable contrast; restrained pale blue marks actions, pale emerald the Fume
  workflow, pale amber event/readiness. Coloured swatches retain state meaning.
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
- Square and circular authored blockages represent off-white architectural pillars:
  use lit mineral surfaces with black feature edges/silhouettes and preserve exact
  metric dimensions. Distinguish existing blue internal walls. Use an off-white
  floor, subtle finish and default-on 1.2 m grid. Its visibility remains adjustable
  on Debug. Grid/outline geometry never becomes model collision geometry.
  The workspace root stays transparent; only actual panels have opaque surfaces.
- On the light floor, selection uses a black ring plus black ID/state text on a
  pale label plate. Panic remains orange; bodies retain their state palette and
  dark outlines. Planning people also retain silhouettes for contrast.
- Humanoids stay volumetric at every zoom. Added detail is cosmetic; rendered
  picking and animation do not affect physical hitboxes, time, randomness or saves.

Font asset: `UnityProject/Assets/CrowdSim/Resources/CrowdSim/Fonts/AtkinsonHyperlegible-Regular.ttf`.
Source: [Google Fonts mirror](https://github.com/google/fonts/tree/main/ofl/atkinsonhyperlegible).
Unmodified downloaded regular TTF SHA-256:
`7fb917c89019896d0b52ee84b7cbb3304c18cb90b19a62f5e32712bd23e97669`.
The complete SIL Open Font License 1.1 and copyright are bundled beside it in
[OFL.txt](../../UnityProject/Assets/CrowdSim/Resources/CrowdSim/Fonts/OFL.txt).
Procedural human/furniture geometry, surfaces and the outline shader are project
source; no external model, material, font package or new third-party NuGet/UPM dependency is used.
The project does not claim complete accessibility certification from these checks.

M10 continues these choices in Recording and stage controls. The single stage has
labelled metre fields and dynamic room-based maximum guidance; rejected edits retain
the last valid footprint and explain the conflict. A matte charcoal raised deck,
slim metal front lip, twin-column truss, LED blue/purple equalizer backdrop, speaker
stacks and warm lamp lenses establish a concert identity without obscuring metres
or introducing an off-footprint platform. The rig is procedural source and remains
visual; the deck's original low-obstacle rules are preserved. Recording state is
always labelled in the header, with path/folder, frames/drops/queue/capture/encode
values together on the scrollable Recording page. Text remains black on pale panels.
Only Unity's built-in imageconversion/screencapture modules are added for capture;
there is no added third-party asset or runtime codec dependency.
