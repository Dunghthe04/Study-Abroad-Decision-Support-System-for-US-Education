# Danh sách cần sửa trong mục I.3 Database Design

Đối chiếu bản tài liệu cũ với DB thật (PostgreSQL, schema `app`). Số mục giữ nguyên như bản cũ.

## A. Sửa chung (áp dụng toàn mục 3)

1. Số bảng: **42 → 51**.
2. Kiểu thời gian: mọi `TIMESTAMP` đổi thành **`TIMESTAMPTZ`** (DB lưu có múi giờ, giá trị UTC).
3. Thêm một câu: bảng `__ef_migrations_history` là bảng hệ thống của EF Core, không thuộc mô hình dữ liệu (để người đếm trong DBeaver ra 50 vẫn khớp).
4. Câu "mọi khóa ngoại đều có index" **đúng**, giữ nguyên (đã kiểm tra cả 59 FK).
5. Bảng nhóm: thêm 7 bảng mới vào các nhóm (xem phần C).
6. Phần III.2.2: index `(student_profile_id, term_order)` của transcript_scores đúng, chỉ cần thêm cột `term_order` vào 3.7. Index `(student_profile_id, test_type)` của profile_certifications đúng, bảng này được thêm ở 3.43.

## B. Sửa từng mục

### 3.1 users

- Sửa `email`: kiểu VARCHAR(255) → VARCHAR(256)
- Sửa `password_hash`: kiểu VARCHAR(255) → VARCHAR(60)
- Sửa `full_name`: kiểu VARCHAR(100) → VARCHAR(128)
- Sửa `role`: kiểu VARCHAR(20) → VARCHAR(16)
- Sửa `status`: kiểu VARCHAR(20) → VARCHAR(16)
- **Thêm các cột:**
  - `parent_acknowledged` BOOLEAN, NOT NULL
  - `updated_at` TIMESTAMPTZ, NULL
  - `phone` VARCHAR(32), NULL
  - `failed_login_attempts` INT, NOT NULL, DEFAULT 0
  - `lockout_end` TIMESTAMPTZ, NULL
  - `forum_restriction_reason` TEXT, NULL
- Lưu ý: `status`: ghi thêm đủ giá trị: active, unverified, pending, temp_locked, locked, deleted (doc cũ thiếu unverified, temp_locked).

### 3.2 user_sessions

- Sửa `token_hash`: kiểu VARCHAR(255) → VARCHAR(64)
- Sửa `ip_address`: kiểu VARCHAR(45) → VARCHAR(64)
- **Thêm các cột:**
  - `user_agent` VARCHAR(512), NULL
  - `created_at` TIMESTAMPTZ, NOT NULL
  - `updated_at` TIMESTAMPTZ, NULL

### 3.3 otp_tokens

- Sửa `purpose`: kiểu VARCHAR(50) → VARCHAR(32)
- Sửa `code_hash`: kiểu VARCHAR(255) → VARCHAR(100)
- **Thêm các cột:**
  - `used_at` TIMESTAMPTZ, NULL
  - `created_at` TIMESTAMPTZ, NOT NULL
  - `updated_at` TIMESTAMPTZ, NULL
- Lưu ý: `purpose`: giá trị thật là verify_email, reset_password (doc cũ ghi activation / password_reset).

### 3.4 consents

- Sửa `purpose`: kiểu VARCHAR(50) → VARCHAR(32)
- Sửa `policy_version`: kiểu VARCHAR(20) → VARCHAR(16); NOT NULL → NULL
- **Thêm các cột:**
  - `updated_at` TIMESTAMPTZ, NULL
  - `granted_by_user_id` UUID, NULL; FK → users(id) ON DELETE SET NULL
  - `granted_ip` VARCHAR(45), NULL

### 3.5 parent_student_links

- **Xóa** cột `relationship` (không tồn tại trong DB).
- **Xóa** cột `invite_code` (không tồn tại trong DB).
- Sửa `parent_user_id`: NOT NULL → NULL
- Sửa `status`: kiểu VARCHAR(20) → VARCHAR(16)
- **Thêm các cột:**
  - `parent_email` VARCHAR(256), NOT NULL
  - `token_hash` VARCHAR(64), NULL
  - `token_expires_at` TIMESTAMPTZ, NULL
  - `invite_sent_count` INT, NOT NULL, DEFAULT 0
  - `last_invite_sent_at` TIMESTAMPTZ, NULL
  - `accepted_at` TIMESTAMPTZ, NULL
  - `consent_id` UUID, NULL; FK → consents(id) ON DELETE SET NULL
  - `revoked_at` TIMESTAMPTZ, NULL
  - `revoked_by_user_id` UUID, NULL; FK → users(id) ON DELETE SET NULL
  - `updated_at` TIMESTAMPTZ, NULL
- Lưu ý: `status`: pending, active, revoked, expired (doc cũ thiếu expired).
- Lưu ý: Ràng buộc: bỏ UNIQUE (parent_user_id, student_user_id). Thay bằng UNIQUE (student_user_id, parent_email) chỉ áp dụng khi status là pending hoặc active (partial index).
- Lưu ý: `token_hash` có UNIQUE index (chỉ khi khác NULL).
- Lưu ý: `parent_user_id` là NULL được: lời mời gửi theo e-mail, tài khoản phụ huynh có thể chưa tồn tại.

### 3.6 student_profiles

- Sửa `target_level`: kiểu VARCHAR(50) → VARCHAR(32); NULL → NOT NULL
- Sửa `current_education_level`: kiểu VARCHAR(50) → VARCHAR(32)
- Sửa `current_grade`: kiểu VARCHAR(50) → VARCHAR(64)
- Sửa `intended_major`: kiểu VARCHAR(255) → VARCHAR(128)
- Sửa `sat`: kiểu NUMERIC(4,0) → NUMERIC(5
- Sửa `budget_tier`: kiểu VARCHAR(50) → VARCHAR(32)
- Sửa `scholarship_need`: kiểu VARCHAR(20) → VARCHAR(16)
- **Thêm các cột:**
  - `current_school` VARCHAR(256), NULL
  - `grade_scale` VARCHAR(8), NOT NULL
  - `toefl` NUMERIC(5,1), NULL
  - `duolingo` NUMERIC(5,1), NULL
  - `act` NUMERIC(4,1), NULL
  - `gre` NUMERIC(5,1), NULL
  - `gmat` NUMERIC(5,1), NULL
  - `other_tests_json` JSONB, NULL
  - `funding_source` VARCHAR(32), NULL
  - `needs_scholarship` BOOLEAN, NOT NULL
  - `preferred_states` TEXT[], NOT NULL
  - `roadmap_progress_json` JSONB, NULL
  - `created_at` TIMESTAMPTZ, NOT NULL
  - `updated_at` TIMESTAMPTZ, NULL
  - `education_system` VARCHAR(64), NULL
  - `graduation_year` INT, NULL
  - `transcript_file_url` VARCHAR(512), NULL
  - `financial_notes` VARCHAR(1000), NULL
  - `max_expected_tuition_usd` NUMERIC(12,2), NULL
  - `budget_min_usd` NUMERIC(12,2), NULL
  - `budget_max_usd` NUMERIC(12,2), NULL
  - `target_school_type` VARCHAR(32), NOT NULL, DEFAULT 'all'
  - `english_test_type` VARCHAR(16), NULL
  - `english_test_date` DATE, NULL
  - `english_planned_date` DATE, NULL
  - `english_test_planned` BOOLEAN, NOT NULL, DEFAULT FALSE
  - `target_intake` VARCHAR(16), NULL
  - `extracurricular_tier` INT, NULL
- Lưu ý: `sat` doc ghi NUMERIC(4,0) → thật là NUMERIC(5,1). `target_level` thật là NOT NULL.
- Lưu ý: Bỏ các CHECK: overall_gpa >= 0, sat 400..1600, ielts 0..9 (DB không có). Chỉ có CHECK extracurricular_tier 1..5.

### 3.7 transcript_scores

- Sửa `term_name`: kiểu VARCHAR(50) → VARCHAR(64)
- Sửa `grade_name`: kiểu VARCHAR(50) → VARCHAR(32); NOT NULL → NULL
- Sửa `semester`: NOT NULL → NULL
- Sửa `subject`: kiểu VARCHAR(100) → VARCHAR(128)
- Sửa `score`: kiểu NUMERIC(4,2) → NUMERIC(6; NULL → NOT NULL
- Sửa `raw_score`: kiểu VARCHAR(50) → VARCHAR(16)
- **Thêm các cột:**
  - `term_order` INT, NOT NULL
  - `created_at` TIMESTAMPTZ, NOT NULL
  - `updated_at` TIMESTAMPTZ, NULL
- Lưu ý: Thêm `term_order` (đang được index ở phần III.2.2 nhưng chưa có trong mục này).
- Lưu ý: `score` thật là NOT NULL, NUMERIC(6,2). `grade_name` và `semester` thật là NULL được.

### 3.8 profile_activities

- Sửa `kind`: kiểu VARCHAR(30) → VARCHAR(16)
- Sửa `title`: kiểu VARCHAR(255) → VARCHAR(256)
- Sửa `role`: kiểu VARCHAR(50) → VARCHAR(128)
- **Thêm các cột:**
  - `organization` VARCHAR(256), NULL
  - `description` VARCHAR(2000), NULL
  - `start_date` DATE, NULL
  - `end_date` DATE, NULL
  - `created_at` TIMESTAMPTZ, NOT NULL
  - `updated_at` TIMESTAMPTZ, NULL
- Lưu ý: CHECK impact_level 1..5 là đúng. Bỏ CHECK duration_months >= 0 (DB không có).

### 3.9 roadmap_steps

- **THAY TOÀN BỘ mục này** (chức năng khác hẳn bản cũ). Nội dung mới:
  - Purpose: Mẫu các bước lộ trình chuẩn bị theo cấp học (dùng chung, không gắn với học sinh cụ thể). Tiến độ của từng học sinh lưu ở student_profiles.roadmap_progress_json.
  - `id` UUID, NOT NULL; PK
  - `study_level` VARCHAR(32), NOT NULL
  - `step_key` VARCHAR(32), NOT NULL
  - `title` VARCHAR(256), NOT NULL
  - `description` VARCHAR(4000), NULL
  - `sort_order` INT, NOT NULL
  - `months_before_start` INT, NULL
  - `resources_json` JSONB, NOT NULL
  - `created_at` TIMESTAMPTZ, NOT NULL
  - `updated_at` TIMESTAMPTZ, NULL
  - UNIQUE (study_level, step_key). Không có FK nào.

### 3.10 universities

- Sửa `code`: kiểu VARCHAR(50) → VARCHAR(32)
- Sửa `name`: kiểu VARCHAR(255) → VARCHAR(256)
- Sửa `city`: kiểu VARCHAR(100) → VARCHAR(128)
- Sửa `state`: NOT NULL → NULL
- Sửa `control`: kiểu VARCHAR(20) → VARCHAR(16); NOT NULL → NULL
- **Thêm các cột:**
  - `website` VARCHAR(512), NULL
  - `international_student_count` INT, NULL
  - `created_at` TIMESTAMPTZ, NOT NULL
  - `updated_at` TIMESTAMPTZ, NULL
  - `is_ivy` BOOLEAN, NOT NULL, DEFAULT FALSE
  - `last_edited_by_user_id` UUID, NULL; FK → users(id) ON DELETE SET NULL
- Lưu ý: Bỏ CHECK acceptance_rate 0..1 (DB không có). `state` và `control` thật là NULL được.

### 3.11 university_offerings

- Sửa `study_level`: kiểu VARCHAR(50) → VARCHAR(32)
- Sửa `min_gpa4`: kiểu NUMERIC(4,2) → NUMERIC(3
- Sửa `avg_gpa4`: kiểu NUMERIC(4,2) → NUMERIC(3
- Sửa `intl_aid_policy`: kiểu VARCHAR(50) → VARCHAR(16)
- **Thêm các cột:**
  - `academic_year` VARCHAR(16), NULL
  - `fees_usd` NUMERIC(12,2), NULL
  - `min_ielts` NUMERIC(3,1), NULL
  - `min_toefl` NUMERIC(5,1), NULL
  - `min_duolingo` NUMERIC(5,1), NULL
  - `sat_policy` VARCHAR(16), NULL
  - `deadlines_json` JSONB, NULL
  - `notes` VARCHAR(2000), NULL
  - `source_url` VARCHAR(512), NULL
  - `retrieved_at` DATE, NULL
  - `created_at` TIMESTAMPTZ, NOT NULL
  - `updated_at` TIMESTAMPTZ, NULL
  - `c7_ec_importance` VARCHAR(16), NULL
  - `c7_gpa_importance` VARCHAR(16), NULL
  - `c7_test_importance` VARCHAR(16), NULL
  - `gpa_pct_250_299` NUMERIC(5,4), NULL
  - `gpa_pct_300_324` NUMERIC(5,4), NULL
  - `gpa_pct_325_349` NUMERIC(5,4), NULL
  - `gpa_pct_350_374` NUMERIC(5,4), NULL
  - `gpa_pct_375_399` NUMERIC(5,4), NULL
  - `gpa_pct_below_250` NUMERIC(5,4), NULL
  - `grad_rate_6yr` NUMERIC(5,4), NULL
  - `intl_acceptance_rate` NUMERIC(5,4), NULL
  - `retention_rate_1yr` NUMERIC(5,4), NULL
  - `sat_submit_pct` NUMERIC(5,4), NULL
  - `other_expenses_usd` NUMERIC(12,2), NULL
  - `act25` INT, NULL
  - `act75` INT, NULL
  - `admitted_total` INT, NULL
  - `applicants_total` INT, NULL
  - `gpa_submit_pct` NUMERIC(5,4), NULL
  - `intl_admitted` INT, NULL
  - `intl_aid_recipients` INT, NULL
  - `intl_applicants` INT, NULL
  - `last_edited_by_user_id` UUID, NULL; FK → users(id) ON DELETE SET NULL
  - `source_reliability` VARCHAR(16), NULL
  - `activities` VARCHAR(2000), NULL
  - `avg_gpa_weighted` NUMERIC(3,2), NULL
  - `campus_setting` VARCHAR(64), NULL
  - `fy_on_campus_pct` NUMERIC(5,4), NULL
  - `gpa_scale_type` VARCHAR(16), NULL
  - `housing_deposit_usd` NUMERIC(12,2), NULL
  - `housing_only_usd` NUMERIC(12,2), NULL
  - `housing_types` VARCHAR(1000), NULL
  - `intl_housing` BOOLEAN, NULL
  - `student_faculty_ratio` INT, NULL
  - `ug_on_campus_pct` NUMERIC(5,4), NULL
- Lưu ý: Có CHECK thật: sat25/sat75 trong 400..1600 và sat25 <= sat75; act25/act75 trong 1..36 và act25 <= act75; avg_gpa4 trong 0..4; gpa_scale_type chỉ weighted/unweighted/both; source_reliability chỉ official/aggregated/self_reported; avg_gpa_weighted >= 0.

### 3.12 scholarships

- Đổi tên cột `title` → `name`; dòng mới: `name` VARCHAR(256), NOT NULL
- **Xóa** cột `requirements_json` (không tồn tại trong DB).
- Sửa `university_id`: NOT NULL → NULL
- Sửa `coverage_type`: kiểu VARCHAR(20) → VARCHAR(32); NOT NULL → NULL
- **Thêm các cột:**
  - `provider` VARCHAR(256), NULL
  - `study_level` VARCHAR(32), NOT NULL
  - `min_gpa4` NUMERIC(3,2), NULL
  - `min_ielts` NUMERIC(3,1), NULL
  - `eligibility_notes` VARCHAR(2000), NULL
  - `source_url` VARCHAR(512), NULL
  - `retrieved_at` DATE, NULL
  - `is_active` BOOLEAN, NOT NULL
  - `created_at` TIMESTAMPTZ, NOT NULL
  - `updated_at` TIMESTAMPTZ, NULL
- Lưu ý: `university_id` thật là NULL được (học bổng chung không thuộc trường nào), ON DELETE SET NULL.

### 3.13 target_schools

- Đổi tên cột `user_category` → `category`; dòng mới: `category` VARCHAR(16), NULL
- Sửa `gap_analysis_json`: kiểu JSONB → TEXT
- Sửa `priority`: NULL → NOT NULL
- **Thêm các cột:**
  - `status` VARCHAR(16), NOT NULL
  - `notes` VARCHAR(1000), NULL
  - `updated_at` TIMESTAMPTZ, NULL
  - `application_round` TEXT, NULL
  - `deadline` TIMESTAMPTZ, NULL
- Lưu ý: `gap_analysis_json` thật là TEXT, không phải JSONB. `priority` thật là NOT NULL.

### 3.14 state_living_costs

- Đổi tên cột `cost_index` → `index_score`; dòng mới: `index_score` NUMERIC(6,2), NOT NULL, DEFAULT 100.0
- Đổi tên cột `avg_living_usd_yearly` → `avg_annual_living_usd`; dòng mới: `avg_annual_living_usd` NUMERIC(12,2), NULL
- Sửa `state_code`: kiểu VARCHAR(2) → VARCHAR(8)
- Sửa `state_name`: kiểu VARCHAR(100) → VARCHAR(64)
- **Thêm các cột:**
  - `notes` VARCHAR(500), NULL
  - `source_url` VARCHAR(512), NULL
  - `retrieved_at` DATE, NULL
  - `created_at` TIMESTAMPTZ, NOT NULL
  - `updated_at` TIMESTAMPTZ, NULL

### 3.15 data_import_batches

- Đổi tên cột `row_count` → `total_rows`; dòng mới: `total_rows` INT, NOT NULL
- Sửa `source_name`: kiểu VARCHAR(256) → VARCHAR(128); NOT NULL → NULL
- **Thêm các cột:**
  - `batch_code` VARCHAR(64), NOT NULL
  - `data_year` VARCHAR(32), NULL
  - `passed_rows` INT, NOT NULL
  - `flagged_rows` INT, NOT NULL
  - `checked_by` VARCHAR(128), NOT NULL, DEFAULT 'USAS Research Team'
  - `created_at` TIMESTAMPTZ, NOT NULL

### 3.16 field_evidence

- **Xóa** cột `batch_id` (không tồn tại trong DB).
- Sửa `field_name`: kiểu VARCHAR(128) → VARCHAR(64)
- **Thêm các cột:**
  - `field_value` TEXT, NULL
  - `source_url` VARCHAR(512), NULL
  - `extracted_quote` TEXT, NULL
  - `verified_by` VARCHAR(128), NOT NULL, DEFAULT 'USAS Audit Panel'
  - `status` VARCHAR(32), NOT NULL, DEFAULT 'verified'
  - `created_at` TIMESTAMPTZ, NOT NULL

### 3.17 app_settings

- Sửa `key`: kiểu VARCHAR(100) → VARCHAR(64)
- Sửa `description`: kiểu VARCHAR(255) → VARCHAR(500)
- Sửa `updated_at`: NOT NULL → NULL
- **Thêm các cột:**
  - `created_at` TIMESTAMPTZ, NOT NULL

### 3.18 analysis_results

- Sửa `kind`: kiểu VARCHAR(50) → VARCHAR(32)
- Sửa `model_version`: kiểu VARCHAR(50) → VARCHAR(64)
- **Thêm các cột:**
  - `updated_at` TIMESTAMPTZ, NULL

### 3.19 recommendations

- Sửa `student_profile_id`: NOT NULL → NULL
- Sửa `algorithm_version`: kiểu VARCHAR(50) → VARCHAR(32); NOT NULL → NULL
- **Thêm các cột:**
  - `study_level` VARCHAR(32), NOT NULL
  - `updated_at` TIMESTAMPTZ, NULL
- Lưu ý: `student_profile_id` thật là NULL được, ON DELETE SET NULL.

### 3.20 recommendation_items

- Sửa `financial_fit_status`: NULL → NOT NULL
- **Thêm các cột:**
  - `program_name` VARCHAR(256), NULL
  - `is_tier_divergent` BOOLEAN, NOT NULL
  - `match_strength` NUMERIC(5,2), NULL
  - `rule_codes_triggered` VARCHAR(256), NULL
  - `gap_reasons` TEXT, NULL
  - `created_at` TIMESTAMPTZ, NOT NULL
- Lưu ý: `financial_fit_status` thật là NOT NULL.

### 3.21 what_if_simulations

- Đổi tên cột `scenario_name` → `title`; dòng mới: `title` VARCHAR(256), NOT NULL
- Đổi tên cột `delta_profile_json` → `simulated_profile_json`; dòng mới: `simulated_profile_json` JSONB, NULL
- Đổi tên cột `simulated_results_json` → `simulated_recommendations_json`; dòng mới: `simulated_recommendations_json` JSONB, NULL
- Sửa `student_profile_id`: NOT NULL → NULL
- **Thêm các cột:**
  - `notes` TEXT, NULL
  - `updated_at` TIMESTAMPTZ, NULL
- Lưu ý: `student_profile_id` thật là NULL được.

### 3.22 expert_rules

- **Xóa** cột `category` (không tồn tại trong DB).
- **Xóa** cột `action_json` (không tồn tại trong DB).
- **Xóa** cột `is_active` (không tồn tại trong DB).
- Sửa `rule_code`: kiểu VARCHAR(50) → VARCHAR(32)
- Sửa `condition_json`: NOT NULL → NULL
- **Thêm các cột:**
  - `rule_name` VARCHAR(256), NOT NULL
  - `degree_level` VARCHAR(64), NOT NULL, DEFAULT 'all'
  - `target_direction` VARCHAR(128), NULL
  - `condition_summary` TEXT, NULL
  - `action_recommendation` TEXT, NULL
  - `supporting_centers_count` INT, NOT NULL
  - `source_reference` VARCHAR(256), NULL
  - `certainty_factor` NUMERIC(3,2), NOT NULL, DEFAULT 0.9
  - `status` VARCHAR(32), NOT NULL, DEFAULT 'active'
  - `version` INT, NOT NULL
  - `verified_by` VARCHAR(128), NOT NULL, DEFAULT 'USAS Advisory Board'
  - `verified_at` TIMESTAMPTZ, NOT NULL
  - `created_at` TIMESTAMPTZ, NOT NULL
  - `updated_at` TIMESTAMPTZ, NULL

### 3.23 study_centers

- Đổi tên cột `rating_avg` → `average_rating`; dòng mới: `average_rating` NUMERIC(3,2), NOT NULL, DEFAULT 5.0
- Đổi tên cột `status` → `verification_status`; dòng mới: `verification_status` VARCHAR(16), NOT NULL, DEFAULT 'unverified'
- **Xóa** cột `is_verified` (không tồn tại trong DB).
- Sửa `name`: kiểu VARCHAR(255) → VARCHAR(256)
- Sửa `address`: kiểu VARCHAR(255) → VARCHAR(512)
- Sửa `phone`: kiểu VARCHAR(20) → VARCHAR(32)
- **Thêm các cột:**
  - `code` VARCHAR(32), NOT NULL
  - `website` VARCHAR(512), NULL
  - `city` VARCHAR(128), NULL
  - `study_levels` TEXT[], NOT NULL
  - `services` TEXT[], NOT NULL
  - `surveyed_at` DATE, NULL
  - `is_active` BOOLEAN, NOT NULL
  - `created_at` TIMESTAMPTZ, NOT NULL
  - `updated_at` TIMESTAMPTZ, NULL
  - `description` VARCHAR(4000), NULL
  - `email` VARCHAR(256), NULL
  - `owner_user_id` UUID, NULL; FK → users(id) ON DELETE SET NULL
  - `verified_at` TIMESTAMPTZ, NULL
  - `verified_by_user_id` UUID, NULL; FK → users(id) ON DELETE SET NULL
  - `accreditations` TEXT[], NOT NULL, DEFAULT '{}'
  - `center_type` VARCHAR(32), NOT NULL, DEFAULT 'study_abroad'
  - `cover_image_url` VARCHAR(1024), NULL
  - `district` VARCHAR(128), NULL
  - `established_year` INT, NULL
  - `gallery_images` TEXT[], NOT NULL, DEFAULT '{}'
  - `latitude` DOUBLE PRECISION, NULL
  - `license_number` VARCHAR(128), NULL
  - `logo_url` VARCHAR(1024), NULL
  - `longitude` DOUBLE PRECISION, NULL
  - `review_count` INT, NOT NULL, DEFAULT 0
  - `staff_count` INT, NULL
  - `visa_success_rate` DOUBLE PRECISION, NULL
  - `certified_counselor_count` INT, NULL
  - `legal_compliance_mode` VARCHAR(32), NOT NULL, DEFAULT 'licensed'
  - `legal_document_urls` TEXT[], NOT NULL, DEFAULT '{}'
  - `legal_representative` VARCHAR(150), NULL
  - `license_issued_by` VARCHAR(256), NULL
  - `license_issued_date` DATE, NULL
  - `tax_code` VARCHAR(32), NULL
  - `training_license_number` VARCHAR(128), NULL
  - `verification_note` VARCHAR(1000), NULL
- Lưu ý: Bỏ CHECK rating 0..5 (DB không có).

### 3.24 leads

- Đổi tên cột `share_profile_consent` → `shared_profile`; dòng mới: `shared_profile` BOOLEAN, NOT NULL
- Đổi tên cột `notes` → `message`; dòng mới: `message` VARCHAR(2000), NULL
- Sửa `status`: kiểu VARCHAR(20) → VARCHAR(16)
- **Thêm các cột:**
  - `consent_id` UUID, NULL; FK → consents(id) ON DELETE SET NULL
  - `study_level` VARCHAR(32), NULL
  - `contact_phone` VARCHAR(32), NULL
  - `updated_at` TIMESTAMPTZ, NULL
  - `acknowledged_center_contact` BOOLEAN, NOT NULL, DEFAULT FALSE
  - `shared_fields` TEXT[], NOT NULL, DEFAULT '{}'
  - `submitted_by_user_id` UUID, NULL; FK → users(id) ON DELETE SET NULL

### 3.25 lead_messages

- Đổi tên cột `message_body` → `content`; dòng mới: `content` VARCHAR(4000), NOT NULL
- **Thêm các cột:**
  - `flag_reason` VARCHAR(500), NULL
  - `status` VARCHAR(32), NOT NULL, DEFAULT 'visible'
  - `removed_by_user_id` UUID, NULL; FK → users(id) ON DELETE SET NULL
  - `updated_at` TIMESTAMPTZ, NULL

### 3.26 center_reviews

- Đổi tên cột `rating_score` → `rating`; dòng mới: `rating` INT, NOT NULL
- Đổi tên cột `review_comment` → `comment`; dòng mới: `comment` VARCHAR(2000), NULL
- **Thêm các cột:**
  - `lead_id` UUID, NULL; FK → leads(id) ON DELETE SET NULL
  - `is_verified` BOOLEAN, NOT NULL
  - `status` VARCHAR(32), NOT NULL, DEFAULT 'visible'
  - `updated_at` TIMESTAMPTZ, NULL
- Lưu ý: CHECK rating 1..5 là đúng nhưng cột tên `rating`.

### 3.27 skills

- Đổi tên cột `skill_code` → `key`; dòng mới: `key` VARCHAR(64), NOT NULL
- Đổi tên cột `system_prompt` → `definition_json`; dòng mới: `definition_json` JSONB, NOT NULL
- Sửa `name`: kiểu VARCHAR(100) → VARCHAR(128)
- **Thêm các cột:**
  - `version` INT, NOT NULL
  - `description` VARCHAR(500), NULL
  - `created_at` TIMESTAMPTZ, NOT NULL
  - `updated_at` TIMESTAMPTZ, NULL
- Lưu ý: UNIQUE là cặp (key, version), không phải một cột.

### 3.28 knowledge_documents

- Đổi tên cột `content` → `answer`; dòng mới: `answer` TEXT, NOT NULL
- **Xóa** cột `title` (không tồn tại trong DB).
- Sửa `question`: NOT NULL → NULL
- Sửa `study_level`: NULL → NOT NULL
- Sửa `scope`: kiểu VARCHAR(32) → VARCHAR(64)
- Sửa `status`: kiểu VARCHAR(20) → VARCHAR(16)
- **Thêm các cột:**
  - `doc_type` VARCHAR(32), NOT NULL, DEFAULT 'faq'
  - `retrieved_at` DATE, NULL
  - `verified_by_user_id` UUID, NULL; FK → users(id) ON DELETE SET NULL
  - `verified_at` TIMESTAMPTZ, NULL
  - `ingested_at` TIMESTAMPTZ, NULL
  - `created_at` TIMESTAMPTZ, NOT NULL
  - `updated_at` TIMESTAMPTZ, NULL
  - `source_type` VARCHAR(64), NOT NULL, DEFAULT 'official'
  - `code` VARCHAR(64), NOT NULL, DEFAULT ''
  - `embedding_hash` VARCHAR(64), NULL
- Lưu ý: `question` thật là NULL được. `study_level` thật là NOT NULL. UNIQUE `code`.

### 3.29 chat_sessions

- Sửa `title`: kiểu VARCHAR(255) → VARCHAR(200)
- **Thêm các cột:**
  - `study_level` VARCHAR(32), NULL
  - `current_step` VARCHAR(8), NULL
  - `updated_at` TIMESTAMPTZ, NULL
  - `state_json` JSONB, NULL

### 3.30 chat_messages

- Sửa `role`: kiểu VARCHAR(20) → VARCHAR(16)
- **Thêm các cột:**
  - `step` VARCHAR(8), NULL
  - `suggested_centers_json` JSONB, NULL
  - `review_status` VARCHAR(16), NULL
  - `updated_at` TIMESTAMPTZ, NULL
  - `meta_json` JSONB, NULL
  - `outcome` VARCHAR(16), NULL
  - `reply_to_message_id` UUID, NULL; FK → chat_messages(id) ON DELETE SET NULL

### 3.31 ai_calls

- Đổi tên cột `model_name` → `model`; dòng mới: `model` VARCHAR(64), NULL
- Sửa `prompt_tokens`: NOT NULL → NULL
- Sửa `cost_usd`: kiểu NUMERIC(8,6) → NUMERIC; NOT NULL → NULL
- **Thêm các cột:**
  - `kind` VARCHAR(16), NOT NULL
  - `completion_tokens` INT, NULL
  - `latency_ms` INT, NULL
  - `success` BOOLEAN, NOT NULL
  - `error` VARCHAR(1000), NULL
  - `updated_at` TIMESTAMPTZ, NULL
  - `fallback_used` BOOLEAN, NOT NULL, DEFAULT FALSE
  - `model_version` TEXT, NULL
  - `plan_code` TEXT, NULL
  - `skill_version` TEXT, NULL
  - `total_tokens` INT, NULL
  - `cached_tokens` INT, NULL
  - `chat_message_id` UUID, NULL; FK → chat_messages(id) ON DELETE SET NULL
  - `purpose` VARCHAR(64), NULL

### 3.32 eval_cases

- **THAY TOÀN BỘ mục này** (chức năng khác hẳn bản cũ). Nội dung mới:
  - Purpose: Các câu hỏi kiểm thử kèm đáp án mong đợi để đánh giá chatbot tư vấn.
  - `id` UUID, NOT NULL; PK
  - `question` VARCHAR(2000), NOT NULL
  - `expected_answer` VARCHAR(4000), NULL
  - `study_level` VARCHAR(32), NULL
  - `category` VARCHAR(32), NULL
  - `expected_sources_json` JSONB, NULL
  - `is_active` BOOLEAN, NOT NULL
  - `created_at` TIMESTAMPTZ, NOT NULL
  - `updated_at` TIMESTAMPTZ, NULL

### 3.33 eval_runs

- **THAY TOÀN BỘ mục này** (chức năng khác hẳn bản cũ). Nội dung mới:
  - Purpose: Một lần chạy đánh giá trên các test case, lưu tổng số case, số case đạt và kết quả từng case dạng JSON.
  - `id` UUID, NOT NULL; PK
  - `name` VARCHAR(128), NOT NULL
  - `model_version` VARCHAR(64), NULL
  - `skill_id` UUID, NULL
  - `total_cases` INT, NOT NULL
  - `passed_cases` INT, NOT NULL
  - `results_json` JSONB, NOT NULL
  - `started_at` TIMESTAMPTZ, NOT NULL
  - `finished_at` TIMESTAMPTZ, NULL
  - `notes` VARCHAR(1000), NULL
  - `created_at` TIMESTAMPTZ, NOT NULL
  - `updated_at` TIMESTAMPTZ, NULL
  - Không có FK nào (`skill_id` chỉ là cột UUID, không có ràng buộc khóa ngoại).

### 3.34 subscription_plans

- Đổi tên cột `plan_code` → `code`; dòng mới: `code` VARCHAR(64), NOT NULL
- Đổi tên cột `price_vnd` → `price_monthly_vnd`; dòng mới: `price_monthly_vnd` NUMERIC(12,2), NOT NULL, DEFAULT 0.0
- Đổi tên cột `daily_quota` → `ai_chat_daily_quota`; dòng mới: `ai_chat_daily_quota` INT, NOT NULL, DEFAULT 10
- Sửa `name`: kiểu VARCHAR(100) → VARCHAR(128)
- **Thêm các cột:**
  - `description` VARCHAR(1000), NULL
  - `duration_days` INT, NOT NULL
  - `max_leads_quota` INT, NOT NULL
  - `is_active` BOOLEAN, NOT NULL
  - `created_at` TIMESTAMPTZ, NOT NULL
  - `updated_at` TIMESTAMPTZ, NULL
- Lưu ý: Bỏ CHECK price >= 0 (DB không có).

### 3.35 user_subscriptions

- Đổi tên cột `subscription_plan_id` → `plan_id`; dòng mới: `plan_id` UUID, NOT NULL; FK → subscription_plans(id) ON DELETE CASCADE
- Sửa `status`: kiểu VARCHAR(20) → VARCHAR(32)
- **Thêm các cột:**
  - `starts_at` TIMESTAMPTZ, NOT NULL
  - `auto_renew` BOOLEAN, NOT NULL
  - `created_at` TIMESTAMPTZ, NOT NULL
  - `updated_at` TIMESTAMPTZ, NULL

### 3.36 user_daily_usages

- Sửa `updated_at`: NOT NULL → NULL
- **Thêm các cột:**
  - `leads_count` INT, NOT NULL
  - `created_at` TIMESTAMPTZ, NOT NULL

### 3.37 payment_transactions

- Đổi tên cột `payment_method` → `gateway`; dòng mới: `gateway` VARCHAR(64), NOT NULL
- Đổi tên cột `transaction_code` → `gateway_transaction_id`; dòng mới: `gateway_transaction_id` VARCHAR(128), NULL
- Sửa `amount_vnd`: kiểu NUMERIC(12,0) → NUMERIC(12
- Sửa `status`: kiểu VARCHAR(20) → VARCHAR(32)
- **Thêm các cột:**
  - `payload_json` TEXT, NULL
  - `paid_at` TIMESTAMPTZ, NULL
  - `created_at` TIMESTAMPTZ, NOT NULL
  - `updated_at` TIMESTAMPTZ, NULL
- Lưu ý: Bỏ UNIQUE ở transaction_code: DB không có unique cho gateway_transaction_id (chỉ là cột NULL được).

### 3.38 forum_posts

- Đổi tên cột `author_user_id` → `user_id`; dòng mới: `user_id` UUID, NULL; FK → users(id) ON DELETE SET NULL
- Đổi tên cột `body` → `content`; dòng mới: `content` TEXT, NOT NULL
- Sửa `title`: kiểu VARCHAR(255) → VARCHAR(300)
- Sửa `status`: kiểu VARCHAR(20) → VARCHAR(16)
- **Thêm các cột:**
  - `study_level` VARCHAR(32), NULL
  - `category` VARCHAR(32), NULL
  - `is_ai_generated` BOOLEAN, NOT NULL
  - `is_pinned` BOOLEAN, NOT NULL
  - `updated_at` TIMESTAMPTZ, NULL
- Lưu ý: `user_id` thật là NULL được (ON DELETE SET NULL).

### 3.39 forum_reports

- Sửa `reason`: kiểu VARCHAR(255) → VARCHAR(64)
- Sửa `status`: kiểu VARCHAR(20) → VARCHAR(16)
- **Thêm các cột:**
  - `detail` VARCHAR(1000), NULL
  - `handled_by_user_id` UUID, NULL; FK → users(id) ON DELETE SET NULL
  - `handled_at` TIMESTAMPTZ, NULL
  - `updated_at` TIMESTAMPTZ, NULL
- Lưu ý: `status` thật là open, resolved, ignored (doc cũ ghi pending/resolved), VARCHAR(16).

### 3.40 notifications

- Sửa `title`: kiểu VARCHAR(255) → VARCHAR(256)
- Sửa `body`: kiểu TEXT → VARCHAR(2000)
- **Thêm các cột:**
  - `type` VARCHAR(32), NOT NULL
  - `link_url` VARCHAR(512), NULL
  - `updated_at` TIMESTAMPTZ, NULL

### 3.41 audit_logs

- Sửa `action`: kiểu VARCHAR(100) → VARCHAR(64)
- Sửa `entity_type`: kiểu VARCHAR(50) → VARCHAR(64)
- Sửa `entity_id`: kiểu VARCHAR(50) → VARCHAR(64)
- Sửa `ip_address`: kiểu VARCHAR(45) → VARCHAR(64)
- **Thêm các cột:**
  - `user_agent` VARCHAR(512), NULL
  - `created_at` TIMESTAMPTZ, NOT NULL
  - `updated_at` TIMESTAMPTZ, NULL

### 3.42 policy_alerts

- **Thêm các cột:**
  - `summary` TEXT, NULL
  - `effective_date` DATE, NULL
  - `source_url` VARCHAR(512), NULL
  - `created_at` TIMESTAMPTZ, NOT NULL
  - `updated_at` TIMESTAMPTZ, NULL

## C. Thêm 7 mục mới (3.43 đến 3.49)

### 3.43 profile_certifications

Purpose: Kết quả chứng chỉ ngoại ngữ và thi chuẩn hóa của hồ sơ (IELTS, TOEFL, SAT...), mỗi bài thi một dòng. Thuộc nhóm Student profile.

- `id` UUID, NOT NULL; PK
- `student_profile_id` UUID, NOT NULL; FK → student_profiles(id) ON DELETE CASCADE
- `category` VARCHAR(16), NOT NULL
- `test_type` VARCHAR(32), NOT NULL
- `custom_name` VARCHAR(128), NULL
- `subject` VARCHAR(128), NULL
- `overall_score` NUMERIC(7,2), NULL
- `scale_text` VARCHAR(32), NULL
- `section_scores_json` JSONB, NULL
- `test_date` DATE, NULL
- `sort_order` INT, NOT NULL
- `created_at` TIMESTAMPTZ, NOT NULL
- `updated_at` TIMESTAMPTZ, NULL
- `is_target` BOOLEAN, NOT NULL, DEFAULT FALSE

### 3.44 majors

Purpose: Danh sách ngành học chuẩn hóa (mã CIP, tên gọi khác) dùng chung cho mọi trường. Nhóm University data.

- `id` UUID, NOT NULL; PK
- `name` VARCHAR(128), NOT NULL
- `cip_code` VARCHAR(7), NULL
- `created_at` TIMESTAMPTZ, NOT NULL
- `updated_at` TIMESTAMPTZ, NULL
- `aliases` TEXT[], NOT NULL, DEFAULT '{}'
- `is_popular` BOOLEAN, NOT NULL, DEFAULT FALSE
- `name_vi` VARCHAR(128), NULL

### 3.45 university_offering_majors

Purpose: Bảng nối nhiều-nhiều giữa university_offerings và majors. Khóa chính kép (university_offering_id, major_id).

- `university_offering_id` UUID, NOT NULL; PK (composite); FK → university_offerings(id) ON DELETE CASCADE
- `major_id` UUID, NOT NULL; PK (composite); FK → majors(id) ON DELETE RESTRICT
- `is_stem` BOOLEAN, NOT NULL, DEFAULT FALSE
- `is_verified` BOOLEAN, NOT NULL, DEFAULT FALSE

### 3.46 ranking_lists

Purpose: Danh sách hoặc nguồn xếp hạng trường (ví dụ bảng xếp hạng theo năm).

- `id` VARCHAR(48), NOT NULL; PK
- `publisher` VARCHAR(64), NOT NULL
- `name` VARCHAR(256), NOT NULL
- `list_group` VARCHAR(32), NULL
- `rank_type` VARCHAR(32), NULL
- `edition` VARCHAR(64), NULL
- `edition_year` INT, NULL
- `url` VARCHAR(512), NULL

### 3.47 university_rankings

Purpose: Bảng nối giữa universities và ranking_lists, lưu thứ hạng của trường trong từng danh sách. Khóa chính kép (university_id, list_id).

- `university_id` UUID, NOT NULL; PK (composite); FK → universities(id) ON DELETE CASCADE
- `list_id` VARCHAR(48), NOT NULL; PK (composite); FK → ranking_lists(id) ON DELETE CASCADE
- `rank` INT, NOT NULL
- `is_tie` BOOLEAN, NOT NULL, DEFAULT FALSE
- `rank_display` VARCHAR(32), NOT NULL

### 3.48 us_states

Purpose: Danh mục các bang của Mỹ.

- `id` UUID, NOT NULL; PK
- `code` VARCHAR(2), NOT NULL
- `name` VARCHAR(64), NOT NULL
- `region` VARCHAR(16), NOT NULL
- `is_popular` BOOLEAN, NOT NULL, DEFAULT FALSE
- `created_at` TIMESTAMPTZ, NOT NULL
- `updated_at` TIMESTAMPTZ, NULL

### 3.49 media

Purpose: Ảnh của một đối tượng (hiện chỉ có trường đại học): logo, cover, gallery, lưu trên Cloudinary. Dùng entity_type + entity_id nên **không có khóa ngoại**; xóa trường thì ảnh không tự bị xóa ở mức DB. Hiện có 996 dòng (249 trường x 4 ảnh).

- `id` UUID, NOT NULL; PK
- `entity_type` VARCHAR(64), NOT NULL
- `entity_id` UUID, NOT NULL
- `type` VARCHAR(32), NOT NULL
- `url` VARCHAR(1000), NOT NULL
- `display_order` INT, NOT NULL, DEFAULT 0
- `created_at` TIMESTAMPTZ, NOT NULL
- `updated_at` TIMESTAMPTZ, NULL

## D. Giá trị liệt kê đúng theo code

- users.role: student, parent, center, moderator, admin
- users.status: active, unverified, pending, temp_locked, locked, deleted
- otp_tokens.purpose: verify_email, reset_password
- parent_student_links.status: pending, active, revoked, expired
- consents.purpose: child_data_processing (policy_version hiện tại: 2026-10)
- forum_reports.status: open, resolved, ignored
- study_level: secondary, community_college, undergraduate, master, phd (không còn chỉ undergraduate/graduate như mô tả ở 3.11)
- leads.shared_fields: contact, academic, tests, goals, budget
- grade_scale: 10, 100, 4, letter

## D. Thay đổi sau migration `AddCenterVerificationRequestAndDocuments` (13/11/2026)

- Số bảng: **49 → 51**.
- **3.23 study_centers:** `verification_status` VARCHAR(16) → VARCHAR(32), giá trị: unverified, pending_review, verified, rejected, suspended. Thêm cột (đều NULL): `email_verified_at`, `phone_verified_at` (không còn dùng, đã bỏ SMS OTP), `terms_agreed_at`, `commitment_agreed_at` TIMESTAMPTZ; `registrant_name`, `registrant_position` VARCHAR(100); `fee_min`, `fee_max` NUMERIC(14,2); `fee_description` VARCHAR(1000). `average_rating` và `review_count` hiện được tính trực tiếp từ `center_reviews` khi đọc, hai cột vẫn tồn tại.
- **Thêm 3.50 `center_verification_requests`** (19 cột; FK `center_id` → study_centers CASCADE, `requested_by_user_id` → users RESTRICT, `reviewed_by_user_id` → users SET NULL; index: center_id, requested_by_user_id, reviewed_by_user_id, status, tax_code).
- **Thêm 3.51 `center_verification_documents`** (11 cột; FK `request_id` → center_verification_requests CASCADE, `center_id` → study_centers CASCADE; index: request_id, center_id).
- Mục 3.50 và 3.51 thuộc nhóm Trung tâm tư vấn.
