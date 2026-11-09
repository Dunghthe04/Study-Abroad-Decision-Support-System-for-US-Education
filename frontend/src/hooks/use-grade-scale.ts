import * as React from "react";

import { getGradeScaleConfig } from "@/lib/academic-api";
import { getGradeScaleTable, setGradeScaleRules, subscribeGradeScale, WES_GPA10_TO_4 } from "@/lib/profile-flow-options";

// Mỗi lần mở trang chỉ tải một lần; lỗi thì để null để lần sau thử lại, trong lúc đó toGpa4 dùng bảng WES mặc định
let loading: Promise<void> | null = null;

function loadGradeScale() {
  loading ??= getGradeScaleConfig()
    .then((config) => setGradeScaleRules(config.rules))
    .catch(() => {
      loading = null;
    });
}

/**
 * Tải bảng quy đổi grade_scale.10 (moderator sửa) từ API cho toGpa4 / calculateTranscriptGpa / draftGpa.
 * Component nào hiện GPA quy đổi thì gọi hook này để vẽ lại khi bảng tải xong
 */
export function useGradeScale() {
  React.useEffect(loadGradeScale, []);
  return React.useSyncExternalStore(subscribeGradeScale, getGradeScaleTable, () => WES_GPA10_TO_4);
}
