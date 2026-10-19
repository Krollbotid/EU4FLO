## **DISCLAIMER**

This app is **almost totally vibecoded** because in understanding C# I'm just little better than Patrick.
![Patrick with a board on his head.jpg](Images/Patrick.jpg)

Though I'll try to fix every issue to be found.

---

# EU4 Fortress Layout Optimizer

[🇷🇺 Русский](README.ru.md) | 🇬🇧 English

A lightweight Windows desktop tool for inspecting and planning with the province map of **Europa Universalis IV** and its mods (including total-conversion mods such as Anbennar).

The viewer loads the `map/` folder of the game, renders it in several grouping modes, computes land adjacency, and offers a **minimum dominating set** solver to plan a minimal set of forts that covers every selected province.

> **Legal notice:** this program does not ship any assets from EU4. It only reads files from your own installation of the game. EU4 and all of its content are property of Paradox Interactive. See [License](#license).

---

## Features

- **Map modes**
  - Provinces — the raw `provinces.bmp` palette
  - Area / Region / Superregion — stable colours for each grouping level
  - Forts mode — green = fort, hatched green = covered by neighbour fort, blue = water, black = wasteland, thin black borders between provinces
  - Optimization — the result of the fort-planner, plus a non-destructive preview layer
- **Interactive canvas** — mouse-wheel zoom around the cursor, pan with the middle / right mouse button, click a province to open its info panel.
- **Province info panel** — id, name, area / region / superregion, whether a fort is present, list of land neighbours. Water and wasteland provinces are marked accordingly and have no fort checkbox or neighbour list.
- **Fort editing** — toggle a fort in `forts.txt` directly from the info panel; the file is rewritten on every change.
- **Adjacency export** — writes a plain-text adjacency matrix grouped by Superregion → Region → Area → Province. Wastelands are exported as `id : wasteland`.
- **Fort optimization** — tool to find optimal fort layout with:
  - coverage set (provinces / areas / regions / superregions)
  - required forts (must be built)
  - preferred forts (tie-breaker for multiple minimum solutions)
  - exceptional provinces for *Homebrew* adjacency mode[^1]
  - group constraints (`Group_Name : p1, p2, ... : MinForts`)
  - three adjacency modes: **NoAdjacent** / **Homebrew** / **AnyAdjacent**
  - save / load configuration to JSON
- **Province tree** — hierarchical view from superregion down to province, click a province in tree to select the province in the info panel.
- **PNG export** — save the whole map or just the currently visible viewport.
- **Localization** — UI available in Russian and English out of the box; any new language can be added by dropping a JSON file into `locales/` (no rebuild needed).

[^1]: Homebrew submod for Anbennar (and maybe some other mods) adds penalty for having forts in neighbour provinces with exception of several province groups where you have to have adjacent forts foc completing missions (such as White Walls of Castanor in Anbennar mod)

## Differences from the Python version

This is a complete rewrite of the earlier [EU4-fort-optimizer](https://github.com/Krollbotid/EU4-fort-optimizer) script.

| Aspect | Python version (previous) | C# version (this repo) |
| :--- | :--- | :--- |
| **Input** | Manual graph text file or in‑app text area. | Direct loading of `provinces.bmp`, `definition.csv`, `area.txt`, etc. from the game/mod folder. |
| **Map visualisation** | Abstract graph rendered with `matplotlib` (nodes and edges). | Real EU4 province map rendered via GDI+ with zoom, pan, and grouping modes. |
| **Solver** | Exact ILP model (PuLP) — finds the true minimum dominating set for small/medium graphs. | Greedy + pruning heuristic — fast on full‑map scale (thousands of provinces), but may not be strictly optimal. |
| **Adjacency** | Taken from the manually provided graph edges. | Auto‑computed from `provinces.bmp` pixel adjacency + `adjacencies.csv` (straits, canals) + `climate.txt` (wastelands) + `default.map` (water). |
| **Workflow** | Abstract planning: build graph, configure constraints, solve, validate. | Visual planning: load map, click provinces for info, see previews, run solver, export result. |
| **Manual validation** | Yes — validate any set of forts. | Not directly, but the map colours show the solution. |
| **Export** | JSON configuration. | JSON configuration, adjacency matrix (TXT), map/view images (PNG). |
| **Platform** | Cross‑platform (Python 3.8+). | Windows 10/11 (x64), single‑file .exe, no runtime installation required. |
| **UI framework** | `matplotlib` canvas. | Windows Forms. |

*If you don't like this version of program you can use it to build adjacency matrix and load it to python version.*

## Requirements

- Windows 10 or 11 (x64)
- .NET 10 Desktop Runtime *(only if you use the framework-dependent build; the self-contained build from `dotnet publish` needs nothing)*
- An installed copy of **Europa Universalis IV** or a mod that ships a `map/` folder

## Installation

### Option 1 — portable binary (recommended for end users)

Grab the latest release archive, unpack it anywhere, run `EU4FLO.exe`.

```bash
EU4FLO.exe
locales/
├── en.json
└── ru.json
```

No installer, no .NET installation, no registry keys.

### Option 2 — build from source

Requires pre-installed .NET 10.0

```bash
git clone https://github.com/Krollbotid/EU4FLO.git
cd EU4FLO
dotnet publish -c Release -o publish
```

The publish folder will contain a single-file `EU4FLO.exe` and `locales/`.

## Usage

1. Launch the program.
2. Click **Load map/ folder** and pick the `map/` folder of EU4 or of a mod (e.g. `…/Europa Universalis IV/common/map` or `…/mod/Anbennar/common/map`).
3. Choose a map mode in the toolbar.
4. Click a province on the map to open the info panel on the right.

### Loading files

The viewer reads the following files from the selected folder, and degrades gracefully if any of them are missing:

| File                | Used for                                          |
| ------------------- | ------------------------------------------------- |
| `provinces.bmp`     | pixel-to-province mapping                         |
| `definition.csv`    | province ids, colours, names                      |
| `area.txt`          | province → area                                   |
| `region.txt`        | area → region                                     |
| `superregion.txt`   | region → superregion                              |
| `default.map`       | `sea_starts` and `lakes` (water provinces)        |
| `climate.txt`       | `impassable` block (wasteland provinces)          |
| `adjacencies.csv`   | sea / canal / strait links between provinces      |
| `forts.txt`[^2]     | one province id per line — provinces with a fort  |

[^2]: Custom file containing id of provinces with a fort, содержащий id провинций. In future there is plan to add alternative way - reading provinces files from `history/` folder В будущем планируется добавить альтернативный способ - чтение файлов провинций из папки history/

### Exporting the adjacency matrix

Click **Export adjacency**, choose a destination `.txt` file. The output groups provinces by superregion → region → area and looks like:

```bash
# bulwar_superregion
    # bahar_region
        # crathanor_area
            516 : 515, 517
            517 : 515, 516, 518, 519
        ...
# (provinces not assigned to any area)
    6749 : wasteland
```

### Fort optimization

1. Click **Optimize forts** in the toolbar. The map switches to the *Optimization* mode.
2. Fill in the fields:
   - **Coverage provinces** — one selector per line:
     `province, Name_or_ID` · `area, Area_Name` · `region, Region_Name` · `superregion, Superregion_Name`
   - **Required forts** — ids separated by commas or spaces
   - **Preferred forts** — ids; used only as a tie-breaker
   - **Exceptional (homebrew)** — ids that are allowed to have a fort adjacent to another fort
   - **Groups** — `Group_Name : p1, p2, … : MinForts`
   - **Fort Adjacency mode** — one of *NoAdjacent*, *Homebrew*, *AnyAdjacent*
3. The **Show on map** button under each field previews that parameter on the map (non-destructive — previews stack, running the solver clears them).
4. Click **Run optimization**. The result is drawn with colours that reflect the role of each fort:

- 🔴 red = required,
- 🟠 orange = preferred,
- 🟣 purple = exceptional,
- 💗 pink = inside a group,
- 🟡 yellow = default.

Provinces without a fort but adjacent to a solution fort are hatched 🟢 green.
5. **Save config** / **Load config** store the whole form as JSON.

### Province tree

Click **Province tree** to open a docked tree of `superregion → region → area → province`. Water and wasteland provinces are suffixed with `[water]` / `[wasteland]`. Clicking a province opens the same info panel as clicking the map.

### Saving images

Two toolbar buttons:

- **Save map (PNG)** — the whole rendered map, at native resolution.
- **Save view (PNG)** — only what you currently see on screen (zoom + pan applied).

## Adding a new language

1. Copy `locales/en.json` to `locales/<code>.json`, e.g. `locales/de.json`.[^3]
2. Change `_language_name` to `Deutsch` and translate the values.
3. Restart the app (or just switch the language in the toolbar — no rebuild needed).

Keys are flat JSON strings. Missing keys fall back to English, then to the key name itself.

[^3]: Deutsch language

## Project layout

```bash
EU4FLO/
├── Models/                Plain data classes (Province, Area, Region, Superregion)
├── Parsers/               Clausewitz / CSV / BMP readers
├── Rendering/             MapRenderer, colour assignment
├── Export/                Adjacency builder and text exporter
├── Optimization/          Dominating-set solver, selector parser, config model
├── Localization/          Loc static class, reads locales/*.json
├── UI/                    MapCanvas, ProvinceInfoForm, OptimizationForm, ProvinceTreeForm
├── locales/               en.json, ru.json
├── MainForm.cs
└── Program.cs
```

## Troubleshooting

| Symptom | Fix |
| --- | --- |
| Map stays black after loading | `provinces.bmp` and `definition.csv` must belong to the same mod version. A stale `definition.csv` from vanilla paired with a modded `provinces.bmp` will mismatch. |
| Empty neighbour list on islands | Make sure `adjacencies.csv` is in the selected `map/` folder and uses `;` as the delimiter (the parser also accepts `,` as fallback). |
| `forts.txt` not written | The folder may be read-only (e.g. inside `Program Files`). Move the mod or the viewer into a writable folder. |
| `CS7065: Icon stream is not in the expected format.` | The `.ico` you supplied is actually a PNG. Rebuild it as a real multi-resolution ICO, or remove `<ApplicationIcon>` from the `.csproj`. |
| Optimization takes a long time on huge coverage sets | Restrict the coverage set (a region or superregion instead of the whole map) for near-instant results. |

## Credits

- Inspired by the general idea of [CzerstfyChlep/Eu4-Mod-Editing-Tool](https://github.com/CzerstfyChlep/Eu4-Mod-Editing-Tool)
  (no code was copied from that repository).
- Thanks to the EU4 modding community for documenting the Clausewitz file format.

## License

This project is distributed under the **MIT License**. See [LICENSE](LICENSE) for details.

The MIT licence covers only the source code of this project. It does **not** cover any assets of Europa Universalis IV — this program does not bundle any. Map files, images and text files of the game remain the property of Paradox Interactive and are read from your own installation at runtime.

*I don't know If this really need a license. I've put it just in case. You may do anything with piece of vibecode as long as it doesn't cause troubles for me, community, game itself.*
