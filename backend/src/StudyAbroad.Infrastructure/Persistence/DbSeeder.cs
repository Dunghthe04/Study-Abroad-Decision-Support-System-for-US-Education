using Microsoft.EntityFrameworkCore;
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
        // 1. Seed trung tâm du học mẫu
        if (!await db.StudyCenters.AnyAsync(ct))
        {
            db.StudyCenters.AddRange(
                new StudyCenter
                {
                    Code = "DEMO_001", Name = "Demo Center Hà Nội", City = "Hà Nội",
                    StudyLevels = [StudyLevels.Secondary, StudyLevels.Undergraduate, StudyLevels.Master],
                    Services = ["CHON_TRUONG", "SAN_HOC_BONG", "LUYEN_PHONG_VAN"],
                },
                new StudyCenter
                {
                    Code = "DEMO_002", Name = "Demo Center TP.HCM", City = "TP.HCM",
                    StudyLevels = [StudyLevels.CommunityCollege, StudyLevels.Undergraduate, StudyLevels.Master, StudyLevels.Phd],
                    Services = ["CHON_TRUONG", "LUYEN_THI", "SUA_BAI_LUAN"],
                });
            await db.SaveChangesAsync(ct);
        }

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
}
