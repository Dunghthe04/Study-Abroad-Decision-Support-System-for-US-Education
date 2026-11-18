// Chức năng 1 "Con của tôi": phụ huynh xem dữ liệu của con đã liên kết, và gợi ý trường mục tiêu cho con
// (con tự thêm vào danh sách hoặc bỏ qua ở trang Trường mục tiêu).
// Gợi ý (UC-87) và kịch bản (UC-86) đã nhận studentId ở recommendation-history-api / scenario-api.

import { apiFetch, profileApi } from "@/lib/api";
import type { ParentChildProfileDto, TargetSchoolDto } from "@/types/api";

export const parentChildrenApi = {
  // Các con đang liên kết, kèm hồ sơ chỉ xem. Dùng lại endpoint UC-117, bỏ lớp bọc { message, data }.
  listChildren: async (): Promise<ParentChildProfileDto[]> =>
    (await profileApi.getChildrenProfiles()).data.children,

  // 1.5a: thêm con chưa có tài khoản (tối đa 5). Lỗi tên, ngày sinh, quá số con thì backend trả 400 kèm câu báo.
  addManagedChild: (body: { fullName: string; dateOfBirth: string }) =>
    apiFetch<{ studentUserId: string; fullName: string; dateOfBirth: string | null }>("/api/v1/parent/children/managed", {
      method: "POST",
      body: JSON.stringify(body),
    }),

  // Trường mục tiêu của một con. Không còn liên kết thì backend trả 404, apiFetch ném lỗi.
  targetSchools: (studentId: string) =>
    apiFetch<TargetSchoolDto[]>(
      `/api/v1/parent/children/${encodeURIComponent(studentId)}/target-schools`,
      { method: "GET" },
    ),

  // Gợi ý một trường cho con: thêm dòng status parent_suggested. Con đã có / đã được gợi ý trường đó thì backend trả 400.
  // Truyền universityId hoặc universityCode (trang chi tiết trường chỉ có mã trường).
  suggestTargetSchool: (
    studentId: string,
    body: { universityId?: string; universityCode?: string; note?: string },
  ) =>
    apiFetch<TargetSchoolDto>(
      `/api/v1/parent/children/${encodeURIComponent(studentId)}/target-schools`,
      { method: "POST", body: JSON.stringify(body) },
    ),

  // Rút lại gợi ý con chưa xem. Con đã thêm hoặc bỏ qua rồi thì backend trả 404.
  withdrawSuggestion: (studentId: string, targetSchoolId: string) =>
    apiFetch<void>(
      `/api/v1/parent/children/${encodeURIComponent(studentId)}/target-schools/${encodeURIComponent(targetSchoolId)}`,
      { method: "DELETE" },
    ),
};
