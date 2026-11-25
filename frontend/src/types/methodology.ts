// UC-83: danh mục tham số của thuật toán gợi ý trường và của ước tính chi phí (GET /api/v1/methodology/parameters), chỉ đọc

export interface AlgorithmParameter {
  key: string;
  /** Bước của thuật toán: B4, B9, "Bậc ngoại khóa", "Cần gì để vào"... hoặc "Chi phí" (tham số ước tính chi phí) */
  step: string;
  name: string;
  /** Chuỗi để hiển thị, số thập phân dùng dấu chấm */
  value: string;
  meaning: string;
}

export interface MethodologyParameters {
  algorithmVersion: string;
  parameters: AlgorithmParameter[];
}
