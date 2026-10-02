// Keep in sync with backend/src/StudyAbroad.Domain/Constants/StudyLevels.cs and advisor/app/core/study_levels.py.
export const STUDY_LEVELS = ["secondary", "community_college", "undergraduate", "master", "phd"] as const;

export type StudyLevel = (typeof STUDY_LEVELS)[number];

export const STUDY_LEVEL_LABELS: Record<StudyLevel, string> = {
  secondary: "THCS/THPT",
  community_college: "Cao đẳng cộng đồng",
  undergraduate: "Đại học",
  master: "Thạc sĩ",
  phd: "Tiến sĩ",
};

export function studyLevelLabel(level: string): string {
  return STUDY_LEVEL_LABELS[level as StudyLevel] ?? level;
}
