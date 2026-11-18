
from flask import Flask, request, jsonify
from pathlib import Path
from datetime import datetime, timezone
import base64
import csv
import json
import os
import re
import threading
import time
import wave

app = Flask(__name__)

lock = threading.Lock()
sessions = {}

# ============================================================
# SOCIOCAST RESEARCH SESSION CONFIGURATION
# ============================================================

# Select Session_01, Session_02, or Session_03
# using the SOCIOCAST_SESSION environment variable.
SESSION_NAME = os.environ.get(
    "SOCIOCAST_SESSION", ""
).strip()

VALID_SESSIONS = {
    "Session_01",
    "Session_02",
    "Session_03"
}

if SESSION_NAME not in VALID_SESSIONS:
    raise SystemExit(
        "ERROR: Set SOCIOCAST_SESSION to "
        "Session_01, Session_02, or Session_03."
    )

# Create a unique recording directory for every run.
# The microseconds make accidental folder-name collisions
# extremely unlikely.
RUN_ID = datetime.now(timezone.utc).strftime(
    "Run_%Y%m%d_%H%M%S_%f"
)

BASE_OUTPUT = (
    Path(__file__).resolve().parent.parent
    / "MR_Research_Data"
    / "SocioCast_Research_Sessions"
)

OUTPUT = BASE_OUTPUT / SESSION_NAME / RUN_ID

# Never reuse an existing run directory.
OUTPUT.mkdir(parents=True, exist_ok=False)

SERVER_START_UTC = datetime.now(
    timezone.utc
).isoformat()

# ============================================================
# TRACKING CSV FIELDS
# ============================================================

# Original tracking fields are preserved.
# Additional fields support later research analysis.

FIELDS = [
    "device_id",
    "session_id",
    "sequence",

    "sample_start",
    "sample_count",
    "sample_rate",

    "capture_utc",
    "unity_time_seconds",
    "received_utc",

    # Additional server-side measurements
    "received_monotonic_ms",
    "message_bytes",
    "sequence_gap",

    "position_x",
    "position_y",
    "position_z",

    "rotation_x",
    "rotation_y",
    "rotation_z",
    "rotation_w",

    "gaze_valid",
    "gaze_time",

    "gaze_origin_x",
    "gaze_origin_y",
    "gaze_origin_z",

    "gaze_direction_x",
    "gaze_direction_y",
    "gaze_direction_z",

    "missing_samples"
]

# ============================================================
# SAVE RESEARCH RUN INFORMATION
# ============================================================

RUN_INFO = {
    "research_project": "SocioCast",
    "research_session": SESSION_NAME,
    "run_id": RUN_ID,
    "server_start_utc": SERVER_START_UTC,
    "server_host": "0.0.0.0",
    "server_port": 5001,
    "recording_folder": str(OUTPUT),
    "expected_devices": [
        "quest_1",
        "quest_2",
        "quest_3",
        "quest_4"
    ],
    "planned_duration_minutes": 10,
    "transport": "HTTP over TCP",
    "recording_notes": (
        "This run records packets received by the server. "
        "Stop the server after 10 minutes of research activity."
    )
}

with open(
    OUTPUT / "run_info.json",
    "w",
    encoding="utf-8"
) as f:
    json.dump(RUN_INFO, f, indent=4)


# ============================================================
# HEALTH CHECK
# ============================================================

@app.get("/health")
def health():
    return jsonify({
        "status": "ok",
        "service": "MR live streaming",
        "research_session": SESSION_NAME,
        "run_id": RUN_ID
    })


# ============================================================
# AUDIO WRITING
# ============================================================

def write_audio(session, audio_bytes):
    """
    Write audio samples to the WAV recording.

    writeframes() also updates the WAV header,
    helping keep the recording playable.
    """
    session["wav"].writeframes(audio_bytes)


def write_silence(session, missing_samples):
    """
    Fill missing audio samples with silence.

    Writing in smaller blocks avoids large
    temporary memory allocations.
    """
    remaining = missing_samples

    while remaining > 0:
        samples = min(remaining, 16000)

        write_audio(
            session,
            b"\x00" * (samples * 2)
        )

        remaining -= samples


# ============================================================
# RECEIVE STREAMING DATA
# ============================================================

@app.post("/stream/chunk")
def receive_chunk():

    # Record arrival time as early as practical.
    received = datetime.now(
        timezone.utc
    ).isoformat()

    received_monotonic_ms = (
        time.monotonic_ns() / 1_000_000
    )

    # Raw HTTP request-body size.
    message_bytes = request.content_length

    packet = request.get_json(silent=True)

    if not isinstance(packet, dict):
        return jsonify({
            "error": "Expected JSON"
        }), 400

    try:

        # ----------------------------------------------------
        # IDENTIFY HEADSET AND UNITY SESSION
        # ----------------------------------------------------

        device = str(
            packet.get("device_id", "quest_1")
        )

        session_id = str(packet["session_id"])
        sequence = int(packet["sequence"])

        start = int(packet["sample_start"])
        rate = int(packet["sample_rate"])

        if not all(
            re.fullmatch(
                r"[A-Za-z0-9_-]{1,80}",
                value
            )
            for value in (device, session_id)
        ):
            return jsonify({
                "error": "Invalid identifier"
            }), 400

        if (
            sequence < 0
            or start < 0
            or not (8000 <= rate <= 48000)
        ):
            return jsonify({
                "error": "Invalid audio metadata"
            }), 400

        # ----------------------------------------------------
        # DECODE AUDIO
        # ----------------------------------------------------

        audio = base64.b64decode(
            packet["audio_base64"],
            validate=True
        )

        if (
            not audio
            or len(audio) % 2 != 0
            or len(audio) > 192000
        ):
            return jsonify({
                "error": "Invalid audio chunk"
            }), 400

        # ----------------------------------------------------
        # EXTRACT HEADSET TRACKING
        # ----------------------------------------------------

        pos = packet["position"]
        rot = packet["rotation"]

        gaze = packet.get("gaze") or {}
        origin = gaze.get("origin") or {}
        direction = gaze.get("direction") or {}

        key = (device, session_id)

        # ----------------------------------------------------
        # THREAD-SAFE RECORDING
        # ----------------------------------------------------

        with lock:

            # Create a recording folder when a headset's
            # first packet arrives for this run.
            if key not in sessions:

                folder = (
                    OUTPUT
                    / device
                    / session_id
                )

                folder.mkdir(
                    parents=True,
                    exist_ok=True
                )

                wav_path = folder / "audio.wav"
                csv_path = folder / "tracking.csv"

                # Use exclusive creation to avoid
                # overwriting existing recordings.
                wav_file = open(
                    wav_path,
                    "x+b",
                    buffering=0
                )

                wf = wave.open(wav_file, "wb")
                wf.setnchannels(1)
                wf.setsampwidth(2)
                wf.setframerate(rate)

                cf = open(
                    csv_path,
                    "x",
                    newline="",
                    encoding="utf-8"
                )

                writer = csv.DictWriter(
                    cf,
                    fieldnames=FIELDS
                )

                writer.writeheader()
                cf.flush()

                sessions[key] = {
                    "wav": wf,
                    "wav_file": wav_file,
                    "csv": cf,
                    "writer": writer,
                    "rate": rate,

                    # Important:
                    # Start from the first sample received
                    # in this research recording.
                    "expected": start,

                    "last_sequence": -1,
                    "count": 0,
                    "missing_sample_total": 0,
                    "sequence_gap_total": 0
                }

                print(
                    f"NEW SESSION: "
                    f"{SESSION_NAME} | "
                    f"{device}/{session_id}",
                    flush=True
                )

            s = sessions[key]

            # ------------------------------------------------
            # VALIDATE AUDIO STREAM
            # ------------------------------------------------

            if rate != s["rate"]:
                return jsonify({
                    "error": "Sample rate changed"
                }), 400

            if (
                sequence <= s["last_sequence"]
                or start < s["expected"]
            ):
                return jsonify({
                    "error": "Old or overlapping chunk"
                }), 409

            # Calculate missing audio samples.
            gap = start - s["expected"]

            # Calculate missing packet sequence numbers.
            # The first packet establishes the baseline.
            if s["last_sequence"] < 0:
                sequence_gap = 0
            else:
                sequence_gap = max(
                    0,
                    sequence - s["last_sequence"] - 1
                )

            # Insert silence only for gaps occurring
            # during this recording.
            if gap:
                write_silence(s, gap)

            write_audio(s, audio)

            count = len(audio) // 2

            # ------------------------------------------------
            # SAVE TRACKING AND RESEARCH METADATA
            # ------------------------------------------------

            s["writer"].writerow({

                "device_id": device,
                "session_id": session_id,
                "sequence": sequence,

                "sample_start": start,
                "sample_count": count,
                "sample_rate": rate,

                "capture_utc": packet.get(
                    "capture_utc", ""
                ),

                "unity_time_seconds": packet.get(
                    "unity_time_seconds", ""
                ),

                "received_utc": received,

                "received_monotonic_ms": (
                    received_monotonic_ms
                ),

                "message_bytes": (
                    message_bytes
                    if message_bytes is not None
                    else ""
                ),

                "sequence_gap": sequence_gap,

                "position_x": pos["x"],
                "position_y": pos["y"],
                "position_z": pos["z"],

                "rotation_x": rot["x"],
                "rotation_y": rot["y"],
                "rotation_z": rot["z"],
                "rotation_w": rot["w"],

                "gaze_valid": gaze.get(
                    "valid", False
                ),

                "gaze_time": gaze.get(
                    "timestamp", ""
                ),

                "gaze_origin_x": origin.get(
                    "x", ""
                ),

                "gaze_origin_y": origin.get(
                    "y", ""
                ),

                "gaze_origin_z": origin.get(
                    "z", ""
                ),

                "gaze_direction_x": direction.get(
                    "x", ""
                ),

                "gaze_direction_y": direction.get(
                    "y", ""
                ),

                "gaze_direction_z": direction.get(
                    "z", ""
                ),

                "missing_samples": gap
            })

            # Flush tracking data to disk.
            s["csv"].flush()

            # ------------------------------------------------
            # UPDATE COUNTERS
            # ------------------------------------------------

            s["expected"] = start + count
            s["last_sequence"] = sequence

            s["count"] += 1
            s["missing_sample_total"] += gap
            s["sequence_gap_total"] += sequence_gap

            # ------------------------------------------------
            # LIVE CONSOLE OUTPUT
            # ------------------------------------------------

            if s["count"] % 10 == 0:

                print(
                    f"LIVE {device} "
                    f"| research={SESSION_NAME} "
                    f"| chunks={s['count']} "
                    f"| head=("
                    f"{float(pos['x']):.2f}, "
                    f"{float(pos['y']):.2f}, "
                    f"{float(pos['z']):.2f}) "
                    f"| gaze={gaze.get('valid', False)}",
                    flush=True
                )

        return jsonify({
            "status": "ok"
        })

    except (
        ValueError,
        TypeError,
        KeyError,
        base64.binascii.Error
    ) as e:

        return jsonify({
            "error": str(e)
        }), 400


# ============================================================
# FINALIZE RECORDINGS
# ============================================================

def finalize_recordings():
    """
    Close all open audio and tracking files
    and save a summary of this research run.
    """

    run_summary = {
        "research_session": SESSION_NAME,
        "run_id": RUN_ID,
        "server_start_utc": SERVER_START_UTC,
        "server_stop_utc": datetime.now(
            timezone.utc
        ).isoformat(),
        "headsets": {}
    }

    with lock:

        for key, s in sessions.items():

            device, unity_session_id = key

            try:

                s["wav"].close()
                s["wav_file"].close()
                s["csv"].close()

                device_summary = {
                    "unity_session_id": unity_session_id,
                    "received_chunks": s["count"],
                    "observed_sequence_gaps": (
                        s["sequence_gap_total"]
                    ),
                    "missing_audio_samples": (
                        s["missing_sample_total"]
                    ),
                    "sample_rate": s["rate"]
                }

                run_summary["headsets"].setdefault(
                    device, []
                ).append(device_summary)

                print(
                    f"FINALIZED: "
                    f"{device}/{unity_session_id} "
                    f"| chunks={s['count']} "
                    f"| sequence_gaps="
                    f"{s['sequence_gap_total']}",
                    flush=True
                )

            except Exception as e:

                print(
                    f"Finalize error for {key}: {e}",
                    flush=True
                )

        sessions.clear()

    summary_path = OUTPUT / "run_summary.json"

    with open(
        summary_path,
        "w",
        encoding="utf-8"
    ) as f:
        json.dump(
            run_summary,
            f,
            indent=4
        )

    print(
        "Recordings finalized.",
        flush=True
    )

    print(
        f"Research data saved to: {OUTPUT}",
        flush=True
    )


# ============================================================
# START SERVER
# ============================================================

if __name__ == "__main__":

    print(
        "=" * 60,
        flush=True
    )

    print(
        "SOCIOCAST RESEARCH STREAM SERVER",
        flush=True
    )

    print(
        f"Research session: {SESSION_NAME}",
        flush=True
    )

    print(
        f"Run ID: {RUN_ID}",
        flush=True
    )

    print(
        f"Saving recordings to: {OUTPUT}",
        flush=True
    )

    print(
        "Server URL: http://172.20.10.6:5001",
        flush=True
    )

    print(
        "=" * 60,
        flush=True
    )

    try:

        app.run(
            host="0.0.0.0",
            port=5001,
            debug=False,
            use_reloader=False,
            threaded=True
        )

    finally:
        finalize_recordings()
