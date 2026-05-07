from flask import Flask, jsonify, request
from matcher import compute_matches, compute_matches_for_user
from db import test_connection
import threading

app = Flask(__name__)

# ── Health check ──────────────────────────────────────────────────────────
@app.route('/health', methods=['GET'])
def health():
    """Check if the matching engine API is running."""
    db_ok = test_connection()
    return jsonify({
        "status": "ok" if db_ok else "db_error",
        "message": "StudySync Matching Engine API",
        "db_connected": db_ok
    }), 200 if db_ok else 500


# ── Compute matches for a single user ─────────────────────────────────────
@app.route('/compute/<int:user_id>', methods=['POST'])
def compute_for_user(user_id):
    """
    Triggered immediately after a user completes their profile.
    Computes matches for the specified user only — fast and targeted.
    """
    if user_id <= 0:
        return jsonify({"error": "Invalid user ID."}), 400

    try:
        # Run in a background thread so the HTTP response
        # returns immediately without waiting for computation
        thread = threading.Thread(
            target=compute_matches_for_user,
            args=(user_id,),
            daemon=True
        )
        thread.start()

        return jsonify({
            "status": "computing",
            "message": f"Match computation started for UserID {user_id}.",
            "user_id": user_id
        }), 202

    except Exception as e:
        return jsonify({"error": str(e)}), 500


# ── Compute matches for ALL users ─────────────────────────────────────────
@app.route('/compute/all', methods=['POST'])
def compute_all():
    """
    Manually trigger a full recomputation for all users.
    Protected by a simple secret key header.
    """
    secret = request.headers.get('X-Engine-Secret', '')
    if secret != 'StudySync@EngineSecret2024':
        return jsonify({"error": "Unauthorized."}), 401

    try:
        thread = threading.Thread(
            target=compute_matches,
            daemon=True
        )
        thread.start()

        return jsonify({
            "status": "computing",
            "message": "Full match computation started for all users."
        }), 202

    except Exception as e:
        return jsonify({"error": str(e)}), 500


# ── Entry point ───────────────────────────────────────────────────────────
if __name__ == '__main__':
    print("=" * 55)
    print("  StudySync Matching Engine API")
    print("  Running on http://localhost:5050")
    print("=" * 55)
    app.run(host='0.0.0.0', port=5050, debug=False)