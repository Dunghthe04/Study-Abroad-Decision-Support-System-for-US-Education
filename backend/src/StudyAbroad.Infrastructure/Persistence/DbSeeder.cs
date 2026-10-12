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
        if (!await db.AppSettings.AnyAsync(s => s.Key == ProfileConstants.ProfileMetadataAppSettingKey, ct))
        {
            var defaultMetadataJson = GetDefaultProfileMetadataJson();
            db.AppSettings.Add(new AppSetting
            {
                Key = ProfileConstants.ProfileMetadataAppSettingKey,
                ValueJson = defaultMetadataJson,
                Description = "Cấu hình toàn bộ danh mục lựa chọn, các mức ngân sách và mẫu biểu điểm nhập hồ sơ đa bước (USAS-640)."
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
          "budgetTiers": [
            {
              "id": "tier_under_20k",
              "label": "Dưới 20.000 USD",
              "minUsd": 0,
              "maxUsd": 20000,
              "approxUsd": 15000,
              "description": "Phù hợp xin học bổng toàn phần hoặc Cao đẳng cộng đồng chi phí tiết kiệm."
            },
            {
              "id": "tier_20k_35k",
              "label": "20.000 – 35.000 USD",
              "minUsd": 20000,
              "maxUsd": 35000,
              "approxUsd": 30000,
              "description": "Chi trả trung bình cho trường công lập chất lượng hoặc có học bổng bán phần."
            },
            {
              "id": "tier_35k_50k",
              "label": "35.000 – 50.000 USD",
              "minUsd": 35000,
              "maxUsd": 50000,
              "approxUsd": 45000,
              "description": "Phù hợp phần lớn các trường Đại học công lập và tư thục tầm trung tại Mỹ."
            },
            {
              "id": "tier_above_50k",
              "label": "Trên 50.000 USD",
              "minUsd": 50000,
              "maxUsd": 120000,
              "approxUsd": 65000,
              "description": "Tự tin chi trả cho các trường tư thục danh tiếng, trường top National Universities."
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
            { "id": "family_support", "label": "Gia đình hỗ trợ toàn bộ / một phần" },
            { "id": "personal_savings", "label": "Tiết kiệm cá nhân" },
            { "id": "bank_loan", "label": "Vay vốn du học ngân hàng" },
            { "id": "scholarship", "label": "Trông đợi vào học bổng" },
            { "id": "other", "label": "Nguồn tài chính khác" }
          ],
          "educationSystems": [
            { "id": "standard", "label": "Hệ chuẩn công lập Việt Nam" },
            { "id": "specialized", "label": "Hệ chuyên (Trường THPT Chuyên / Năng khiếu)" },
            { "id": "dual_degree", "label": "Hệ song bằng (Việt - Anh / Cambridge)" },
            { "id": "international", "label": "Hệ quốc tế hoàn toàn (IB, AP, Cambridge)" },
            { "id": "private", "label": "Dân lập / Tư thục chất lượng cao" },
            { "id": "university", "label": "Hệ Đại học chính quy / Tiên tiến" },
            { "id": "other", "label": "Hệ khác" }
          ],
          "gradeScales": [
            { "id": "10", "name": "Thang 10", "desc": "0.0 - 10.0 (Phổ biến tại Việt Nam)", "min": 0, "max": 10, "step": 0.1 },
            { "id": "100", "name": "Thang 100", "desc": "0 - 100 (Hệ phần trăm)", "min": 0, "max": 100, "step": 1 },
            { "id": "4", "name": "Thang 4", "desc": "0.0 - 4.0 (Hệ tín chỉ Đại học)", "min": 0, "max": 4.0, "step": 0.01 },
            { "id": "letter", "name": "Thang chữ", "desc": "A+, A, B+, B, C, D, F", "min": 0, "max": 4.0, "step": 0.1 }
          ],
          "gradeFormTemplates": {
            "high_school": {
              "levelType": "high_school",
              "description": "Bảng điểm 3 năm THPT (Lớp 10, 11, 12)",
              "defaultGradeScale": "10",
              "terms": [
                { "termOrder": 1, "termName": "Lớp 10 - Học kỳ 1" },
                { "termOrder": 2, "termName": "Lớp 10 - Học kỳ 2" },
                { "termOrder": 3, "termName": "Lớp 11 - Học kỳ 1" },
                { "termOrder": 4, "termName": "Lớp 11 - Học kỳ 2" },
                { "termOrder": 5, "termName": "Lớp 12 - Học kỳ 1" },
                { "termOrder": 6, "termName": "Lớp 12 - Học kỳ 2" }
              ],
              "defaultSubjects": [
                { "subject": "Toán học", "credits": 2 },
                { "subject": "Ngữ văn", "credits": 2 },
                { "subject": "Tiếng Anh", "credits": 2 },
                { "subject": "Vật lý", "credits": 1 },
                { "subject": "Hóa học", "credits": 1 },
                { "subject": "Sinh học", "credits": 1 },
                { "subject": "Lịch sử", "credits": 1 },
                { "subject": "Địa lý", "credits": 1 },
                { "subject": "Tin học", "credits": 1 }
              ]
            },
            "university": {
              "levelType": "university",
              "description": "Bảng điểm Đại học (Tín chỉ / 4 năm)",
              "defaultGradeScale": "4",
              "terms": [
                { "termOrder": 1, "termName": "Năm 1 - Học kỳ 1" },
                { "termOrder": 2, "termName": "Năm 1 - Học kỳ 2" },
                { "termOrder": 3, "termName": "Năm 2 - Học kỳ 1" },
                { "termOrder": 4, "termName": "Năm 2 - Học kỳ 2" },
                { "termOrder": 5, "termName": "Năm 3 - Học kỳ 1" },
                { "termOrder": 6, "termName": "Năm 3 - Học kỳ 2" },
                { "termOrder": 7, "termName": "Năm 4 - Học kỳ 1" },
                { "termOrder": 8, "termName": "Năm 4 - Học kỳ 2" }
              ],
              "defaultSubjects": [
                { "subject": "Toán cao cấp / Đại số", "credits": 3 },
                { "subject": "Xác suất thống kê", "credits": 3 },
                { "subject": "Tiếng Anh chuyên ngành", "credits": 3 },
                { "subject": "Nhập môn chuyên ngành", "credits": 3 },
                { "subject": "Cơ sở ngành", "credits": 3 },
                { "subject": "Chuyên ngành nâng cao", "credits": 3 }
              ]
            }
          },
          "certifications": [
            { "id": "ielts", "name": "IELTS", "category": "english", "min": 0, "max": 9.0, "step": 0.5, "placeholder": "Ví dụ: 7.0" },
            { "id": "toefl", "name": "TOEFL iBT", "category": "english", "min": 0, "max": 120, "step": 1, "placeholder": "Ví dụ: 95" },
            { "id": "duolingo", "name": "Duolingo English Test", "category": "english", "min": 10, "max": 160, "step": 5, "placeholder": "Ví dụ: 125" },
            { "id": "sat", "name": "SAT", "category": "standardized", "min": 400, "max": 1600, "step": 10, "placeholder": "Ví dụ: 1420" },
            { "id": "act", "name": "ACT", "category": "standardized", "min": 1, "max": 36, "step": 1, "placeholder": "Ví dụ: 31" },
            { "id": "gre", "name": "GRE General", "category": "graduate", "min": 260, "max": 340, "step": 1, "placeholder": "Ví dụ: 320" },
            { "id": "gmat", "name": "GMAT", "category": "graduate", "min": 200, "max": 800, "step": 10, "placeholder": "Ví dụ: 680" }
          ],
          "activityCategories": [
            { "id": "extracurricular", "label": "Hoạt động ngoại khóa / CLB" },
            { "id": "award", "label": "Giải thưởng / Học sinh giỏi" },
            { "id": "experience", "label": "Kinh nghiệm thực tế / Tình nguyện" }
          ],
          "impactLevels": [
            { "level": 1, "label": "Cấp Trường / CLB", "desc": "Thành viên, ban chủ nhiệm CLB cấp trường" },
            { "level": 2, "label": "Cấp Quận / Huyện", "desc": "Dự án, cuộc thi cấp quận hoặc huyện" },
            { "level": 3, "label": "Cấp Tỉnh / Thành phố", "desc": "Giải thưởng, phong trào cấp tỉnh hoặc thành phố" },
            { "level": 4, "label": "Cấp Quốc gia", "desc": "Học sinh giỏi, dự án cộng đồng toàn quốc" },
            { "level": 5, "label": "Cấp Quốc tế", "desc": "Huy chương Olympic, diễn đàn quốc tế" }
          ],
          "usStates": [
            { "code": "CA", "name": "California" },
            { "code": "TX", "name": "Texas" },
            { "code": "NY", "name": "New York" },
            { "code": "MA", "name": "Massachusetts" },
            { "code": "IL", "name": "Illinois" },
            { "code": "PA", "name": "Pennsylvania" },
            { "code": "WA", "name": "Washington" },
            { "code": "FL", "name": "Florida" },
            { "code": "GA", "name": "Georgia" },
            { "code": "NC", "name": "North Carolina" }
          ],
          "usMajors": [
            { "id": "cs", "name": "Khoa học máy tính", "englishName": "Computer Science" },
            { "id": "ds", "name": "Khoa học dữ liệu", "englishName": "Data Science" },
            { "id": "ai", "name": "Trí tuệ nhân tạo", "englishName": "Artificial Intelligence" },
            { "id": "se", "name": "Kỹ thuật phần mềm", "englishName": "Software Engineering" },
            { "id": "ba", "name": "Quản trị kinh doanh", "englishName": "Business Administration" },
            { "id": "fin", "name": "Tài chính", "englishName": "Finance" },
            { "id": "acc", "name": "Kế toán", "englishName": "Accounting" },
            { "id": "mkt", "name": "Tiếp thị số", "englishName": "Marketing" },
            { "id": "eco", "name": "Kinh tế học", "englishName": "Economics" },
            { "id": "me", "name": "Kỹ thuật cơ khí", "englishName": "Mechanical Engineering" },
            { "id": "ee", "name": "Kỹ thuật điện - điện tử", "englishName": "Electrical Engineering" },
            { "id": "bio", "name": "Sinh học", "englishName": "Biological Sciences" },
            { "id": "psy", "name": "Tâm lý học", "englishName": "Psychology" },
            { "id": "undecided", "name": "Chưa quyết định / Tổng quát", "englishName": "Undecided / Exploratory" }
          ],
          "institutionTypes": [
            { "id": "any", "label": "Không lọc" },
            { "id": "public", "label": "Công lập" },
            { "id": "private", "label": "Tư thục" }
          ]
        }
        """;
    }
}
