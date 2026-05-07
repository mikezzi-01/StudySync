import schedule
import time
import threading
from db import test_connection
from matcher import compute_matches
from api import app

def run_scheduler():
    """Runs the 24-hour scheduled recomputation in a background thread."""
    schedule.every(24).hours.do(compute_matches)
    print("[Scheduler] Active — recomputing matches every 24 hours.")
    while True:
        schedule.run_pending()
        time.sleep(60)


def run():
    """
    Entry point for the StudySync AI Matching Engine.
    - Tests DB connection
    - Runs immediate first computation
    - Starts the Flask API on port 5050
    - Starts the 24-hour scheduler in background
    """
    print("=" * 55)
    print("  StudySync AI Matching Engine")
    print("  Cosine Similarity — VARK + Interests + Availability")
    print("=" * 55)

    # Test DB connection
    if not test_connection():
        print("[Main] Cannot connect to database. Exiting.")
        return

    # Run immediate first computation on startup
    compute_matches()

    # Start scheduler in background thread
    scheduler_thread = threading.Thread(
        target=run_scheduler,
        daemon=True
    )
    scheduler_thread.start()

    # Start Flask API (blocking — keeps the process alive)
    print("[Main] Starting Flask API on http://localhost:5050")
    print("[Main] Press Ctrl+C to stop.\n")
    app.run(host='0.0.0.0', port=5050, debug=False)


if __name__ == "__main__":
    run()