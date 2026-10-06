"use client";

import { useEffect, useState } from "react";
import {
  deleteTranscriptScore,
  getLatestAcademicAnalysis,
  getTranscriptScores,
  saveTranscriptScores,
  triggerAcademicAnalysis,
} from "@/lib/academic-api";
import type {
  AcademicAnalysisResponse,
  TranscriptScore,
  UpsertTranscriptScoreItem,
} from "@/types/academic";
import { GpaSummaryCard } from "@/components/academic/GpaSummaryCard";
import { SubjectGroupBreakdown } from "@/components/academic/SubjectGroupBreakdown";
import { TermTrendChart } from "@/components/academic/TermTrendChart";
import { TranscriptScoreTable } from "@/components/academic/TranscriptScoreTable";

export default function AcademicAnalysisPage() {
  const [scores, setScores] = useState<TranscriptScore[]>([]);
  const [analysis, setAnalysis] = useState<AcademicAnalysisResponse | null>(null);
  const [loading, setLoading] = useState(true);
  const [fetchError, setFetchError] = useState<string | null>(null);
  const [isAnalyzing, setIsAnalyzing] = useState(false);
  const [message, setMessage] = useState<{ type: "success" | "error"; text: string } | null>(null);

  useEffect(() => {
    let ignore = false;

    async function fetchInitialData() {
      try {
        const [scoresData, analysisData] = await Promise.all([
          getTranscriptScores(),
          getLatestAcademicAnalysis().catch((err: unknown) => {
            const msg = err instanceof Error ? err.message : String(err);
            if (msg.includes("404") || msg.toLowerCase().includes("not found")) {
              return null;
            }
            throw err;
          }),
        ]);
        if (!ignore) {
          setScores(scoresData);
          setAnalysis(analysisData);
        }
      } catch (err) {
        if (!ignore) {
          console.error("Lỗi kết nối dữ liệu học thuật:", err);
          const msg =
            err instanceof Error
              ? err.message
              : "Không thể tải dữ liệu bảng điểm. Vui lòng kiểm tra kết nối mạng hoặc thử lại.";
          setFetchError(msg);
        }
      } finally {
        if (!ignore) {
          setLoading(false);
        }
      }
    }

    fetchInitialData();

    return () => {
      ignore = true;
    };
  }, []);

  const handleRetry = async () => {
    setLoading(true);
    setFetchError(null);
    try {
      const [scoresData, analysisData] = await Promise.all([
        getTranscriptScores(),
        getLatestAcademicAnalysis().catch((err: unknown) => {
          const msg = err instanceof Error ? err.message : String(err);
          if (msg.includes("404") || msg.toLowerCase().includes("not found")) {
            return null;
          }
          throw err;
        }),
      ]);
      setScores(scoresData);
      setAnalysis(analysisData);
    } catch (err) {
      const msg =
        err instanceof Error
          ? err.message
          : "Không thể tải dữ liệu bảng điểm. Vui lòng kiểm tra kết nối mạng hoặc thử lại.";
      setFetchError(msg);
    } finally {
      setLoading(false);
    }
  };

  const handleSaveScores = async (items: UpsertTranscriptScoreItem[]) => {
    try {
      const updated = await saveTranscriptScores(items);
      setScores(updated);
      setMessage({ type: "success", text: "Đã cập nhật bảng điểm học tập thành công." });
      setTimeout(() => setMessage(null), 3000);
    } catch (err) {
      const msg = err instanceof Error ? err.message : "Lỗi lưu bảng điểm.";
      setMessage({ type: "error", text: msg });
      // Fix C365-03: Ném lại lỗi để TranscriptScoreTable giữ nguyên dữ liệu trong form và không xóa input
      throw err;
    }
  };

  const handleDeleteScore = async (scoreId: string) => {
    try {
      await deleteTranscriptScore(scoreId);
      setScores((prev) => prev.filter((s) => s.id !== scoreId));
      setMessage({ type: "success", text: "Đã xóa môn học khỏi bảng điểm." });
      setTimeout(() => setMessage(null), 3000);
    } catch (err) {
      const msg = err instanceof Error ? err.message : "Lỗi xóa môn học.";
      setMessage({ type: "error", text: msg });
      // Fix C365-08: Không rethrow để tránh unhandled rejection / pageerror tại caller
    }
  };

  const handleAnalyze = async () => {
    setIsAnalyzing(true);
    setMessage(null);
    try {
      const result = await triggerAcademicAnalysis();
      setAnalysis(result);
      setMessage({
        type: "success",
        text: "Phân tích điểm học thuật hoàn tất. Chỉ số GPA và xu hướng đã được cập nhật.",
      });
      setTimeout(() => setMessage(null), 4000);
    } catch (err) {
      const msg = err instanceof Error ? err.message : "Lỗi phân tích điểm học thuật.";
      setMessage({ type: "error", text: msg });
    } finally {
      setIsAnalyzing(false);
    }
  };

  if (loading) {
    return (
      <div className="py-24 text-center">
        <div className="inline-block h-6 w-6 animate-spin rounded-full border-2 border-slate-900 border-r-transparent" />
        <p className="mt-3 text-xs text-slate-500 font-medium">Đang tải hồ sơ học thuật...</p>
      </div>
    );
  }

  return (
    <div className="space-y-8 min-w-0 w-full">
      {/* Header bar */}
      <div className="flex flex-col sm:flex-row sm:items-end justify-between gap-4 border-b border-slate-200/80 pb-5">
        <div>
          <div className="flex items-center gap-2">
            <h1 className="text-2xl font-bold tracking-tight text-slate-900">
              Đánh Giá Năng Lực Học Thuật & GPA
            </h1>
            <span className="rounded-full bg-slate-100 px-2.5 py-0.5 text-xs font-semibold text-slate-700">
              Quy đổi WES 4.0
            </span>
          </div>
          <p className="mt-1 text-xs sm:text-sm text-slate-600 max-w-2xl">
            Quy đổi bảng điểm theo tiêu chuẩn giáo dục Hoa Kỳ (WES 4.0 tham khảo), phân tích điểm theo nhóm môn và nhận diện đà tăng trưởng học thuật (Growth Mindset).
          </p>
        </div>
      </div>

      {/* Thông báo lỗi tải dữ liệu (Fix C365-06: Tách biệt hoàn toàn khỏi empty table/form) */}
      {fetchError ? (
        <div className="rounded-xl border border-red-200 bg-red-50 p-6 text-center space-y-3">
          <p className="text-sm font-semibold text-red-800">{fetchError}</p>
          <p className="text-xs text-red-600 max-w-md mx-auto">
            Không thể tải dữ liệu bảng điểm từ máy chủ. Vui lòng kiểm tra lại kết nối mạng và thử lại.
          </p>
          <div className="pt-1">
            <button
              type="button"
              onClick={handleRetry}
              className="rounded-lg bg-red-600 px-4 py-2 text-xs font-semibold text-white hover:bg-red-700 transition cursor-pointer"
            >
              Thử tải lại dữ liệu
            </button>
          </div>
        </div>
      ) : (
        <>
          {/* Toast Feedback */}
          {message && (
            <div
              className={`rounded-lg p-3 text-xs font-semibold ${
                message.type === "success"
                  ? "bg-emerald-50 text-emerald-800 border border-emerald-200"
                  : "bg-red-50 text-red-800 border border-red-200"
              }`}
            >
              {message.text}
            </div>
          )}

          {/* Analytics Dashboard (Executive Summary + 2-col analytics) */}
          {analysis && (
            <div className="space-y-6 min-w-0 w-full">
              <GpaSummaryCard analysis={analysis} />
              {/* Fix C365-01: Thêm min-w-0 vào grid 2 cột để ngăn tràn layout */}
              <div className="grid gap-6 md:grid-cols-2 min-w-0 w-full">
                <SubjectGroupBreakdown groups={analysis.subjectGroups} />
                <TermTrendChart terms={analysis.termAverages} />
              </div>
            </div>
          )}

          {/* Detailed Transcript Manager */}
          <TranscriptScoreTable
            scores={scores}
            onSaveScores={handleSaveScores}
            onDeleteScore={handleDeleteScore}
            onAnalyze={handleAnalyze}
            isAnalyzing={isAnalyzing}
          />
        </>
      )}
    </div>
  );
}
