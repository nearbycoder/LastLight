# Third-party notices

Everything in Last Light that you see or hear was made for it by script: the models are
generated with Blender's Python API (`ArtSource/blender/`), the sounds, voices and music are
synthesized with numpy and scipy (`ArtSource/audio/`), and the map and missions are generated
from `ArtSource/map/`. There are no stock, sampled or downloaded art or audio assets.

The exceptions are the fonts, and the engine and libraries the game is built with.

## Fonts (in this repository and in the built game)

| Font | Copyright | License | License text |
|---|---|---|---|
| Cormorant Garamond (Medium, SemiBold, Bold) | © 2015 The Cormorant Project Authors ([CatharsisFonts/Cormorant](https://github.com/CatharsisFonts/Cormorant)) | SIL Open Font License 1.1 | [`Assets/Resources/Fonts/OFL-Cormorant.txt`](Assets/Resources/Fonts/OFL-Cormorant.txt) |
| Alegreya Sans (Regular, Medium, Bold, Italic) | © 2013 The Alegreya Sans Project Authors ([huertatipografica/Alegreya-Sans](https://github.com/huertatipografica/Alegreya-Sans)) | SIL Open Font License 1.1 | [`Assets/Resources/Fonts/OFL-AlegreyaSans.txt`](Assets/Resources/Fonts/OFL-AlegreyaSans.txt) |
| Special Elite | © 2010 Brian J. Bonislawsky DBA Astigmatic (AOETI) | Apache License 2.0 | [`Assets/Resources/Fonts/LICENSE-SpecialElite.txt`](Assets/Resources/Fonts/LICENSE-SpecialElite.txt) |

The trailer (`docs/media/LastLight_trailer.mp4`) and the README media use the same three fonts.

## Engine and packages (fetched by Unity, not stored in this repository)

- **Unity 6000.6.2f1** and its packages (Universal Render Pipeline 17.6, Input System 1.20,
  uGUI 2.6, Test Framework 1.8 and their dependencies), under the
  [Unity Terms of Service](https://unity.com/legal/terms-of-service) and the
  [Unity Companion License](https://unity.com/legal/licenses/unity-companion-license).
  The Linux build contains the Unity player runtime, distributed under Unity's terms.
- **Newtonsoft.Json** (`com.unity.nuget.newtonsoft-json`, pulled in by the `com.unity.pipeline`
  editor tooling package), MIT License, © James Newton-King. It ships in the Linux build as
  `LastLight_Data/Managed/Newtonsoft.Json.dll`.
- **libdecor** (Wayland window decorations, shipped next to the Linux player as
  `libdecor-0.so.0` and `libdecor-cairo.so`), MIT License.

## Tools used to make the assets (not distributed)

Blender 4.5 (GPL; its output is not covered by the GPL), Python with numpy and scipy (BSD), Pillow
and matplotlib (for the review chart and the trailer's captions), and FFmpeg (for the trailer and
gameplay captures).
