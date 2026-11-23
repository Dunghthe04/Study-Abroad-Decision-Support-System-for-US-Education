export interface CostLine {
  key: string;
  amountUsd: number | null;
  isOneTime: boolean;
}

export interface BudgetCompare {
  source: "profile" | "input";
  annualBudgetUsd: number | null;
  unlimited: boolean;
  /** Âm = còn dư, dương = còn thiếu */
  annualGapUsd: number | null;
  totalGapUsd: number | null;
}

export interface SchoolCost {
  code: string;
  name: string;
  state: string | null;
  studyLevel: string;
  academicYear: string | null;
  years: number | null;
  lines: CostLine[];
  annualUsd: number | null;
  firstYearUsd: number | null;
  totalUsd: number | null;
  scholarshipUsdPerYear: number;
  annualAfterScholarshipUsd: number | null;
  totalAfterScholarshipUsd: number | null;
  isComplete: boolean;
  missingKeys: string[];
  sourceUrl: string | null;
  retrievedAt: string | null;
  budget: BudgetCompare | null;
}

export interface ExchangeRate {
  vndPerUsd: number;
  asOf: string | null;
  source: string;
  isFallback: boolean;
}

export interface CostEstimate {
  rate: ExchangeRate;
  schools: SchoolCost[];
  missingCodes: string[];
  academicYearsDiffer: boolean;
}

export interface StateLivingCost {
  code: string;
  name: string;
  region: string;
  index: number | null;
  housingRentsIndex: number | null;
  year: number | null;
  sourceUrl: string | null;
  retrievedAt: string | null;
}

export interface CostEstimateParams {
  codes: string[];
  studyLevel?: string;
  scholarshipUsdPerYear?: number;
  budgetUsd?: number;
}
