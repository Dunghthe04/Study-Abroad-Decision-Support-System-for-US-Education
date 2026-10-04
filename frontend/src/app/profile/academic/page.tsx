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
  const [isAnalyzing, setIsAnalyzing] = useState(false);
  const [message, setMessage] = useState<{ type: "success" | "error"; text: string } | null>(null);

  // Load ban đầu: điểm số và phân tích gần nhất
  useEffect(() => {
    async function loadData() {
      try {
        const [scoresData, analysisData] = await Promise.all([
          getTranscriptScores().catch(() => []),
          getLatestAcademicAnalysis().catch(() => null),
        ]);
        setScores(scoresData);
        setAnalysis(analysisData);
      } catch (err: any) {
        console.error("Lỗi tải dữ liệu học thuật:", err);
      } finally {
        setLoading(false);
      }
    }

    loadData();
  }, []);

  const handleSaveScores = async (items: UpsertTranscriptScoreItem[]) => {
    try {
      const updated = await saveTranscriptScores(items);
      setScores(updated);
      setMessage({ type: "success", text: "Đã lưu cập nhật bảng điểm thành công!" });
      setTimeout(() => setMessage(null), 3000);
    } catch (err: any) {
      setMessage({ type: "error", text: err.message ?? "Lỗi lưu bảng điểm." });
    }
  };

  const handleDeleteScore = async (scoreId: string) => {
    try {
      await deleteTranscriptScore(scoreId);
      setScores((prev) => prev.filter((s) => s.id !== scoreId));
      setMessage({ type: "success", text: "Đã xóa môn học khỏi bảng điểm." });
      setTimeout(() => setMessage(null), 3000);
    } catch (err: any) {
      setMessage({ type: "error", text: err.message ?? "Lỗi xóa môn học." });
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
        text: "Thuật toán phân tích học thuật hoàn tất! Điểm GPA và xu hướng đã được cập nhật.",
      });
      setTimeout(() => setMessage(null), 4000);
    } catch (err: any) {
      setMessage({ type: "error", text: err.message ?? "Lỗi phân tích học thuật." });
    } finally {
      setIsAnalyzing(false);
    }
  };

  if (loading) {
    return (
      <div className="mx-auto max-w-5xl px-4 py-16 text-center">
        <div className="inline-block h-8 w-8 animate-spin rounded-full border-4 border-solid border-blue-600 border-r-transparent" />
        <p className="mt-3 text-sm text-slate-500">Đang tải hồ sơ học thuật...</p>
      </div>
    );
  }

  return (
    <div className="mx-auto max-w-5xl px-4 py-8">
      {/* Header trang */}
      <div className="mb-6">
        <h1 className="text-2xl font-black text-slate-900">
          Phân Tích Năng Lực Học Thuật & GPA
        </h1>
        <p className="mt-1 text-sm text-slate-500">
          Quy đổi điểm hệ 10 sang thang 4.0 chuẩn WES, phân tích điểm mạnh theo nhóm ngành (STEM / Xã hội / Ngoại ngữ) và đánh giá xu hướng 3 năm.
        </p>
      </div>

      {/* Thông báo Toast Feedback */}
      {message && (
        <div
          className={`mb-6 rounded-lg p-3 text-xs font-semibold ${
            message.type === "success"
              ? "bg-emerald-50 text-emerald-800 border border-emerald-200"
              : "bg-red-50 text-red-800 border border-red-200"
          }`}
        >
          {message.text}
        </div>
      )}

      {/* Dashboard kết quả phân tích (nếu đã có kết quả) */}
      {analysis && (
        <div className="space-y-6 mb-8">
          <GpaSummaryCard analysis={analysis} />
          <SubjectGroupBreakdown groups={analysis.subjectGroups} />
          <TermTrendChart terms={analysis.termAverages} />
        </div>
      )}

      {/* Bảng điểm chi tiết & Form nhập liệu */}
      <TranscriptScoreTable
        scores={scores}
        onSaveScores={handleSaveScores}
        onDeleteScore={handleDeleteScore}
        onAnalyze={handleAnalyze}
        isAnalyzing={isAnalyzing}
      />
    </div>
  );
}
