"""
Generate the ShadowShift metric dashboard.

Usage:
  python3 dashboard_metrics.py
"""

from __future__ import annotations

import json
import math
from dataclasses import dataclass
from pathlib import Path
from typing import Iterable
from urllib.error import HTTPError, URLError
from urllib.request import urlopen


FIREBASE_ANALYTICS_URL = (
    "https://shadowshift-af31e-default-rtdb.firebaseio.com/analytics.json"
)
OUTPUT_PATH = Path("metrics_dashboard.png")
OUTPUT_DPI = 180

LEVELS = ["level1", "level2", "level3"]
LEVEL_LABELS = ["Level 1", "Level 2", "Level 3"]

COLORS = {
    "bg": "#0a0a0f",
    "card": "#0f0f18",
    "border": "#1e1e2e",
    "text": "#e8e8f0",
    "muted": "#5a5a7a",
    "accent1": "#7c6af7",
    "accent2": "#f76a8c",
    "accent3": "#6af7c4",
    "accent4": "#f7c46a",
}

SUBTITLE_COLOR = COLORS["muted"]
SUBTITLE_SIZE = 10


@dataclass
class ChartSpec:
    title: str
    subtitle: str
    badge: str
    accent: str


def fetch_remote_json(url: str) -> dict:
    with urlopen(url, timeout=15) as response:
        return json.load(response)


def load_matplotlib():
    try:
        import matplotlib

        matplotlib.use("Agg")
        from matplotlib import pyplot as plt
        from matplotlib.colors import to_rgba
    except ImportError as exc:
        raise RuntimeError(
            "matplotlib is not installed.\n"
            "Install it first, for example:\n"
            "  source .venv/bin/activate\n"
            "  python -m pip install matplotlib"
        ) from exc

    return plt, to_rgba


def normalize_analytics_root(payload: dict) -> dict:
    if not isinstance(payload, dict):
        raise ValueError("Analytics payload must be a JSON object.")

    metric_keys = {"completion-times", "retries", "switches"}
    if metric_keys.intersection(payload):
        return payload

    nested = payload.get("analytics")
    if isinstance(nested, dict) and metric_keys.intersection(nested):
        return nested

    raise ValueError(
        "Could not find analytics data. Expected keys like "
        "'completion-times', 'retries', or 'switches'."
    )


def load_analytics() -> dict:
    try:
        return normalize_analytics_root(fetch_remote_json(FIREBASE_ANALYTICS_URL))
    except HTTPError as exc:
        raise RuntimeError(f"Failed to fetch Firebase data: HTTP {exc.code}") from exc
    except URLError as exc:
        raise RuntimeError(f"Failed to fetch Firebase data: {exc.reason}") from exc


def avg_completion_time(level_node: dict | None) -> float | None:
    if not isinstance(level_node, dict):
        return None

    session_times: list[float] = []
    for user_sessions in level_node.values():
        if not isinstance(user_sessions, dict):
            continue
        for session in user_sessions.values():
            if not isinstance(session, dict):
                continue
            events = session.get("events")
            if not isinstance(events, dict):
                continue

            elapsed_values = []
            for event in events.values():
                if not isinstance(event, dict):
                    continue
                elapsed_ms = event.get("elapsedMs")
                if isinstance(elapsed_ms, (int, float)) and elapsed_ms > 0:
                    elapsed_values.append(float(elapsed_ms))

            if elapsed_values:
                session_times.append(max(elapsed_values) / 1000.0)

    if not session_times:
        return None
    return sum(session_times) / len(session_times)


def avg_event_count(level_node: dict | None) -> float | None:
    if not isinstance(level_node, dict):
        return None

    counts: list[int] = []
    for user_sessions in level_node.values():
        if not isinstance(user_sessions, dict):
            continue
        for session in user_sessions.values():
            if not isinstance(session, dict):
                continue
            events = session.get("events")
            if isinstance(events, dict):
                counts.append(len(events))

    if not counts:
        return None
    return sum(counts) / len(counts)


def total_sessions_for_levels(metric_node: dict | None, levels: Iterable[str]) -> int:
    if not isinstance(metric_node, dict):
        return 0

    count = 0
    for level in levels:
        level_node = metric_node.get(level)
        if not isinstance(level_node, dict):
            continue
        for user_sessions in level_node.values():
            if isinstance(user_sessions, dict):
                count += len(user_sessions)
    return count


def average_of(values: Iterable[float | None]) -> float | None:
    valid = [value for value in values if value is not None]
    if not valid:
        return None
    return sum(valid) / len(valid)


def format_stat(value: float | int | None, suffix: str = "", digits: int = 1) -> str:
    if value is None:
        return "-"
    if isinstance(value, int):
        return f"{value}{suffix}"
    return f"{value:.{digits}f}{suffix}"


def build_metrics(data: dict) -> dict:
    completion_times = data.get("completion-times", {})
    all_collected = completion_times.get("all-collected", {})
    missing_collectibles = completion_times.get("missing-collectibles", {})
    retries = data.get("retries", {})
    switches = data.get("switches", {})

    completion_values = [avg_completion_time(all_collected.get(level)) for level in LEVELS]
    retry_values = [avg_event_count(retries.get(level)) for level in LEVELS]
    switch_values = [avg_event_count(switches.get(level)) for level in LEVELS]
    no_star_values = [avg_completion_time(missing_collectibles.get(level)) for level in LEVELS]

    return {
        "summary": {
            "total_sessions": total_sessions_for_levels(retries, LEVELS),
            "avg_time": average_of(completion_values),
            "avg_retries": average_of(retry_values),
            "avg_switches": average_of(switch_values),
        },
        "metric1": completion_values,
        "metric2": retry_values,
        "metric3": switch_values,
        "metric4_no_stars": no_star_values,
        "metric4_all_stars": completion_values,
    }


def nice_max(values: Iterable[float | None]) -> float:
    valid = [value for value in values if value is not None and value > 0]
    if not valid:
        return 1.0

    maximum = max(valid)
    magnitude = 10 ** math.floor(math.log10(maximum)) if maximum > 0 else 1
    normalized = maximum / magnitude

    if normalized <= 1:
        rounded = 1
    elif normalized <= 2:
        rounded = 2
    elif normalized <= 5:
        rounded = 5
    else:
        rounded = 10

    return rounded * magnitude


def style_summary_axis(ax, accent: str) -> None:
    ax.set_facecolor(COLORS["card"])
    ax.set_xticks([])
    ax.set_yticks([])
    for side, spine in ax.spines.items():
        spine.set_visible(True)
        spine.set_color(COLORS["border"])
        spine.set_linewidth(1.0)
        if side == "top":
            spine.set_color(accent)
            spine.set_linewidth(2.2)


def style_chart_axis(ax) -> None:
    ax.set_facecolor(COLORS["card"])
    ax.grid(axis="y", color=COLORS["border"], linewidth=1)
    ax.grid(axis="x", visible=False)
    ax.set_axisbelow(True)
    ax.tick_params(colors=COLORS["muted"], labelsize=10)
    for spine in ax.spines.values():
        spine.set_color(COLORS["border"])
        spine.set_linewidth(1.0)


def add_badge(ax, label: str, accent: str) -> None:
    ax.text(
        0.98,
        1.16,
        label,
        transform=ax.transAxes,
        ha="right",
        va="center",
        color=accent,
        fontsize=9,
        fontweight="bold",
        family="monospace",
        bbox={
            "boxstyle": "round,pad=0.3",
            "facecolor": COLORS["card"],
            "edgecolor": accent,
            "linewidth": 1.0,
        },
    )


def annotate_bars(ax, bars, values: list[float | None], suffix: str, y_max: float) -> None:
    offset = y_max * 0.03
    for bar, value in zip(bars, values):
        height = bar.get_height()
        label = "-" if value is None else f"{value:.1f}{suffix}"
        text_y = (height + offset) if value is not None else offset
        ax.text(
            bar.get_x() + bar.get_width() / 2,
            text_y,
            label,
            ha="center",
            va="bottom",
            color=COLORS["text"] if value is not None else COLORS["muted"],
            fontsize=9,
            family="monospace",
        )


def plot_metric(ax, spec: ChartSpec, values: list[float | None], suffix: str, to_rgba) -> None:
    style_chart_axis(ax)
    ax.set_title(
        spec.title,
        loc="left",
        fontsize=13,
        fontweight="bold",
        color=COLORS["text"],
        pad=22,
    )
    ax.text(
        0.0,
        1.04,
        spec.subtitle,
        transform=ax.transAxes,
        ha="left",
        va="bottom",
        color=SUBTITLE_COLOR,
        fontsize=SUBTITLE_SIZE,
        family="monospace",
    )
    add_badge(ax, spec.badge, spec.accent)

    y_max = nice_max(values)
    plotted_values = [value if value is not None else 0 for value in values]
    bar_colors = [
        to_rgba(spec.accent, 0.72 if value is not None else 0.18) for value in values
    ]
    bars = ax.bar(
        LEVEL_LABELS,
        plotted_values,
        color=bar_colors,
        edgecolor=spec.accent,
        linewidth=1.0,
        width=0.58,
    )
    ax.set_ylim(0, y_max * 1.18)
    ax.set_yticks([y_max * tick / 4 for tick in range(5)])
    annotate_bars(ax, bars, values, suffix, y_max)


def plot_grouped_metric(
    ax,
    spec: ChartSpec,
    left_label: str,
    left_values: list[float | None],
    left_color: str,
    right_label: str,
    right_values: list[float | None],
    right_color: str,
    suffix: str,
    to_rgba,
) -> None:
    style_chart_axis(ax)
    ax.set_title(
        spec.title,
        loc="left",
        fontsize=13,
        fontweight="bold",
        color=COLORS["text"],
        pad=22,
    )
    ax.text(
        0.0,
        1.04,
        spec.subtitle,
        transform=ax.transAxes,
        ha="left",
        va="bottom",
        color=SUBTITLE_COLOR,
        fontsize=SUBTITLE_SIZE,
        family="monospace",
    )
    add_badge(ax, spec.badge, spec.accent)

    x_positions = list(range(len(LEVEL_LABELS)))
    width = 0.34
    y_max = nice_max(left_values + right_values)

    left_plot = [value if value is not None else 0 for value in left_values]
    right_plot = [value if value is not None else 0 for value in right_values]

    left_bars = ax.bar(
        [x - width / 2 for x in x_positions],
        left_plot,
        width=width,
        color=[to_rgba(left_color, 0.72 if value is not None else 0.18) for value in left_values],
        edgecolor=left_color,
        linewidth=1.0,
        label=left_label,
    )
    right_bars = ax.bar(
        [x + width / 2 for x in x_positions],
        right_plot,
        width=width,
        color=[to_rgba(right_color, 0.72 if value is not None else 0.18) for value in right_values],
        edgecolor=right_color,
        linewidth=1.0,
        label=right_label,
    )

    ax.set_xticks(x_positions, LEVEL_LABELS)
    ax.set_ylim(0, y_max * 1.22)
    ax.set_yticks([y_max * tick / 4 for tick in range(5)])
    annotate_bars(ax, left_bars, left_values, suffix, y_max)
    annotate_bars(ax, right_bars, right_values, suffix, y_max)

    legend = ax.legend(
        loc="upper left",
        bbox_to_anchor=(0.0, 1.0),
        frameon=False,
        fontsize=9,
        ncol=2,
    )
    for text in legend.get_texts():
        text.set_color(COLORS["muted"])


def render_matplotlib(metrics: dict, plt, to_rgba) -> None:
    fig = plt.figure(figsize=(14, 10), facecolor=COLORS["bg"])
    grid = fig.add_gridspec(3, 4, height_ratios=[0.34, 1, 1], hspace=0.42, wspace=0.28)

    fig.suptitle(
        "ShadowShift Analytics Dashboard",
        x=0.06,
        y=0.98,
        ha="left",
        color=COLORS["text"],
        fontsize=22,
        fontweight="bold",
    )
    fig.text(
        0.06,
        0.945,
        "Python matplotlib export",
        color=COLORS["muted"],
        fontsize=10,
        family="monospace",
    )

    summary = metrics["summary"]
    summary_specs = [
        ("Total Sessions", format_stat(summary["total_sessions"], digits=0), COLORS["text"]),
        ("Avg Completion (s)", format_stat(summary["avg_time"], "s"), COLORS["accent1"]),
        ("Avg Retries", format_stat(summary["avg_retries"]), COLORS["accent2"]),
        ("Avg Switches", format_stat(summary["avg_switches"]), COLORS["accent3"]),
    ]

    for index, (label, value, accent) in enumerate(summary_specs):
        ax = fig.add_subplot(grid[0, index])
        style_summary_axis(ax, accent)
        ax.text(
            0.06,
            0.58,
            value,
            transform=ax.transAxes,
            ha="left",
            va="center",
            color=COLORS["text"],
            fontsize=24,
            fontweight="bold",
        )
        ax.text(
            0.06,
            0.22,
            label,
            transform=ax.transAxes,
            ha="left",
            va="center",
            color=COLORS["muted"],
            fontsize=9,
            fontweight="bold",
            family="monospace",
        )

    chart1 = fig.add_subplot(grid[1, 0:2])
    chart2 = fig.add_subplot(grid[1, 2:4])
    chart3 = fig.add_subplot(grid[2, 0:2])
    chart4 = fig.add_subplot(grid[2, 2:4])

    plot_metric(
        chart1,
        ChartSpec(
            title="Metric #1 - Completion Time",
            subtitle="Avg seconds to reach the flag per level",
            badge="TIME",
            accent=COLORS["accent1"],
        ),
        metrics["metric1"],
        "s",
        to_rgba,
    )
    plot_metric(
        chart2,
        ChartSpec(
            title="Metric #2 - Death & Retry Frequency",
            subtitle="Avg retries per player session per level",
            badge="RETRY",
            accent=COLORS["accent2"],
        ),
        metrics["metric2"],
        "",
        to_rgba,
    )
    plot_metric(
        chart3,
        ChartSpec(
            title="Metric #3 - Shadow State Switch Count",
            subtitle="Avg shadow switches per level",
            badge="SWITCH",
            accent=COLORS["accent3"],
        ),
        metrics["metric3"],
        "",
        to_rgba,
    )
    plot_grouped_metric(
        chart4,
        ChartSpec(
            title="Metric #4 - Detour Time for Star Collection",
            subtitle="Level 1 to Level 3: with stars vs. without stars",
            badge="DETOUR",
            accent=COLORS["accent4"],
        ),
        "No Stars",
        metrics["metric4_no_stars"],
        COLORS["accent1"],
        "All Stars",
        metrics["metric4_all_stars"],
        COLORS["accent4"],
        "s",
        to_rgba,
    )

    fig.text(
        0.06,
        0.03,
        "ShadowShift Analytics - Data from Firebase Realtime Database",
        color=COLORS["muted"],
        fontsize=10,
        family="monospace",
    )
    fig.savefig(OUTPUT_PATH, dpi=OUTPUT_DPI, facecolor=fig.get_facecolor(), bbox_inches="tight")
    plt.close(fig)


def print_level_breakdown(metrics: dict) -> None:
    print("Per-level metrics:")
    for index, label in enumerate(LEVEL_LABELS):
        completion_time = metrics["metric1"][index]
        retries = metrics["metric2"][index]
        switches = metrics["metric3"][index]
        no_stars = metrics["metric4_no_stars"][index]
        all_stars = metrics["metric4_all_stars"][index]
        detour_delta = (
            all_stars - no_stars
            if all_stars is not None and no_stars is not None
            else None
        )

        print(f"{label}:")
        print(f"  Metric 1 - Completion Time: {format_stat(completion_time, 's')}")
        print(f"  Metric 2 - Retries: {format_stat(retries)}")
        print(f"  Metric 3 - Shadow Switches: {format_stat(switches)}")
        print(f"  Metric 4 - No Stars Time: {format_stat(no_stars, 's')}")
        print(f"  Metric 4 - All Stars Time: {format_stat(all_stars, 's')}")
        print(f"  Metric 4 - Detour Delta: {format_stat(detour_delta, 's')}")


def main() -> int:
    try:
        plt, to_rgba = load_matplotlib()
        analytics = load_analytics()
        metrics = build_metrics(analytics)
        render_matplotlib(metrics, plt, to_rgba)
    except (OSError, ValueError, RuntimeError) as exc:
        print(f"Error: {exc}")
        return 1

    print(f"Generated {OUTPUT_PATH}")
    print(f"Total sessions: {metrics['summary']['total_sessions']}")
    print(f"Avg completion time: {format_stat(metrics['summary']['avg_time'], 's')}")
    print(f"Avg retries: {format_stat(metrics['summary']['avg_retries'])}")
    print(f"Avg switches: {format_stat(metrics['summary']['avg_switches'])}")
    print_level_breakdown(metrics)
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
