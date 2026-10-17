# Tools

Development tools for Bedrock support. Neither is part of the shipped app or `Mapee.sln`.

## BedrockProbe

Loads a Bedrock world through Mapee's own loading path (level reader, region store, chunk
enumerator, version converter) and reports what came out. With image paths it also writes rough
top-down renders of the Overworld and Nether, plus a biome map of each. It runs on Linux and
macOS as well as Windows, so the reader can be tested without the WPF GUI.

Run it from the repository root so `Resources/` resolves:

    dotnet run --project Tools/BedrockProbe -- <world folder> [overworld.png] [nether.png]

## BedrockMappings

`generate.py` regenerates `Resources/WorldEditor/Translators/Bedrock` from upstream mapping data.
Pass a minecraft-data Bedrock version to target a newer game release:

    python3 Tools/BedrockMappings/generate.py 1.26.30
