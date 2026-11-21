# Player guide (GitHub Pages)

The public player guide at <https://rhisijmuiden.github.io/datacenter-architect/>, built with Jekyll by
[`.github/workflows/pages.yml`](../.github/workflows/pages.yml) whenever `site/` changes on `main`.

- English pages sit in this folder, Dutch pages with the **same file names** in `nl/`. Every page exists in both languages.
- The left menu comes from `_data/nav.yml`, interface words (language switch, download button, footer) from `_data/ui.yml`.
- Screenshots live in `assets/shots/` as 1600 px wide JPGs. Use `{% include shot.html src="name" caption="..." %}`;
  add `nl=true` when a Dutch version `assets/shots/nl-name.jpg` exists.
- Callouts: a blockquote followed by `{: .tip}`, `.power`, `.cooling`, `.fiber`, `.ok` or `.warn`.
- Colours and fonts follow `brand/tokens.json` (`assets/css/site.css`).

Fresh screenshots come from the game's own screenshot flags, for example:

```bash
godot --path game --resolution 1920x1080 -- --screenshot out.png --in-game --interior --overlay-power --time 2027-06-18T14:00 --weather clear
```

Preview locally with Jekyll 3.10 (the version GitHub Pages uses):

```bash
gem install jekyll -v 3.10.0 && gem install kramdown-parser-gfm webrick
cd site && jekyll serve
```

Never put the reference screenshots from the project folder (`style-references`) in this repository.
