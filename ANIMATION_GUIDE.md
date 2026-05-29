# Animation Pipeline Guide

Komplette Anleitung für die 4 Animation-Komponenten:
- **assembly_animation** — interpoliert Breps zwischen zwei Positionen
- **helix_camera** — fährt die Kamera auf einer Helixbahn
- **nice_preview** — persistente shaded Vorschau
- **make_gif** — baut animierte GIFs aus PNG-Sequenzen via ffmpeg

Am Ende: vollständiger Workflow zum Erzeugen eines Assembly-Videos / GIFs.

---

## Big Picture: was die Pipeline macht

Du hast Teile (Breps) die in zwei Zuständen existieren:
- **Start**: z.B. flach auf einer Stanzplatte ausgelegt (nest)
- **Ende**: zusammengebaut zur fertigen Konstruktion

Die Pipeline:
1. **assembly_animation** rechnet pro Frame eine Zwischenposition (Brep wandert von Nest → Endposition)
2. **helix_camera** lässt die Kamera dabei um's Objekt fliegen
3. **nice_preview** zeigt alles schön gerendert im Viewport
4. **GH Animate** (eingebautes Feature) macht PNG-Snapshots von 0 bis t=1
5. **make_gif** verbindet die PNGs zu einem GIF, optional als Boomerang mit Halten am Ende

Resultat: GIF wo sich deine Konstruktion zusammenbaut während die Kamera kreist.

---

## 1. assembly_animation

**Was es macht:** Interpoliert eine Liste von Breps von Startpositionen zu Endpositionen über einen Parameter `t` (0 bis 1).

### Inputs

| Input | Typ | Default | Bedeutung |
|---|---|---|---|
| `parts_start` | List<Brep> | – | Breps in Startposition (z.B. flach im Nest) |
| `parts_end` | List<Brep> | – | dieselben Breps in Endposition (zusammengebaut) |
| `t` | double | 0 | Animations-Parameter 0..1. Mit **Number Slider** verbinden! |
| `stagger` | double | 0 | 0 = alle Teile bewegen sich gleichzeitig, 1 = strikt nacheinander |
| `easing` | double | 1 | 1 = linear, 2 = ease-out (langsames Ankommen), 0.5 = ease-in |
| `reverse` | bool | false | true = rückwärts spielen (montiert → Nest) |
| `order` | List<int> | leer | optional: Reihenfolge der Montage (Index-Liste) |

### Output
- `out_parts` — List<Brep> bei Zeit `t`

### Wichtig
- `parts_start[i]` und `parts_end[i]` müssen das **gleiche Brep** sein, nur an unterschiedlichen Positionen
- Das heißt: beide Listen müssen gleich lang sein und in gleicher Reihenfolge sortiert
- Topologie muss übereinstimmen (gleiche Vertex-Anzahl) — sonst schlägt die Plane-Berechnung fehl

### Tipp: Stagger + Easing
- `stagger = 0.3`, `easing = 1.5` → schöner "erst Basis, dann oben drauf" Effekt
- `stagger = 0`, `easing = 1` → alle Teile schweben gleichzeitig rein
- `stagger = 0.8`, `easing = 2` → dramatisch sequenziell, jedes Teil landet sanft

### Typischer Aufbau

```
Nest-Breps (Liste)     ──→ parts_start
Original-Breps (Liste) ──→ parts_end
Slider t (0..1)        ──→ t
Slider 0..1            ──→ stagger
Slider 1..3            ──→ easing
Toggle                 ──→ reverse
                            ↓
                        assembly_animation
                            ↓
                        out_parts → an Custom Preview / nice_preview
```

---

## 2. helix_camera

**Was es macht:** Berechnet die Kameraposition auf einer Helixbahn um einen Mittelpunkt und setzt sie im aktiven (oder benannten) Viewport.

### Inputs

| Input | Typ | Default | Bedeutung |
|---|---|---|---|
| `t` | double | 0 | gleicher Slider wie bei assembly_animation |
| `cam_center` | Point3d | (0,0,0) | Drehzentrum (typischerweise BBox-Center der Endposition) |
| `look_at` | Point3d | (0,0,0) | wo die Kamera hinschaut. Leer/(0,0,0) → fällt zurück auf cam_center |
| `radius_start` | double | – | Start-Abstand zur Drehachse |
| `radius_end` | double | – | End-Abstand (größer = rauszoomen, kleiner = ranzoomen) |
| `height_start` | double | – | Start-Höhe über cam_center (Z-Offset) |
| `height_end` | double | – | End-Höhe |
| `rotation_deg` | double | 360 | Gesamtrotation. 720 = zwei Umdrehungen |
| `start_angle_deg` | double | 0 | Anfangswinkel (Phase) |
| `view_name` | string | "" | leer = aktiver Viewport. Sonst Name z.B. "Perspektive" |
| `apply_camera` | bool | true | false = nur Preview, kein Verschieben |
| `preview_segments` | int | 60 | Auflösung der Helix-Vorschau-Polyline |

### Outputs
- `cam_location` — Point3d wo die Kamera grad ist
- `cam_target` — Point3d wohin sie schaut
- `helix_preview` — PolylineCurve der gesamten Flugbahn
- `info` — Live-Status

### Wichtig
- **`view_name` muss exakt zum Animate-Dialog passen** — auf Deutsch heißt's `Perspektive`, nicht `Perspective`
- `cam_center` sollte die **BBox-Mitte der zusammengebauten Endposition** sein (nicht der flachen Nest-Teile)
- `look_at` kann gleich `cam_center` sein oder ein anderer Punkt (z.B. ein Detail das du betonen willst)

### Bauplan-Beispiel für ein 3m hohes Objekt

| Parameter | Wert | Wirkung |
|---|---|---|
| `cam_center` | BBox.Center der montierten Teile | sicher mittig |
| `radius_start` | 4000 | nah genug für Details |
| `radius_end` | 6500 | weiter weg am Ende für Übersicht |
| `height_start` | 1500 | unten beginnen |
| `height_end` | 2500 | nach oben aufsteigen |
| `rotation_deg` | 360 | eine volle Runde |
| `start_angle_deg` | 0 | Frontansicht zum Start |

### Tipp: Helix-Preview nutzen

Setze erst `apply_camera = false` und schau dir die `helix_preview` als Linie im Viewport an. Wenn die Linie das Objekt sauber umkreist, dann `apply_camera = true` schalten und Slider t durchziehen.

---

## 3. nice_preview

**Was es macht:** Zeichnet Breps schön schattiert und persistent ins Viewport — unabhängig von GH-Selektion oder Display Mode.

### Warum?

GH's eingebaute Preview hat Probleme:
- Wechselt Farbe wenn Komponente selektiert ist
- Sieht in Arctic/Rendered Mode komisch aus
- Verschwindet manchmal beim Klicken

`nice_preview` umgeht das mit einem **DisplayConduit** — die Geometrie wird direkt von Rhino gezeichnet, unabhängig vom GH-Zustand.

### Inputs

| Input | Typ | Default | Bedeutung |
|---|---|---|---|
| `breps` | List<Brep> | – | Geometrie zum Zeichnen (typisch: `out_parts` von assembly_animation) |
| `color` | Color | RGB(180,180,180) | Diffuse Grundfarbe |
| `shine` | double | 0.4 | 0 = matt, 1 = hochglanz |
| `transparency` | double | 0 | 0 = solid, 1 = unsichtbar |
| `two_sided` | bool | true | auch Rückseiten zeichnen (gut für offene Geometrie) |
| `enabled` | bool | true | Master-Schalter |

### Output
- `info` — Status-String

### Wichtig
- Source-Brep-Komponenten Preview AUS — sonst doppelte Anzeige (Wireframe + Custom)
- `nice_preview` AN — und alles andere AUS

### Empfohlene Settings

- **Holzlook:** `color = RGB(165, 130, 90)`, `shine = 0.3`
- **Metalllook:** `color = RGB(160, 160, 170)`, `shine = 0.7`
- **Pappkarton:** `color = RGB(190, 160, 110)`, `shine = 0.15`
- **Beton:** `color = RGB(170, 170, 165)`, `shine = 0.1`

### Achtung: Conduit ist statisch

Der Conduit lebt für die gesamte Rhino-Session. Wenn du die Komponente löschst ohne sie vorher mit `enabled = false` zu deaktivieren, bleibt die Vorschau bis Rhino neu startet.

---

## 4. make_gif

**Was es macht:** Baut aus einem Ordner voller PNG-Frames ein animiertes GIF via ffmpeg. Unterstützt Boomerang-Effekt (vor-zurück) mit Halten am Ende.

### Voraussetzung

**ffmpeg muss installiert sein.**
- Windows: `winget install ffmpeg` oder von [ffmpeg.org](https://ffmpeg.org/download.html)
- Im PATH oder vollen Pfad im `ffmpeg_path` Input angeben

### Inputs

| Input | Typ | Default | Bedeutung |
|---|---|---|---|
| `input_folder` | string | – | Ordner mit PNG-Frames (von GH Animate) |
| `output_gif` | string | – | Zielpfad inkl. Dateiname (z.B. `C:\out\anim.gif`) |
| `fps` | int | 30 | Frames pro Sekunde im GIF |
| `file_pattern` | string | `*.png` | auto-detect via Scan, oder explizit `Frame_%05d.png` |
| `ffmpeg_path` | string | `ffmpeg` | Pfad zu ffmpeg.exe oder leer wenn im PATH |
| `width` | int | -1 | Breite in px (-1 = Originalgröße behalten) |
| `boomerang` | bool | true | Forward → Hold → Reverse → Loop |
| `hold_sec` | double | 3 | Sekunden Stillstand am letzten Frame |
| `run` | bool | false | **Toggle** — false→true startet ffmpeg (rising edge) |

### Outputs
- `out_gif_path` — Pfad zum fertigen GIF (wenn erfolgreich)
- `out_log` — ffmpeg-Output zum Debuggen

### Boomerang-Logik

Bei `boomerang = true` mit `hold_sec = 3` und 120 Frames bei 30 fps:

```
Forward (0..119)          → 4 Sek
Hold an Frame 119         → 3 Sek (90 Klone)
Reverse (118..1)          → 4 Sek (Frame 119 und 0 nicht doppelt)
─────────────────────────────────
Total                     → 11 Sek pro Loop, dann endlos
```

### Wichtig
- Toggle muss **manuell zurück auf false** danach (Rising-Edge-Detection)
- PNG-Dateinamen müssen ein Nummern-Pattern haben: `Frame_00000.png`, `Frame_00001.png`, ...
- BMP geht auch — file_pattern auf `*.bmp` setzen

### Pattern Auto-Detect

`make_gif` scannt den Ordner, nimmt die erste Datei und extrahiert das Muster automatisch:
- `Frame_00045.png` → erkennt `Frame_%05d.png` mit start_number 45
- Funktioniert auch mit `render_001.png` oder `bogen_0017.bmp`

### Wenn ffmpeg-Fehler kommt

Schau in `out_log` — die letzten Zeilen zeigen den genauen Fehler. Häufige Probleme:
- `Pattern type 'glob' not supported` → Pattern manuell auf `Frame_%05d.png` setzen
- `No such file or directory` → input_folder Pfad falsch oder leer
- `ffmpeg not found` → Pfad zu ffmpeg.exe explizit angeben

---

## Komplett-Workflow: dein Bogen-Assembly-Video

### Setup im GH-Canvas

```
┌─────────────────────────────────────────────────────────────┐
│                                                             │
│  Nest-Breps (flat)  ─────┐                                  │
│                          ├──→ assembly_animation            │
│  Original-Breps   ───────┘     ↓                            │
│                                out_parts ──→ nice_preview   │
│  ┌────────────────────┐                       ↓             │
│  │  Slider t (0..1)   │──┬────────────────→ Viewport        │
│  └────────────────────┘  │                                  │
│                          │                                  │
│                          ├──→ helix_camera                  │
│                          │     ↓                            │
│  cam_center (Point) ─────┘     applies camera               │
│                                                             │
└─────────────────────────────────────────────────────────────┘
                              ↓
                     Slider rechtsklicken → Animate
                              ↓
                     ┌────────────────────┐
                     │ GH Animate Dialog  │
                     │ Folder: C:\frames\ │
                     │ Frames: 120        │
                     │ Viewport: Perspektive
                     └────────────────────┘
                              ↓
                  120 PNG-Frames im Ordner
                              ↓
                     ┌──────────────┐
                     │  make_gif    │
                     │  run: True   │
                     └──────────────┘
                              ↓
                     animation.gif fertig
```

### Schritt-für-Schritt

**1. Geometrie vorbereiten**
- Konstruktion (Breps) in Rhino → Original-Position
- Nest-Layout (flach) → mit `flatten_plates_for_nesting` Komponente
- → liefert `parts_start` (flat) und du hast `parts_end` (montiert)

**2. assembly_animation einrichten**
- `parts_start` = flache Breps
- `parts_end` = originale Breps
- `t` = Number Slider 0..1, Decimal 3
- `stagger = 0.3`, `easing = 1.5`

**3. nice_preview einrichten**
- `breps` = `out_parts` von assembly_animation
- `color` = passend zum Material
- `shine = 0.3`
- **GH Preview AUS** für die Source-Brep-Komponenten (Rechtsklick → Preview)

**4. helix_camera einrichten**
- `cam_center` = BBox.Center der `parts_end`
- `radius_start`, `radius_end`, `height_*` siehe Bauplan oben
- `view_name = "Perspektive"` (deutsche Rhino-UI)
- Erst `apply_camera = false` → Helix-Vorschau prüfen → dann `true`

**5. Test mit Slider**

Slider `t` manuell von 0 nach 1 ziehen.
Du solltest sehen:
- Bei t=0: Teile flach im Nest
- Bei t=0.5: Teile schweben Richtung Endposition
- Bei t=1: Konstruktion zusammengebaut
- Während des ganzen Vorgangs kreist die Kamera

**6. Display Mode auf Rendered (für hübsche Frames)**
- Im Viewport oben links auf `Perspektive` klicken
- Anzeigemodus → Gerendert (oder Arctic für sehr clean)

**7. Animate starten**
- Rechtsklick auf Slider `t` → **Animate**
- Im Dialog:
  - **Folder**: z.B. `C:\Users\leona\Pictures\bogen_anim\`
  - **Frame count**: 120 (= 4 Sek bei 30 fps für glatte Anim)
  - **Resolution**: 1920×1080 oder 1280×720
  - **Viewport**: `Perspektive`
  - **Filename template**: `Frame_{0:00000}.png`
- OK drücken → GH rechnet 120 Frames durch und speichert PNGs

**8. GIF machen**
- `make_gif` Komponente:
  - `input_folder` = der Animate-Ordner
  - `output_gif` = z.B. `C:\Users\leona\Pictures\bogen.gif`
  - `fps = 30`
  - `boomerang = true` (oder false wenn du nur Forward willst)
  - `hold_sec = 3`
- `run` Toggle: false → true
- In `out_log` schauen ob `DONE: ...` steht
- Toggle wieder auf false (für nächsten Run)

**9. GIF angucken**

In deinem Bilder-Ordner liegt jetzt `bogen.gif`. In jedem Browser/Bildbetrachter öffnen.

---

## Häufige Probleme

### "Beim Animate sehen alle Frames gleich aus"

→ `view_name` im helix_camera passt nicht zum Animate-Dialog. Auf Deutsch ist es `Perspektive`, nicht `Perspective`.

### "Teile sind durchsichtig / Wireframe statt solide"

→ Source-Brep-Komponenten haben Preview AN. Rechtsklick → Preview off. Nur `nice_preview` soll preview machen.

### "Kamera bewegt sich nicht durch Animate, nur per Slider"

→ Display Mode während Animate ist falsch. Vor Animate-Klick den gewünschten Display Mode setzen (Rendered / Arctic / etc.) — Animate übernimmt den aktuellen.

### "ffmpeg-Fehler: pattern type 'glob' not supported"

→ Auf neueren ffmpeg-Versionen funktioniert das. Im Skript ist bereits Auto-Detect drin. Falls trotzdem Fehler: `file_pattern` manuell auf `Frame_%05d.png` setzen.

### "Objekt zu klein im Bild"

→ `radius_start` und `radius_end` reduzieren. Faustregel: Min-Radius = größte Objekt-Dimension × 1.1, Max-Radius = × 2.0.

### "Hintergrund leer/weiß, sieht langweilig aus"

→ Im Rhino eine Bodenebene unter die Konstruktion legen (Rechteck, 6m × 6m). Gibt Schatten + Bezugspunkt. Display Mode mit Schatten aktivieren (Rendered).

### "GIF wird riesig (> 50 MB)"

→ `width` reduzieren (z.B. 720) und/oder `fps` runter (z.B. 24). Faktor 2-3 kleiner ist normal.

---

## Beispiel-Ergebnisse zum Tunen

| Use-Case | t-Frames | stagger | easing | rotation | hold_sec | Größe |
|---|---|---|---|---|---|---|
| Klassisch (Konstruktion zeigen) | 120 | 0.3 | 1.5 | 360° | 3 | mittel |
| Dramatisch (Slow-Mo) | 240 | 0.5 | 2.0 | 540° | 5 | groß |
| Schnell (LinkedIn-Post) | 60 | 0.2 | 1.2 | 360° | 2 | klein |
| Detail-Walk (kein Boomerang) | 180 | 0.4 | 1.5 | 720° | 0 | mittel |

Probier verschiedene `random_seed` Werte für die Kameraposition (start_angle_deg) bis der Blickwinkel sitzt.

---

## Nächste Schritte

- Falls du **Kamera-Pfade komplexer** brauchst (nicht nur Helix): sag Bescheid, ich kann dir z.B. eine "follow curve" Kamera bauen
- Falls du **video.mp4 statt gif** willst: `make_gif` ist erweiterbar — ffmpeg kann beides, nur Endung und Codec ändern
- Falls die **Geschwindigkeit der Animation variabel** sein soll (langsamer in der Mitte, schneller am Ende): kann ich `easing` mit einer Graph-Mapper-Curve ersetzen
