// [USAS-365] TypeScript interfaces cho chức năng phân tích điểm học thuật

export interface TranscriptScore {
  id: string;
  termName: string;
  termOrder: number;
  subject: string;
  score: number;
  credits?: number | null;
  gpa4: number;
  subjectGroup: string;
  subjectGroupName: string;
}

export interface UpsertTranscriptScoreItem {
  id?: string | null;
  termName: string;
  termOrder: number;
  subject: string;
  score: number;
  credits?: number | null;
}

export interface BatchTranscriptScoresRequest {
  scores: UpsertTranscriptScoreItem[];
}

export interface SubjectGroupScore {
  groupKey: string;
  groupName: string;
  rawAverage: number;
  gpa4: number;
  subjectsCount: number;
  subjects: string[];
}

export interface TermTrend {
  termOrder: number;
  termName: string;
  rawAverage: number;
  gpa4: number;
  subjectCount: number;
}

export interface GradeScaleRule {
  minScore: number;
  maxScore: number;
  gpa4: number;
  letterGrade: string;
}

export interface GradeScaleConfig {
  scaleKey: string;
  sourceName: string;
  method: string;
  rules: GradeScaleRule[];
}

export interface AcademicAnalysisResponse {
  analysisId: string;
  studentProfileId: string;
  unweightedGpa: number;
  weightedGpa: number;
  rawAverage: number;
  totalSubjects: number;
  totalTerms: number;
  trend: "upward" | "consistent" | "downward" | string;
  trendDescription: string;
  scaleSource: string;
  subjectGroups: SubjectGroupScore[];
  termAverages: TermTrend[];
  disclaimer: string;
  createdAt: string;
}
