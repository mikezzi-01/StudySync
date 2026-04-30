import numpy as np
from db import fetch_all_interest_ids

# ── Feature weights ────────────────────────────────────────────────────────
# These control how much each attribute contributes to the similarity score.
# Must sum to 1.0
WEIGHTS = {
    "vark":          0.25,   # Learning style (VARK)
    "interests":     0.30,   # Academic interests (most important)
    "pace":          0.15,   # Study pace
    "collaboration": 0.10,   # Collaboration mode
    "interaction":   0.10,   # Synchronous vs asynchronous
    "availability":  0.10,   # Schedule overlap
}

# Pre-load all valid interest IDs once at startup
ALL_INTEREST_IDS = []

def load_interest_ids():
    """Load all interest IDs from DB once at startup."""
    global ALL_INTEREST_IDS
    ALL_INTEREST_IDS = fetch_all_interest_ids()
    print(f"[Vectorizer] Loaded {len(ALL_INTEREST_IDS)} interest dimensions.")

def build_vark_vector(profile: dict) -> np.ndarray:
    """
    Returns a 4-dimensional normalised VARK vector.
    [Visual, Auditory, ReadWrite, Kinesthetic]
    """
    raw = np.array([
        profile["VarkVisual"],
        profile["VarkAuditory"],
        profile["VarkReadWrite"],
        profile["VarkKinesthetic"]
    ], dtype=float)

    norm = np.linalg.norm(raw)
    return raw / norm if norm > 0 else raw

def build_interest_vector(profile: dict) -> np.ndarray:
    """
    Returns a binary vector of length = number of interests.
    1 if the user selected that interest, 0 otherwise.
    """
    selected = set(profile["Interests"])
    return np.array(
        [1.0 if iid in selected else 0.0 for iid in ALL_INTEREST_IDS],
        dtype=float
    )

def build_pace_vector(profile: dict) -> np.ndarray:
    """
    One-hot encodes study pace (1–5) into a 5-dim vector.
    """
    vec = np.zeros(5, dtype=float)
    pace = profile["StudyPace"]
    if 1 <= pace <= 5:
        vec[pace - 1] = 1.0
    return vec

def build_collaboration_vector(profile: dict) -> np.ndarray:
    """
    One-hot encodes collaboration mode (1–4) into a 4-dim vector.
    """
    vec = np.zeros(4, dtype=float)
    mode = profile["CollaborationMode"]
    if 1 <= mode <= 4:
        vec[mode - 1] = 1.0
    return vec

def build_interaction_vector(profile: dict) -> np.ndarray:
    """
    One-hot encodes interaction type (1–3) into a 3-dim vector.
    """
    vec = np.zeros(3, dtype=float)
    itype = profile["InteractionType"]
    if 1 <= itype <= 3:
        vec[itype - 1] = 1.0
    return vec

def build_availability_vector(profile: dict) -> np.ndarray:
    """
    Converts the comma-separated availability slot string into a
    binary vector of length 28 (4 time slots × 7 days).
    e.g. "MORN_MON,EVE_TUE" → [1,0,0,...,1,...,0]
    """
    all_slots = [
        f"{slot}_{day}"
        for slot in ["MORN", "AFT", "EVE", "LATE"]
        for day  in ["MON", "TUE", "WED", "THU", "FRI", "SAT", "SUN"]
    ]
    selected = set(profile["AvailabilityVector"].split(",")) if profile["AvailabilityVector"] else set()
    return np.array(
        [1.0 if s in selected else 0.0 for s in all_slots],
        dtype=float
    )

def build_feature_vector(profile: dict) -> np.ndarray:
    """
    Builds a single weighted composite feature vector for a profile.
    Each sub-vector is L2-normalised before weighting so that
    larger sub-vectors don't dominate just because of dimensionality.
    """
    def safe_norm(v: np.ndarray) -> np.ndarray:
        n = np.linalg.norm(v)
        return v / n if n > 0 else v

    vark         = safe_norm(build_vark_vector(profile))         * WEIGHTS["vark"]
    interests    = safe_norm(build_interest_vector(profile))     * WEIGHTS["interests"]
    pace         = safe_norm(build_pace_vector(profile))         * WEIGHTS["pace"]
    collaboration= safe_norm(build_collaboration_vector(profile))* WEIGHTS["collaboration"]
    interaction  = safe_norm(build_interaction_vector(profile))  * WEIGHTS["interaction"]
    availability = safe_norm(build_availability_vector(profile)) * WEIGHTS["availability"]

    return np.concatenate([vark, interests, pace, collaboration, interaction, availability])

def vectorize_all_profiles(profiles: list) -> dict:
    """
    Converts all profiles into a dict of {UserID: feature_vector}.
    """
    vectors = {}
    for profile in profiles:
        vectors[profile["UserID"]] = build_feature_vector(profile)
    print(f"[Vectorizer] Built {len(vectors)} feature vectors.")
    return vectors