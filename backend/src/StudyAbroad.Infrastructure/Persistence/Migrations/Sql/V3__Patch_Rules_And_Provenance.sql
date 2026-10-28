-- ==============================================================================
-- USAS Decision Support System (US-DSS) - SQL Migration Patch V3
-- File: 03-patch-rules-and-provenance.sql / V3__Patch_Rules_And_Provenance.sql
-- Purpose:
--   1. Thêm bảng app.expert_rules lưu trữ 29 luật chuyên gia rẽ nhánh (R01-R29).
--   2. Thêm bảng app.recommendation_items theo dõi kiểm định chéo giữa AI và Rule.
--   3. Thêm bảng app.policy_alerts phục vụ cảnh báo visa, trường, chính sách.
--   4. Thêm bảng app.data_import_batches và app.field_evidence cho quy trình ETL & audit.
--   5. Bổ sung hơn 20 cột nguồn gốc (CDS provenance, COA, C1/C7/C9/H6) vào universities, university_offerings, scholarships.
--   6. Bổ sung cột lưu hash & kết quả Qwen extractor cho profile_activities và forum_posts.
--   7. Chuẩn hóa thang điểm đại học (10 sang 4.0 và thang chữ) cho Thạc sĩ/Tiến sĩ.
--   8. Khắc phục cấu hình seed ngưỡng điểm: match_min_strength = 25 (SRS chuẩn: 25 và 75).
--   9. Củng cố tính bất biến (immutability) cho audit_logs.
-- ==============================================================================

-- 1. BẢNG EXPERT_RULES (FE-04, FE-05, UC-22, UC-69, UC-87)
CREATE TABLE IF NOT EXISTS app.expert_rules (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    rule_code VARCHAR(32) NOT NULL UNIQUE,
    rule_name VARCHAR(256) NOT NULL,
    degree_level VARCHAR(64) NOT NULL DEFAULT 'all',
    target_direction VARCHAR(128) NOT NULL,
    condition_summary TEXT NOT NULL,
    condition_json JSONB,
    action_recommendation TEXT NOT NULL,
    supporting_centers_count INT DEFAULT 1,
    source_reference VARCHAR(256),
    certainty_factor NUMERIC(3,2) DEFAULT 0.90,
    status VARCHAR(32) DEFAULT 'active',
    version INT DEFAULT 1,
    verified_by VARCHAR(128) DEFAULT 'USAS Advisory Board',
    verified_at TIMESTAMPTZ DEFAULT CURRENT_TIMESTAMP,
    created_at TIMESTAMPTZ NOT NULL DEFAULT CURRENT_TIMESTAMP,
    updated_at TIMESTAMPTZ NOT NULL DEFAULT CURRENT_TIMESTAMP
);

CREATE OR REPLACE TRIGGER trg_expert_rules_updated_at BEFORE UPDATE ON app.expert_rules
FOR EACH ROW EXECUTE FUNCTION app.update_timestamp();

CREATE INDEX IF NOT EXISTS ix_expert_rules_code ON app.expert_rules(rule_code);
CREATE INDEX IF NOT EXISTS ix_expert_rules_level ON app.expert_rules(degree_level);

-- 2. BẢNG RECOMMENDATION_ITEMS (Kiểm định chéo AI vs Luật nghiệp vụ)
CREATE TABLE IF NOT EXISTS app.recommendation_items (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    recommendation_id UUID NOT NULL REFERENCES app.recommendations(id) ON DELETE CASCADE,
    university_id UUID NOT NULL REFERENCES app.universities(id) ON DELETE CASCADE,
    program_name VARCHAR(256),
    tier_ai VARCHAR(32),                     -- 'reach', 'match', 'safety'
    tier_rule VARCHAR(32),                   -- 'reach', 'match', 'safety'
    is_tier_divergent BOOLEAN DEFAULT FALSE, -- Cờ phát hiện AI và Luật lệch tầng
    match_strength NUMERIC(5,2),             -- Điểm Strength (0 - 100)
    rule_codes_triggered VARCHAR(256),       -- Danh sách luật kích hoạt (vd: 'R01,R05')
    financial_fit_status VARCHAR(32) DEFAULT 'affordable', -- 'affordable', 'stretch', 'unsolvable'
    gap_reasons TEXT,                        -- Phân tích khoảng cách tiêu chí
    created_at TIMESTAMPTZ NOT NULL DEFAULT CURRENT_TIMESTAMP
);

CREATE INDEX IF NOT EXISTS ix_recommendation_items_rec_id ON app.recommendation_items(recommendation_id);
CREATE INDEX IF NOT EXISTS ix_recommendation_items_uni_id ON app.recommendation_items(university_id);

-- 3. BẢNG POLICY_ALERTS (Thông báo chính sách trên dashboard)
CREATE TABLE IF NOT EXISTS app.policy_alerts (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    alert_type VARCHAR(32) NOT NULL DEFAULT 'visa',
    title VARCHAR(256) NOT NULL,
    summary TEXT NOT NULL,
    effective_date DATE,
    source_url VARCHAR(512),
    severity VARCHAR(32) DEFAULT 'info',
    is_active BOOLEAN NOT NULL DEFAULT TRUE,
    created_at TIMESTAMPTZ NOT NULL DEFAULT CURRENT_TIMESTAMP,
    updated_at TIMESTAMPTZ NOT NULL DEFAULT CURRENT_TIMESTAMP
);

CREATE OR REPLACE TRIGGER trg_policy_alerts_updated_at BEFORE UPDATE ON app.policy_alerts
FOR EACH ROW EXECUTE FUNCTION app.update_timestamp();

-- 4. BẢNG ETL AUDIT & EVIDENCE (Minh chứng kiểm toán mẫu & quy trình nạp dữ liệu)
CREATE TABLE IF NOT EXISTS app.data_import_batches (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    batch_code VARCHAR(64) NOT NULL UNIQUE,
    source_name VARCHAR(128) NOT NULL,
    data_year VARCHAR(32) NOT NULL,
    total_rows INT NOT NULL DEFAULT 0,
    passed_rows INT NOT NULL DEFAULT 0,
    flagged_rows INT NOT NULL DEFAULT 0,
    checked_by VARCHAR(128) DEFAULT 'USAS Research Team / DuongNX',
    status VARCHAR(32) DEFAULT 'verified',
    created_at TIMESTAMPTZ NOT NULL DEFAULT CURRENT_TIMESTAMP
);

CREATE TABLE IF NOT EXISTS app.field_evidence (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    university_id UUID REFERENCES app.universities(id) ON DELETE CASCADE,
    field_name VARCHAR(64) NOT NULL,
    field_value TEXT,
    source_url VARCHAR(512),
    source_section VARCHAR(64),
    extracted_quote TEXT,
    verified_by VARCHAR(128) DEFAULT 'USAS Audit Panel',
    status VARCHAR(32) DEFAULT 'verified',
    created_at TIMESTAMPTZ NOT NULL DEFAULT CURRENT_TIMESTAMP
);

-- 5. BỔ SUNG CỘT CHO UNIVERSITIES
ALTER TABLE app.universities ADD COLUMN IF NOT EXISTS us_news_rank INT;
ALTER TABLE app.universities ADD COLUMN IF NOT EXISTS rank_list VARCHAR(128) DEFAULT 'National Universities';
ALTER TABLE app.universities ADD COLUMN IF NOT EXISTS institution_type VARCHAR(64) DEFAULT 'National University';
ALTER TABLE app.universities ADD COLUMN IF NOT EXISTS is_ivy BOOLEAN DEFAULT FALSE;
ALTER TABLE app.universities ADD COLUMN IF NOT EXISTS gpa_average NUMERIC(4,2);
ALTER TABLE app.universities ADD COLUMN IF NOT EXISTS gpa_policy VARCHAR(128) DEFAULT 'Holistic Review';
ALTER TABLE app.universities ADD COLUMN IF NOT EXISTS ielts_policy VARCHAR(128) DEFAULT 'Standard International Requirement';
ALTER TABLE app.universities ADD COLUMN IF NOT EXISTS sat_policy_basis VARCHAR(256);

-- 6. BỔ SUNG CỘT CHO UNIVERSITY_OFFERINGS (Đầy đủ tiêu chí CDS & Provenance)
ALTER TABLE app.university_offerings ADD COLUMN IF NOT EXISTS act25 INT;
ALTER TABLE app.university_offerings ADD COLUMN IF NOT EXISTS act75 INT;
ALTER TABLE app.university_offerings ADD COLUMN IF NOT EXISTS applicants_total INT;
ALTER TABLE app.university_offerings ADD COLUMN IF NOT EXISTS admitted_total INT;
ALTER TABLE app.university_offerings ADD COLUMN IF NOT EXISTS intl_applicants INT;
ALTER TABLE app.university_offerings ADD COLUMN IF NOT EXISTS intl_admitted INT;
ALTER TABLE app.university_offerings ADD COLUMN IF NOT EXISTS gpa_submit_pct NUMERIC(5,2);
ALTER TABLE app.university_offerings ADD COLUMN IF NOT EXISTS coa_total_usd NUMERIC(12,2);
ALTER TABLE app.university_offerings ADD COLUMN IF NOT EXISTS cost_year VARCHAR(16) DEFAULT '2025-26';
ALTER TABLE app.university_offerings ADD COLUMN IF NOT EXISTS cost_basis VARCHAR(256);
ALTER TABLE app.university_offerings ADD COLUMN IF NOT EXISTS intl_aid_recipients INT;
ALTER TABLE app.university_offerings ADD COLUMN IF NOT EXISTS deadline_ea DATE;
ALTER TABLE app.university_offerings ADD COLUMN IF NOT EXISTS deadline_ed DATE;
ALTER TABLE app.university_offerings ADD COLUMN IF NOT EXISTS deadline_rd DATE;
ALTER TABLE app.university_offerings ADD COLUMN IF NOT EXISTS deadline_basis VARCHAR(256);
ALTER TABLE app.university_offerings ADD COLUMN IF NOT EXISTS cds_year VARCHAR(16) DEFAULT '2025-26';
ALTER TABLE app.university_offerings ADD COLUMN IF NOT EXISTS cds_source_file VARCHAR(256);
ALTER TABLE app.university_offerings ADD COLUMN IF NOT EXISTS cds_url VARCHAR(512);
ALTER TABLE app.university_offerings ADD COLUMN IF NOT EXISTS verified_c1 BOOLEAN DEFAULT TRUE;
ALTER TABLE app.university_offerings ADD COLUMN IF NOT EXISTS verified_c7 BOOLEAN DEFAULT TRUE;
ALTER TABLE app.university_offerings ADD COLUMN IF NOT EXISTS verified_c9 BOOLEAN DEFAULT TRUE;
ALTER TABLE app.university_offerings ADD COLUMN IF NOT EXISTS verified_c11 BOOLEAN DEFAULT TRUE;
ALTER TABLE app.university_offerings ADD COLUMN IF NOT EXISTS verified_h6 BOOLEAN DEFAULT TRUE;
ALTER TABLE app.university_offerings ADD COLUMN IF NOT EXISTS checked_by VARCHAR(128) DEFAULT 'USAS Research Team / DuongNX';
ALTER TABLE app.university_offerings ADD COLUMN IF NOT EXISTS checked_at TIMESTAMPTZ DEFAULT CURRENT_TIMESTAMP;
ALTER TABLE app.university_offerings ADD COLUMN IF NOT EXISTS data_status VARCHAR(32) DEFAULT 'VERIFIED';
ALTER TABLE app.university_offerings ADD COLUMN IF NOT EXISTS data_flags VARCHAR(256);
ALTER TABLE app.university_offerings ADD COLUMN IF NOT EXISTS admission_source_url VARCHAR(512);
ALTER TABLE app.university_offerings ADD COLUMN IF NOT EXISTS cost_source_url VARCHAR(512);
ALTER TABLE app.university_offerings ADD COLUMN IF NOT EXISTS english_source_url VARCHAR(512);

-- 7. BỔ SUNG CỘT CHO SCHOLARSHIPS
ALTER TABLE app.scholarships ADD COLUMN IF NOT EXISTS scholarship_type VARCHAR(32) DEFAULT 'merit';
ALTER TABLE app.scholarships ADD COLUMN IF NOT EXISTS intl_eligible BOOLEAN DEFAULT TRUE;
ALTER TABLE app.scholarships ADD COLUMN IF NOT EXISTS renewable BOOLEAN DEFAULT TRUE;
ALTER TABLE app.scholarships ADD COLUMN IF NOT EXISTS num_awards INT;
ALTER TABLE app.scholarships ADD COLUMN IF NOT EXISTS coverage_type VARCHAR(64) DEFAULT 'partial_tuition';
ALTER TABLE app.scholarships ADD COLUMN IF NOT EXISTS min_gpa4 NUMERIC(4,2);
ALTER TABLE app.scholarships ADD COLUMN IF NOT EXISTS source_basis VARCHAR(256);
ALTER TABLE app.scholarships ADD COLUMN IF NOT EXISTS verified BOOLEAN DEFAULT TRUE;
ALTER TABLE app.scholarships ADD COLUMN IF NOT EXISTS is_active BOOLEAN DEFAULT TRUE;

-- 8. BỔ SUNG CỘT CHO STUDENT_PROFILES (38 nhóm ngành CIP chuẩn CDS)
ALTER TABLE app.student_profiles ADD COLUMN IF NOT EXISTS intended_cip_code VARCHAR(16);
ALTER TABLE app.student_profiles ADD COLUMN IF NOT EXISTS intended_cip_category VARCHAR(128);

-- 9. BỔ SUNG CỘT CHO PROFILE_ACTIVITIES (Qwen Extractor Hash & Multi-user trace)
ALTER TABLE app.profile_activities ADD COLUMN IF NOT EXISTS created_by_user_id UUID REFERENCES app.users(id) ON DELETE SET NULL;
ALTER TABLE app.profile_activities ADD COLUMN IF NOT EXISTS ai_role VARCHAR(128);
ALTER TABLE app.profile_activities ADD COLUMN IF NOT EXISTS ai_scope VARCHAR(128);
ALTER TABLE app.profile_activities ADD COLUMN IF NOT EXISTS ai_level VARCHAR(128);
ALTER TABLE app.profile_activities ADD COLUMN IF NOT EXISTS ai_model VARCHAR(64);
ALTER TABLE app.profile_activities ADD COLUMN IF NOT EXISTS ai_extracted_at TIMESTAMPTZ;
ALTER TABLE app.profile_activities ADD COLUMN IF NOT EXISTS ai_input_hash VARCHAR(64);

-- 10. BỔ SUNG CỘT CHO TRANSCRIPT_SCORES (Hệ đào tạo ĐH & User trace)
ALTER TABLE app.transcript_scores ADD COLUMN IF NOT EXISTS created_by_user_id UUID REFERENCES app.users(id) ON DELETE SET NULL;
ALTER TABLE app.transcript_scores ADD COLUMN IF NOT EXISTS education_level VARCHAR(32) NOT NULL DEFAULT 'high_school';

-- 11. BỔ SUNG CỘT CHO FORUM_POSTS (Qwen Topic Hash)
ALTER TABLE app.forum_posts ADD COLUMN IF NOT EXISTS qwen_extracted JSONB;
ALTER TABLE app.forum_posts ADD COLUMN IF NOT EXISTS extractor_hash VARCHAR(64);

-- 12. BỔ SUNG CỘT CHO KNOWLEDGE_DOCUMENTS (RAG vector & phân loại)
ALTER TABLE app.knowledge_documents ADD COLUMN IF NOT EXISTS question TEXT;
ALTER TABLE app.knowledge_documents ADD COLUMN IF NOT EXISTS topic VARCHAR(128);
ALTER TABLE app.knowledge_documents ADD COLUMN IF NOT EXISTS study_level VARCHAR(64) DEFAULT 'all';
ALTER TABLE app.knowledge_documents ADD COLUMN IF NOT EXISTS stage VARCHAR(64);
ALTER TABLE app.knowledge_documents ADD COLUMN IF NOT EXISTS scope VARCHAR(64) DEFAULT 'system_advisory';
ALTER TABLE app.knowledge_documents ADD COLUMN IF NOT EXISTS quote TEXT;
ALTER TABLE app.knowledge_documents ADD COLUMN IF NOT EXISTS source_type VARCHAR(64) DEFAULT 'official';
ALTER TABLE app.knowledge_documents ADD COLUMN IF NOT EXISTS review_due_at TIMESTAMPTZ;
ALTER TABLE app.knowledge_documents ADD COLUMN IF NOT EXISTS version INT DEFAULT 1;
DO $$ 
BEGIN 
    IF NOT EXISTS (
        SELECT 1 FROM information_schema.columns 
        WHERE table_schema = 'app' AND table_name = 'knowledge_documents' AND column_name = 'embedding'
    ) THEN 
        ALTER TABLE app.knowledge_documents ADD COLUMN embedding vector(1536); 
    END IF; 
END $$;

-- 13. KHẮC PHỤC TÍNH BẤT BIẾN CHO AUDIT_LOGS
DROP TRIGGER IF EXISTS trg_audit_logs_updated_at ON app.audit_logs;
ALTER TABLE app.audit_logs DROP COLUMN IF EXISTS updated_at;

-- 14. KHẮC PHỤC CẤU HÌNH SEED NGƯỠNG ĐIỂM VÀ THANG ĐIỂM ĐẠI HỌC
UPDATE app.app_settings 
SET value_json = '{"safety_min_strength": 75, "match_min_strength": 25, "reach_min_strength": 0}'::jsonb
WHERE key = 'recommend.thresholds';

INSERT INTO app.app_settings (id, key, value_json, description, category)
VALUES 
(
    gen_random_uuid(),
    'grade_scale.university_10_to_4',
    '{"ranges": [{"min": 8.5, "max": 10.0, "letter": "A", "gpa4": 4.0}, {"min": 7.0, "max": 8.4, "letter": "B", "gpa4": 3.0}, {"min": 5.5, "max": 6.9, "letter": "C", "gpa4": 2.0}, {"min": 4.0, "max": 5.4, "letter": "D", "gpa4": 1.0}, {"min": 0.0, "max": 3.9, "letter": "F", "gpa4": 0.0}]}',
    'Quy đổi điểm Đại học Việt Nam thang 10 sang thang 4 WES theo Thông tư 08/2021/TT-BGDĐT cho học viên Thạc sĩ/Tiến sĩ',
    'academic'
),
(
    gen_random_uuid(),
    'grade_scale.university_letter_to_4',
    '{"grades": [{"letter": "A+", "gpa4": 4.0}, {"letter": "A", "gpa4": 4.0}, {"letter": "B+", "gpa4": 3.5}, {"letter": "B", "gpa4": 3.0}, {"letter": "C+", "gpa4": 2.5}, {"letter": "C", "gpa4": 2.0}, {"letter": "D+", "gpa4": 1.5}, {"letter": "D", "gpa4": 1.0}, {"letter": "F", "gpa4": 0.0}]}',
    'Quy đổi điểm chữ tín chỉ Đại học sang điểm số thang 4 WES cho ứng viên Thạc sĩ',
    'academic'
) ON CONFLICT (key) DO NOTHING;

-- 15. NẠP 29 LUẬT CHUYÊN GIA

-- Nạp 29 luật chuyên gia R01-R29 từ sheet 07_Flow
INSERT INTO app.expert_rules (
    rule_code, rule_name, degree_level, target_direction,
    condition_summary, condition_json, action_recommendation,
    supporting_centers_count, source_reference, certainty_factor, status, version, verified_by
) VALUES
(
    'R01', 'Phân luồng theo bậc học quan tâm', 'Mọi bậc', 'Chọn skill tương ứng (THPT / CĐ-ĐH / Thạc sĩ / Tiến sĩ)',
    'Biết bậc muốn học', '{"field": "target_level", "options": ["k12", "undergraduate", "master", "phd"]}'::jsonb, 'Chọn skill THPT / CĐ-ĐH / Thạc sĩ / Tiến sĩ',
    1, 'Logic chung', 0.85, 'active', 1, 'USAS Advisory Board'
),
(
    'R02', 'Lộ trình THPT Mỹ để chuẩn bị ĐH Top', 'THPT, ĐH', 'Du học THPT Mỹ (Lớp 9-10) tích lũy AP/Dual Credit',
    'Muốn vào ĐH top và có thể đi sớm', '{"target_tier": "top_50", "current_grade": [9, 10], "early_departure": true}'::jsonb, 'Đi THPT Mỹ từ lớp 9–10 (AP, Dual Credit); còn lại học hết lớp 12 ở Việt Nam',
    1, 'RAW-ON-003', 0.85, 'active', 1, 'USAS Advisory Board'
),
(
    'R03', 'Lộ trình Lớp 11 muốn đi sớm', 'ĐH', 'High School Completion (CĐ Cộng đồng) hoặc học hết lớp 12',
    'Lớp 11 muốn đi năm sau', '{"current_grade": 11, "departure_year": 1}'::jsonb, 'Học hết lớp 12, hoặc High School Completion tại CĐ cộng đồng',
    1, 'RAW-ON-005', 0.85, 'active', 1, 'USAS Advisory Board'
),
(
    'R04', 'Phân nhánh Nộp thẳng 4 năm vs Cao đẳng 2+2 theo năng lực', 'ĐH', 'Nộp thẳng ĐH 4 năm vs CĐ 2+2',
    'Học lực và tiếng Anh đạt mức ĐH', '{"gpa_min": 3.0, "ielts_min": 6.5, "fallback_path": "community_college_2plus2"}'::jsonb, 'Nộp thẳng ĐH (thuận lợi khi xin visa); chưa đạt hoặc muốn tiết kiệm → CĐ 2+2 hoặc pathway',
    4, 'RAW-ON-003, 004, 005, 007', 0.95, 'active', 1, 'USAS Advisory Board'
),
(
    'R05', 'Khớp nối ngân sách gia đình và học phí lộ trình', 'ĐH', 'Phân luồng lộ trình theo ngân sách (ĐH 4 năm vs CĐ 2+2 vs Học bổng/Bang rẻ)',
    'So ngân sách với chi phí lộ trình', '{"budget_thresholds": {"direct_university": 40000, "community_college": 18000, "merit_aid": 18000}}'::jsonb, 'Đủ cho ĐH 4 năm → ĐH; chỉ đủ mức CĐ → CĐ 2+2; thấp hơn → học bổng lớn, bang chi phí thấp',
    3, 'RAW-ON-003, 005, 009', 0.95, 'active', 1, 'USAS Advisory Board'
),
(
    'R06', 'Xử lý hồ sơ thiếu tiếng Anh hoặc chứng chỉ hết hạn', 'ĐH, thạc sĩ', 'Kế hoạch ôn thi lại vs Khóa tiếng Anh dự bị ESL/Pathway',
    'Tiếng Anh chưa đủ hoặc chứng chỉ hết hạn', '{"english_validity": "insufficient_or_expired", "retest_months": 3}'::jsonb, 'Còn thời gian → ôn và thi lại; không kịp → pathway/ESL',
    2, 'RAW-ON-004, 009', 0.85, 'active', 1, 'USAS Advisory Board'
),
(
    'R07', 'Chiến lược hồ sơ không có SAT/ACT', 'ĐH', 'Lọc trường Test-Optional / Test-Blind',
    'Không có SAT', '{"has_standardized_test": false, "policy_filter": "test_optional_or_blind"}'::jsonb, 'Chọn trường xét GPA hoặc không bắt buộc SAT',
    3, 'RAW-ON-005, 006, 007', 0.95, 'active', 1, 'USAS Advisory Board'
),
(
    'R08', 'Chiến lược bù đắp khi GPA không nổi bật', 'ĐH', 'Tăng cường hồ sơ ngoại khóa, bài luận và điểm chuẩn hóa',
    'GPA không nổi bật', '{"gpa_max": 3.0, "compensate_with": ["extracurriculars", "leadership", "essay"]}'::jsonb, 'Bù bằng ngoại khóa, tình nguyện, thể thao',
    1, 'RAW-ON-004', 0.85, 'active', 1, 'USAS Advisory Board'
),
(
    'R09', 'Chiến lược săn học bổng Merit cho hồ sơ xuất sắc', 'ĐH', 'Target trường có chính sách học bổng học thuật tự động (Merit Aid)',
    'Hồ sơ mạnh (GPA, tiếng Anh cao)', '{"gpa_min": 3.6, "ielts_min": 7.5, "sat_min": 1450, "target_benefit": "merit_scholarship"}'::jsonb, 'Có cơ hội học bổng merit; nêu rõ điều kiện còn thiếu',
    4, 'RAW-ON-003, 005, 006, 007', 0.95, 'active', 1, 'USAS Advisory Board'
),
(
    'R10', 'Nguyên tắc ưu tiên tài chính trước khi chọn trường', 'Mọi bậc', 'Thẩm định trần tài chính gia đình trước khi lập danh sách trường',
    'Ngân sách hạn chế', '{"budget_priority": "family_budget_first"}'::jsonb, 'Xem tài chính gia đình trước, rồi tìm học bổng, hỗ trợ tài chính',
    2, 'RAW-OFF-001, 002', 0.85, 'active', 1, 'USAS Advisory Board'
),
(
    'R11', 'Thứ tự ưu tiên lọc trường (Bộ lọc 3 tầng)', 'Mọi bậc', 'Thứ tự lọc chuẩn: 1. Có ngành -> 2. Vừa tài chính -> 3. Phù hợp học lực',
    'Lọc trường', '{"funnel_order": [1, 2, 3], "steps": ["major_available", "financial_fit", "academic_fit"]}'::jsonb, 'Theo thứ tự: có ngành → hợp tài chính → học lực đáp ứng',
    3, 'RAW-ON-004, 008, RAW-OFF-002', 0.95, 'active', 1, 'USAS Advisory Board'
),
(
    'R12', 'Cấu trúc danh sách trường nộp đơn (Reach/Match/Safety)', 'Mọi bậc', 'Lập danh mục 5-10 trường trải đều 3 tầng Reach/Match/Safety',
    'Lập danh sách trường', '{"list_structure": {"reach": 2, "match": 4, "safety": 2, "min_total": 5}}'::jsonb, '5–10 trường nhiều mức (tốt, vừa sức, chi phí hợp lý); nộp ít nhất 5',
    3, 'RAW-ON-005, 009, RAW-OFF-001', 0.95, 'active', 1, 'USAS Advisory Board'
),
(
    'R13', 'Chuyển đổi ngành học bậc Thạc sĩ', 'Thạc sĩ', 'Khóa học bổ sung kiến thức nền (Prerequisites/Bridge course)',
    'Học thạc sĩ khác ngành đã học', '{"career_pivot": true, "degree_level": "master", "bridge_required": true}'::jsonb, 'Cùng nền → không cần học bù; khác khối → học bridge/prerequisites',
    2, 'RAW-ON-008, 009', 0.85, 'active', 1, 'USAS Advisory Board'
),
(
    'R14', 'Quản lý thời gian chuẩn bị hồ sơ', 'Mọi bậc', 'Dời kỳ nhập học (Spring/Fall năm sau) bảo đảm đủ 6-12 tháng chuẩn bị',
    'Không đủ thời gian chuẩn bị', '{"prep_months_remaining": 3, "recommended_action": "defer_term"}'::jsonb, 'Dời sang kỳ sau (Spring hoặc Fall năm sau); làm hồ sơ trước 6–12 tháng',
    4, 'RAW-ON-003, 007, 008, 009', 0.95, 'active', 1, 'USAS Advisory Board'
),
(
    'R15', 'Chiến lược vòng nộp Early Decision / Early Action', 'ĐH', 'Tận dụng vòng ED1/EA cho trường mơ ước số 1',
    'Nhắm ĐH top, có trường mơ ước', '{"target_top_school": true, "application_round": "ED1_or_EA"}'::jsonb, 'Chiến lược vòng nộp ED1 / EA / Regular',
    1, 'RAW-OFF-001', 0.85, 'active', 1, 'USAS Advisory Board'
),
(
    'R16', 'Quy định làm thêm và thực tập CPT/OPT theo diện F-1', 'Thạc sĩ', 'Lọc trường hỗ trợ CPT sớm; cảnh báo giới hạn pháp lý F-1',
    'Cần làm thêm khi học', '{"needs_employment": true, "f1_limit_hours": 20, "cpt_filter": true}'::jsonb, 'Lọc trường cho CPT sớm; giải thích giới hạn làm thêm của F-1',
    2, 'RAW-ON-008, 009', 0.85, 'active', 1, 'USAS Advisory Board'
),
(
    'R17', 'Chứng minh tài chính cho phụ huynh làm nghề tự do', 'Mọi bậc', 'Giải trình tài chính đa nguồn: Sổ tiết kiệm + Tài sản tích lũy',
    'Bố mẹ làm nghề tự do', '{"parent_employment": "freelance_or_business", "documentation": ["bank_passbook", "asset_deed"]}'::jsonb, 'Chứng minh tài chính bằng sổ tiết kiệm, giải trình nguồn thu nhập',
    2, 'RAW-ON-008, RAW-OFF-002', 0.85, 'active', 1, 'USAS Advisory Board'
),
(
    'R18', 'Lộ trình chuyển tiếp từ Kỹ thuật sang Quản trị (MBA)', 'Thạc sĩ', 'Học Thạc sĩ chuyên ngành STEM trước, sau đó tích lũy kinh nghiệm học MBA',
    'Muốn học MBA sau ngành kỹ thuật', '{"undergrad_stem": true, "target_program": "MBA", "recommended": "MS_then_MBA"}'::jsonb, 'Học thạc sĩ chuyên ngành trước rồi MBA',
    1, 'RAW-ON-009', 0.85, 'active', 1, 'USAS Advisory Board'
),
(
    'R19', 'Ứng xử khi có người thân định cư hoặc hồ sơ bảo lãnh tại Mỹ', 'Mọi bậc', 'Khai báo trung thực trên đơn DS-160, chuẩn bị giải trình mục đích học tập',
    'Có người thân ở Mỹ hoặc hồ sơ bảo lãnh định cư', '{"has_us_relatives": true, "ds160_truthful": true, "non_immigrant_intent": true}'::jsonb, 'Khuyên khai trung thực; giải thích ảnh hưởng tới visa (hồ sơ bảo lãnh đang mở là bất lợi)',
    3, 'RAW-ON-005, 007, 009; cộng đồng: 8 bài', 0.95, 'active', 1, 'USAS Advisory Board'
),
(
    'R20', 'Chiến lược tái phỏng vấn sau khi bị từ chối visa (214b)', 'Mọi bậc', 'Phân tích nguyên nhân từ chối, bổ sung bằng chứng thay đổi đáng kể',
    'Từng bị từ chối visa', '{"visa_rejection_history": true, "ina_section": "214b", "reapply_condition": "material_change"}'::jsonb, 'Hỏi lý do lần trước; cải thiện hồ sơ (tiếng Anh, kế hoạch học, giải trình) trước khi nộp lại',
    1, 'RAW-ON-007; cộng đồng: 14 bài', 0.85, 'active', 1, 'USAS Advisory Board'
),
(
    'R21', 'Cảnh báo rủi ro khi học lùi bậc (Reverse degree)', 'Mọi bậc', 'Cảnh báo rủi ro visa F-1; định hướng học lên Thạc sĩ/Văn bằng 2',
    'Đã có bằng ĐH, muốn học CĐ hoặc học nghề (học lùi)', '{"has_bachelor": true, "target_level": "community_college", "risk": "visa_rejection"}'::jsonb, 'Giải thích rủi ro visa; khuyên học lên bậc cao hơn hoặc giải trình rõ lý do',
    1, 'Cộng đồng: 6 bài', 0.85, 'active', 1, 'USAS Advisory Board'
),
(
    'R22', 'Tư vấn cho ứng viên lớn tuổi hoặc có khoảng trống học tập (Gap year)', 'Mọi bậc', 'Giải trình kinh nghiệm làm việc liên tục gắn liền với ngành học mới',
    'Lớn tuổi hoặc nghỉ học lâu', '{"gap_years": 2, "interview_prep": "high_intensity"}'::jsonb, 'Hỏi lý do và kế hoạch; ưu tiên học lên; nói rõ phỏng vấn sẽ khó hơn',
    1, 'Cộng đồng: 7 bài', 0.85, 'active', 1, 'USAS Advisory Board'
),
(
    'R23', 'Lộ trình cho học sinh chưa tốt nghiệp THPT', 'Đại học', 'Chương trình GED hoặc High School Completion tại Cao đẳng',
    'Chưa có bằng THPT', '{"has_high_school_diploma": false, "path": "GED_or_WA_dual_completion"}'::jsonb, 'Giới thiệu GED (tương đương THPT ở Mỹ) trước khi nộp',
    1, 'Cộng đồng: 3 bài', 0.85, 'active', 1, 'USAS Advisory Board'
),
(
    'R24', 'Du học sinh đang cư trú hoặc học tập tại nước thứ ba', 'Mọi bậc', 'Thực hiện thủ tục công nhận văn bằng quốc tế (WES/ECE) và xin visa tại nước sở tại',
    'Đang sống ở nước khác', '{"residing_third_country": true, "transcript_eval": "WES_or_ECE"}'::jsonb, 'Điều kiện như khi đi từ Việt Nam; giải thích lộ trình học',
    1, 'Cộng đồng: 3 bài', 0.85, 'active', 1, 'USAS Advisory Board'
),
(
    'R25', 'Tuân thủ tuyệt đối mục đích học tập của visa F-1', 'Mọi bậc', 'Khẳng định mục đích visa F-1 là học thuật, từ chối tư vấn lách luật lao động',
    'Mục đích là đi làm, kiếm tiền', '{"intent_flag": "work_only", "action": "refuse_and_educate_f1_rules"}'::jsonb, 'Giải thích visa du học là để học; không khuyên khai sai mục đích',
    1, 'Cộng đồng: 1 bài', 0.85, 'active', 1, 'USAS Advisory Board'
),
(
    'R26', 'Xử lý sự cố tình trạng lưu trú F-1 (I-20, SEVIS, chuyển trường khẩn cấp)', 'Mọi bậc', 'Cung cấp hướng dẫn khẩn cấp và chuyển hướng liên hệ DSO',
    'Đang học ở Mỹ, gặp vấn đề tình trạng F-1 (I-20, SEVIS, chuyển trường)', '{"f1_emergency": true, "action": "contact_dso_immediately"}'::jsonb, 'Thông tin chung, rồi hướng dẫn liên hệ ngay DSO của trường',
    1, 'Cộng đồng: 24 bài', 0.85, 'active', 1, 'USAS Advisory Board'
),
(
    'R27', 'Chuyển giao dịch vụ chuyên sâu cho Trung tâm tư vấn (UC-38)', 'Mọi bậc', 'Kết nối trung tâm du học uy tín (B6 / UC-38)',
    'Cần dịch vụ (luyện thi, dịch thuật, luyện phỏng vấn, mentor)', '{"requires_agency_service": true, "flow_step": "B6", "use_case": "UC-38"}'::jsonb, 'Chuyển trung tâm (B6)',
    1, 'Cộng đồng: 8 bài', 0.85, 'active', 1, 'USAS Advisory Board'
),
(
    'R28', 'Ranh giới hệ thống: Câu hỏi ngoài phạm vi du học (Định cư, H-1B, EB-5)', '—', 'Thông báo giới hạn hệ thống, dẫn nguồn chính thức USCIS/Luật sư di trú',
    'Hỏi về định cư, visa du lịch, H-1B, đời sống', '{"out_of_scope_topic": ["immigration", "green_card", "h1b", "eb5"], "action": "refer_uscis"}'::jsonb, 'Nói rõ ngoài phạm vi; gợi ý nguồn chính thức hoặc luật sư',
    1, 'Cộng đồng: 30 bài', 0.85, 'active', 1, 'USAS Advisory Board'
),
(
    'R29', 'Hỗ trợ tâm lý và giới hạn tư vấn y tế', 'Mọi bậc', 'Đường dây hỗ trợ tâm lý trường học, từ chối can thiệp y tế',
    'Khó khăn tâm lý', '{"mental_health_flag": true, "action": "refer_university_counseling_center"}'::jsonb, 'Hướng dẫn liên hệ tư vấn tâm lý của trường, đường dây hỗ trợ; không tư vấn y tế',
    1, 'Cộng đồng: 2 bài', 0.85, 'active', 1, 'USAS Advisory Board'
)
ON CONFLICT (rule_code) DO UPDATE SET
    rule_name = EXCLUDED.rule_name,
    degree_level = EXCLUDED.degree_level,
    target_direction = EXCLUDED.target_direction,
    condition_summary = EXCLUDED.condition_summary,
    condition_json = EXCLUDED.condition_json,
    action_recommendation = EXCLUDED.action_recommendation,
    supporting_centers_count = EXCLUDED.supporting_centers_count,
    source_reference = EXCLUDED.source_reference,
    certainty_factor = EXCLUDED.certainty_factor,
    status = EXCLUDED.status,
    updated_at = CURRENT_TIMESTAMP;


-- 16. BỔ SUNG CỘT CHO AI_CALLS (UC-83: ĐO LƯỜNG TOKEN & CHI PHÍ THEO GÓI/SKILL)
ALTER TABLE app.ai_calls ADD COLUMN IF NOT EXISTS skill_version VARCHAR(64);
ALTER TABLE app.ai_calls ADD COLUMN IF NOT EXISTS plan_tier VARCHAR(32) DEFAULT 'free';
ALTER TABLE app.ai_calls ADD COLUMN IF NOT EXISTS result_source VARCHAR(32) DEFAULT 'ai';

-- 17. BỔ SUNG CỘT CHO EVAL_CASES & EVAL_RUNS (UC-90: KIỂM MẪU CHẤT LƯỢNG AI & BENCHMARK)
ALTER TABLE app.eval_cases ADD COLUMN IF NOT EXISTS source VARCHAR(128) DEFAULT 'benchmark_standard';
ALTER TABLE app.eval_cases ADD COLUMN IF NOT EXISTS study_level VARCHAR(64) DEFAULT 'undergraduate';
ALTER TABLE app.eval_cases ADD COLUMN IF NOT EXISTS expected_tier VARCHAR(32);
ALTER TABLE app.eval_cases ADD COLUMN IF NOT EXISTS expected_sources TEXT[];
ALTER TABLE app.eval_cases ADD COLUMN IF NOT EXISTS test_profile_json JSONB;

ALTER TABLE app.eval_runs ADD COLUMN IF NOT EXISTS run_type VARCHAR(64) DEFAULT 'benchmark';
ALTER TABLE app.eval_runs ADD COLUMN IF NOT EXISTS model_version VARCHAR(64);
ALTER TABLE app.eval_runs ADD COLUMN IF NOT EXISTS skill_version VARCHAR(64);
ALTER TABLE app.eval_runs ADD COLUMN IF NOT EXISTS accuracy_tier_pct NUMERIC(5,2);
ALTER TABLE app.eval_runs ADD COLUMN IF NOT EXISTS score_error_margin NUMERIC(4,2);
ALTER TABLE app.eval_runs ADD COLUMN IF NOT EXISTS hallucination_rate NUMERIC(5,2);
ALTER TABLE app.eval_runs ADD COLUMN IF NOT EXISTS fallback_rate NUMERIC(5,2);
ALTER TABLE app.eval_runs ADD COLUMN IF NOT EXISTS alert_triggered BOOLEAN DEFAULT FALSE;

-- 18. BỔ SUNG CỘT CHO RECOMMENDATION_ITEMS (UC-23: ĐỘ ĐẦY ĐỦ DỮ LIỆU & NHÃN SÁT NGƯỠNG)
ALTER TABLE app.recommendation_items ADD COLUMN IF NOT EXISTS data_completeness_pct NUMERIC(5,2);
ALTER TABLE app.recommendation_items ADD COLUMN IF NOT EXISTS is_near_threshold BOOLEAN DEFAULT FALSE;
ALTER TABLE app.recommendation_items ADD COLUMN IF NOT EXISTS result_source VARCHAR(32) DEFAULT 'ai';
