"use client";

import { useEffect, useState } from "react";
import Link from "next/link";
import { ArrowRightIcon, BotIcon, FileTextIcon, PencilIcon } from "lucide-react";
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
import { Alert, AlertDescription, AlertTitle } from "@/components/ui/alert";
import { Badge } from "@/components/ui/badge";
import { Button, buttonVariants } from "@/components/ui/button";
import { Card } from "@/components/ui/card";
import {
  Empty,
  EmptyContent,
  EmptyDescription,
  EmptyHeader,
  EmptyMedia,
  EmptyTitle,
} from "@/components/ui/empty";
import { Spinner } from "@/components/ui/spinner";

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
      <div className="flex flex-col items-center gap-3 py-24 text-sm text-muted-foreground">
        <Spinner />
        <p>Đang tải báo cáo phân tích năng lực học thuật...</p>
      </div>
    );
  }

  return (
    <div className="w-full min-w-0 space-y-8">
      {/* Header bar */}
      <div className="flex flex-col justify-between gap-4 sm:flex-row sm:items-end">
        <div>
          <div className="flex items-center gap-2">
            <h1 className="text-h1">
              Đánh Giá Năng Lực Học Thuật & GPA
            </h1>
            <Badge variant="brand">
              Quy đổi WES 4.0
            </Badge>
          </div>
          <p className="mt-1 max-w-2xl text-sm text-muted-foreground">
            Quy đổi bảng điểm theo tiêu chuẩn giáo dục Hoa Kỳ (WES 4.0 tham khảo), phân tích điểm theo nhóm môn và nhận diện đà tăng trưởng học thuật (Growth Mindset).
          </p>
        </div>

        <div className="flex items-center gap-2">
          <Link
            href="/profile/academic"
            className={buttonVariants({ variant: "outline", size: "lg" })}
          >
            <PencilIcon aria-hidden="true" /> Chỉnh sửa bảng điểm
          </Link>
          <Link
            href="/advisor"
            className={buttonVariants({ size: "lg" })}
          >
            <BotIcon aria-hidden="true" /> Tư vấn AI <ArrowRightIcon aria-hidden="true" />
          </Link>
        </div>
      </div>

      {/* Thông báo lỗi tải dữ liệu */}
      {fetchError ? (
        <Alert variant="destructive">
          <AlertTitle>{fetchError}</AlertTitle>
          <AlertDescription>
            Không thể tải dữ liệu phân tích từ máy chủ. Vui lòng kiểm tra lại kết nối và thử lại.
          </AlertDescription>
          <div className="mt-2">
            <Button
              type="button"
              variant="destructive"
              size="lg"
              onClick={handleRetry}
            >
              Thử tải lại dữ liệu
            </Button>
          </div>
        </Alert>
      ) : (
        <>
          {/* Toast Feedback */}
          {message && (
            <Alert variant={message.type === "success" ? "success" : "destructive"}>
              <AlertDescription>
                {message.text}
              </AlertDescription>
            </Alert>
          )}

          {/* Empty State: Nếu chưa có môn học nào được nhập */}
          {scores.length === 0 ? (
            <Card>
              <Empty>
                <EmptyHeader>
                  <EmptyMedia variant="icon">
                    <FileTextIcon aria-hidden="true" />
                  </EmptyMedia>
                  <EmptyTitle>
                    Chưa có dữ liệu bảng điểm học tập
                  </EmptyTitle>
                  <EmptyDescription>
                    Bạn cần nhập bảng điểm các kỳ học (hoặc tải mẫu học bạ) tại mục <strong>Hồ sơ học thuật</strong> trước khi hệ thống có thể tính toán GPA 4.0 và phân tích đà tăng trưởng.
                  </EmptyDescription>
                </EmptyHeader>
                <EmptyContent>
                  <Link
                    href="/profile/academic"
                    className={buttonVariants({ size: "lg" })}
                  >
                    <FileTextIcon aria-hidden="true" /> Nhập bảng điểm tại Hồ sơ học thuật <ArrowRightIcon aria-hidden="true" />
                  </Link>
                </EmptyContent>
              </Empty>
            </Card>
          ) : (
            <>
              {/* Analytics Dashboard (Executive Summary + 2-col analytics) */}
              {analysis && (
                <div className="w-full min-w-0 space-y-6">
                  <GpaSummaryCard analysis={analysis} />
                  <div className="grid w-full min-w-0 gap-6 md:grid-cols-2">
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
