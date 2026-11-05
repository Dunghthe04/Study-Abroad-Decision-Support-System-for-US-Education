using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Nodes;
using StudyAbroad.Application.Profile.Flow;
using GpaModes = StudyAbroad.Domain.Constants.ProfileConstants.GpaModes;

namespace StudyAbroad.Application.Advisor;

/// <summary>
/// Học bạ theo năm gửi sang chat thành mục riêng (AdvisorChatRequest.Transcript), không nằm trong hồ sơ chat:
/// chat chỉ đọc (học bạ từng lớp chỉ nhập hoặc OCR ở form), SLM chọn skill và Qwen đọc tin không cần thấy.
/// Skill tư vấn dùng để nhận xét xu hướng điểm và từng môn.
/// </summary>
public static class ChatTranscript
{
    // Dòng điểm mục tiêu của học sinh THCS (Step2Transcript.tsx): là mục tiêu, không phải điểm thật
    private const string TargetRow = "GPA Mục Tiêu";

    /// <summary>
    /// Chỉ có khi nhập theo học bạ; nhập GPA tổng thì null, không ép học sinh nhập điểm từng lớp.
    /// Mỗi môn = TranscriptGpa.YearAverage, TB năm = trung bình các môn, tính đủ số lẻ rồi chỉ làm tròn 2 chữ số
    /// khi gửi đi. Năm THCS chỉ có dòng mục tiêu thì ghi target; môn và năm chưa có điểm thì bỏ
    /// </summary>
    public static JsonObject? From(FlowTranscriptDraftDto? step2)
    {
        if (TranscriptGpa.Mode(step2) != GpaModes.Transcript) return null;

        var years = new List<JsonNode>();
        foreach (var year in step2?.AcademicYears ?? [])
        {
            var subjects = year.Subjects ?? [];
            var target = subjects.FirstOrDefault(s => s.SubjectName == TargetRow)?.Semester1Score;
            var scored = subjects
                .Where(s => s.SubjectName != TargetRow)
                .Select(s => (Name: s.SubjectName, Average: TranscriptGpa.YearAverage(s.Semester1Score, s.Semester2Score)))
                .Where(s => s.Average is not null)
                .Select(s => (s.Name, Average: s.Average!.Value))
                .ToList();

            if (scored.Count > 0)
                years.Add(new JsonObject
                {
                    ["grade"] = year.GradeName,
                    ["average"] = Math.Round(scored.Average(s => s.Average), 2),
                    ["subjects"] = new JsonArray(scored
                        .Select(s => (JsonNode)new JsonObject { ["name"] = s.Name, ["average"] = Math.Round(s.Average, 2) })
                        .ToArray()),
                });
            else if (target is { } goal)
                years.Add(new JsonObject { ["grade"] = year.GradeName, ["target"] = goal });
        }
        return years.Count == 0 ? null : new JsonObject { ["years"] = new JsonArray(years.ToArray()) };
    }
}
