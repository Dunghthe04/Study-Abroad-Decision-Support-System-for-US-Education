// Chức năng 1 "Con của tôi": phụ huynh xem dữ liệu của con đã liên kết. Chỉ đọc.
// Gợi ý (UC-87) và kịch bản (UC-86) đã nhận studentId ở recommendation-history-api / scenario-api.

import { apiFetch, profileApi } from "@/lib/api";
import type { ParentChildProfileDto, TargetSchoolDto } from "@/types/api";

export const parentChildrenApi = {
  // Các con đang liên kết, kèm hồ sơ chỉ xem. Dùng lại endpoint UC-117, bỏ lớp bọc { message, data }.
  listChildren: async (): Promise<ParentChildProfileDto[]> =>
    (await profileApi.getChildrenProfiles()).data.children,

  // Trường mục tiêu của một con. Không còn liên kết thì backend trả 404, apiFetch ném lỗi.
  targetSchools: (studentId: string) =>
    apiFetch<TargetSchoolDto[]>(
      `/api/v1/parent/children/${encodeURIComponent(studentId)}/target-schools`,
      { method: "GET" },
    ),
};
