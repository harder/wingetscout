"""Build and validate the dependency-free Scout documentation site."""

from html.parser import HTMLParser
from pathlib import Path
from urllib.parse import unquote, urlsplit
import shutil

SOURCE = Path(__file__).resolve().parent
OUTPUT = SOURCE / "_build"
FILES = (
    "index.html", "docs.html", "styles.css", "app.js", "favicon.svg",
    "CNAME", "robots.txt", "sitemap.xml", "media/scout-dog.svg",
    "media/scout-sage-search.png", "media/scout-amber-installed.png",
    "media/scout-moss-upgrades.png", "media/scout-rose-search.png",
    "media/scout-search-demo.gif", "media/scout-upgrades-demo.gif",
)


class References(HTMLParser):
    def __init__(self):
        super().__init__()
        self.values = []
        self.ids = set()

    def handle_starttag(self, tag, attrs):
        for name, value in attrs:
            if name == "id" and value:
                self.ids.add(value)
            if name in {"href", "src", "data-theme-src"} and value:
                self.values.append(value)


if not OUTPUT.resolve().is_relative_to(SOURCE.resolve()) or OUTPUT == SOURCE:
    raise SystemExit("Build output must stay inside the site directory")
if OUTPUT.exists():
    shutil.rmtree(OUTPUT)
OUTPUT.mkdir()
for name in FILES:
    source = SOURCE / name
    if not source.is_file():
        raise SystemExit(f"Missing site file: {source}")
    destination = OUTPUT / name
    destination.parent.mkdir(parents=True, exist_ok=True)
    shutil.copy2(source, destination)
(OUTPUT / ".nojekyll").touch()

pages = {}
for page in ("index.html", "docs.html"):
    parser = References()
    parser.feed((OUTPUT / page).read_text(encoding="utf-8"))
    pages[page] = parser

for page, parser in pages.items():
    for reference in parser.values:
        parsed = urlsplit(reference)
        if parsed.scheme or parsed.netloc:
            continue
        local = unquote(parsed.path) or page
        if local in {".", "./"}:
            local = "index.html"
        target = (OUTPUT / local).resolve()
        if not target.is_relative_to(OUTPUT.resolve()) or not target.is_file():
            raise SystemExit(f"Broken local link in {page}: {reference}")
        if parsed.fragment and target.suffix == ".html":
            target_page = target.name
            if unquote(parsed.fragment) not in pages[target_page].ids:
                raise SystemExit(f"Broken local anchor in {page}: {reference}")

if (OUTPUT / "CNAME").read_text(encoding="utf-8").strip() != "wingetscout.com":
    raise SystemExit("CNAME must contain wingetscout.com")

print(f"Built and validated {len(FILES)} site files in {OUTPUT}")
