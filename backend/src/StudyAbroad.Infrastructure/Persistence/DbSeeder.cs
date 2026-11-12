using Microsoft.EntityFrameworkCore;
using StudyAbroad.Application.Profile.Academic;
using StudyAbroad.Domain.Constants;
using StudyAbroad.Domain.Entities;
using StudyAbroad.Infrastructure.Auth;

namespace StudyAbroad.Infrastructure.Persistence;

/// <summary>Seeds a few demo rows so the UI has data in development. Real data is imported from the survey.</summary>
public static class DbSeeder
{
    /// <param name="adminPassword">Mật khẩu admin mặc định (env SEED_ADMIN_PASSWORD). Để trống thì không tạo admin.</param>
    public static async Task SeedAsync(AppDbContext db, string? adminPassword, CancellationToken ct = default)
    {
        // 1. Seed danh sách trung tâm du học từ khảo sát thực tế và dữ liệu chuẩn hoá
        await SeedStudyCentersAsync(db, ct);

        // 2. [USAS-362] Seed tài khoản Quản trị viên (Admin) mặc định, chỉ khi có mật khẩu từ env (không fix cứng trong code)
        if (!string.IsNullOrWhiteSpace(adminPassword) && !await db.Users.AnyAsync(u => u.Role == UserRoles.Admin, ct))
        {
            var hasher = new PasswordHasher();
            db.Users.Add(new User
            {
                Email = "admin@usas.edu.vn",
                PasswordHash = hasher.Hash(adminPassword),
                FullName = "Quản trị viên Hệ thống",
                Role = UserRoles.Admin,
                Status = UserStatuses.Active,
                Phone = "0988888888"
            });
            await db.SaveChangesAsync(ct);
        }

        // 3. [USAS-640] Seed siêu dữ liệu biểu mẫu hồ sơ (không fix cứng code)
        var existingMetadata = await db.AppSettings.FirstOrDefaultAsync(s => s.Key == ProfileConstants.ProfileMetadataAppSettingKey, ct);
        var defaultMetadataJson = GetDefaultProfileMetadataJson();
        if (existingMetadata is null)
        {
            db.AppSettings.Add(new AppSetting
            {
                Key = ProfileConstants.ProfileMetadataAppSettingKey,
                ValueJson = defaultMetadataJson,
                Description = "Cấu hình toàn bộ danh mục lựa chọn, các mức ngân sách và mẫu biểu điểm nhập hồ sơ đa bước (USAS-640)."
            });
            await db.SaveChangesAsync(ct);
        }
        else
        {
            existingMetadata.ValueJson = defaultMetadataJson;
            await db.SaveChangesAsync(ct);
        }

        // 4. Bảng quy đổi điểm thang 10 → thang 4 (grade_scale.10). Chỉ thêm khi chưa có, không ghi đè:
        // moderator sửa bảng này trên trang quản trị, ghi đè mỗi lần chạy sẽ mất bảng đã sửa
        if (!await db.AppSettings.AnyAsync(s => s.Key == AcademicConstants.GradeScale10AppSettingKey, ct))
        {
            db.AppSettings.Add(new AppSetting
            {
                Key = AcademicConstants.GradeScale10AppSettingKey,
                ValueJson = GradeScale.DefaultJson(),
                Description = "Bảng quy đổi điểm thang 10 sang GPA thang 4 (tra cận dưới MinScore), moderator sửa trên trang quản trị."
            });
            await db.SaveChangesAsync(ct);
        }
    }

    private static string GetDefaultProfileMetadataJson()
    {
        return """
{
  "studyLevels": [
    {
      "id": "community_college",
      "name": "Cao đẳng",
      "subName": "Community college",
      "duration": "2 năm",
      "description": "Cao đẳng cộng đồng chuyển tiếp 2+2 lên Đại học",
      "defaultGradeScale": "10",
      "defaultEducationSystem": "standard"
    },
    {
      "id": "undergraduate",
      "name": "Đại học",
      "subName": "Bachelor",
      "duration": "4 năm",
      "description": "Cử nhân Đại học chính quy 4 năm tại Mỹ",
      "defaultGradeScale": "10",
      "defaultEducationSystem": "standard"
    },
    {
      "id": "master",
      "name": "Thạc sĩ",
      "subName": "Master",
      "duration": "1–2 năm",
      "description": "Sau đại học (Thạc sĩ khoa học / quản trị kinh doanh)",
      "defaultGradeScale": "4",
      "defaultEducationSystem": "university"
    },
    {
      "id": "phd",
      "name": "Tiến sĩ",
      "subName": "PhD",
      "duration": "4–6 năm",
      "description": "Nghiên cứu sinh & Tiến sĩ học thuật tại Mỹ",
      "defaultGradeScale": "4",
      "defaultEducationSystem": "university"
    }
  ],
  "currentEducationLevels": [
    {
      "id": "middle_school",
      "label": "Trung học cơ sở"
    },
    {
      "id": "high_school",
      "label": "Trung học phổ thông"
    },
    {
      "id": "college_university",
      "label": "Cao đẳng / Đại học"
    },
    {
      "id": "master",
      "label": "Thạc sĩ"
    }
  ],
  "budgetTiers": [
    {
      "id": "tier_under_20k",
      "label": "Dưới 20.000 USD",
      "minUsd": 0,
      "maxUsd": 20000,
      "approxUsd": 15000,
      "description": "Phù hợp xin học bổng lớn hoặc Cao đẳng cộng đồng chi phí tiết kiệm."
    },
    {
      "id": "tier_20k_35k",
      "label": "20.000 – 35.000 USD",
      "minUsd": 20000,
      "maxUsd": 35000,
      "approxUsd": 28000,
      "description": "Mức chi trả phổ biến cho các trường Đại học công lập chất lượng."
    },
    {
      "id": "tier_35k_50k",
      "label": "35.000 – 50.000 USD",
      "minUsd": 35000,
      "maxUsd": 50000,
      "approxUsd": 42000,
      "description": "Phù hợp phần lớn trường Đại học công lập và tư thục tầm trung tại Mỹ."
    },
    {
      "id": "tier_50k_70k",
      "label": "50.000 – 70.000 USD",
      "minUsd": 50000,
      "maxUsd": 70000,
      "approxUsd": 60000,
      "description": "Phù hợp trường tư thục danh tiếng và các trường top tier."
    },
    {
      "id": "tier_above_70k",
      "label": "Trên 70.000 USD",
      "minUsd": 70000,
      "maxUsd": null,
      "approxUsd": 85000,
      "description": "Tự tin chi trả cho mọi trường đại học hàng đầu không cần học bổng."
    }
  ],
  "scholarshipNeeds": [
    {
      "id": "mandatory",
      "label": "Bắt buộc",
      "subLabel": "Không có học bổng thì chưa thể đi",
      "needsScholarship": true
    },
    {
      "id": "preferred",
      "label": "Nên có",
      "subLabel": "Càng nhiều càng tốt",
      "needsScholarship": true
    },
    {
      "id": "optional",
      "label": "Không cần",
      "subLabel": "Gia đình tự chi trả được",
      "needsScholarship": false
    }
  ],
  "fundingSources": [
    {
      "id": "family_support",
      "label": "Gia đình hỗ trợ toàn bộ / một phần"
    },
    {
      "id": "personal_savings",
      "label": "Tiết kiệm cá nhân"
    },
    {
      "id": "bank_loan",
      "label": "Vay vốn du học ngân hàng"
    },
    {
      "id": "scholarship",
      "label": "Trông đợi vào học bổng"
    },
    {
      "id": "other",
      "label": "Nguồn tài chính khác"
    }
  ],
  "educationSystems": [
    {
      "id": "standard",
      "label": "Hệ chuẩn công lập Việt Nam"
    },
    {
      "id": "specialized",
      "label": "Hệ chuyên (Trường THPT Chuyên / Năng khiếu)"
    },
    {
      "id": "dual_degree",
      "label": "Hệ song bằng (Việt - Anh / Cambridge)"
    },
    {
      "id": "international",
      "label": "Hệ quốc tế hoàn toàn (IB, AP, Cambridge)"
    },
    {
      "id": "private",
      "label": "Dân lập / Tư thục chất lượng cao"
    },
    {
      "id": "university",
      "label": "Hệ Đại học chính quy / Tiên tiến"
    },
    {
      "id": "other",
      "label": "Hệ khác"
    }
  ],
  "gradeScales": [
    {
      "id": "10",
      "name": "Thang 10",
      "desc": "0.0 - 10.0 (Phổ biến tại Việt Nam)",
      "min": 0,
      "max": 10,
      "step": 0.1
    },
    {
      "id": "100",
      "name": "Thang 100",
      "desc": "0 - 100 (Hệ phần trăm)",
      "min": 0,
      "max": 100,
      "step": 1
    },
    {
      "id": "4",
      "name": "Thang 4",
      "desc": "0.0 - 4.0 (Hệ tín chỉ Đại học)",
      "min": 0,
      "max": 4,
      "step": 0.01
    },
    {
      "id": "letter",
      "name": "Thang chữ",
      "desc": "A+, A, B+, B, C, D, F",
      "min": 0,
      "max": 4,
      "step": 0.1
    }
  ],
  "gradeFormTemplates": {
    "high_school": {
      "levelType": "high_school",
      "description": "Bảng điểm 3 năm THPT (Lớp 10, 11, 12)",
      "defaultGradeScale": "10",
      "terms": [
        {
          "termOrder": 1,
          "termName": "Lớp 10 - Học kỳ 1"
        },
        {
          "termOrder": 2,
          "termName": "Lớp 10 - Học kỳ 2"
        },
        {
          "termOrder": 3,
          "termName": "Lớp 11 - Học kỳ 1"
        },
        {
          "termOrder": 4,
          "termName": "Lớp 11 - Học kỳ 2"
        },
        {
          "termOrder": 5,
          "termName": "Lớp 12 - Học kỳ 1"
        },
        {
          "termOrder": 6,
          "termName": "Lớp 12 - Học kỳ 2"
        }
      ],
      "defaultSubjects": [
        {
          "subject": "Toán học",
          "credits": 2
        },
        {
          "subject": "Ngữ văn",
          "credits": 2
        },
        {
          "subject": "Tiếng Anh",
          "credits": 2
        },
        {
          "subject": "Vật lý",
          "credits": 1
        },
        {
          "subject": "Hóa học",
          "credits": 1
        },
        {
          "subject": "Sinh học",
          "credits": 1
        },
        {
          "subject": "Lịch sử",
          "credits": 1
        },
        {
          "subject": "Địa lý",
          "credits": 1
        },
        {
          "subject": "Tin học",
          "credits": 1
        }
      ]
    },
    "university": {
      "levelType": "university",
      "description": "Bảng điểm Đại học (Tín chỉ / 4 năm)",
      "defaultGradeScale": "4",
      "terms": [
        {
          "termOrder": 1,
          "termName": "Năm 1 - Học kỳ 1"
        },
        {
          "termOrder": 2,
          "termName": "Năm 1 - Học kỳ 2"
        },
        {
          "termOrder": 3,
          "termName": "Năm 2 - Học kỳ 1"
        },
        {
          "termOrder": 4,
          "termName": "Năm 2 - Học kỳ 2"
        },
        {
          "termOrder": 5,
          "termName": "Năm 3 - Học kỳ 1"
        },
        {
          "termOrder": 6,
          "termName": "Năm 3 - Học kỳ 2"
        },
        {
          "termOrder": 7,
          "termName": "Năm 4 - Học kỳ 1"
        },
        {
          "termOrder": 8,
          "termName": "Năm 4 - Học kỳ 2"
        }
      ],
      "defaultSubjects": [
        {
          "subject": "Toán cao cấp / Đại số",
          "credits": 3
        },
        {
          "subject": "Xác suất thống kê",
          "credits": 3
        },
        {
          "subject": "Tiếng Anh chuyên ngành",
          "credits": 3
        },
        {
          "subject": "Nhập môn chuyên ngành",
          "credits": 3
        },
        {
          "subject": "Cơ sở ngành",
          "credits": 3
        },
        {
          "subject": "Chuyên ngành nâng cao",
          "credits": 3
        }
      ]
    }
  },
  "certifications": [
    {
      "id": "ielts",
      "name": "IELTS Academic",
      "category": "english",
      "group": "popular",
      "order": 1,
      "aliases": [
        "ielts",
        "ielts academic"
      ],
      "min": 0,
      "max": 9,
      "step": 0.5,
      "placeholder": "Ví dụ: 6.5",
      "scaleLabel": "Thang 0–9",
      "validityYears": 2,
      "sections": [
        {
          "id": "listening",
          "label": "Nghe",
          "min": 0,
          "max": 9,
          "step": 0.5
        },
        {
          "id": "reading",
          "label": "Đọc",
          "min": 0,
          "max": 9,
          "step": 0.5
        },
        {
          "id": "writing",
          "label": "Viết",
          "min": 0,
          "max": 9,
          "step": 0.5
        },
        {
          "id": "speaking",
          "label": "Nói",
          "min": 0,
          "max": 9,
          "step": 0.5
        }
      ],
      "sectionsHint": "Nên điền, nhiều trường yêu cầu điểm tối thiểu mỗi kỹ năng",
      "requiresSubject": false,
      "subjectLabel": null,
      "profileField": "ielts"
    },
    {
      "id": "toefl",
      "name": "TOEFL iBT",
      "category": "english",
      "group": "popular",
      "order": 2,
      "aliases": [
        "toefl",
        "toefl ibt",
        "ibt"
      ],
      "min": 0,
      "max": 120,
      "step": 1,
      "placeholder": "Ví dụ: 95",
      "scaleLabel": "Thang 0–120",
      "validityYears": 2,
      "sections": [
        {
          "id": "listening",
          "label": "Nghe",
          "min": 0,
          "max": 30,
          "step": 1
        },
        {
          "id": "reading",
          "label": "Đọc",
          "min": 0,
          "max": 30,
          "step": 1
        },
        {
          "id": "writing",
          "label": "Viết",
          "min": 0,
          "max": 30,
          "step": 1
        },
        {
          "id": "speaking",
          "label": "Nói",
          "min": 0,
          "max": 30,
          "step": 1
        }
      ],
      "sectionsHint": "Nên điền, nhiều trường yêu cầu điểm tối thiểu mỗi kỹ năng",
      "requiresSubject": false,
      "subjectLabel": null,
      "profileField": "toefl"
    },
    {
      "id": "duolingo",
      "name": "Duolingo English Test",
      "category": "english",
      "group": "popular",
      "order": 3,
      "aliases": [
        "duolingo",
        "det"
      ],
      "min": 10,
      "max": 160,
      "step": 5,
      "placeholder": "Ví dụ: 125",
      "scaleLabel": "Thang 10–160",
      "validityYears": 2,
      "sections": null,
      "sectionsHint": null,
      "requiresSubject": false,
      "subjectLabel": null,
      "profileField": "duolingo"
    },
    {
      "id": "pte",
      "name": "PTE Academic",
      "category": "english",
      "group": "popular",
      "order": 4,
      "aliases": [
        "pte",
        "pearson"
      ],
      "min": 10,
      "max": 90,
      "step": 1,
      "placeholder": "Ví dụ: 65",
      "scaleLabel": "Thang 10–90",
      "validityYears": 2,
      "sections": [
        {
          "id": "listening",
          "label": "Nghe",
          "min": 10,
          "max": 90,
          "step": 1
        },
        {
          "id": "reading",
          "label": "Đọc",
          "min": 10,
          "max": 90,
          "step": 1
        },
        {
          "id": "writing",
          "label": "Viết",
          "min": 10,
          "max": 90,
          "step": 1
        },
        {
          "id": "speaking",
          "label": "Nói",
          "min": 10,
          "max": 90,
          "step": 1
        }
      ],
      "sectionsHint": "Nên điền, nhiều trường yêu cầu điểm tối thiểu mỗi kỹ năng",
      "requiresSubject": false,
      "subjectLabel": null,
      "profileField": null
    },
    {
      "id": "cambridge",
      "name": "Cambridge English (C1 Advanced, C2 Proficiency)",
      "category": "english",
      "group": "other",
      "order": 5,
      "aliases": [
        "cambridge",
        "cae",
        "cpe",
        "c1 advanced",
        "c2 proficiency"
      ],
      "min": 80,
      "max": 230,
      "step": 1,
      "placeholder": "Ví dụ: 185",
      "scaleLabel": "Thang 80–230",
      "validityYears": null,
      "sections": null,
      "sectionsHint": null,
      "requiresSubject": false,
      "subjectLabel": null,
      "profileField": null
    },
    {
      "id": "itep",
      "name": "iTEP Academic",
      "category": "english",
      "group": "other",
      "order": 6,
      "aliases": [
        "itep"
      ],
      "min": 0,
      "max": 6,
      "step": 0.5,
      "placeholder": "Ví dụ: 4.5",
      "scaleLabel": "Thang 0–6",
      "validityYears": 2,
      "sections": null,
      "sectionsHint": null,
      "requiresSubject": false,
      "subjectLabel": null,
      "profileField": null
    },
    {
      "id": "languagecert",
      "name": "LanguageCert Academic",
      "category": "english",
      "group": "other",
      "order": 7,
      "aliases": [
        "languagecert",
        "language cert"
      ],
      "min": 0,
      "max": 100,
      "step": 1,
      "placeholder": "Ví dụ: 75",
      "scaleLabel": "Thang 0–100",
      "validityYears": 2,
      "sections": null,
      "sectionsHint": null,
      "requiresSubject": false,
      "subjectLabel": null,
      "profileField": null
    },
    {
      "id": "sat",
      "name": "SAT",
      "category": "standardized",
      "group": "popular",
      "order": 1,
      "aliases": [
        "sat"
      ],
      "min": 400,
      "max": 1600,
      "step": 10,
      "placeholder": "Ví dụ: 1420",
      "scaleLabel": "Thang 400–1600",
      "validityYears": null,
      "sections": [
        {
          "id": "math",
          "label": "Toán",
          "min": 200,
          "max": 800,
          "step": 10
        },
        {
          "id": "reading_writing",
          "label": "Đọc và Viết",
          "min": 200,
          "max": 800,
          "step": 10
        }
      ],
      "sectionsHint": "Không bắt buộc",
      "requiresSubject": false,
      "subjectLabel": null,
      "profileField": "sat"
    },
    {
      "id": "act",
      "name": "ACT",
      "category": "standardized",
      "group": "popular",
      "order": 2,
      "aliases": [
        "act"
      ],
      "min": 1,
      "max": 36,
      "step": 1,
      "placeholder": "Ví dụ: 31",
      "scaleLabel": "Thang 1–36",
      "validityYears": null,
      "sections": null,
      "sectionsHint": null,
      "requiresSubject": false,
      "subjectLabel": null,
      "profileField": "act"
    },
    {
      "id": "ap",
      "name": "AP (Advanced Placement)",
      "category": "standardized",
      "group": "popular",
      "order": 3,
      "aliases": [
        "ap",
        "advanced placement"
      ],
      "min": 1,
      "max": 5,
      "step": 1,
      "placeholder": "Ví dụ: 5",
      "scaleLabel": "Thang 1–5",
      "validityYears": null,
      "sections": null,
      "sectionsHint": null,
      "requiresSubject": true,
      "subjectLabel": "Tên môn AP",
      "profileField": "ap"
    },
    {
      "id": "ib",
      "name": "IB Diploma",
      "category": "standardized",
      "group": "other",
      "order": 4,
      "aliases": [
        "ib",
        "international baccalaureate"
      ],
      "min": 1,
      "max": 45,
      "step": 1,
      "placeholder": "Ví dụ: 38",
      "scaleLabel": "Thang 1–45",
      "validityYears": null,
      "sections": null,
      "sectionsHint": null,
      "requiresSubject": false,
      "subjectLabel": null,
      "profileField": null
    },
    {
      "id": "alevel",
      "name": "A-Level",
      "category": "standardized",
      "group": "other",
      "order": 5,
      "aliases": [
        "a level",
        "a-level",
        "alevel",
        "gce"
      ],
      "min": null,
      "max": null,
      "step": null,
      "placeholder": "Ví dụ: A*",
      "scaleLabel": "Điểm chữ A*–E",
      "validityYears": null,
      "sections": null,
      "sectionsHint": null,
      "requiresSubject": true,
      "subjectLabel": "Tên môn A-Level",
      "profileField": null
    },
    {
      "id": "gre",
      "name": "GRE General",
      "category": "graduate",
      "group": "other",
      "order": 6,
      "aliases": [
        "gre"
      ],
      "min": 260,
      "max": 340,
      "step": 1,
      "placeholder": "Ví dụ: 320",
      "scaleLabel": "Thang 260–340",
      "validityYears": 5,
      "sections": null,
      "sectionsHint": null,
      "requiresSubject": false,
      "subjectLabel": null,
      "profileField": "gre"
    },
    {
      "id": "gmat",
      "name": "GMAT",
      "category": "graduate",
      "group": "other",
      "order": 7,
      "aliases": [
        "gmat"
      ],
      "min": 200,
      "max": 800,
      "step": 10,
      "placeholder": "Ví dụ: 680",
      "scaleLabel": "Thang 200–800",
      "validityYears": 5,
      "sections": null,
      "sectionsHint": null,
      "requiresSubject": false,
      "subjectLabel": null,
      "profileField": "gmat"
    }
  ],
  "activityCategories": [
    {
      "id": "extracurricular",
      "label": "Hoạt động ngoại khóa / CLB"
    },
    {
      "id": "award",
      "label": "Giải thưởng / Thành tích"
    }
  ],
  "impactLevels": [
    {
      "id": "school",
      "level": 1,
      "label": "Trường",
      "desc": "Cấp trường / CLB học sinh"
    },
    {
      "id": "district",
      "level": 2,
      "label": "Quận / Huyện",
      "desc": "Cấp quận / huyện"
    },
    {
      "id": "province",
      "level": 3,
      "label": "Tỉnh / Thành",
      "desc": "Cấp tỉnh / thành phố"
    },
    {
      "id": "national",
      "level": 4,
      "label": "Quốc gia",
      "desc": "Học sinh giỏi / kỳ thi / dự án cấp toàn quốc"
    },
    {
      "id": "international",
      "level": 5,
      "label": "Quốc tế",
      "desc": "Olympic / kỳ thi / tổ chức quốc tế"
    }
  ],
  "activityRoles": [
    {
      "id": "founder",
      "label": "Sáng lập"
    },
    {
      "id": "leader",
      "label": "Trưởng / Chủ nhiệm"
    },
    {
      "id": "vice_leader",
      "label": "Phó"
    },
    {
      "id": "member",
      "label": "Thành viên"
    }
  ],
  "institutionTypes": [
    {
      "id": "any",
      "label": "Không lọc"
    },
    {
      "id": "public",
      "label": "Công lập"
    },
    {
      "id": "private",
      "label": "Tư thục"
    }
  ]
}
""";
    }

    private static async Task SeedStudyCentersAsync(AppDbContext db, CancellationToken ct)
    {
        var centers = GetPredefinedStudyCenters();
        foreach (var center in centers)
        {
            var existing = await db.StudyCenters.FirstOrDefaultAsync(s => s.Code == center.Code, ct);
            if (existing is null)
            {
                db.StudyCenters.Add(center);
            }
            else
            {
                existing.Name = center.Name;
                existing.CenterType = center.CenterType;
                existing.City = center.City;
                existing.District = center.District;
                existing.Address = center.Address;
                existing.Latitude = center.Latitude;
                existing.Longitude = center.Longitude;
                existing.Phone = center.Phone;
                existing.Email = center.Email;
                existing.Website = center.Website;
                existing.Description = center.Description;
                existing.StudyLevels = center.StudyLevels;
                existing.Services = center.Services;
                existing.StaffCount = center.StaffCount;
                existing.EstablishedYear = center.EstablishedYear;
                existing.LicenseNumber = center.LicenseNumber;
                existing.TaxCode = center.TaxCode;
                existing.LegalRepresentative = center.LegalRepresentative;
                existing.LicenseIssuedBy = center.LicenseIssuedBy;
                existing.LicenseIssuedDate = center.LicenseIssuedDate;
                existing.LegalComplianceMode = center.LegalComplianceMode;
                existing.TrainingLicenseNumber = center.TrainingLicenseNumber;
                existing.CertifiedCounselorCount = center.CertifiedCounselorCount;
                existing.LegalDocumentUrls = center.LegalDocumentUrls;
                existing.VerificationNote = center.VerificationNote;
                existing.Accreditations = center.Accreditations;
                existing.VisaSuccessRate = center.VisaSuccessRate;
                existing.AverageRating = center.AverageRating;
                existing.ReviewCount = center.ReviewCount;
                existing.LogoUrl = center.LogoUrl;
                existing.CoverImageUrl = center.CoverImageUrl;
                existing.GalleryImages = center.GalleryImages;
                existing.VerificationStatus = center.VerificationStatus;
                existing.IsActive = center.IsActive;
            }
        }
        await db.SaveChangesAsync(ct);
    }

    private static List<StudyCenter> GetPredefinedStudyCenters()
    {
        return
        [
            new StudyCenter
            {
                Code = "CTY_013",
                Name = "Summit Education Services",
                CenterType = "hybrid",
                City = "Hà Nội",
                District = "Đống Đa",
                Address = "Tầng 2, Tòa nhà 101 Láng Hạ, Đống Đa, Hà Nội",
                Latitude = 21.0152,
                Longitude = 105.8156,
                Phone = "0901 702 889",
                Email = "contact@summit.edu.vn",
                Website = "https://summit.edu.vn",
                StaffCount = 65,
                EstablishedYear = 2010,
                LicenseNumber = "GPHĐ-GD-2010/HN-088",
                TaxCode = "0104671982",
                LegalRepresentative = "Lê Minh Hoàng",
                LicenseIssuedBy = "Sở GD&ĐT Hà Nội",
                LicenseIssuedDate = new DateOnly(2010, 6, 15),
                LegalComplianceMode = "licensed",
                TrainingLicenseNumber = "GPĐT-NN-102/QĐ-SGDĐT-HN",
                CertifiedCounselorCount = 28,
                VerificationNote = "Đã đối chiếu GCN ĐKDN, giấy phép tư vấn du học và quyết định đào tạo ngoại ngữ.",
                Accreditations = ["AIRC Accredited", "ICEF Agent Member", "ETS Certified Partner", "British Council Platinum Member"],
                VisaSuccessRate = 99.2,
                AverageRating = 4.90m,
                ReviewCount = 186,
                VerificationStatus = "verified",
                StudyLevels = [StudyLevels.Secondary, StudyLevels.Undergraduate, StudyLevels.Master],
                Services = ["CHON_TRUONG", "SAN_HOC_BONG", "LUYEN_THI_SAT", "LUYEN_THI_IELTS", "HOAT_DONG_NGOAI_KHOA", "SUA_BAI_LUAN", "LUYEN_PHONG_VAN", "XIN_VISA"],
                Description = "Summit Education Services là tổ chức tư vấn du học và đào tạo học thuật uy tín hàng đầu, chuyên sâu chiến lược săn học bổng vào các trường Đại học Top đầu Mỹ và Ivy League. Cung cấp chẩn đoán SAT miễn phí, lộ trình 1-1 cá nhân hóa, đồng hành xây dựng hoạt động ngoại khóa theo chiều sâu ngành học và cố vấn các vòng nộp đơn Early/Regular.",
                LogoUrl = "https://images.unsplash.com/photo-1546410531-bb4caa6b424d?auto=format&fit=crop&w=400&q=80",
                CoverImageUrl = "https://images.unsplash.com/photo-1523050854058-8df90110c9f1?auto=format&fit=crop&w=1600&q=80",
                GalleryImages =
                [
                    "https://images.unsplash.com/photo-1523240795612-9a054b0db644?auto=format&fit=crop&w=800&q=80",
                    "https://images.unsplash.com/photo-1577896851231-70ef18881754?auto=format&fit=crop&w=800&q=80",
                    "https://images.unsplash.com/photo-1517486808906-6ca8b3f04846?auto=format&fit=crop&w=800&q=80"
                ]
            },
            new StudyCenter
            {
                Code = "CTY_014",
                Name = "Du học New Ocean (Tân Đại Dương)",
                CenterType = "study_abroad",
                City = "Hà Nội",
                District = "Cầu Giấy",
                Address = "Tòa nhà New Ocean, 56 Phạm Thận Duật, Mai Dịch, Cầu Giấy, Hà Nội",
                Latitude = 21.0402,
                Longitude = 105.7761,
                Phone = "0964 562 233",
                Email = "contact@newocean.edu.vn",
                Website = "https://newocean.edu.vn",
                StaffCount = 48,
                EstablishedYear = 2007,
                LicenseNumber = "ĐKKD-0102381920/Sở GD&ĐT Hà Nội",
                TaxCode = "0102381920",
                LegalRepresentative = "Nguyễn Thị Lan Phương",
                LicenseIssuedBy = "Sở GD&ĐT Hà Nội",
                LicenseIssuedDate = new DateOnly(2007, 9, 20),
                LegalComplianceMode = "licensed",
                CertifiedCounselorCount = 24,
                VerificationNote = "Đã kiểm tra GCN ĐKDN, giấy phép Sở GD&ĐT Hà Nội và chứng chỉ tư vấn viên.",
                Accreditations = ["ICEF Screened Agency", "WEBA Member", "VIECA Vietnam"],
                VisaSuccessRate = 98.6,
                AverageRating = 4.80m,
                ReviewCount = 142,
                VerificationStatus = "verified",
                StudyLevels = [StudyLevels.CommunityCollege, StudyLevels.Undergraduate, StudyLevels.Master, StudyLevels.Phd],
                Services = ["CHON_TRUONG", "CHUYEN_TIEP_2_CONG_2", "THAC_SI_STEM", "CHUNG_MINH_TAI_CHINH", "LUYEN_PHONG_VAN", "XIN_VISA"],
                Description = "Với gần 20 năm kinh nghiệm tư vấn du học và di trú Hoa Kỳ, New Ocean chuyên sâu xử lý các bộ hồ sơ chứng minh tài chính phức tạp, giải trình nguồn tiền an toàn, chuyển tiếp CĐCĐ 2+2 tối ưu chi phí và lộ trình Thạc sĩ khối STEM ở lại làm việc 3 năm (OPT 36 tháng).",
                LogoUrl = "https://images.unsplash.com/photo-1516321318423-f06f85e504b3?auto=format&fit=crop&w=400&q=80",
                CoverImageUrl = "https://images.unsplash.com/photo-1498243691581-b145c3f54a5a?auto=format&fit=crop&w=1600&q=80",
                GalleryImages =
                [
                    "https://images.unsplash.com/photo-1522202176988-66273c2fd55f?auto=format&fit=crop&w=800&q=80",
                    "https://images.unsplash.com/photo-1531482615713-2afd69097998?auto=format&fit=crop&w=800&q=80"
                ]
            },
            new StudyCenter
            {
                Code = "CTY_010",
                Name = "Học viện Giáo dục & Du học American Study",
                CenterType = "hybrid",
                City = "Hà Nội",
                District = "Cầu Giấy",
                Address = "Tòa nhà Yên Hòa Sunshine, số 9 Vũ Phạm Hàm, Yên Hòa, Cầu Giấy, Hà Nội",
                Latitude = 21.0185,
                Longitude = 105.7972,
                Phone = "0964 102 268",
                Email = "contact@americanstudy.edu.vn",
                Website = "https://americanstudy.edu.vn",
                StaffCount = 80,
                EstablishedYear = 2014,
                LicenseNumber = "GP-2819/QĐ-SGDĐT-HN",
                TaxCode = "0106549321",
                LegalRepresentative = "Trần Quốc Bảo",
                LicenseIssuedBy = "Sở GD&ĐT Hà Nội",
                LicenseIssuedDate = new DateOnly(2014, 4, 10),
                LegalComplianceMode = "licensed",
                TrainingLicenseNumber = "GPĐT-2820/QĐ-SGDĐT-HN",
                CertifiedCounselorCount = 32,
                VerificationNote = "Đủ điều kiện hoạt động song song tư vấn du học và luyện thi chuẩn hóa.",
                Accreditations = ["College Board Test Center", "ACT Global Partner", "NACAC Member", "AIRC Certified"],
                VisaSuccessRate = 99.5,
                AverageRating = 4.95m,
                ReviewCount = 215,
                VerificationStatus = "verified",
                StudyLevels = [StudyLevels.Secondary, StudyLevels.Undergraduate, StudyLevels.Master],
                Services = ["LUYEN_THI_SAT", "LUYEN_THI_ACT", "SAN_HOC_BONG", "HOAT_DONG_NGOAI_KHOA", "SUA_BAI_LUAN", "LUYEN_PHONG_VAN", "XIN_VISA"],
                Description = "American Study là học viện hàng đầu chuyên luyện thi SAT/ACT/AP và tư vấn chiến lược du học Mỹ vào các trường Top 10 - Top 50. Hỗ trợ học sinh xây dựng bài luận mang đậm dấu ấn cá nhân và dự án ngoại khóa quốc tế giúp chinh phục học bổng lên tới 100% học phí.",
                LogoUrl = "https://images.unsplash.com/photo-1594608661623-aa0bd3a69d98?auto=format&fit=crop&w=400&q=80",
                CoverImageUrl = "https://images.unsplash.com/photo-1541339907198-e08756dedf3f?auto=format&fit=crop&w=1600&q=80",
                GalleryImages =
                [
                    "https://images.unsplash.com/photo-1524178232363-1fb2b075b655?auto=format&fit=crop&w=800&q=80",
                    "https://images.unsplash.com/photo-1523240795612-9a054b0db644?auto=format&fit=crop&w=800&q=80"
                ]
            },
            new StudyCenter
            {
                Code = "CTY_005",
                Name = "Du học New World (New World Education)",
                CenterType = "study_abroad",
                City = "TP.HCM",
                District = "Quận 3",
                Address = "Tòa nhà New World, 183 Trần Cao Vân, Phường 6, Quận 3, TP. Hồ Chí Minh",
                Latitude = 10.7845,
                Longitude = 106.6942,
                Phone = "0908 898 699",
                Email = "info@newworldedu.vn",
                Website = "https://newworldedu.vn",
                StaffCount = 52,
                EstablishedYear = 2012,
                LicenseNumber = "0311749202/Sở GD&ĐT TP.HCM",
                TaxCode = "0311749202",
                LegalRepresentative = "Trần Thị Minh Nguyệt",
                LicenseIssuedBy = "Sở GD&ĐT TP.HCM",
                LicenseIssuedDate = new DateOnly(2012, 5, 18),
                LegalComplianceMode = "licensed",
                CertifiedCounselorCount = 22,
                VerificationNote = "Đã kiểm tra GCN ĐKDN TP.HCM và văn bản chấp thuận tư vấn du học.",
                Accreditations = ["AIRC Certified", "ICEF Agency Member", "English USA Partner"],
                VisaSuccessRate = 98.8,
                AverageRating = 4.85m,
                ReviewCount = 168,
                VerificationStatus = "verified",
                StudyLevels = [StudyLevels.Secondary, StudyLevels.CommunityCollege, StudyLevels.Undergraduate, StudyLevels.Master],
                Services = ["CHON_TRUONG", "CHUYEN_TIEP_2_CONG_2", "SAN_HOC_BONG", "LUYEN_PHONG_VAN", "XIN_VISA"],
                Description = "Đại diện tuyển sinh chính thức của hơn 300 trường đại học và cao đẳng danh tiếng tại Mỹ. Nổi bật với chương trình Hoàn tất trung học kép (High School Completion) từ 16 tuổi, chuyển tiếp CĐCĐ bang Washington và săn học bổng Merit tự động theo GPA.",
                LogoUrl = "https://images.unsplash.com/photo-1509062522246-3755977927d7?auto=format&fit=crop&w=400&q=80",
                CoverImageUrl = "https://images.unsplash.com/photo-1562774053-701939374585?auto=format&fit=crop&w=1600&q=80",
                GalleryImages =
                [
                    "https://images.unsplash.com/photo-1517486808906-6ca8b3f04846?auto=format&fit=crop&w=800&q=80",
                    "https://images.unsplash.com/photo-1522202176988-66273c2fd55f?auto=format&fit=crop&w=800&q=80"
                ]
            },
            new StudyCenter
            {
                Code = "CTY_009",
                Name = "Tổ chức Giáo dục Quốc tế PEC (PEC Education)",
                CenterType = "hybrid",
                City = "Hải Phòng",
                District = "Ngô Quyền",
                Address = "41 Lạch Tray, Quận Ngô Quyền, TP. Hải Phòng",
                Latitude = 20.8491,
                Longitude = 106.6897,
                Phone = "0931 595 588",
                Email = "duhoc@pec.edu.vn",
                Website = "https://pec.edu.vn",
                StaffCount = 38,
                EstablishedYear = 2005,
                LicenseNumber = "GPHĐ-02013894/Sở GD&ĐT Hải Phòng",
                TaxCode = "0200638491",
                LegalRepresentative = "Phạm Văn Hùng",
                LicenseIssuedBy = "Sở GD&ĐT Hải Phòng",
                LicenseIssuedDate = new DateOnly(2005, 8, 12),
                LegalComplianceMode = "licensed",
                TrainingLicenseNumber = "GPĐT-HP-02013895/SGDĐT",
                CertifiedCounselorCount = 16,
                VerificationNote = "Đã xác minh giấy phép tư vấn du học và quyết định đào tạo ngoại ngữ.",
                Accreditations = ["British Council Test Centre", "IDP IELTS Partner", "ICEF Trained"],
                VisaSuccessRate = 97.9,
                AverageRating = 4.75m,
                ReviewCount = 110,
                VerificationStatus = "verified",
                StudyLevels = [StudyLevels.Secondary, StudyLevels.CommunityCollege, StudyLevels.Undergraduate, StudyLevels.Master],
                Services = ["LUYEN_THI_IELTS", "LUYEN_THI_SAT", "CHON_TRUONG", "SAN_HOC_BONG", "LUYEN_PHONG_VAN", "XIN_VISA"],
                Description = "Hệ thống Anh ngữ và Tư vấn Du học Quốc tế uy tín bậc nhất tại Hải Phòng và khu vực Duyên hải phía Bắc. Cung cấp quy trình khép kín từ đào tạo IELTS/SAT cam kết đầu ra đến nộp hồ sơ xin I-20 và luyện phỏng vấn visa cùng cố vấn nước ngoài.",
                LogoUrl = "https://images.unsplash.com/photo-1577495508048-b635879837f1?auto=format&fit=crop&w=400&q=80",
                CoverImageUrl = "https://images.unsplash.com/photo-1523240795612-9a054b0db644?auto=format&fit=crop&w=1600&q=80",
                GalleryImages =
                [
                    "https://images.unsplash.com/photo-1517486808906-6ca8b3f04846?auto=format&fit=crop&w=800&q=80"
                ]
            },
            new StudyCenter
            {
                Code = "CTY_003",
                Name = "Tư vấn Du học & Học bổng Edu4life",
                CenterType = "study_abroad",
                City = "Hà Nội",
                District = "Ba Đình",
                Address = "Tầng 4, Tòa nhà DMC, 535 Kim Mã, Ngọc Khánh, Ba Đình, Hà Nội",
                Latitude = 21.0312,
                Longitude = 105.8123,
                Phone = "0983 234 580",
                Email = "info@edu4life.vn",
                Website = "https://edu4life.vn",
                StaffCount = 25,
                EstablishedYear = 2016,
                LicenseNumber = "1204/QĐ-SGDĐT Hà Nội",
                TaxCode = "0107428156",
                LegalRepresentative = "Nguyễn Hoàng Nam",
                LicenseIssuedBy = "Sở GD&ĐT Hà Nội",
                LicenseIssuedDate = new DateOnly(2016, 11, 8),
                LegalComplianceMode = "licensed",
                CertifiedCounselorCount = 14,
                VerificationNote = "Đã đối chiếu hồ sơ doanh nghiệp và chứng chỉ tư vấn viên du học.",
                Accreditations = ["EducationUSA Network Associate", "ICEF Qualified Agency", "VIECA"],
                VisaSuccessRate = 98.2,
                AverageRating = 4.80m,
                ReviewCount = 95,
                VerificationStatus = "verified",
                StudyLevels = [StudyLevels.Undergraduate, StudyLevels.Master],
                Services = ["CHON_TRUONG", "SAN_HOC_BONG", "SUA_BAI_LUAN", "LUYEN_PHONG_VAN", "XIN_VISA"],
                Description = "Tư vấn du học dựa trên phương pháp 3 phễu lọc và đối soát ngân sách thực tế. Chuyên gia đồng hành hỗ trợ học sinh xây dựng bài luận giàu cảm xúc, khắc phục điểm GPA khiêm tốn bằng hoạt động ngoại khóa có chiều sâu và luyện phỏng vấn visa di trú INA 214(b).",
                LogoUrl = "https://images.unsplash.com/photo-1522202176988-66273c2fd55f?auto=format&fit=crop&w=400&q=80",
                CoverImageUrl = "https://images.unsplash.com/photo-1523050854058-8df90110c9f1?auto=format&fit=crop&w=1600&q=80",
                GalleryImages =
                [
                    "https://images.unsplash.com/photo-1523240795612-9a054b0db644?auto=format&fit=crop&w=800&q=80"
                ]
            },
            new StudyCenter
            {
                Code = "CTY_004",
                Name = "Du học DuhocEZ (EZ Education Vietnam)",
                CenterType = "study_abroad",
                City = "TP.HCM",
                District = "Bình Thạnh",
                Address = "Tòa nhà Landmark Plus, 208 Nguyễn Hữu Cảnh, Phường 22, Bình Thạnh, TP. Hồ Chí Minh",
                Latitude = 10.7938,
                Longitude = 106.7214,
                Phone = "0974 930 860",
                Email = "contact@duhocez.com",
                Website = "https://duhocez.com",
                StaffCount = 30,
                EstablishedYear = 2018,
                LicenseNumber = "GP-031589123/TP.HCM",
                TaxCode = "0315891234",
                LegalRepresentative = "Võ Thanh Tùng",
                LicenseIssuedBy = "Sở GD&ĐT TP.HCM",
                LicenseIssuedDate = new DateOnly(2018, 7, 22),
                LegalComplianceMode = "licensed",
                CertifiedCounselorCount = 15,
                VerificationNote = "Đã đối chiếu mã số doanh nghiệp TP.HCM và giấy xác nhận hoạt động.",
                Accreditations = ["AIRC Institutional Member", "ICEF Verified", "NAFSA Attendee"],
                VisaSuccessRate = 98.4,
                AverageRating = 4.85m,
                ReviewCount = 124,
                VerificationStatus = "verified",
                StudyLevels = [StudyLevels.CommunityCollege, StudyLevels.Undergraduate, StudyLevels.Master],
                Services = ["CHON_TRUONG", "CHUYEN_TIEP_2_CONG_2", "LUYEN_PHONG_VAN", "XIN_VISA"],
                Description = "Nền tảng tư vấn du học số hóa giúp rút ngắn quy trình nộp đơn. Chuyên gia hướng dẫn khai đơn DS-160 chuẩn xác, giải trình sổ tiết kiệm và dòng tiền gia đình minh bạch, đảm bảo tối đa tỷ lệ cấp visa F-1.",
                LogoUrl = "https://images.unsplash.com/photo-1498243691581-b145c3f54a5a?auto=format&fit=crop&w=400&q=80",
                CoverImageUrl = "https://images.unsplash.com/photo-1492538368677-f6e0afe31dcc?auto=format&fit=crop&w=1600&q=80",
                GalleryImages =
                [
                    "https://images.unsplash.com/photo-1522202176988-66273c2fd55f?auto=format&fit=crop&w=800&q=80"
                ]
            },
            new StudyCenter
            {
                Code = "CTY_012",
                Name = "Tổ chức Hướng nghiệp & Du học Mission Edu",
                CenterType = "study_abroad",
                City = "Hà Nội",
                District = "Nam Từ Liêm",
                Address = "Tòa tháp The Manor, Đường Mễ Trì, Mỹ Đình 1, Nam Từ Liêm, Hà Nội",
                Latitude = 21.0140,
                Longitude = 105.7766,
                Phone = "0915 638 998",
                Email = "admissions@missionedu.vn",
                Website = "https://missionedu.vn",
                StaffCount = 22,
                EstablishedYear = 2017,
                LicenseNumber = "892/QĐ-GDĐT Hà Nội",
                TaxCode = "0107913405",
                LegalRepresentative = "Đặng Thu Hà",
                LicenseIssuedBy = "Sở GD&ĐT Hà Nội",
                LicenseIssuedDate = new DateOnly(2017, 10, 5),
                LegalComplianceMode = "licensed",
                CertifiedCounselorCount = 12,
                VerificationNote = "Hồ sơ pháp lý hợp lệ, đủ tư vấn viên có chứng chỉ.",
                Accreditations = ["ICEF Screened", "EducationUSA Partner"],
                VisaSuccessRate = 98.0,
                AverageRating = 4.78m,
                ReviewCount = 88,
                VerificationStatus = "verified",
                StudyLevels = [StudyLevels.Secondary, StudyLevels.Undergraduate],
                Services = ["CHON_TRUONG", "SAN_HOC_BONG", "SUA_BAI_LUAN", "LUYEN_PHONG_VAN", "XIN_VISA"],
                Description = "Đơn vị chuyên tư vấn định hướng du học sớm bậc THPT và Đại học tại Mỹ. Hỗ trợ phụ huynh quản lý ngân sách vừa vặn ($25k - $35k/năm) và cá nhân hóa lộ trình chuẩn bị bài luận 650 từ.",
                LogoUrl = "https://images.unsplash.com/photo-1577896851231-70ef18881754?auto=format&fit=crop&w=400&q=80",
                CoverImageUrl = "https://images.unsplash.com/photo-1523050854058-8df90110c9f1?auto=format&fit=crop&w=1600&q=80",
                GalleryImages =
                [
                    "https://images.unsplash.com/photo-1517486808906-6ca8b3f04846?auto=format&fit=crop&w=800&q=80"
                ]
            },
            new StudyCenter
            {
                Code = "CTY_011",
                Name = "Học viện Du học Sau Đại học & Thạc sĩ Kosam",
                CenterType = "study_abroad",
                City = "Hà Nội",
                District = "Hoàn Kiếm",
                Address = "Tầng 6, Số 24 Tràng Tiền, Phường Tràng Tiền, Hoàn Kiếm, Hà Nội",
                Latitude = 21.0253,
                Longitude = 105.8561,
                Phone = "0904 886 522",
                Email = "consult@kosam.edu.vn",
                Website = "https://kosam.edu.vn",
                StaffCount = 20,
                EstablishedYear = 2015,
                LicenseNumber = "GP-4521/Sở GD&ĐT Hà Nội",
                TaxCode = "0106984521",
                LegalRepresentative = "Phạm Đức Anh",
                LicenseIssuedBy = "Sở GD&ĐT Hà Nội",
                LicenseIssuedDate = new DateOnly(2015, 3, 19),
                LegalComplianceMode = "licensed",
                CertifiedCounselorCount = 11,
                VerificationNote = "Đã xác minh GCN ĐKDN và quyết định tư vấn du học Sau đại học.",
                Accreditations = ["GRE/GMAT Educational Partner", "AIRC Member", "NAFSA Member"],
                VisaSuccessRate = 99.0,
                AverageRating = 4.88m,
                ReviewCount = 76,
                VerificationStatus = "verified",
                StudyLevels = [StudyLevels.Undergraduate, StudyLevels.Master, StudyLevels.Phd],
                Services = ["CHON_TRUONG", "SAN_HOC_BONG", "SUA_BAI_LUAN", "LUYEN_PHONG_VAN", "XIN_VISA"],
                Description = "Đơn vị tư vấn hàng đầu về các chương trình Thạc sĩ, Tiến sĩ và nghiên cứu chuyên sâu tại Mỹ (AI, Data Science, Kỹ thuật phần mềm, MBA). Hướng dẫn sinh viên trái ngành bổ sung môn tiên quyết, tối ưu SOP và chuẩn bị thực tập CPT/OPT.",
                LogoUrl = "https://images.unsplash.com/photo-1531482615713-2afd69097998?auto=format&fit=crop&w=400&q=80",
                CoverImageUrl = "https://images.unsplash.com/photo-1562774053-701939374585?auto=format&fit=crop&w=1600&q=80",
                GalleryImages =
                [
                    "https://images.unsplash.com/photo-1522202176988-66273c2fd55f?auto=format&fit=crop&w=800&q=80"
                ]
            },
            new StudyCenter
            {
                Code = "CTY_002",
                Name = "Tổ chức Giáo dục Beestudious",
                CenterType = "study_abroad",
                City = "Hà Nội",
                District = "Cầu Giấy",
                Address = "Tầng 5, Tòa nhà Detech, số 8 Tôn Thất Thuyết, Cầu Giấy, Hà Nội",
                Latitude = 21.0289,
                Longitude = 105.7818,
                Phone = "0989 332 211",
                Email = "contact@beestudious.edu.vn",
                Website = "https://beestudious.edu.vn",
                StaffCount = 18,
                EstablishedYear = 2019,
                LicenseNumber = "010789221/Sở GD&ĐT Hà Nội",
                TaxCode = "0108789221",
                LegalRepresentative = "Bùi Thảo Vân",
                LicenseIssuedBy = "Sở GD&ĐT Hà Nội",
                LicenseIssuedDate = new DateOnly(2019, 6, 14),
                LegalComplianceMode = "notified",
                CertifiedCounselorCount = 9,
                VerificationNote = "Đã cập nhật hồ sơ thông báo hoạt động tư vấn du học tới Sở GD&ĐT Hà Nội.",
                Accreditations = ["ICEF Screened", "VIECA Member"],
                VisaSuccessRate = 97.5,
                AverageRating = 4.70m,
                ReviewCount = 62,
                VerificationStatus = "verified",
                StudyLevels = [StudyLevels.Secondary, StudyLevels.Undergraduate],
                Services = ["CHON_TRUONG", "SUA_BAI_LUAN", "LUYEN_PHONG_VAN", "XIN_VISA"],
                Description = "Đồng hành xây dựng năng lực tự chủ cho học sinh, khai phá tiềm năng cá nhân và thiết kế hồ sơ du học Mỹ sáng tạo, thực chất.",
                LogoUrl = "https://images.unsplash.com/photo-1524178232363-1fb2b075b655?auto=format&fit=crop&w=400&q=80",
                CoverImageUrl = "https://images.unsplash.com/photo-1498243691581-b145c3f54a5a?auto=format&fit=crop&w=1600&q=80",
                GalleryImages =
                [
                    "https://images.unsplash.com/photo-1523050854058-8df90110c9f1?auto=format&fit=crop&w=800&q=80"
                ]
            },
            new StudyCenter
            {
                Code = "DEMO_001",
                Name = "Demo Center Hà Nội",
                CenterType = "study_abroad",
                City = "Hà Nội",
                District = "Cầu Giấy",
                Address = "Số 1 Đại Cồ Việt, Hai Bà Trưng, Hà Nội",
                Latitude = 21.0074,
                Longitude = 105.8431,
                Phone = "0988 888 001",
                Email = "hanoi@democenter.edu.vn",
                Website = "https://democenter.edu.vn",
                StaffCount = 20,
                EstablishedYear = 2018,
                LicenseNumber = "DEMO-HN-2018",
                TaxCode = "0109998801",
                LegalRepresentative = "Nguyễn Văn An",
                LicenseIssuedBy = "Sở GD&ĐT Hà Nội",
                LicenseIssuedDate = new DateOnly(2018, 1, 15),
                LegalComplianceMode = "licensed",
                CertifiedCounselorCount = 10,
                VerificationNote = "Hồ sơ mẫu kiểm thử hệ thống tại Hà Nội.",
                Accreditations = ["AIRC Candidate"],
                VisaSuccessRate = 98.0,
                AverageRating = 4.80m,
                ReviewCount = 45,
                VerificationStatus = "verified",
                StudyLevels = [StudyLevels.Secondary, StudyLevels.Undergraduate, StudyLevels.Master],
                Services = ["CHON_TRUONG", "SAN_HOC_BONG", "LUYEN_PHONG_VAN"],
                Description = "Trung tâm khảo nghiệm mẫu phục vụ đào tạo và kiểm thử hệ thống tại Hà Nội.",
                LogoUrl = "https://images.unsplash.com/photo-1546410531-bb4caa6b424d?auto=format&fit=crop&w=400&q=80",
                CoverImageUrl = "https://images.unsplash.com/photo-1523050854058-8df90110c9f1?auto=format&fit=crop&w=1600&q=80",
                GalleryImages = ["https://images.unsplash.com/photo-1523240795612-9a054b0db644?auto=format&fit=crop&w=800&q=80"]
            },
            new StudyCenter
            {
                Code = "DEMO_002",
                Name = "Demo Center TP.HCM",
                CenterType = "hybrid",
                City = "TP.HCM",
                District = "Quận 1",
                Address = "Tòa nhà Bitexco, số 2 Hải Triều, Bến Nghé, Quận 1, TP. Hồ Chí Minh",
                Latitude = 10.7718,
                Longitude = 106.7044,
                Phone = "0988 888 002",
                Email = "hcm@democenter.edu.vn",
                Website = "https://democenter.edu.vn",
                StaffCount = 25,
                EstablishedYear = 2019,
                LicenseNumber = "DEMO-HCM-2019",
                TaxCode = "0319998802",
                LegalRepresentative = "Trần Thị Bình",
                LicenseIssuedBy = "Sở GD&ĐT TP.HCM",
                LicenseIssuedDate = new DateOnly(2019, 2, 20),
                LegalComplianceMode = "licensed",
                TrainingLicenseNumber = "DEMO-DT-HCM-2019",
                CertifiedCounselorCount = 12,
                VerificationNote = "Hồ sơ mẫu kiểm thử hệ thống tại TP.HCM (tư vấn + đào tạo).",
                Accreditations = ["ICEF Screened"],
                VisaSuccessRate = 98.5,
                AverageRating = 4.85m,
                ReviewCount = 58,
                VerificationStatus = "verified",
                StudyLevels = [StudyLevels.CommunityCollege, StudyLevels.Undergraduate, StudyLevels.Master, StudyLevels.Phd],
                Services = ["CHON_TRUONG", "LUYEN_THI", "SUA_BAI_LUAN"],
                Description = "Trung tâm khảo nghiệm mẫu phục vụ đào tạo và kiểm thử hệ thống tại TP. Hồ Chí Minh.",
                LogoUrl = "https://images.unsplash.com/photo-1509062522246-3755977927d7?auto=format&fit=crop&w=400&q=80",
                CoverImageUrl = "https://images.unsplash.com/photo-1562774053-701939374585?auto=format&fit=crop&w=1600&q=80",
                GalleryImages = ["https://images.unsplash.com/photo-1517486808906-6ca8b3f04846?auto=format&fit=crop&w=800&q=80"]
            }
        ];
    }
}
