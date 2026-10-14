#!/usr/bin/env python3
"""Normalize the internal family names of the bundled OFL fonts.

Fontsource ships Bricolage Grotesque's static instances carrying the variable
font's default-instance name as the family ("Bricolage Grotesque 96pt
ExtraBold"), and JetBrains Mono Medium declares itself as a separate family
("JetBrains Mono Medium"). The design tokens expect one family per typeface,
with the weight expressed through OS/2 usWeightClass, so this script rewrites
the 'name' table to the family/subfamily names Google Fonts publishes (which is
what the design mockups load).

It is idempotent. Run from the repository root:

    python tools/normalize-font-names.py
"""

from __future__ import annotations

import os

from fontTools.ttLib import TTFont

REPOSITORY_ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
FONT_DIRECTORY = os.path.join(REPOSITORY_ROOT, "src", "Vineeeeea.App", "Assets", "Fonts")

# file name -> (typographic family, subfamily)
FONTS = {
    "BricolageGrotesque-Regular.ttf": ("Bricolage Grotesque", "Regular"),
    "BricolageGrotesque-Medium.ttf": ("Bricolage Grotesque", "Medium"),
    "BricolageGrotesque-SemiBold.ttf": ("Bricolage Grotesque", "SemiBold"),
    "BricolageGrotesque-Bold.ttf": ("Bricolage Grotesque", "Bold"),
    "JetBrainsMono-Medium.ttf": ("JetBrains Mono", "Medium"),
    "JetBrainsMono-Bold.ttf": ("JetBrains Mono", "Bold"),
}

# Name IDs that describe the family and its style.
NAME_IDS = {1, 2, 3, 4, 6, 16, 17, 21, 22}

# (platformID, platEncID, langID): Windows Unicode and Mac Roman.
ENCODINGS = ((3, 1, 0x409), (1, 0, 0))


def normalize(path: str, family: str, subfamily: str) -> None:
    font = TTFont(path)
    name_table = font["name"]
    name_table.names = [record for record in name_table.names if record.nameID not in NAME_IDS]

    postscript = f"{family.replace(' ', '')}-{subfamily}"
    full_name = family if subfamily == "Regular" else f"{family} {subfamily}"
    values = {
        1: family,
        2: subfamily,
        3: f"{full_name};normalize-font-names",
        4: full_name,
        6: postscript,
        16: family,
        17: subfamily,
        21: family,
        22: subfamily,
    }

    for name_id, value in values.items():
        for platform_id, encoding_id, language_id in ENCODINGS:
            name_table.setName(value, name_id, platform_id, encoding_id, language_id)

    font.save(path)


def main() -> None:
    for file_name, (family, subfamily) in FONTS.items():
        path = os.path.join(FONT_DIRECTORY, file_name)
        normalize(path, family, subfamily)
        print(f"{file_name}: {family} {subfamily}")


if __name__ == "__main__":
    main()
