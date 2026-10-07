"use client";

import { useEffect, useState } from "react";
import Link from "next/link";
import {
  getLatestAcademicAnalysis,
  getTranscriptScores,
  triggerAcademicAnalysis,
} from "@/lib/academic-api";
import type {
  AcademicAnalysisResponse,
  TranscriptScore,
} from "@/types/academic";
import { GpaSummaryCard } from "@/components/academic/GpaSummaryCard";
import { SubjectGroupBreakdown } from "@/components/academic/SubjectGroupBreakdown";
import { TermTrendChart } from "@/components/academic/TermTrendChart";
import { ReadOnlyTranscriptTable } from "@/components/academic/ReadOnlyTranscriptTable";

export function AcademicAnalysisView() {
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

          // Kiểm tra nếu chưa từng phân tích HOẶC bảng điểm đã có sự thay đổi số lượng môn so với lần phân tích trước
          const isStale =
            scoresData.length > 0 &&
            (!analysisData || analysisData.totalSubjects !== scoresData.length);

          if (isStale) {
            try {
              const computed = await triggerAcademicAnalysis();
              if (!ignore) setAnalysis(computed);
            } catch {
              if (!ignore && analysisData) setAnalysis(analysisData);
            }
          } else if (analysisData) {
            setAnalysis(analysisData);
          }
        }
      } catch (err) {
        if (!ignore) {
          console.error("Lỗi kết nối dữ liệu học thuật:", err);
          const msg =
            err instanceof Error
              ? err.message
              : "Không thể tải dữ liệu phân tích học thuật. Vui lòng kiểm tra kết nối mạng.";
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
      const isStale =
        scoresData.length > 0 &&
        (!analysisData || analysisData.totalSubjects !== scoresData.length);

      if (isStale) {
        try {
          const computed = await triggerAcademicAnalysis();
          setAnalysis(computed);
        } catch {
          setAnalysis(analysisData);
        }
      } else {
        setAnalysis(analysisData);
      }
    } catch (err) {
      const msg =
        err instanceof Error
          ? err.message
          : "Không thể tải dữ liệu bảng điểm. Vui lòng kiểm tra kết nối mạng.";
      setFetchError(msg);
    } finally {
      setLoading(false);
    }
  };

  const handleTriggerAnalysis = async () => {
    setIsAnalyzing(true);
    setMessage(null);
    try {
      const [result, refreshedScores] = await Promise.all([
        triggerAcademicAnalysis(),
        getTranscriptScores(),
      ]);
      setAnalysis(result);
      setScores(refreshedScores);
      setMessage({
        type: "success",
        text: "Phân tích điểm học thuật hoàn tất. Chỉ số GPA WES 4.0 và xu hướng đã được cập nhật thành công.",
      });
      setTimeout(() => setMessage(null), 4000);
    } catch (err) {
      const msg = err instanceof Error ? err.message : "Lỗi khi kích hoạt phân tích điểm.";
      setMessage({ type: "error", text: msg });
    } finally {
      setIsAnalyzing(false);
    }
  };

  if (loading) {
    return (
      <div className="py-24 text-center">
        <div className="inline-block h-8 w-8 animate-spin rounded-full border-4 border-slate-900 border-r-transparent" />
        <p className="mt-3 text-xs text-slate-500 font-medium">Đang tải báo cáo phân tích năng lực học thuật...</p>
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
            <span className="rounded-full bg-blue-50 border border-blue-200 px-2.5 py-0.5 text-xs font-semibold text-blue-700">
              Quy đổi WES 4.0
            </span>
          </div>
          <p className="mt-1 text-xs sm:text-sm text-slate-600 max-w-2xl">
            Quy đổi bảng điểm theo tiêu chuẩn giáo dục Hoa Kỳ (WES 4.0 tham khảo), phân tích điểm theo nhóm môn và nhận diện đà tăng trưởng học thuật (Growth Mindset).
          </p>
        </div>

        <div className="flex items-center gap-2">
          <Link
            href="/profile/academic"
            className="inline-flex items-center gap-1.5 rounded-lg border border-slate-200 bg-white px-3.5 py-2 text-xs font-semibold text-slate-700 hover:bg-slate-50 transition"
          >
            <span>✏️</span> Chỉnh sửa bảng điểm
          </Link>
          <Link
            href="/advisor"
            className="inline-flex items-center gap-1.5 rounded-lg bg-blue-600 px-4 py-2 text-xs font-semibold text-white hover:bg-blue-700 transition"
          >
            <span>🤖</span> Tư vấn AI &rarr;
          </Link>
        </div>
      </div>

      {/* Thông báo lỗi tải dữ liệu */}
      {fetchError ? (
        <div className="rounded-xl border border-red-200 bg-red-50 p-6 text-center space-y-3">
          <p className="text-sm font-semibold text-red-800">{fetchError}</p>
          <p className="text-xs text-red-600 max-w-md mx-auto">
            Không thể tải dữ liệu phân tích từ máy chủ. Vui lòng kiểm tra lại kết nối và thử lại.
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

          {/* Empty State: Nếu chưa có môn học nào được nhập */}
          {scores.length === 0 ? (
            <div className="rounded-2xl border border-slate-200 bg-white p-10 text-center space-y-4 shadow-xs">
              <div className="mx-auto flex h-14 w-14 items-center justify-center rounded-full bg-blue-50 text-blue-600 text-2xl">
                📑
              </div>
              <div>
                <h3 className="text-base font-bold text-slate-900">
                  Chưa có dữ liệu bảng điểm học tập
                </h3>
                <p className="mt-1 text-xs sm:text-sm text-slate-500 max-w-md mx-auto">
                  Bạn cần nhập bảng điểm các kỳ học (hoặc tải mẫu học bạ) tại mục <strong>Hồ sơ học thuật</strong> trước khi hệ thống có thể tính toán GPA 4.0 và phân tích đà tăng trưởng.
                </p>
              </div>
              <div className="pt-2">
                <Link
                  href="/profile/academic"
                  className="inline-flex items-center gap-2 rounded-xl bg-blue-600 px-5 py-2.5 text-xs font-bold text-white shadow-sm hover:bg-blue-700 transition"
                >
                  <span>📝</span> Nhập bảng điểm tại Hồ sơ học thuật &rarr;
                </Link>
              </div>
            </div>
          ) : (
            <>
              {/* Analytics Dashboard (Executive Summary + 2-col analytics) */}
              {analysis && (
                <div className="space-y-6 min-w-0 w-full">
                  <GpaSummaryCard analysis={analysis} />
                  <div className="grid gap-6 md:grid-cols-2 min-w-0 w-full">
                    <SubjectGroupBreakdown groups={analysis.subjectGroups} />
                    <TermTrendChart terms={analysis.termAverages} />
                  </div>
                </div>
              )}

              {/* Bảng danh sách môn học đã trích xuất từ DB (Read-Only View) */}
              <ReadOnlyTranscriptTable
                scores={scores}
                onRefreshAnalysis={handleTriggerAnalysis}
                isAnalyzing={isAnalyzing}
              />
            </>
          )}
        </>
      )}
    </div>
  );
}
