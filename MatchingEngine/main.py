import schedule
import time
from db import test_connection
from matcher import compute_matches

def run():
    """
    Entry point for the StudySync AI Matching Engine.
    - Tests the database connection
    - Runs an immediate first computation
    - Then schedules recomputation every 24 hours
    """
    print("=" * 55)
    print("  StudySync AI Matching Engine")
    print("  Cosine Similarity — VARK + Interests + Availability")
    print("=" * 55)

    # Test DB connection before anything else
    if not test_connection():
        print("[Main] Cannot connect to database. Exiting.")
        return

    # Run immediately on startup
    compute_matches()

    # Schedule to run every 24 hours
    schedule.every(24).hours.do(compute_matches)
    print("[Main] Scheduler active — recomputing matches every 24 hours.")
    print("[Main] Press Ctrl+C to stop.\n")

    while True:
        schedule.run_pending()
        time.sleep(60)


if __name__ == "__main__":
    run()