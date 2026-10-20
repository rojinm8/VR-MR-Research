
from flask import Flask, request, jsonify
from pathlib import Path
from datetime import datetime, timezone
import json
import threading

app = Flask(__name__)

LOG_FILE = Path(__file__).parent / "received_events.txt"
log_lock = threading.Lock()

ALLOWED_ACTIONS = {
    "SESSION_STARTED",
    "GRABBED",
    "RELEASED"
}

@app.route("/health", methods=["GET"])
def health():
    return jsonify({"status": "ok"})

@app.route("/interaction", methods=["POST"])
def receive_interaction():
    data = request.get_json(silent=True)

    if not isinstance(data, dict):
        return jsonify({
            "error": "Expected a JSON object"
        }), 400

    for field in ["timestamp", "object_name", "action"]:
        value = data.get(field)
        if not isinstance(value, str) or not value.strip():
            return jsonify({
                "error": f"Missing or invalid field: {field}"
            }), 400

    if data["action"] not in ALLOWED_ACTIONS:
        return jsonify({
            "error": "Invalid interaction action"
        }), 400

    data["received_at"] = datetime.now(
        timezone.utc
    ).isoformat()

    with log_lock:
        with LOG_FILE.open("a", encoding="utf-8") as file:
            file.write(json.dumps(data) + "\n")

    print("RECEIVED:", data, flush=True)

    return jsonify({"status": "received"}), 200


if __name__ == "__main__":
    print("Starting Meta Quest Interaction Server...")

    app.run(
        host="0.0.0.0",
        port=5000,
        debug=False
    )
