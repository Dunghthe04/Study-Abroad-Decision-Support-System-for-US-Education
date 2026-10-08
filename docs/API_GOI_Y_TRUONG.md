# API gợi ý trường (cho frontend)

Tài liệu cho người làm giao diện nút **"Lọc trường theo hồ sơ"**. Cách hệ thống chọn trường và chấm điểm nằm ở [GOI_Y_TRUONG_GIAI_THICH.md](GOI_Y_TRUONG_GIAI_THICH.md).

## Đọc nhanh

- Chỉ có **một nút**. Bấm nút gọi `POST /api/v1/recommendations`, chờ kết quả rồi hiện danh sách trường.
- Request **mất khoảng 90–110 giây** vì LLM viết giải thích cho từng trường. Giao diện phải có trạng thái chờ rõ ràng, và proxy không được cắt request sớm (xem mục Timeout).
- Lần sau mở trang thì gọi `GET /api/v1/recommendations/latest` để hiện kết quả đã lưu, không phải bấm lại.

## Endpoint

| Method | URL | Dùng khi | Thời gian |
|---|---|---|---|
| `POST` | `/api/v1/recommendations` | Bấm nút "Lọc trường theo hồ sơ" | 90–110 s |
| `GET` | `/api/v1/recommendations/latest` | Mở trang, hiện kết quả lần trước | < 1 s |

**Đăng nhập:** dùng cookie đăng nhập như các API khác (`credentials: "include"`), không gửi `userId`. Server lấy người dùng từ cookie.

| Mã | Ý nghĩa | Giao diện nên làm |
|---|---|---|
| 200 | Có kết quả (danh sách có thể rỗng, xem `warnings`) | Hiện danh sách và cảnh báo |
| 401 | Chưa đăng nhập | Chuyển sang trang đăng nhập |
| 404 | Chưa có hồ sơ học sinh (`POST`), hoặc chưa lọc lần nào (`GET latest`) | `POST`: nhắc tạo hồ sơ. `GET`: hiện nút lọc, chưa có kết quả |

## Timeout

Mặc định dev proxy của Next.js cắt request sau 30 giây, nên nút sẽ báo lỗi dù backend vẫn đang chạy. Cần:

```ts
// next.config.ts
const nextConfig: NextConfig = {
  // ...giữ nguyên các dòng cũ
  experimental: { proxyTimeout: 180_000 },   // 3 phút cho API gợi ý trường
};
```

```bash
# frontend/.env.local (không commit)
API_PROXY_TARGET=http://localhost:5080
```

Khi deploy, Nginx đã để 300 giây cho `/api/v1/recommendations` (`deploy/nginx/snippets/locations.conf`).

## Kết quả trả về

```ts
type Category = "reach" | "match" | "safety" | "insufficient_data";
type EnglishStatus = "met" | "below_min" | "no_score" | "unknown";

interface RecommendationResult {
  id: string;
  createdAt: string;          // ISO, giờ UTC
  studyLevel: string;         // undergraduate, master...
  items: RecommendationItem[];
  warnings: string[];         // câu tiếng Việt, hiện nguyên văn
}

interface RecommendationItem {
  rank: number;               // 1, 2, 3... theo thứ tự hiển thị
  universityId: string;
  offeringId: string;
  code: string;
  name: string;
  state: string | null;
  category: Category;
  score: number | null;       // điểm xếp hạng nội bộ 0–1, KHÔNG phải xác suất đậu. Không hiện cho học sinh
  totalCostUsd: number | null;// tổng chi phí/năm
  costUnknown: boolean;       // true = trường không công bố chi phí
  english: EnglishStatus;
  openAdmission: boolean;     // true = trường tuyển sinh mở (cao đẳng cộng đồng)
  reason: string;             // giải thích 2–3 câu, tiếng Việt
  aiExplained: boolean;       // true = AI viết, false = câu soạn sẵn
  school: SchoolInfo | null;  // null ở kết quả cũ lưu trước ngày 08/10
}

interface SchoolInfo {
  city: string | null;
  state: string | null;
  control: "public" | "private" | null;
  website: string | null;
  acceptanceRate: number | null;      // 0–1, ví dụ 0.45 = 45%
  internationalStudents: number | null;
  satPolicy: "required" | "optional" | "not_accepted" | null;
  tuitionUsd: number | null;
  livingUsd: number | null;
  feesUsd: number | null;
  minIelts: number | null;
  minToefl: number | null;
  minDuolingo: number | null;
}
```

### Ví dụ (một trường, lấy từ lần chạy thật)

```json
{
  "rank": 4,
  "universityId": "43d38b9f-0c1b-4c1a-af06-b7ff4b300b14",
  "offeringId": "6c0790e3-5707-435f-8d13-1846d80efb3d",
  "code": "212805",
  "name": "Grove City College",
  "state": "PA",
  "category": "match",
  "score": 0.7424,
  "totalCostUsd": 33930.0,
  "costUnknown": false,
  "english": "unknown",
  "openAdmission": false,
  "reason": "Điểm SAT 1380 của bạn nằm trong khoảng 25%-75% (1141-1388) của sinh viên trúng tuyển, nên đây là lựa chọn vừa sức với bạn. Chi phí 33,930 USD/năm trong ngân sách. Hồ sơ ngoại khóa 3.5/4 là lợi thế, hãy dùng nó làm chủ đề bài luận.",
  "aiExplained": true,
  "school": {
    "city": "Grove City",
    "state": "PA",
    "control": "private",
    "website": "https://www.gcc.edu/",
    "acceptanceRate": 0.7232,
    "internationalStudents": 23,
    "satPolicy": null,
    "tuitionUsd": 21700.0,
    "livingUsd": 12230.0,
    "feesUsd": null,
    "minIelts": null,
    "minToefl": null,
    "minDuolingo": null
  }
}
```

## Hiển thị

### Nhóm trường

`items` đã được sắp xếp sẵn: reach → match → safety → chưa đủ dữ liệu. Chia nhóm theo `category`, giữ nguyên thứ tự trong mảng.

| `category` | Nhãn | Gợi ý màu |
|---|---|---|
| `reach` | Thử sức | Cam |
| `match` | Vừa sức | Xanh dương |
| `safety` | An toàn | Xanh lá |
| `insufficient_data` | Chưa đủ dữ liệu | Xám |

### Tiếng Anh

| `english` | Hiển thị |
|---|---|
| `met` | Đạt yêu cầu tiếng Anh |
| `below_min` | **Chưa đạt** mức tối thiểu (cảnh báo đỏ) |
| `no_score` | Bạn chưa nhập điểm tiếng Anh |
| `unknown` | Trường chưa công bố mức tối thiểu (không cần hiện gì) |

### Chi phí

- `costUnknown = true`: hiện "Chưa có dữ liệu chi phí".
- Ngược lại hiện `totalCostUsd`. Có `school` thì có thể tách học phí / sinh hoạt / phí khác.

### Giải thích

- Hiện `reason` nguyên văn.
- `aiExplained = false` nghĩa là AI không phản hồi hoặc giải thích AI bị bỏ vì sai số liệu; câu đang hiện là câu soạn sẵn. Có thể thêm chú thích nhỏ, không bắt buộc.
- **Không hiện `score`** và không biến nó thành phần trăm: đây là điểm xếp hạng, không phải khả năng đậu.

### Cảnh báo

`warnings` là các câu tiếng Việt hiện nguyên văn phía trên danh sách. Ví dụ:

- "Chưa có GPA thang 4 (chưa phân tích bảng điểm), tạm chỉ xét SAT."
- "Chưa có điểm tiếng Anh (IELTS/TOEFL/Duolingo), chưa kiểm tra điều kiện tiếng Anh."
- "AI tạm thời chưa phản hồi, danh sách và giải thích theo kết quả chấm điểm."
- "Không có trường nào phù hợp với ngành, bang và ngân sách hiện tại. Hãy thử nới điều kiện." (khi `items` rỗng)

## Bảng điểm ngoại khóa

Mỗi lần bấm nút, hệ thống chấm lại điểm ngoại khóa từ các hoạt động loại `extracurricular` và `experience` trong hồ sơ (giải thưởng `award` chưa tính), rồi trả kèm trường `extracurricular`. Kết quả lưu trước ngày 08/10 không có trường này, nên giao diện coi nó là tùy chọn (`?`). `GET /latest` cũng trả trường này.

```ts
interface RecommendationResult {
  // ...các trường ở trên
  extracurricular?: {
    score: number | null;       // 0–4; null = chưa tính được, SAW bỏ tiêu chí ngoại khóa
    fresh: boolean;             // false = đang dùng điểm lần trước
    aiUsed: boolean;
    activities: {
      id: string;               // id của hoạt động trong hồ sơ (profile_activities)
      name: string;             // tên hoạt động học sinh nhập
      role: "member" | "deputy" | "head" | "founder";
      reputableOrg: boolean;
      impactLevel: number;      // 1 trường … 5 quốc tế, sau khi AI đối chiếu mô tả
      quality: number;          // 0–1
      points: number;           // đóng góp vào điểm tổng
      counted: boolean;         // false = ngoài 4 hoạt động tốt nhất
    }[];
  };
}
```

Gợi ý hiển thị: điểm tổng "3.0 / 4" và bảng từng hoạt động (vai trò, phạm vi, điểm). Hoạt động có `counted = false` thì làm mờ. Nếu `impactLevel` thấp hơn mức học sinh đã khai, ghi chú "AI đánh giá phạm vi thực tế là cấp trường".
