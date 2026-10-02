from typing import Literal

# Shared vocabulary for study levels. Keep in sync with
# backend/src/StudyAbroad.Domain/Constants/StudyLevels.cs and frontend/src/lib/study-levels.ts.
StudyLevel = Literal["secondary", "community_college", "undergraduate", "master", "phd"]

# Documents tagged "general" apply to every level (F-1 visa, SEVIS, pre-departure, ...).
GENERAL = "general"

LABELS_VI: dict[str, str] = {
    "secondary": "THCS/THPT",
    "community_college": "Cao đẳng cộng đồng",
    "undergraduate": "Đại học",
    "master": "Thạc sĩ",
    "phd": "Tiến sĩ",
    GENERAL: "Chung mọi bậc",
}
