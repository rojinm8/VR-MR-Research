
from pathlib import Path
import csv
import math
import statistics
import wave
import audioop

ROOT = (
    Path(__file__).resolve().parent.parent
    / "MR_Research_Data"
    / "Live_Streams"
    / "quest_1"
)

sessions = [
    p for p in ROOT.iterdir()
    if p.is_dir()
    and (p / "tracking.csv").exists()
    and (p / "audio.wav").exists()
]

if not sessions:
    raise SystemExit("No complete recording session found.")

session = max(sessions, key=lambda p: p.stat().st_mtime)

print("\nTEST 4 - SYNCHRONIZATION CHECK")
print("Session:", session.name)

with open(
    session / "tracking.csv",
    newline="",
    encoding="utf-8"
) as f:
    rows = list(csv.DictReader(f))

if not rows:
    raise SystemExit("Tracking CSV is empty.")

with wave.open(str(session / "audio.wav"), "rb") as wav:
    rate = wav.getframerate()
    channels = wav.getnchannels()
    width = wav.getsampwidth()
    frames = wav.getnframes()
    audio = wav.readframes(frames)

duration = frames / rate

print("\n--- AUDIO ---")
print("Sample rate:", rate, "Hz")
print("Channels:", channels)
print("Sample width:", width, "bytes")
print("Audio duration:", round(duration, 3), "seconds")
print("Audio frames:", frames)

print("\n--- TRACKING ---")
print("Tracking rows:", len(rows))

sequences = [int(r["sequence"]) for r in rows]
starts = [int(r["sample_start"]) for r in rows]
counts = [int(r["sample_count"]) for r in rows]

sequence_gaps = sum(
    b != a + 1
    for a, b in zip(sequences, sequences[1:])
)

sample_gaps = sum(
    starts[i] != starts[i-1] + counts[i-1]
    for i in range(1, len(rows))
)

reported_missing = sum(
    int(r["missing_samples"])
    for r in rows
)

expected_frames = starts[-1] + counts[-1]

print("Sequence discontinuities:", sequence_gaps)
print("Sample discontinuities:", sample_gaps)
print("Reported missing samples:", reported_missing)

print("Expected audio frames:", expected_frames)
print("Actual audio frames:", frames)

audio_match = frames == expected_frames

print("Audio timeline matches CSV:", audio_match)

print("\n--- EYE GAZE ---")

valid_rows = [
    r for r in rows
    if str(r["gaze_valid"]).lower() == "true"
]

gaze_times = [
    float(r["gaze_time"])
    for r in valid_rows
    if r["gaze_time"]
]

gaze_increases = all(
    b > a
    for a, b in zip(gaze_times, gaze_times[1:])
)

print("Valid gaze rows:", len(valid_rows))
print("Gaze timestamps strictly increasing:", gaze_increases)

print("\n--- UNITY TIMING ---")

unity_times = [
    float(r["unity_time_seconds"])
    for r in rows
]

unity_elapsed = unity_times[-1] - unity_times[0]
audio_elapsed = (
    starts[-1] - starts[0]
) / rate

print("Unity elapsed:", round(unity_elapsed, 4), "s")
print("Audio sample elapsed:", round(audio_elapsed, 4), "s")
print(
    "Elapsed-time difference:",
    round(unity_elapsed - audio_elapsed, 4),
    "s"
)

print("\n--- EVENT TIMING ---")

if channels == 1 and width == 2:
    loudest_rms = -1
    loudest_row = None

    for index, row in enumerate(rows):
        offset = starts[index] * 2
        length = counts[index] * 2
        chunk = audio[offset:offset + length]

        if len(chunk) != length:
            continue

        rms = audioop.rms(chunk, 2)

        if rms > loudest_rms:
            loudest_rms = rms
            loudest_row = row

    if loudest_row is not None:
        print(
            "Loudest audio interval:",
            round(
                int(loudest_row["sample_start"]) / rate,
                3
            ),
            "s"
        )
        print("Peak interval RMS:", loudest_rms)

largest_move = -1
move_row = None

for previous, current in zip(rows, rows[1:]):
    distance = math.sqrt(sum(
        (
            float(current[f"position_{axis}"])
            - float(previous[f"position_{axis}"])
        ) ** 2
        for axis in ("x", "y", "z")
    ))

    if distance > largest_move:
        largest_move = distance
        move_row = current

if move_row is not None:
    print(
        "Largest head-position change:",
        round(int(move_row["sample_start"]) / rate, 3),
        "s"
    )
    print(
        "Movement between rows:",
        round(largest_move * 100, 2),
        "cm"
    )

print("\n--- BASIC RESULTS ---")

print(
    "Packet sequence:",
    "PASS" if sequence_gaps == 0 else "CHECK"
)

print(
    "Audio sample continuity:",
    "PASS" if sample_gaps == 0
    and reported_missing == 0 else "CHECK"
)

print(
    "Audio file length:",
    "PASS" if audio_match else "CHECK"
)

print(
    "Eye gaze timestamps:",
    "PASS" if gaze_increases and gaze_times
    else "CHECK"
)

print(
    "\nNote: These checks do not establish"
    " precise cross-signal synchronization."
)

