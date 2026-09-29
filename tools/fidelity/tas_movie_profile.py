"""Physical movie playback: independent polls, update cadence and gameplay."""
from tas_profile import COVERAGE as SHARED_COVERAGE, differences

PROFILE = "ages-movie-gameplay-v1"
BOUNDARY = "movie-end-completed-main-loop-updates-v1"
CLOCK_ORIGIN = 65468
FRAME_CLOCKS = 140448
COVERAGE = {key: list(values) for key, values in SHARED_COVERAGE.items()}
COVERAGE["supported"] = [value for value in COVERAGE["supported"] if not value.startswith("sound ")]
COVERAGE["supported"].append("physical movie-frame update cadence and independently sampled held/pressed buttons")
COVERAGE["diagnostic"] = ["sound fade/enable/volume and eight channel enable/wait fields; differences are retained but nonfatal"]


def movie_differences(reference, candidate):
    for key in ("movieFrame", "input"):
        if type(candidate.get(key)) is not int or candidate[key] != reference[key]:
            raise ValueError(f"Misaligned movie {key}: {reference.get(key)} / {candidate.get(key)}")
    a, b = reference["updates"], candidate["updates"]
    if len(a) != len(b):
        return [{"field": "timing.completedUpdates", "rom": len(a), "godot": len(b)}], []
    audio = []
    for original, port in zip(a, b):
        # Unlike the diagnostic update adapter, these samples come from two
        # independent input polls. A mismatch is a fidelity failure, not a
        # transport alignment error and never an instruction to resynchronize.
        for key in ("update", "input", "pressed"):
            if type(port.get(key)) is not int or port[key] != original[key]:
                return [{"field": "timing." + key, "rom": original[key], "godot": port.get(key)}], audio
        drift = differences(original, port)
        audio.extend(dict(d, update=original["update"]) for d in drift if d["field"].startswith("audio."))
        gameplay = [dict(d, update=original["update"]) for d in drift if not d["field"].startswith("audio.")]
        if gameplay:
            return gameplay, audio
    return [], audio
