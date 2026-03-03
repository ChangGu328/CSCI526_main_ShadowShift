import requests
import matplotlib.pyplot as plt
from urllib.parse import urljoin
# =====================================
# Configuration
# =====================================

from dotenv import load_dotenv
import os

load_dotenv()

API_KEY = os.getenv("FIREBASE_API_KEY")

if not API_KEY:
    raise ValueError("FIREBASE_API_KEY not found")

FIREBASE_URL = "https://shadowshift-af31e-default-rtdb.firebaseio.com/"

# =====================================
# Anonymous login to get idToken
# =====================================

def anonymous_login():
    url = f"https://identitytoolkit.googleapis.com/v1/accounts:signUp?key={API_KEY}"
    
    payload = {
        "returnSecureToken": True
    }

    response = requests.post(url, json=payload)

    if response.status_code != 200:
        print("Anonymous login failed:", response.text)
        return None

    data = response.json()
    print("Anonymous login successful")
    return data["idToken"]


# =====================================
# Fetch retry data from Firebase
# =====================================


def fetch_retry_data(id_token):
    # Ensure base ends with '/'
    base = FIREBASE_URL.rstrip("/") + "/"
    path = "analytics/retries.json"
    url = urljoin(base, path)
    url_with_auth = f"{url}?auth={id_token}"

    print("[fetch] GET", url_with_auth)
    resp = requests.get(url_with_auth, timeout=20)

    print("[fetch] status_code:", resp.status_code)
    # Print Content-Type header to help debugging
    print("[fetch] Content-Type:", resp.headers.get("Content-Type"))

    # If body is empty, show that explicitly
    if resp.text is None or resp.text.strip() == "":
        print("[fetch] Response body is empty.")
        return None

    # Try to parse JSON, but handle errors gracefully
    try:
        data = resp.json()
        return data
    except Exception as e:
        print("[fetch] Failed to parse JSON:", repr(e))
        print("[fetch] Raw response text (first 2000 chars):")
        print(resp.text[:2000])
        return None


# =====================================
# Compute average retry count per level
# =====================================

def compute_level_averages(data):
    level_averages = {}

    if not data:
        return level_averages

    for level_id, level_data in data.items():
        session_counts = []

        for uid, uid_data in level_data.items():
            for session_id, session_data in uid_data.items():

                # Some structures store events under an "events" node
                if "events" in session_data:
                    events = session_data["events"]
                else:
                    events = session_data

                # Count number of retry events in this session
                session_counts.append(len(events))

        # Calculate average retry count per session
        if session_counts:
            avg = sum(session_counts) / len(session_counts)
        else:
            avg = 0

        level_averages[level_id] = avg

    return level_averages


# =====================================
# Plot bar chart
# =====================================

def plot_bar_chart(level_averages):
    if not level_averages:
        print("No data available to plot")
        return

    # customized order 
    desired_order = ["tutorial", "level1", "level2", "level3"]

    levels = [lvl for lvl in desired_order if lvl in level_averages]
    averages = [level_averages[lvl] for lvl in levels]

    plt.figure(figsize=(10, 6))
    plt.bar(levels, averages)
    plt.xlabel("Level")
    plt.ylabel("Average Retry Count per Session")
    plt.title("Average Retry Count per Level")
    plt.xticks(rotation=45)
    plt.tight_layout()
    plt.show()


# =====================================
# Main execution
# =====================================

if __name__ == "__main__":
    print("Logging in anonymously...")
    token = anonymous_login()

    if not token:
        print("Could not obtain idToken. Exiting.")
        exit()

    print("Fetching retry data...")
    data = fetch_retry_data(token)

    if not data:
        print("Database returned no data.")
        exit()

    averages = compute_level_averages(data)

    print("\nAverage Retry Count Per Level:")
    for level, avg in averages.items():
        print(f"{level}: {avg:.2f}")

    plot_bar_chart(averages)