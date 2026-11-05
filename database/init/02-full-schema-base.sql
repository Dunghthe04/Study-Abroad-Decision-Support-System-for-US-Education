-- ==============================================================================
-- USAS (US Study Abroad DSS) - Full Database Schema Base (37 Tables)
-- Schema: app
-- Target: PostgreSQL 16 + pgvector
-- Scope: 13 Functional Epics (FE-01 to FE-19), 90 Use Cases (UC-01 to UC-90)
-- Auto-loaded by Docker on startup: /docker-entrypoint-initdb.d/02-full-schema-base.sql
-- ==============================================================================

CREATE EXTENSION IF NOT EXISTS "uuid-ossp";
CREATE EXTENSION IF NOT EXISTS "pgcrypto";

CREATE SCHEMA IF NOT EXISTS app;

-- ------------------------------------------------------------------------------
-- Helper Trigger Function: Auto-update updated_at timestamp
-- ------------------------------------------------------------------------------
CREATE OR REPLACE FUNCTION app.update_timestamp()
RETURNS TRIGGER AS $$
BEGIN
    NEW.updated_at = CURRENT_TIMESTAMP;
    RETURN NEW;
END;
$$ LANGUAGE plpgsql;

-- ==============================================================================
-- 1. TÀI KHOẢN, XÁC THỰC & GIA ĐÌNH (FE-01)
-- ==============================================================================

-- 1.1 users
CREATE TABLE IF NOT EXISTS app.users (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    email VARCHAR(256) NOT NULL UNIQUE,
    password_hash VARCHAR(256) NOT NULL,
    full_name VARCHAR(128) NOT NULL,
    role VARCHAR(32) NOT NULL DEFAULT 'student',
    status VARCHAR(32) NOT NULL DEFAULT 'active',
    phone VARCHAR(32),
    date_of_birth DATE,
    is_birthdate_locked BOOLEAN NOT NULL DEFAULT FALSE,
    parent_acknowledged BOOLEAN NOT NULL DEFAULT FALSE,
    failed_login_attempts INT NOT NULL DEFAULT 0,
    lockout_end TIMESTAMPTZ,
    created_at TIMESTAMPTZ NOT NULL DEFAULT CURRENT_TIMESTAMP,
    updated_at TIMESTAMPTZ NOT NULL DEFAULT CURRENT_TIMESTAMP
);

CREATE TRIGGER trg_users_updated_at BEFORE UPDATE ON app.users
FOR EACH ROW EXECUTE FUNCTION app.update_timestamp();

CREATE INDEX IF NOT EXISTS ix_users_email ON app.users(email);
CREATE INDEX IF NOT EXISTS ix_users_role_status ON app.users(role, status);

-- 1.2 user_sessions
CREATE TABLE IF NOT EXISTS app.user_sessions (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    user_id UUID NOT NULL REFERENCES app.users(id) ON DELETE CASCADE,
    token_hash VARCHAR(256) NOT NULL,
    expires_at TIMESTAMPTZ NOT NULL,
    is_revoked BOOLEAN NOT NULL DEFAULT FALSE,
    ip_address VARCHAR(64),
    user_agent VARCHAR(512),
    created_at TIMESTAMPTZ NOT NULL DEFAULT CURRENT_TIMESTAMP,
    updated_at TIMESTAMPTZ NOT NULL DEFAULT CURRENT_TIMESTAMP
);

CREATE TRIGGER trg_user_sessions_updated_at BEFORE UPDATE ON app.user_sessions
FOR EACH ROW EXECUTE FUNCTION app.update_timestamp();

CREATE INDEX IF NOT EXISTS ix_user_sessions_user_id ON app.user_sessions(user_id);
CREATE INDEX IF NOT EXISTS ix_user_sessions_token_hash ON app.user_sessions(token_hash);

-- 1.3 otp_tokens
CREATE TABLE IF NOT EXISTS app.otp_tokens (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    user_id UUID NOT NULL REFERENCES app.users(id) ON DELETE CASCADE,
    purpose VARCHAR(32) NOT NULL,
    code_hash VARCHAR(256) NOT NULL,
    expires_at TIMESTAMPTZ NOT NULL,
    used_at TIMESTAMPTZ,
    attempts INT NOT NULL DEFAULT 0,
    created_at TIMESTAMPTZ NOT NULL DEFAULT CURRENT_TIMESTAMP,
    updated_at TIMESTAMPTZ NOT NULL DEFAULT CURRENT_TIMESTAMP
);

CREATE TRIGGER trg_otp_tokens_updated_at BEFORE UPDATE ON app.otp_tokens
FOR EACH ROW EXECUTE FUNCTION app.update_timestamp();

CREATE INDEX IF NOT EXISTS ix_otp_tokens_user_id ON app.otp_tokens(user_id);

-- 1.4 consents
CREATE TABLE IF NOT EXISTS app.consents (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    user_id UUID NOT NULL REFERENCES app.users(id) ON DELETE CASCADE,
    purpose VARCHAR(64) NOT NULL,
    granted BOOLEAN NOT NULL DEFAULT TRUE,
    policy_version VARCHAR(16) NOT NULL DEFAULT 'v1.0',
    created_at TIMESTAMPTZ NOT NULL DEFAULT CURRENT_TIMESTAMP,
    updated_at TIMESTAMPTZ NOT NULL DEFAULT CURRENT_TIMESTAMP
);

CREATE TRIGGER trg_consents_updated_at BEFORE UPDATE ON app.consents
FOR EACH ROW EXECUTE FUNCTION app.update_timestamp();

CREATE INDEX IF NOT EXISTS ix_consents_user_id ON app.consents(user_id);

-- 1.5 parent_student_links (UC-75)
CREATE TABLE IF NOT EXISTS app.parent_student_links (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    student_user_id UUID NOT NULL REFERENCES app.users(id) ON DELETE CASCADE,
    parent_user_id UUID NOT NULL REFERENCES app.users(id) ON DELETE CASCADE,
    status VARCHAR(32) NOT NULL DEFAULT 'pending',
    invite_code VARCHAR(64),
    linked_at TIMESTAMPTZ,
    created_at TIMESTAMPTZ NOT NULL DEFAULT CURRENT_TIMESTAMP,
    updated_at TIMESTAMPTZ NOT NULL DEFAULT CURRENT_TIMESTAMP,
    CONSTRAINT uq_parent_student_pair UNIQUE(student_user_id, parent_user_id)
);

CREATE TRIGGER trg_parent_student_links_updated_at BEFORE UPDATE ON app.parent_student_links
FOR EACH ROW EXECUTE FUNCTION app.update_timestamp();

CREATE INDEX IF NOT EXISTS ix_parent_student_links_student ON app.parent_student_links(student_user_id);
CREATE INDEX IF NOT EXISTS ix_parent_student_links_parent ON app.parent_student_links(parent_user_id);

-- ==============================================================================
-- 2. HỒ SƠ HỌC SINH, BẢNG ĐIỂM & NGOẠI KHÓA (FE-02)
-- ==============================================================================

-- 2.1 student_profiles
CREATE TABLE IF NOT EXISTS app.student_profiles (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    user_id UUID NOT NULL UNIQUE REFERENCES app.users(id) ON DELETE CASCADE,
    target_level VARCHAR(32) NOT NULL DEFAULT 'undergraduate',
    current_school VARCHAR(256),
    education_system VARCHAR(64) DEFAULT 'standard',
    current_education_level VARCHAR(64) DEFAULT 'high_school',
    graduation_year INT,
    current_grade VARCHAR(32),
    grade_scale VARCHAR(32) DEFAULT '10',
    overall_gpa NUMERIC(5,2),
    intended_major VARCHAR(128),
    intended_cip_code VARCHAR(16),
    intended_cip_category VARCHAR(128),
    intended_cip_code VARCHAR(16),
    intended_cip_category VARCHAR(128),
    ielts NUMERIC(3,1),
    toefl NUMERIC(5,1),
    duolingo NUMERIC(5,1),
    sat NUMERIC(6,1),
    act NUMERIC(4,1),
    gre NUMERIC(5,1),
    gmat NUMERIC(5,1),
    english_test_type VARCHAR(32) DEFAULT 'none',
    english_test_date DATE,
    other_tests_json JSONB,
    extracurricular_score NUMERIC(4,2) DEFAULT 0,
    budget_tier VARCHAR(64) DEFAULT 'tier_20k_35k',
    budget_min_usd NUMERIC(12,2),
    budget_max_usd NUMERIC(12,2),
    max_expected_tuition_usd NUMERIC(12,2),
    funding_source VARCHAR(64) DEFAULT 'family_support',
    needs_scholarship BOOLEAN DEFAULT FALSE,
    scholarship_need VARCHAR(32) DEFAULT 'preferred',
    financial_notes VARCHAR(1000),
    preferred_states TEXT[],
    target_school_type VARCHAR(32) DEFAULT 'all',
    roadmap_progress_json JSONB,
    transcript_file_url VARCHAR(512),
    draft_flow_state_json JSONB,
    flow_submitted_at TIMESTAMPTZ,
    created_at TIMESTAMPTZ NOT NULL DEFAULT CURRENT_TIMESTAMP,
    updated_at TIMESTAMPTZ NOT NULL DEFAULT CURRENT_TIMESTAMP
);

CREATE TRIGGER trg_student_profiles_updated_at BEFORE UPDATE ON app.student_profiles
FOR EACH ROW EXECUTE FUNCTION app.update_timestamp();

CREATE INDEX IF NOT EXISTS ix_student_profiles_user_id ON app.student_profiles(user_id);

-- 2.2 transcript_scores
CREATE TABLE IF NOT EXISTS app.transcript_scores (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    student_profile_id UUID NOT NULL REFERENCES app.student_profiles(id) ON DELETE CASCADE,
    created_by_user_id UUID REFERENCES app.users(id) ON DELETE SET NULL,
    education_level VARCHAR(32) NOT NULL DEFAULT 'high_school',
    term_name VARCHAR(64) NOT NULL,
    term_order INT NOT NULL DEFAULT 1,
    grade_name VARCHAR(32),
    semester INT NOT NULL DEFAULT 1,
    subject VARCHAR(128) NOT NULL,
    score NUMERIC(5,2) NOT NULL,
    raw_score VARCHAR(32),
    credits NUMERIC(4,1),
    created_at TIMESTAMPTZ NOT NULL DEFAULT CURRENT_TIMESTAMP,
    updated_at TIMESTAMPTZ NOT NULL DEFAULT CURRENT_TIMESTAMP
);

CREATE TRIGGER trg_transcript_scores_updated_at BEFORE UPDATE ON app.transcript_scores
FOR EACH ROW EXECUTE FUNCTION app.update_timestamp();

CREATE INDEX IF NOT EXISTS ix_transcript_scores_profile_id ON app.transcript_scores(student_profile_id);

-- 2.3 profile_activities
CREATE TABLE IF NOT EXISTS app.profile_activities (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    student_profile_id UUID NOT NULL REFERENCES app.student_profiles(id) ON DELETE CASCADE,
    created_by_user_id UUID REFERENCES app.users(id) ON DELETE SET NULL,
    kind VARCHAR(32) NOT NULL DEFAULT 'extracurricular',
    title VARCHAR(256) NOT NULL,
    organization VARCHAR(256),
    role VARCHAR(128),
    description VARCHAR(2000),
    start_date DATE,
    end_date DATE,
    award_year INT,
    impact_level INT DEFAULT 1,
    duration_months INT DEFAULT 0,
    is_ongoing BOOLEAN DEFAULT FALSE,
    ai_role VARCHAR(128),
    ai_scope VARCHAR(128),
    ai_level VARCHAR(128),
    ai_model VARCHAR(64),
    ai_extracted_at TIMESTAMPTZ,
    ai_input_hash VARCHAR(64),
    ai_role VARCHAR(128),
    ai_scope VARCHAR(128),
    ai_level VARCHAR(128),
    ai_model VARCHAR(64),
    ai_extracted_at TIMESTAMPTZ,
    ai_input_hash VARCHAR(64),
    created_at TIMESTAMPTZ NOT NULL DEFAULT CURRENT_TIMESTAMP,
    updated_at TIMESTAMPTZ NOT NULL DEFAULT CURRENT_TIMESTAMP
);

CREATE TRIGGER trg_profile_activities_updated_at BEFORE UPDATE ON app.profile_activities
FOR EACH ROW EXECUTE FUNCTION app.update_timestamp();

CREATE INDEX IF NOT EXISTS ix_profile_activities_profile_id ON app.profile_activities(student_profile_id);

-- 2.4 roadmap_steps
CREATE TABLE IF NOT EXISTS app.roadmap_steps (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    study_level VARCHAR(32) NOT NULL DEFAULT 'undergraduate',
    step_key VARCHAR(64) NOT NULL,
    title VARCHAR(256) NOT NULL,
    sort_order INT NOT NULL DEFAULT 1,
    summary VARCHAR(1000),
    content_json JSONB,
    created_at TIMESTAMPTZ NOT NULL DEFAULT CURRENT_TIMESTAMP,
    updated_at TIMESTAMPTZ NOT NULL DEFAULT CURRENT_TIMESTAMP,
    CONSTRAINT uq_roadmap_step_level_key UNIQUE(study_level, step_key)
);

CREATE TRIGGER trg_roadmap_steps_updated_at BEFORE UPDATE ON app.roadmap_steps
FOR EACH ROW EXECUTE FUNCTION app.update_timestamp();

-- ==============================================================================
-- 3. TRƯỜNG ĐẠI HỌC, TUYỂN SINH, HỌC BỔNG & CHI PHÍ (FE-06, FE-12)
-- ==============================================================================

-- 3.1 universities
CREATE TABLE IF NOT EXISTS app.universities (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    code VARCHAR(64) NOT NULL UNIQUE,
    name VARCHAR(256) NOT NULL,
    city VARCHAR(128),
    state VARCHAR(8),
    website VARCHAR(256),
    control VARCHAR(32) DEFAULT 'public',
    us_news_rank INT,
    rank_list VARCHAR(128) DEFAULT 'National Universities',
    institution_type VARCHAR(64) DEFAULT 'National University',
    is_ivy BOOLEAN DEFAULT FALSE,
    acceptance_rate NUMERIC(6,4),
    international_student_count INT DEFAULT 0,
    gpa_average NUMERIC(4,2),
    gpa_policy VARCHAR(128) DEFAULT 'Holistic Review',
    ielts_policy VARCHAR(128) DEFAULT 'Standard International Requirement',
    sat_policy_basis VARCHAR(256),
    is_active BOOLEAN NOT NULL DEFAULT TRUE,
    created_at TIMESTAMPTZ NOT NULL DEFAULT CURRENT_TIMESTAMP,
    updated_at TIMESTAMPTZ NOT NULL DEFAULT CURRENT_TIMESTAMP
);

CREATE TRIGGER trg_universities_updated_at BEFORE UPDATE ON app.universities
FOR EACH ROW EXECUTE FUNCTION app.update_timestamp();

CREATE INDEX IF NOT EXISTS ix_universities_code ON app.universities(code);
CREATE INDEX IF NOT EXISTS ix_universities_state ON app.universities(state);

-- 3.2 university_offerings
CREATE TABLE IF NOT EXISTS app.university_offerings (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    university_id UUID NOT NULL REFERENCES app.universities(id) ON DELETE CASCADE,
    study_level VARCHAR(32) NOT NULL DEFAULT 'undergraduate',
    majors TEXT[],
    stem_majors TEXT[],
    academic_year VARCHAR(16) DEFAULT '2026-27',
    act25 INT,
    act75 INT,
    applicants_total INT,
    admitted_total INT,
    intl_applicants INT,
    intl_admitted INT,
    gpa_submit_pct NUMERIC(5,2),
    tuition_usd NUMERIC(12,2) DEFAULT 0,
    living_usd NUMERIC(12,2) DEFAULT 0,
    fees_usd NUMERIC(12,2) DEFAULT 0,
    other_expenses_usd NUMERIC(12,2) DEFAULT 0,
    coa_total_usd NUMERIC(12,2) DEFAULT 0,
    cost_year VARCHAR(16) DEFAULT '2025-26',
    cost_basis VARCHAR(256),
    min_gpa4 NUMERIC(4,2),
    min_ielts NUMERIC(3,1),
    min_toefl NUMERIC(5,1),
    min_duolingo NUMERIC(5,1),
    avg_gpa4 NUMERIC(4,2),
    sat25 INT,
    sat75 INT,
    sat_policy VARCHAR(32) DEFAULT 'optional',
    deadline_ea DATE,
    deadline_ed DATE,
    deadline_rd DATE,
    deadline_basis VARCHAR(256),
    deadlines_json JSONB,
    intl_acceptance_rate NUMERIC(6,4),
    c7_gpa_importance VARCHAR(32),
    c7_test_importance VARCHAR(32),
    c7_ec_importance VARCHAR(32),
    sat_submit_pct NUMERIC(5,2),
    gpa_pct_4_0 NUMERIC(5,2),
    gpa_pct_375_399 NUMERIC(5,2),
    gpa_pct_350_374 NUMERIC(5,2),
    gpa_pct_325_349 NUMERIC(5,2),
    gpa_pct_300_324 NUMERIC(5,2),
    gpa_pct_250_299 NUMERIC(5,2),
    gpa_pct_below_250 NUMERIC(5,2),
    intl_aid_policy VARCHAR(64),
    intl_aid_avg_usd NUMERIC(12,2),
    intl_aid_recipients INT,
    retention_rate_1yr NUMERIC(5,2),
    grad_rate_6yr NUMERIC(5,2),
    cds_year VARCHAR(16) DEFAULT '2025-26',
    cds_source_file VARCHAR(256),
    cds_url VARCHAR(512),
    verified_c1 BOOLEAN DEFAULT TRUE,
    verified_c7 BOOLEAN DEFAULT TRUE,
    verified_c9 BOOLEAN DEFAULT TRUE,
    verified_c11 BOOLEAN DEFAULT TRUE,
    verified_h6 BOOLEAN DEFAULT TRUE,
    checked_by VARCHAR(128) DEFAULT 'USAS Research Team / DuongNX',
    checked_at TIMESTAMPTZ DEFAULT CURRENT_TIMESTAMP,
    data_status VARCHAR(32) DEFAULT 'VERIFIED',
    data_flags VARCHAR(256),
    notes VARCHAR(2000),
    source_url VARCHAR(512),
    admission_source_url VARCHAR(512),
    cost_source_url VARCHAR(512),
    english_source_url VARCHAR(512),
    retrieved_at DATE DEFAULT CURRENT_DATE,
    created_at TIMESTAMPTZ NOT NULL DEFAULT CURRENT_TIMESTAMP,
    updated_at TIMESTAMPTZ NOT NULL DEFAULT CURRENT_TIMESTAMP,
    CONSTRAINT uq_uni_offering_level UNIQUE(university_id, study_level)
);

CREATE TRIGGER trg_university_offerings_updated_at BEFORE UPDATE ON app.university_offerings
FOR EACH ROW EXECUTE FUNCTION app.update_timestamp();

CREATE INDEX IF NOT EXISTS ix_university_offerings_uni_id ON app.university_offerings(university_id);
CREATE INDEX IF NOT EXISTS ix_university_offerings_level ON app.university_offerings(study_level);

-- 3.3 scholarships
CREATE TABLE IF NOT EXISTS app.scholarships (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    university_id UUID REFERENCES app.universities(id) ON DELETE SET NULL,
    name VARCHAR(256) NOT NULL,
    study_level VARCHAR(32) NOT NULL DEFAULT 'undergraduate',
    scholarship_type VARCHAR(32) DEFAULT 'merit',
    amount_usd NUMERIC(12,2),
    intl_eligible BOOLEAN DEFAULT TRUE,
    renewable BOOLEAN DEFAULT TRUE,
    num_awards INT,
    coverage_type VARCHAR(64) DEFAULT 'partial_tuition',
    min_gpa4 NUMERIC(4,2),
    description VARCHAR(2000),
    deadline DATE,
    requirements_json JSONB,
    source_basis VARCHAR(256),
    source_url VARCHAR(512),
    verified BOOLEAN DEFAULT TRUE,
    retrieved_at DATE DEFAULT CURRENT_DATE,
    is_active BOOLEAN DEFAULT TRUE,
    created_at TIMESTAMPTZ NOT NULL DEFAULT CURRENT_TIMESTAMP,
    updated_at TIMESTAMPTZ NOT NULL DEFAULT CURRENT_TIMESTAMP
);

CREATE TRIGGER trg_scholarships_updated_at BEFORE UPDATE ON app.scholarships
FOR EACH ROW EXECUTE FUNCTION app.update_timestamp();

CREATE INDEX IF NOT EXISTS ix_scholarships_uni_id ON app.scholarships(university_id);

-- 3.4 target_schools (UC-30, 32, 33, 81)
CREATE TABLE IF NOT EXISTS app.target_schools (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    user_id UUID NOT NULL REFERENCES app.users(id) ON DELETE CASCADE,
    university_id UUID NOT NULL REFERENCES app.universities(id) ON DELETE CASCADE,
    study_level VARCHAR(32) NOT NULL DEFAULT 'undergraduate',
    status VARCHAR(32) NOT NULL DEFAULT 'considering',
    priority INT NOT NULL DEFAULT 1,
    application_round VARCHAR(32) DEFAULT 'Regular',
    deadline DATE,
    gap_analysis_json JSONB,
    created_at TIMESTAMPTZ NOT NULL DEFAULT CURRENT_TIMESTAMP,
    updated_at TIMESTAMPTZ NOT NULL DEFAULT CURRENT_TIMESTAMP,
    CONSTRAINT uq_target_school_user_uni UNIQUE(user_id, university_id, study_level)
);

CREATE TRIGGER trg_target_schools_updated_at BEFORE UPDATE ON app.target_schools
FOR EACH ROW EXECUTE FUNCTION app.update_timestamp();

CREATE INDEX IF NOT EXISTS ix_target_schools_user_id ON app.target_schools(user_id);

-- 3.5 state_living_costs (UC-28)
CREATE TABLE IF NOT EXISTS app.state_living_costs (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    state_code VARCHAR(8) NOT NULL UNIQUE,
    state_name VARCHAR(64) NOT NULL,
    index_score NUMERIC(6,2) NOT NULL DEFAULT 100.0,
    avg_annual_living_usd NUMERIC(12,2) NOT NULL,
    notes VARCHAR(500),
    created_at TIMESTAMPTZ NOT NULL DEFAULT CURRENT_TIMESTAMP,
    updated_at TIMESTAMPTZ NOT NULL DEFAULT CURRENT_TIMESTAMP
);

CREATE TRIGGER trg_state_living_costs_updated_at BEFORE UPDATE ON app.state_living_costs
FOR EACH ROW EXECUTE FUNCTION app.update_timestamp();

-- ==============================================================================
-- 4. MÔ HÌNH DSS, PHÂN TÍCH, GỢI Ý & KỊCH BẢN (FE-04, FE-16)
-- ==============================================================================

-- 4.1 app_settings (UC-68, 69, 70, 88, 89)
CREATE TABLE IF NOT EXISTS app.app_settings (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    key VARCHAR(128) NOT NULL UNIQUE,
    value_json JSONB NOT NULL,
    description VARCHAR(512),
    category VARCHAR(64) DEFAULT 'general',
    created_at TIMESTAMPTZ NOT NULL DEFAULT CURRENT_TIMESTAMP,
    updated_at TIMESTAMPTZ NOT NULL DEFAULT CURRENT_TIMESTAMP
);

CREATE TRIGGER trg_app_settings_updated_at BEFORE UPDATE ON app.app_settings
FOR EACH ROW EXECUTE FUNCTION app.update_timestamp();

CREATE INDEX IF NOT EXISTS ix_app_settings_key ON app.app_settings(key);

-- 4.2 analysis_results (UC-18, 19, 81)
CREATE TABLE IF NOT EXISTS app.analysis_results (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    student_profile_id UUID NOT NULL REFERENCES app.student_profiles(id) ON DELETE CASCADE,
    kind VARCHAR(64) NOT NULL,
    result_json JSONB NOT NULL,
    model_version VARCHAR(32) DEFAULT 'v1.0',
    created_at TIMESTAMPTZ NOT NULL DEFAULT CURRENT_TIMESTAMP,
    updated_at TIMESTAMPTZ NOT NULL DEFAULT CURRENT_TIMESTAMP
);

CREATE TRIGGER trg_analysis_results_updated_at BEFORE UPDATE ON app.analysis_results
FOR EACH ROW EXECUTE FUNCTION app.update_timestamp();

CREATE INDEX IF NOT EXISTS ix_analysis_results_profile_id ON app.analysis_results(student_profile_id);

-- 4.3 recommendations (UC-22, 23, 24, 25, 76, 87)
CREATE TABLE IF NOT EXISTS app.recommendations (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    user_id UUID NOT NULL REFERENCES app.users(id) ON DELETE CASCADE,
    student_profile_id UUID REFERENCES app.student_profiles(id) ON DELETE SET NULL,
    study_level VARCHAR(32) NOT NULL DEFAULT 'undergraduate',
    criteria_json JSONB NOT NULL,
    items_json JSONB NOT NULL,
    algorithm_version VARCHAR(32) DEFAULT 'v1.0_heuristic',
    model_source VARCHAR(32) DEFAULT 'hybrid',
    reconciliation_json JSONB,
    comparison_json JSONB,
    created_at TIMESTAMPTZ NOT NULL DEFAULT CURRENT_TIMESTAMP,
    updated_at TIMESTAMPTZ NOT NULL DEFAULT CURRENT_TIMESTAMP
);

CREATE TRIGGER trg_recommendations_updated_at BEFORE UPDATE ON app.recommendations
FOR EACH ROW EXECUTE FUNCTION app.update_timestamp();

CREATE INDEX IF NOT EXISTS ix_recommendations_user_id ON app.recommendations(user_id);

-- 4.4 what_if_simulations (UC-82, 85, 86)
CREATE TABLE IF NOT EXISTS app.what_if_simulations (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    user_id UUID NOT NULL REFERENCES app.users(id) ON DELETE CASCADE,
    student_profile_id UUID REFERENCES app.student_profiles(id) ON DELETE CASCADE,
    name VARCHAR(256) NOT NULL DEFAULT 'Kịch bản chưa đặt tên',
    simulation_type VARCHAR(32) NOT NULL DEFAULT 'what_if',
    input_params_json JSONB NOT NULL,
    results_json JSONB NOT NULL,
    is_saved BOOLEAN NOT NULL DEFAULT FALSE,
    created_at TIMESTAMPTZ NOT NULL DEFAULT CURRENT_TIMESTAMP,
    updated_at TIMESTAMPTZ NOT NULL DEFAULT CURRENT_TIMESTAMP
);

CREATE TRIGGER trg_what_if_simulations_updated_at BEFORE UPDATE ON app.what_if_simulations
FOR EACH ROW EXECUTE FUNCTION app.update_timestamp();

CREATE INDEX IF NOT EXISTS ix_what_if_simulations_user_id ON app.what_if_simulations(user_id);

-- ==============================================================================
-- 5. TRUNG TÂM TƯ VẤN & LIÊN HỆ (FE-07, FE-08, FE-10, FE-14)
-- ==============================================================================

-- 5.1 study_centers (UC-39, 40, 59, 71, 72)
CREATE TABLE IF NOT EXISTS app.study_centers (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    code VARCHAR(64) NOT NULL UNIQUE,
    name VARCHAR(256) NOT NULL,
    city VARCHAR(128),
    state VARCHAR(64),
    website VARCHAR(256),
    phone VARCHAR(32),
    email VARCHAR(256),
    description VARCHAR(4000),
    services TEXT[],
    min_fee_usd NUMERIC(12,2) DEFAULT 0,
    max_fee_usd NUMERIC(12,2) DEFAULT 0,
    published_fee_notes VARCHAR(1000),
    rating_avg NUMERIC(3,2) DEFAULT 0,
    review_count INT DEFAULT 0,
    verification_status VARCHAR(32) NOT NULL DEFAULT 'unverified',
    verified_at TIMESTAMPTZ,
    verified_by_user_id UUID REFERENCES app.users(id) ON DELETE SET NULL,
    owner_user_id UUID REFERENCES app.users(id) ON DELETE SET NULL,
    is_active BOOLEAN NOT NULL DEFAULT TRUE,
    created_at TIMESTAMPTZ NOT NULL DEFAULT CURRENT_TIMESTAMP,
    updated_at TIMESTAMPTZ NOT NULL DEFAULT CURRENT_TIMESTAMP
);

CREATE TRIGGER trg_study_centers_updated_at BEFORE UPDATE ON app.study_centers
FOR EACH ROW EXECUTE FUNCTION app.update_timestamp();

CREATE INDEX IF NOT EXISTS ix_study_centers_code ON app.study_centers(code);
CREATE INDEX IF NOT EXISTS ix_study_centers_rating ON app.study_centers(rating_avg);

-- 5.2 leads (UC-42, 43, 73)
CREATE TABLE IF NOT EXISTS app.leads (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    user_id UUID NOT NULL REFERENCES app.users(id) ON DELETE CASCADE,
    study_center_id UUID NOT NULL REFERENCES app.study_centers(id) ON DELETE CASCADE,
    consent_id UUID REFERENCES app.consents(id) ON DELETE SET NULL,
    status VARCHAR(32) NOT NULL DEFAULT 'new',
    notes VARCHAR(2000),
    shared_fields_json JSONB NOT NULL,
    created_at TIMESTAMPTZ NOT NULL DEFAULT CURRENT_TIMESTAMP,
    updated_at TIMESTAMPTZ NOT NULL DEFAULT CURRENT_TIMESTAMP
);

CREATE TRIGGER trg_leads_updated_at BEFORE UPDATE ON app.leads
FOR EACH ROW EXECUTE FUNCTION app.update_timestamp();

CREATE INDEX IF NOT EXISTS ix_leads_user_id ON app.leads(user_id);
CREATE INDEX IF NOT EXISTS ix_leads_center_id ON app.leads(study_center_id);

-- 5.3 lead_messages (UC-44, 58, 74)
CREATE TABLE IF NOT EXISTS app.lead_messages (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    lead_id UUID NOT NULL REFERENCES app.leads(id) ON DELETE CASCADE,
    sender_user_id UUID NOT NULL REFERENCES app.users(id) ON DELETE CASCADE,
    message_body VARCHAR(4000) NOT NULL,
    is_flagged BOOLEAN NOT NULL DEFAULT FALSE,
    moderation_note VARCHAR(500),
    created_at TIMESTAMPTZ NOT NULL DEFAULT CURRENT_TIMESTAMP,
    updated_at TIMESTAMPTZ NOT NULL DEFAULT CURRENT_TIMESTAMP
);

CREATE TRIGGER trg_lead_messages_updated_at BEFORE UPDATE ON app.lead_messages
FOR EACH ROW EXECUTE FUNCTION app.update_timestamp();

CREATE INDEX IF NOT EXISTS ix_lead_messages_lead_id ON app.lead_messages(lead_id);

-- 5.4 center_reviews (UC-41, 45, 57)
CREATE TABLE IF NOT EXISTS app.center_reviews (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    study_center_id UUID NOT NULL REFERENCES app.study_centers(id) ON DELETE CASCADE,
    user_id UUID NOT NULL REFERENCES app.users(id) ON DELETE CASCADE,
    lead_id UUID REFERENCES app.leads(id) ON DELETE SET NULL,
    rating INT NOT NULL CHECK (rating >= 1 AND rating <= 5),
    comment VARCHAR(2000),
    is_flagged BOOLEAN NOT NULL DEFAULT FALSE,
    is_hidden BOOLEAN NOT NULL DEFAULT FALSE,
    created_at TIMESTAMPTZ NOT NULL DEFAULT CURRENT_TIMESTAMP,
    updated_at TIMESTAMPTZ NOT NULL DEFAULT CURRENT_TIMESTAMP
);

CREATE TRIGGER trg_center_reviews_updated_at BEFORE UPDATE ON app.center_reviews
FOR EACH ROW EXECUTE FUNCTION app.update_timestamp();

CREATE INDEX IF NOT EXISTS ix_center_reviews_center_id ON app.center_reviews(study_center_id);

-- ==============================================================================
-- 6. TRỢ LÝ AI RAG, TRI THỨC & KIỂM ĐỊNH (FE-03, FE-16)
-- ==============================================================================

-- 6.1 skills (UC-34, 35)
CREATE TABLE IF NOT EXISTS app.skills (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    key VARCHAR(64) NOT NULL,
    version INT NOT NULL DEFAULT 1,
    name VARCHAR(128) NOT NULL,
    description VARCHAR(512),
    is_active BOOLEAN NOT NULL DEFAULT TRUE,
    prompt_template TEXT NOT NULL,
    created_at TIMESTAMPTZ NOT NULL DEFAULT CURRENT_TIMESTAMP,
    updated_at TIMESTAMPTZ NOT NULL DEFAULT CURRENT_TIMESTAMP,
    CONSTRAINT uq_skill_key_version UNIQUE(key, version)
);

CREATE TRIGGER trg_skills_updated_at BEFORE UPDATE ON app.skills
FOR EACH ROW EXECUTE FUNCTION app.update_timestamp();

-- 6.2 knowledge_documents (UC-51, 52, 84)
CREATE TABLE IF NOT EXISTS app.knowledge_documents (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    title VARCHAR(256) NOT NULL,
    question TEXT,
    topic VARCHAR(128),
    study_level VARCHAR(64) DEFAULT 'all',
    stage VARCHAR(64),
    scope VARCHAR(64) DEFAULT 'system_advisory',
    doc_type VARCHAR(64) NOT NULL DEFAULT 'faq',
    status VARCHAR(32) NOT NULL DEFAULT 'draft',
    quote TEXT,
    content TEXT NOT NULL,
    source_type VARCHAR(64) DEFAULT 'official',
    source_url VARCHAR(512),
    review_due_at TIMESTAMPTZ,
    version INT DEFAULT 1,
    embedding vector(1536),
    retrieved_at DATE DEFAULT CURRENT_DATE,
    verified_by_user_id UUID REFERENCES app.users(id) ON DELETE SET NULL,
    verified_at TIMESTAMPTZ,
    created_at TIMESTAMPTZ NOT NULL DEFAULT CURRENT_TIMESTAMP,
    updated_at TIMESTAMPTZ NOT NULL DEFAULT CURRENT_TIMESTAMP
);

CREATE TRIGGER trg_knowledge_documents_updated_at BEFORE UPDATE ON app.knowledge_documents
FOR EACH ROW EXECUTE FUNCTION app.update_timestamp();

CREATE INDEX IF NOT EXISTS ix_knowledge_documents_status ON app.knowledge_documents(status);

-- 6.3 chat_sessions (UC-34, 35, 36)
CREATE TABLE IF NOT EXISTS app.chat_sessions (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    user_id UUID NOT NULL REFERENCES app.users(id) ON DELETE CASCADE,
    skill_id UUID REFERENCES app.skills(id) ON DELETE SET NULL,
    title VARCHAR(256) NOT NULL DEFAULT 'Cuộc trò chuyện mới',
    study_level VARCHAR(32) DEFAULT 'undergraduate',
    created_at TIMESTAMPTZ NOT NULL DEFAULT CURRENT_TIMESTAMP,
    updated_at TIMESTAMPTZ NOT NULL DEFAULT CURRENT_TIMESTAMP
);

CREATE TRIGGER trg_chat_sessions_updated_at BEFORE UPDATE ON app.chat_sessions
FOR EACH ROW EXECUTE FUNCTION app.update_timestamp();

CREATE INDEX IF NOT EXISTS ix_chat_sessions_user_id ON app.chat_sessions(user_id);

-- 6.4 chat_messages (UC-36, 37, 38, 53)
CREATE TABLE IF NOT EXISTS app.chat_messages (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    chat_session_id UUID NOT NULL REFERENCES app.chat_sessions(id) ON DELETE CASCADE,
    role VARCHAR(32) NOT NULL,
    content TEXT NOT NULL,
    sources_json JSONB,
    review_status VARCHAR(32) DEFAULT 'unreviewed',
    created_at TIMESTAMPTZ NOT NULL DEFAULT CURRENT_TIMESTAMP,
    updated_at TIMESTAMPTZ NOT NULL DEFAULT CURRENT_TIMESTAMP
);

CREATE TRIGGER trg_chat_messages_updated_at BEFORE UPDATE ON app.chat_messages
FOR EACH ROW EXECUTE FUNCTION app.update_timestamp();

CREATE INDEX IF NOT EXISTS ix_chat_messages_session_id ON app.chat_messages(chat_session_id);

-- 6.5 ai_calls (UC-83)
CREATE TABLE IF NOT EXISTS app.ai_calls (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    user_id UUID REFERENCES app.users(id) ON DELETE SET NULL,
    kind VARCHAR(64) NOT NULL,
    model VARCHAR(64),
    skill_version VARCHAR(64),
    plan_tier VARCHAR(32) DEFAULT 'free',
    result_source VARCHAR(32) DEFAULT 'ai',
    prompt_tokens INT DEFAULT 0,
    completion_tokens INT DEFAULT 0,
    latency_ms INT DEFAULT 0,
    cost_usd NUMERIC(10,6) DEFAULT 0,
    success BOOLEAN NOT NULL DEFAULT TRUE,
    error_message VARCHAR(1000),
    created_at TIMESTAMPTZ NOT NULL DEFAULT CURRENT_TIMESTAMP,
    updated_at TIMESTAMPTZ NOT NULL DEFAULT CURRENT_TIMESTAMP
);

CREATE TRIGGER trg_ai_calls_updated_at BEFORE UPDATE ON app.ai_calls
FOR EACH ROW EXECUTE FUNCTION app.update_timestamp();

CREATE INDEX IF NOT EXISTS ix_ai_calls_user_id ON app.ai_calls(user_id);
CREATE INDEX IF NOT EXISTS ix_ai_calls_created_at ON app.ai_calls(created_at);

-- 6.6 eval_cases
CREATE TABLE IF NOT EXISTS app.eval_cases (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    question TEXT NOT NULL,
    expected_answer TEXT,
    category VARCHAR(64) DEFAULT 'general',
    source VARCHAR(128) DEFAULT 'benchmark_standard',
    study_level VARCHAR(64) DEFAULT 'undergraduate',
    expected_tier VARCHAR(32),
    expected_sources TEXT[],
    test_profile_json JSONB,
    is_active BOOLEAN NOT NULL DEFAULT TRUE,
    created_at TIMESTAMPTZ NOT NULL DEFAULT CURRENT_TIMESTAMP,
    updated_at TIMESTAMPTZ NOT NULL DEFAULT CURRENT_TIMESTAMP
);

CREATE TRIGGER trg_eval_cases_updated_at BEFORE UPDATE ON app.eval_cases
FOR EACH ROW EXECUTE FUNCTION app.update_timestamp();

-- 6.7 eval_runs
CREATE TABLE IF NOT EXISTS app.eval_runs (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    skill_id UUID REFERENCES app.skills(id) ON DELETE SET NULL,
    name VARCHAR(256) NOT NULL,
    run_type VARCHAR(64) DEFAULT 'benchmark',
    model_version VARCHAR(64),
    skill_version VARCHAR(64),
    passed_cases INT NOT NULL DEFAULT 0,
    total_cases INT NOT NULL DEFAULT 0,
    accuracy_tier_pct NUMERIC(5,2),
    score_error_margin NUMERIC(4,2),
    hallucination_rate NUMERIC(5,2),
    fallback_rate NUMERIC(5,2),
    alert_triggered BOOLEAN DEFAULT FALSE,
    details_json JSONB,
    created_at TIMESTAMPTZ NOT NULL DEFAULT CURRENT_TIMESTAMP,
    updated_at TIMESTAMPTZ NOT NULL DEFAULT CURRENT_TIMESTAMP
);

CREATE TRIGGER trg_eval_runs_updated_at BEFORE UPDATE ON app.eval_runs
FOR EACH ROW EXECUTE FUNCTION app.update_timestamp();

-- ==============================================================================
-- 7. GÓI DỊCH VỤ, THANH TOÁN & HẠN MỨC (FE-19, UC-77, 78, 79, 83)
-- ==============================================================================

-- 7.1 subscription_plans (UC-79)
CREATE TABLE IF NOT EXISTS app.subscription_plans (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    code VARCHAR(64) NOT NULL UNIQUE,
    name VARCHAR(128) NOT NULL,
    description VARCHAR(1000),
    price_monthly_vnd NUMERIC(12,2) NOT NULL DEFAULT 0,
    ai_chat_daily_quota INT NOT NULL DEFAULT 10,
    full_rec_daily_quota INT NOT NULL DEFAULT 2,
    is_active BOOLEAN NOT NULL DEFAULT TRUE,
    created_at TIMESTAMPTZ NOT NULL DEFAULT CURRENT_TIMESTAMP,
    updated_at TIMESTAMPTZ NOT NULL DEFAULT CURRENT_TIMESTAMP
);

CREATE TRIGGER trg_subscription_plans_updated_at BEFORE UPDATE ON app.subscription_plans
FOR EACH ROW EXECUTE FUNCTION app.update_timestamp();

-- 7.2 user_subscriptions (UC-78)
CREATE TABLE IF NOT EXISTS app.user_subscriptions (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    user_id UUID NOT NULL REFERENCES app.users(id) ON DELETE CASCADE,
    plan_id UUID NOT NULL REFERENCES app.subscription_plans(id) ON DELETE CASCADE,
    payer_user_id UUID NOT NULL REFERENCES app.users(id) ON DELETE CASCADE,
    start_date DATE NOT NULL,
    end_date DATE NOT NULL,
    auto_renew BOOLEAN NOT NULL DEFAULT FALSE,
    status VARCHAR(32) NOT NULL DEFAULT 'active',
    created_at TIMESTAMPTZ NOT NULL DEFAULT CURRENT_TIMESTAMP,
    updated_at TIMESTAMPTZ NOT NULL DEFAULT CURRENT_TIMESTAMP
);

CREATE TRIGGER trg_user_subscriptions_updated_at BEFORE UPDATE ON app.user_subscriptions
FOR EACH ROW EXECUTE FUNCTION app.update_timestamp();

CREATE INDEX IF NOT EXISTS ix_user_subscriptions_user_id ON app.user_subscriptions(user_id);

-- 7.3 user_daily_usages (UC-77)
CREATE TABLE IF NOT EXISTS app.user_daily_usages (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    user_id UUID NOT NULL REFERENCES app.users(id) ON DELETE CASCADE,
    usage_date DATE NOT NULL DEFAULT CURRENT_DATE,
    ai_chat_count INT NOT NULL DEFAULT 0,
    recommendation_count INT NOT NULL DEFAULT 0,
    contact_request_count INT NOT NULL DEFAULT 0,
    created_at TIMESTAMPTZ NOT NULL DEFAULT CURRENT_TIMESTAMP,
    updated_at TIMESTAMPTZ NOT NULL DEFAULT CURRENT_TIMESTAMP,
    CONSTRAINT uq_user_daily_usage_date UNIQUE(user_id, usage_date)
);

CREATE TRIGGER trg_user_daily_usages_updated_at BEFORE UPDATE ON app.user_daily_usages
FOR EACH ROW EXECUTE FUNCTION app.update_timestamp();

CREATE INDEX IF NOT EXISTS ix_user_daily_usages_user_date ON app.user_daily_usages(user_id, usage_date);

-- 7.4 payment_transactions (UC-78, 79)
CREATE TABLE IF NOT EXISTS app.payment_transactions (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    user_id UUID NOT NULL REFERENCES app.users(id) ON DELETE CASCADE,
    plan_id UUID NOT NULL REFERENCES app.subscription_plans(id) ON DELETE CASCADE,
    gateway VARCHAR(64) NOT NULL DEFAULT 'VNPAY',
    transaction_code VARCHAR(128) NOT NULL UNIQUE,
    amount_vnd NUMERIC(12,2) NOT NULL,
    status VARCHAR(32) NOT NULL DEFAULT 'pending',
    paid_at TIMESTAMPTZ,
    receipt_url VARCHAR(512),
    created_at TIMESTAMPTZ NOT NULL DEFAULT CURRENT_TIMESTAMP,
    updated_at TIMESTAMPTZ NOT NULL DEFAULT CURRENT_TIMESTAMP
);

CREATE TRIGGER trg_payment_transactions_updated_at BEFORE UPDATE ON app.payment_transactions
FOR EACH ROW EXECUTE FUNCTION app.update_timestamp();

CREATE INDEX IF NOT EXISTS ix_payment_transactions_user_id ON app.payment_transactions(user_id);
CREATE INDEX IF NOT EXISTS ix_payment_transactions_code ON app.payment_transactions(transaction_code);

-- ==============================================================================
-- 8. DIỄN ĐÀN, THÔNG BÁO & KIỂM TOÁN (FE-18, FE-01)
-- ==============================================================================

-- 8.1 forum_posts (UC-46, 47, 48, 49, 55)
CREATE TABLE IF NOT EXISTS app.forum_posts (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    user_id UUID NOT NULL REFERENCES app.users(id) ON DELETE CASCADE,
    parent_post_id UUID REFERENCES app.forum_posts(id) ON DELETE CASCADE,
    title VARCHAR(256),
    content TEXT NOT NULL,
    topic VARCHAR(64) DEFAULT 'general',
    status VARCHAR(32) NOT NULL DEFAULT 'published',
    upvotes INT NOT NULL DEFAULT 0,
    qwen_extracted JSONB,
    extractor_hash VARCHAR(64),
    qwen_extracted JSONB,
    extractor_hash VARCHAR(64),
    created_at TIMESTAMPTZ NOT NULL DEFAULT CURRENT_TIMESTAMP,
    updated_at TIMESTAMPTZ NOT NULL DEFAULT CURRENT_TIMESTAMP
);

CREATE TRIGGER trg_forum_posts_updated_at BEFORE UPDATE ON app.forum_posts
FOR EACH ROW EXECUTE FUNCTION app.update_timestamp();

CREATE INDEX IF NOT EXISTS ix_forum_posts_user_id ON app.forum_posts(user_id);
CREATE INDEX IF NOT EXISTS ix_forum_posts_parent_id ON app.forum_posts(parent_post_id);

-- 8.2 forum_reports (UC-50, 54)
CREATE TABLE IF NOT EXISTS app.forum_reports (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    forum_post_id UUID NOT NULL REFERENCES app.forum_posts(id) ON DELETE CASCADE,
    reporter_user_id UUID NOT NULL REFERENCES app.users(id) ON DELETE CASCADE,
    handled_by_user_id UUID REFERENCES app.users(id) ON DELETE SET NULL,
    reason VARCHAR(512) NOT NULL,
    status VARCHAR(32) NOT NULL DEFAULT 'pending',
    resolution_notes VARCHAR(1000),
    created_at TIMESTAMPTZ NOT NULL DEFAULT CURRENT_TIMESTAMP,
    updated_at TIMESTAMPTZ NOT NULL DEFAULT CURRENT_TIMESTAMP
);

CREATE TRIGGER trg_forum_reports_updated_at BEFORE UPDATE ON app.forum_reports
FOR EACH ROW EXECUTE FUNCTION app.update_timestamp();

-- 8.3 notifications
CREATE TABLE IF NOT EXISTS app.notifications (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    user_id UUID NOT NULL REFERENCES app.users(id) ON DELETE CASCADE,
    type VARCHAR(64) NOT NULL,
    title VARCHAR(256) NOT NULL,
    body VARCHAR(2000),
    link_url VARCHAR(512),
    read_at TIMESTAMPTZ,
    created_at TIMESTAMPTZ NOT NULL DEFAULT CURRENT_TIMESTAMP,
    updated_at TIMESTAMPTZ NOT NULL DEFAULT CURRENT_TIMESTAMP
);

CREATE TRIGGER trg_notifications_updated_at BEFORE UPDATE ON app.notifications
FOR EACH ROW EXECUTE FUNCTION app.update_timestamp();

CREATE INDEX IF NOT EXISTS ix_notifications_user_id ON app.notifications(user_id);

-- 8.4 audit_logs (UC-80)
CREATE TABLE IF NOT EXISTS app.audit_logs (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    actor_user_id UUID REFERENCES app.users(id) ON DELETE SET NULL,
    action VARCHAR(64) NOT NULL,
    entity_type VARCHAR(64),
    entity_id VARCHAR(64),
    detail_json JSONB,
    ip_address VARCHAR(64),
    user_agent VARCHAR(512),
    created_at TIMESTAMPTZ NOT NULL DEFAULT CURRENT_TIMESTAMP
);
-- Lưu ý: audit_logs là bảng ghi nhật ký bất biến (append-only), không có updated_at và không cho phép cập nhật.

CREATE INDEX IF NOT EXISTS ix_audit_logs_actor ON app.audit_logs(actor_user_id);
CREATE INDEX IF NOT EXISTS ix_audit_logs_action ON app.audit_logs(action);

-- ==============================================================================

-- ==============================================================================
-- 9. QUY TẮC CHUYÊN GIA & KIỂM ĐỊNH MÔ HÌNH DSS (FE-04, FE-05, UC-22, UC-69, UC-87)
-- ==============================================================================

-- 9.1 expert_rules: 29 luật rẽ nhánh chuyên gia (R01-R29)
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

CREATE TRIGGER trg_expert_rules_updated_at BEFORE UPDATE ON app.expert_rules
FOR EACH ROW EXECUTE FUNCTION app.update_timestamp();

CREATE INDEX IF NOT EXISTS ix_expert_rules_code ON app.expert_rules(rule_code);
CREATE INDEX IF NOT EXISTS ix_expert_rules_level ON app.expert_rules(degree_level);

-- 9.2 recommendation_items: Kiểm định chéo giữa AI và Luật
CREATE TABLE IF NOT EXISTS app.recommendation_items (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    recommendation_id UUID NOT NULL REFERENCES app.recommendations(id) ON DELETE CASCADE,
    university_id UUID NOT NULL REFERENCES app.universities(id) ON DELETE CASCADE,
    program_name VARCHAR(256),
    tier_ai VARCHAR(32),
    tier_rule VARCHAR(32),
    is_tier_divergent BOOLEAN DEFAULT FALSE,
    match_strength NUMERIC(5,2),
    rule_codes_triggered VARCHAR(256),
    financial_fit_status VARCHAR(32) DEFAULT 'affordable',
    data_completeness_pct NUMERIC(5,2),
    is_near_threshold BOOLEAN DEFAULT FALSE,
    result_source VARCHAR(32) DEFAULT 'ai',
    gap_reasons TEXT,
    created_at TIMESTAMPTZ NOT NULL DEFAULT CURRENT_TIMESTAMP
);

CREATE INDEX IF NOT EXISTS ix_recommendation_items_rec_id ON app.recommendation_items(recommendation_id);
CREATE INDEX IF NOT EXISTS ix_recommendation_items_uni_id ON app.recommendation_items(university_id);

-- 9.3 policy_alerts: Cảnh báo chính sách visa & học phí trên Dashboard
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

CREATE TRIGGER trg_policy_alerts_updated_at BEFORE UPDATE ON app.policy_alerts
FOR EACH ROW EXECUTE FUNCTION app.update_timestamp();

-- 9.4 data_import_batches & field_evidence: Minh chứng quy trình ETL
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

-- -- DỮ LIỆU SEED MẪU CỐT LÕI (INIT SEED)
-- ==============================================================================

-- 1. Tài khoản Quản trị viên Administrator mặc định (Mật khẩu băm BCrypt bảo mật)
INSERT INTO app.users (id, email, password_hash, full_name, role, status, parent_acknowledged, is_birthdate_locked, date_of_birth, created_at, updated_at)
VALUES (
    'a0000000-0000-0000-0000-000000000001',
    'admin@usas.edu.vn',
    '$2a$11$0wO.9u7z.Yy9r27VbQnOq.B.7r1eQjE64L6hA0fWbW1R6RzD8e5eW', -- BCrypt hash
    'Hệ Thống Quản Trị USAS',
    'admin',
    'active',
    FALSE,
    TRUE,
    '1990-01-01',
    CURRENT_TIMESTAMP,
    CURRENT_TIMESTAMP
) ON CONFLICT (email) DO NOTHING;

-- 2. Gói dịch vụ mặc định (FE-19)
INSERT INTO app.subscription_plans (id, code, name, description, price_monthly_vnd, ai_chat_daily_quota, full_rec_daily_quota, is_active)
VALUES 
(
    'b0000000-0000-0000-0000-000000000001',
    'plan_free',
    'Gói Miễn Phí (Free Tier)',
    'Hạn mức 10 câu hỏi chat AI mỗi ngày, tra cứu trường không giới hạn, mô phỏng what-if bằng mô hình luật',
    0,
    10,
    1,
    TRUE
),
(
    'b0000000-0000-0000-0000-000000000002',
    'plan_monthly_premium',
    'Gói Trả Phí Hàng Tháng (Premium Tier)',
    'Hạn mức 30 câu hỏi chat AI mỗi ngày, phân tích hồ sơ và gợi ý chuyên sâu không giới hạn',
    199000,
    30,
    5,
    TRUE
) ON CONFLICT (code) DO NOTHING;

-- 3. Cấu hình hệ thống mặc định (app_settings: tỷ giá, quy đổi điểm, ngưỡng)
INSERT INTO app.app_settings (id, key, value_json, description, category)
VALUES 
(
    gen_random_uuid(),
    'currency.usd_vnd',
    '{"rate": 25400, "updated_at": "2026-10-20"}',
    'Tỷ giá quy đổi USD sang VND dùng cho ước tính chi phí (UC-70)',
    'finance'
),
(
    gen_random_uuid(),
    'recommend.thresholds',
    '{"safety_min_strength": 75, "match_min_strength": 25, "reach_min_strength": 0}',
    'Ngưỡng phân nhóm Reach/Match/Safety theo điểm Strength (UC-69, SRS chuẩn 25 và 75)',
    'dss_model'
),
(
    gen_random_uuid(),
    'grade_scale.10_to_4',
    '{"ranges": [{"min": 9.0, "max": 10.0, "gpa4": 4.0}, {"min": 8.0, "max": 8.9, "gpa4": 3.5}, {"min": 7.0, "max": 7.9, "gpa4": 3.0}, {"min": 6.0, "max": 6.9, "gpa4": 2.5}, {"min": 5.0, "max": 5.9, "gpa4": 2.0}, {"min": 0.0, "max": 4.9, "gpa4": 1.0}]}',
    'Bảng quy đổi điểm phổ thông Việt Nam hệ 10 sang thang 4.0 WES (UC-68)',
    'academic'
),
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

