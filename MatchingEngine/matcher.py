import numpy as np
from sklearn.metrics.pairwise import cosine_similarity
from config import MIN_SCORE_THRESHOLD, TOP_N_MATCHES
from db import fetch_all_profiles, save_recommendations, clear_expired_cache
from vectorizer import load_interest_ids, vectorize_all_profiles


def compute_matches():
    """
    Main matching pipeline:
    1. Load all eligible profiles from the database
    2. Build feature vectors for each profile
    3. Compute pairwise cosine similarity
    4. Store top N matches per user in RecommendationCache
    """
    print("\n[Matcher] ── Starting match computation ──")

    # Step 1: Load profiles
    profiles = fetch_all_profiles()

    if len(profiles) < 2:
        print("[Matcher] Not enough profiles to compute matches (need at least 2).")
        return

    # Step 2: Load interest dimensions and build vectors
    load_interest_ids()
    vectors = vectorize_all_profiles(profiles)

    user_ids = list(vectors.keys())
    matrix   = np.array([vectors[uid] for uid in user_ids])

    # Step 3: Compute full pairwise cosine similarity matrix
    # Result is an NxN matrix where result[i][j] = similarity(user_i, user_j)
    print(f"[Matcher] Computing {len(user_ids)}x{len(user_ids)} similarity matrix...")
    similarity_matrix = cosine_similarity(matrix)

    # Step 4: For each user, extract top N matches and save
    for i, user_id in enumerate(user_ids):
        scores = []

        for j, target_id in enumerate(user_ids):
            # Skip self-match
            if i == j:
                continue

            score = similarity_matrix[i][j]

            # Only store matches above the minimum threshold
            if score >= MIN_SCORE_THRESHOLD:
                scores.append((target_id, score))

        # Sort by score descending
        scores.sort(key=lambda x: x[1], reverse=True)

        # Save top N to database
        save_recommendations(user_id, scores[:TOP_N_MATCHES])

    # Step 5: Clean up expired cache entries
    clear_expired_cache()

    print(f"[Matcher] ── Match computation complete for {len(user_ids)} users ──\n")


def compute_matches_for_user(user_id: int):
    """
    Recomputes matches for a single user only.
    Called when a user updates their profile.
    """
    print(f"\n[Matcher] Recomputing matches for UserID {user_id}...")

    profiles = fetch_all_profiles()

    if len(profiles) < 2:
        print("[Matcher] Not enough profiles.")
        return

    load_interest_ids()
    vectors = vectorize_all_profiles(profiles)

    if user_id not in vectors:
        print(f"[Matcher] UserID {user_id} not found in eligible profiles.")
        return

    user_vector = vectors[user_id].reshape(1, -1)
    scores = []

    for target_id, target_vector in vectors.items():
        if target_id == user_id:
            continue

        score = cosine_similarity(user_vector, target_vector.reshape(1, -1))[0][0]

        if score >= MIN_SCORE_THRESHOLD:
            scores.append((target_id, score))

    scores.sort(key=lambda x: x[1], reverse=True)
    save_recommendations(user_id, scores[:TOP_N_MATCHES])

    print(f"[Matcher] Done. Found {len(scores)} matches for UserID {user_id}.")