#!/usr/bin/env python3
"""Regenerates Resources/WorldEditor/Translators/Bedrock from upstream mapping data.

Blocks.json  Bedrock block state -> Java block state, from PrismarineJS/minecraft-data (MIT)
Biomes.json  Bedrock numeric biome ID -> Java biome name, from GeyserMC/mappings (MIT),
             plus legacy IDs that older Bedrock worlds still contain

Usage: python3 Tools/BedrockMappings/generate.py [minecraft-data bedrock version]
"""
import json
import sys
import urllib.request
from pathlib import Path

MCDATA_VERSION = sys.argv[1] if len(sys.argv) > 1 else "1.26.30"
BLOCKS_URL = f"https://raw.githubusercontent.com/PrismarineJS/minecraft-data/master/data/bedrock/{MCDATA_VERSION}/blocksB2J.json"
BIOMES_URL = "https://raw.githubusercontent.com/GeyserMC/mappings/master/biomes.json"

OUTPUT = Path(__file__).resolve().parents[2] / "Resources" / "WorldEditor" / "Translators" / "Bedrock"

# Pre-1.18 Bedrock biome IDs with no modern equivalent of their own, folded into the Java biome they became
LEGACY_BIOMES = {
    10: "minecraft:frozen_ocean",
    13: "minecraft:snowy_plains",
    15: "minecraft:mushroom_fields",
    17: "minecraft:desert",
    18: "minecraft:forest",
    19: "minecraft:taiga",
    20: "minecraft:windswept_hills",
    22: "minecraft:jungle",
    28: "minecraft:birch_forest",
    31: "minecraft:snowy_taiga",
    33: "minecraft:old_growth_pine_taiga",
    39: "minecraft:badlands",
    49: "minecraft:bamboo_jungle",
    130: "minecraft:desert",
    133: "minecraft:taiga",
    134: "minecraft:swamp",
    149: "minecraft:jungle",
    151: "minecraft:sparse_jungle",
    156: "minecraft:old_growth_birch_forest",
    157: "minecraft:dark_forest",
    158: "minecraft:snowy_taiga",
    161: "minecraft:old_growth_spruce_taiga",
    162: "minecraft:windswept_gravelly_hills",
    164: "minecraft:windswept_savanna",
    166: "minecraft:wooded_badlands",
    167: "minecraft:eroded_badlands",
}


def fetch(url):
    with urllib.request.urlopen(url) as response:
        return json.load(response)


def main():
    OUTPUT.mkdir(parents=True, exist_ok=True)

    blocks = fetch(BLOCKS_URL)
    with open(OUTPUT / "Blocks.json", "w", encoding="utf-8") as f:
        json.dump(blocks, f, indent=0, separators=(",", ":"), sort_keys=True)
    print(f"Blocks.json: {len(blocks)} states from minecraft-data bedrock/{MCDATA_VERSION}")

    biomes = {}
    for java, entry in fetch(BIOMES_URL).items():
        biomes.setdefault(entry["bedrock_id"], java)
    added = 0
    for bedrock_id, java in LEGACY_BIOMES.items():
        if bedrock_id not in biomes:
            biomes[bedrock_id] = java
            added += 1

    with open(OUTPUT / "Biomes.json", "w", encoding="utf-8") as f:
        json.dump({str(k): biomes[k] for k in sorted(biomes)}, f, indent=2)
    print(f"Biomes.json: {len(biomes)} IDs ({added} legacy)")


if __name__ == "__main__":
    main()
