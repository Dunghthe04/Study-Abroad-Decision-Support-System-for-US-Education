using Microsoft.EntityFrameworkCore;
using StudyAbroad.Domain.Constants;
using StudyAbroad.Domain.Entities;
using StudyAbroad.Infrastructure.Auth;

namespace StudyAbroad.Infrastructure.Persistence;

/// <summary>Seeds a few demo rows so the UI has data in development. Real data is imported from the survey.</summary>
public static class DbSeeder
{
    public static async Task SeedAsync(AppDbContext db, CancellationToken ct = default)
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

        // 2. [USAS-362] Seed tài khoản Quản trị viên (Admin) mặc định
        if (!await db.Users.AnyAsync(u => u.Role == UserRoles.Admin, ct))
        {
            var hasher = new PasswordHasher();
            db.Users.Add(new User
            {
                Email = "admin@usas.edu.vn",
                PasswordHash = hasher.Hash("123456789"),
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
      "maxUsd": 120000,
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
      "name": "IELTS",
      "category": "english",
      "min": 0,
      "max": 9,
      "step": 0.5,
      "placeholder": "Ví dụ: 7.0"
    },
    {
      "id": "toefl",
      "name": "TOEFL iBT",
      "category": "english",
      "min": 0,
      "max": 120,
      "step": 1,
      "placeholder": "Ví dụ: 95"
    },
    {
      "id": "duolingo",
      "name": "Duolingo English Test",
      "category": "english",
      "min": 10,
      "max": 160,
      "step": 5,
      "placeholder": "Ví dụ: 125"
    },
    {
      "id": "sat",
      "name": "SAT",
      "category": "standardized",
      "min": 400,
      "max": 1600,
      "step": 10,
      "placeholder": "Ví dụ: 1420"
    },
    {
      "id": "act",
      "name": "ACT",
      "category": "standardized",
      "min": 1,
      "max": 36,
      "step": 1,
      "placeholder": "Ví dụ: 31"
    },
    {
      "id": "ap",
      "name": "Advanced Placement (AP)",
      "category": "standardized",
      "min": 1,
      "max": 5,
      "step": 1,
      "placeholder": "Ví dụ: 5"
    },
    {
      "id": "gre",
      "name": "GRE General",
      "category": "graduate",
      "min": 260,
      "max": 340,
      "step": 1,
      "placeholder": "Ví dụ: 320"
    },
    {
      "id": "gmat",
      "name": "GMAT",
      "category": "graduate",
      "min": 200,
      "max": 800,
      "step": 10,
      "placeholder": "Ví dụ: 680"
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
      "level": 1,
      "label": "Trường",
      "desc": "Cấp trường / CLB học sinh"
    },
    {
      "level": 2,
      "label": "Tỉnh / Thành",
      "desc": "Cấp tỉnh / thành phố / quận huyện"
    },
    {
      "level": 3,
      "label": "Quốc gia",
      "desc": "Học sinh giỏi / kỳ thi / dự án cấp toàn quốc"
    },
    {
      "level": 4,
      "label": "Quốc tế",
      "desc": "Olympic / kỳ thi / tổ chức quốc tế"
    }
  ],
  "usStates": [
    {
      "code": "AL",
      "name": "Alabama",
      "isPopular": false
    },
    {
      "code": "AK",
      "name": "Alaska",
      "isPopular": false
    },
    {
      "code": "AZ",
      "name": "Arizona",
      "isPopular": false
    },
    {
      "code": "AR",
      "name": "Arkansas",
      "isPopular": false
    },
    {
      "code": "CA",
      "name": "California",
      "isPopular": true
    },
    {
      "code": "CO",
      "name": "Colorado",
      "isPopular": false
    },
    {
      "code": "CT",
      "name": "Connecticut",
      "isPopular": false
    },
    {
      "code": "DE",
      "name": "Delaware",
      "isPopular": false
    },
    {
      "code": "FL",
      "name": "Florida",
      "isPopular": true
    },
    {
      "code": "GA",
      "name": "Georgia",
      "isPopular": false
    },
    {
      "code": "HI",
      "name": "Hawaii",
      "isPopular": false
    },
    {
      "code": "ID",
      "name": "Idaho",
      "isPopular": false
    },
    {
      "code": "IL",
      "name": "Illinois",
      "isPopular": true
    },
    {
      "code": "IN",
      "name": "Indiana",
      "isPopular": false
    },
    {
      "code": "IA",
      "name": "Iowa",
      "isPopular": false
    },
    {
      "code": "KS",
      "name": "Kansas",
      "isPopular": false
    },
    {
      "code": "KY",
      "name": "Kentucky",
      "isPopular": false
    },
    {
      "code": "LA",
      "name": "Louisiana",
      "isPopular": false
    },
    {
      "code": "ME",
      "name": "Maine",
      "isPopular": false
    },
    {
      "code": "MD",
      "name": "Maryland",
      "isPopular": false
    },
    {
      "code": "MA",
      "name": "Massachusetts",
      "isPopular": true
    },
    {
      "code": "MI",
      "name": "Michigan",
      "isPopular": false
    },
    {
      "code": "MN",
      "name": "Minnesota",
      "isPopular": false
    },
    {
      "code": "MS",
      "name": "Mississippi",
      "isPopular": false
    },
    {
      "code": "MO",
      "name": "Missouri",
      "isPopular": false
    },
    {
      "code": "MT",
      "name": "Montana",
      "isPopular": false
    },
    {
      "code": "NE",
      "name": "Nebraska",
      "isPopular": false
    },
    {
      "code": "NV",
      "name": "Nevada",
      "isPopular": false
    },
    {
      "code": "NH",
      "name": "New Hampshire",
      "isPopular": false
    },
    {
      "code": "NJ",
      "name": "New Jersey",
      "isPopular": false
    },
    {
      "code": "NM",
      "name": "New Mexico",
      "isPopular": false
    },
    {
      "code": "NY",
      "name": "New York",
      "isPopular": true
    },
    {
      "code": "NC",
      "name": "North Carolina",
      "isPopular": false
    },
    {
      "code": "ND",
      "name": "North Dakota",
      "isPopular": false
    },
    {
      "code": "OH",
      "name": "Ohio",
      "isPopular": false
    },
    {
      "code": "OK",
      "name": "Oklahoma",
      "isPopular": false
    },
    {
      "code": "OR",
      "name": "Oregon",
      "isPopular": false
    },
    {
      "code": "PA",
      "name": "Pennsylvania",
      "isPopular": true
    },
    {
      "code": "RI",
      "name": "Rhode Island",
      "isPopular": false
    },
    {
      "code": "SC",
      "name": "South Carolina",
      "isPopular": false
    },
    {
      "code": "SD",
      "name": "South Dakota",
      "isPopular": false
    },
    {
      "code": "TN",
      "name": "Tennessee",
      "isPopular": false
    },
    {
      "code": "TX",
      "name": "Texas",
      "isPopular": true
    },
    {
      "code": "UT",
      "name": "Utah",
      "isPopular": false
    },
    {
      "code": "VT",
      "name": "Vermont",
      "isPopular": false
    },
    {
      "code": "VA",
      "name": "Virginia",
      "isPopular": false
    },
    {
      "code": "WA",
      "name": "Washington",
      "isPopular": true
    },
    {
      "code": "WV",
      "name": "West Virginia",
      "isPopular": false
    },
    {
      "code": "WI",
      "name": "Wisconsin",
      "isPopular": false
    },
    {
      "code": "WY",
      "name": "Wyoming",
      "isPopular": false
    },
    {
      "code": "DC",
      "name": "District of Columbia",
      "isPopular": false
    }
  ],
  "popularStateCodes": [
    "CA",
    "TX",
    "NY",
    "MA",
    "IL",
    "PA",
    "WA",
    "FL"
  ],
  "usMajors": [
    {
      "id": "cs",
      "name": "Khoa học máy tính",
      "englishName": "Computer Science",
      "category": "STEM"
    },
    {
      "id": "ds",
      "name": "Khoa học dữ liệu",
      "englishName": "Data Science",
      "category": "STEM"
    },
    {
      "id": "ai",
      "name": "Trí tuệ nhân tạo",
      "englishName": "Artificial Intelligence",
      "category": "STEM"
    },
    {
      "id": "se",
      "name": "Kỹ thuật phần mềm",
      "englishName": "Software Engineering",
      "category": "STEM"
    },
    {
      "id": "ce",
      "name": "Kỹ thuật máy tính",
      "englishName": "Computer Engineering",
      "category": "STEM"
    },
    {
      "id": "it",
      "name": "Công nghệ thông tin",
      "englishName": "Information Technology",
      "category": "STEM"
    },
    {
      "id": "cyber",
      "name": "An toàn không gian mạng",
      "englishName": "Cybersecurity",
      "category": "STEM"
    },
    {
      "id": "me",
      "name": "Kỹ thuật cơ khí",
      "englishName": "Mechanical Engineering",
      "category": "STEM"
    },
    {
      "id": "ee",
      "name": "Kỹ thuật điện - điện tử",
      "englishName": "Electrical Engineering",
      "category": "STEM"
    },
    {
      "id": "cve",
      "name": "Kỹ thuật xây dựng & dân dụng",
      "englishName": "Civil Engineering",
      "category": "STEM"
    },
    {
      "id": "che",
      "name": "Kỹ thuật hóa học",
      "englishName": "Chemical Engineering",
      "category": "STEM"
    },
    {
      "id": "bme",
      "name": "Kỹ thuật y sinh",
      "englishName": "Biomedical Engineering",
      "category": "STEM"
    },
    {
      "id": "ae",
      "name": "Kỹ thuật hàng không vũ trụ",
      "englishName": "Aerospace Engineering",
      "category": "STEM"
    },
    {
      "id": "ie",
      "name": "Kỹ thuật công nghiệp",
      "englishName": "Industrial Engineering",
      "category": "STEM"
    },
    {
      "id": "bio",
      "name": "Khoa học sinh học",
      "englishName": "Biological Sciences",
      "category": "Khoa học tự nhiên"
    },
    {
      "id": "chem",
      "name": "Hóa học",
      "englishName": "Chemistry",
      "category": "Khoa học tự nhiên"
    },
    {
      "id": "phy",
      "name": "Vật lý học",
      "englishName": "Physics",
      "category": "Khoa học tự nhiên"
    },
    {
      "id": "math",
      "name": "Toán học & Thống kê",
      "englishName": "Mathematics",
      "category": "Khoa học tự nhiên"
    },
    {
      "id": "ba",
      "name": "Quản trị kinh doanh",
      "englishName": "Business Administration",
      "category": "Kinh doanh & Quản lý"
    },
    {
      "id": "fin",
      "name": "Tài chính",
      "englishName": "Finance",
      "category": "Kinh doanh & Quản lý"
    },
    {
      "id": "acc",
      "name": "Kế toán",
      "englishName": "Accounting",
      "category": "Kinh doanh & Quản lý"
    },
    {
      "id": "mkt",
      "name": "Tiếp thị & Truyền thông",
      "englishName": "Marketing",
      "category": "Kinh doanh & Quản lý"
    },
    {
      "id": "eco",
      "name": "Kinh tế học",
      "englishName": "Economics",
      "category": "Kinh doanh & Quản lý"
    },
    {
      "id": "psy",
      "name": "Tâm lý học",
      "englishName": "Psychology",
      "category": "Khoa học xã hội"
    },
    {
      "id": "comm",
      "name": "Truyền thông & Đa phương tiện",
      "englishName": "Communication",
      "category": "Khoa học xã hội"
    },
    {
      "id": "arch",
      "name": "Kiến trúc & Quy hoạch",
      "englishName": "Architecture",
      "category": "Nghệ thuật & Thiết kế"
    },
    {
      "id": "design",
      "name": "Thiết kế đồ họa & Tương tác",
      "englishName": "Graphic Design",
      "category": "Nghệ thuật & Thiết kế"
    },
    {
      "id": "nursing",
      "name": "Điều dưỡng & Y tế",
      "englishName": "Nursing",
      "category": "Y tế & Sức khỏe"
    },
    {
      "id": "premed",
      "name": "Dự bị y khoa",
      "englishName": "Pre-Medicine",
      "category": "Y tế & Sức khỏe"
    },
    {
      "id": "undecided",
      "name": "Chưa quyết định / Khám phá",
      "englishName": "Undecided / Exploratory",
      "category": "Tổng quát"
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
