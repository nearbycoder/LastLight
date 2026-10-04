"""Synthesizes every sound in Last Light into Assets/Resources/Audio.

    Tools/.venv/bin/python ArtSource/audio/build_audio.py [sfx] [voices] [music]

Nothing is sampled: effects, the radio babble and the score are all generated with numpy/scipy.
"""
import os
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))

import music  # noqa: E402
import sfx  # noqa: E402
import voices  # noqa: E402

parts = sys.argv[1:] or ["sfx", "voices", "music"]
if "sfx" in parts:
    sfx.build()
if "voices" in parts:
    voices.build()
if "music" in parts:
    music.build()
