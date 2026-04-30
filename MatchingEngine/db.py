from sqlalchemy import create_engine, text
from sqlalchemy.orm import Session
from config import CONNECTION_STRING, CACHE_EXPIRY_HOURS, TOP_N_MATCHES
from datetime import datetime, timedelta

# Create the database engine
engine = create_engine(CONNECTION_STRING, echo=False)

def test_connection():
    """Test that the database connection works."""
    try:
        with engine.connect() as conn:
            conn.execute(text("SELECT 1"))
        print("[DB] Connection successful.")
        return True
    except Exception as e:
        print(f"[DB] Connection failed: {e}")
        return False

def fetch_all_profiles():
    """
    Fetch all learner profiles with their VARK scores,
    study preferences, and selected interests.
    Returns a list of dicts, one per user.
    """
    query = text("""
        SELECT
            u.UserID,
            u.FirstName,
            u.LastName,
            u.AcademicLevel,
            lp.VarkVisual,
            lp.VarkAuditory,
            lp.VarkReadWrite,
            lp.VarkKinesthetic,
            lp.StudyPace,
            lp.CollaborationMode,
            lp.InteractionType,
            lp.StudyConsistency,
            lp.AvailabilityVector,
            lp.ProfileCompletion
        FROM Users u
        INNER JOIN LearnerProfiles lp ON u.UserID = lp.UserID
        WHERE u.IsActive = 1
          AND lp.ProfileCompletion >= 60
    """)

    with engine.connect() as conn:
        rows = conn.execute(query).fetchall()

    profiles = []
    for row in rows:
        # Fetch interests for this user
        interests = fetch_user_interests(row.UserID)
        profiles.append({
            "UserID":           row.UserID,
            "FirstName":        row.FirstName,
            "LastName":         row.LastName,
            "AcademicLevel":    row.AcademicLevel,
            "VarkVisual":       float(row.VarkVisual),
            "VarkAuditory":     float(row.VarkAuditory),
            "VarkReadWrite":    float(row.VarkReadWrite),
            "VarkKinesthetic":  float(row.VarkKinesthetic),
            "StudyPace":        int(row.StudyPace),
            "CollaborationMode":int(row.CollaborationMode),
            "InteractionType":  int(row.InteractionType),
            "StudyConsistency": int(row.StudyConsistency),
            "AvailabilityVector": row.AvailabilityVector or "",
            "ProfileCompletion":float(row.ProfileCompletion),
            "Interests":        interests
        })

    print(f"[DB] Fetched {len(profiles)} eligible profiles.")
    return profiles

def fetch_user_interests(user_id: int):
    """Returns a list of InterestIDs for a given user."""
    query = text("""
        SELECT InterestID
        FROM LearnerProfileInterests
        WHERE ProfileID = :uid
    """)
    with engine.connect() as conn:
        rows = conn.execute(query, {"uid": user_id}).fetchall()
    return [row.InterestID for row in rows]

def fetch_all_interest_ids():
    """Returns all possible InterestIDs from the Interests table."""
    query = text("SELECT InterestID FROM Interests ORDER BY InterestID")
    with engine.connect() as conn:
        rows = conn.execute(query).fetchall()
    return [row.InterestID for row in rows]

def save_recommendations(user_id: int, recommendations: list):
    """
    Save computed match scores to RecommendationCache.
    Clears old entries for the user first, then inserts fresh results.
    recommendations: list of (target_user_id, cosine_score) tuples
    """
    expiry = datetime.utcnow() + timedelta(hours=CACHE_EXPIRY_HOURS)

    with engine.begin() as conn:
        # Clear existing cache for this user
        conn.execute(
            text("DELETE FROM RecommendationCache WHERE UserID = :uid"),
            {"uid": user_id}
        )

        # Insert new recommendations
        for target_id, score in recommendations[:TOP_N_MATCHES]:
            conn.execute(text("""
                INSERT INTO RecommendationCache
                    (UserID, TargetUserID, CosineScore, ComputedAt, ExpiryAt)
                VALUES
                    (:uid, :tid, :score, :now, :expiry)
            """), {
                "uid":    user_id,
                "tid":    target_id,
                "score":  round(float(score), 4),
                "now":    datetime.utcnow(),
                "expiry": expiry
            })

    print(f"[DB] Saved {min(len(recommendations), TOP_N_MATCHES)} recommendations for UserID {user_id}.")

def clear_expired_cache():
    """Remove all expired entries from RecommendationCache."""
    with engine.begin() as conn:
        result = conn.execute(
            text("DELETE FROM RecommendationCache WHERE ExpiryAt < :now"),
            {"now": datetime.utcnow()}
        )
    print(f"[DB] Cleared expired cache entries.")