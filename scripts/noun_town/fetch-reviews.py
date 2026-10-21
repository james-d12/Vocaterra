
import json
import time
import requests

APP_ID = 2313720
OUTPUT_FILE = "negative_reviews_for_ai.json"

URL = f"https://store.steampowered.com/appreviews/{APP_ID}"

params = {
    "json": 1,
    "filter": "recent",
    "review_type": "negative",
    "language": "all",
    "purchase_type": "all",
    "num_per_page": 100,
    "cursor": "*",
}

reviews = []
seen_ids = set()
seen_cursors = set()

with requests.Session() as session:
    while True:
        response = session.get(URL, params=params, timeout=30)
        response.raise_for_status()
        data = response.json()

        batch = data.get("reviews", [])

        if not batch:
            break

        for review in batch:
            review_id = review.get("recommendationid")

            if review_id in seen_ids:
                continue

            seen_ids.add(review_id)

            reviews.append({
                "review": review.get("review", "").strip(),
                "language": review.get("language"),
                "timestamp": review.get("timestamp_created"),
                "playtime_hours": round(
                    review.get("author", {}).get(
                        "playtime_at_review", 0
                    ) / 60,
                    1,
                ),
                "written_during_early_access": review.get(
                    "written_during_early_access", False
                ),
            })

        print(f"Retrieved {len(reviews)} reviews")

        cursor = data.get("cursor")

        if not cursor or cursor in seen_cursors:
            break

        seen_cursors.add(cursor)
        params["cursor"] = cursor

        time.sleep(0.5)

with open(OUTPUT_FILE, "w", encoding="utf-8") as file:
    json.dump(reviews, file, ensure_ascii=False, indent=2)

print(f"\nSaved {len(reviews)} reviews to {OUTPUT_FILE}")
