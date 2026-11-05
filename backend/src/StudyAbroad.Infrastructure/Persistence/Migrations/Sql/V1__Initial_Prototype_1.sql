-- ==============================================================================
-- USAS - SQL Migration V1: Prototype 1 Core (13 Tables)
-- Scope: UC-01 to UC-25 and UC-80 (26 Use Cases)
-- Target: PostgreSQL 16
-- Schema: app
-- ==============================================================================

CREATE EXTENSION IF NOT EXISTS "uuid-ossp";
CREATE EXTENSION IF NOT EXISTS "pgcrypto";
CREATE SCHEMA IF NOT EXISTS app;

CREATE OR REPLACE FUNCTION app.update_timestamp()
RETURNS TRIGGER AS $$
BEGIN
    NEW.updated_at = CURRENT_TIMESTAMP;
    RETURN NEW;
END;
$$ LANGUAGE plpgsql;

-- 1. users
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

-- 2. user_sessions
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

-- 3. otp_tokens
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

-- 4. consents
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

-- 5. student_profiles
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

-- 6. transcript_scores
CREATE TABLE IF NOT EXISTS app.transcript_scores (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    student_profile_id UUID NOT NULL REFERENCES app.student_profiles(id) ON DELETE CASCADE,
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

-- 7. profile_activities
CREATE TABLE IF NOT EXISTS app.profile_activities (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    student_profile_id UUID NOT NULL REFERENCES app.student_profiles(id) ON DELETE CASCADE,
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
    created_at TIMESTAMPTZ NOT NULL DEFAULT CURRENT_TIMESTAMP,
    updated_at TIMESTAMPTZ NOT NULL DEFAULT CURRENT_TIMESTAMP
);

CREATE TRIGGER trg_profile_activities_updated_at BEFORE UPDATE ON app.profile_activities
FOR EACH ROW EXECUTE FUNCTION app.update_timestamp();

-- 8. universities
CREATE TABLE IF NOT EXISTS app.universities (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    code VARCHAR(64) NOT NULL UNIQUE,
    name VARCHAR(256) NOT NULL,
    city VARCHAR(128),
    state VARCHAR(8),
    website VARCHAR(256),
    control VARCHAR(32) DEFAULT 'public',
    acceptance_rate NUMERIC(6,4),
    international_student_count INT DEFAULT 0,
    is_active BOOLEAN NOT NULL DEFAULT TRUE,
    created_at TIMESTAMPTZ NOT NULL DEFAULT CURRENT_TIMESTAMP,
    updated_at TIMESTAMPTZ NOT NULL DEFAULT CURRENT_TIMESTAMP
);

CREATE TRIGGER trg_universities_updated_at BEFORE UPDATE ON app.universities
FOR EACH ROW EXECUTE FUNCTION app.update_timestamp();

-- 9. university_offerings
CREATE TABLE IF NOT EXISTS app.university_offerings (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    university_id UUID NOT NULL REFERENCES app.universities(id) ON DELETE CASCADE,
    study_level VARCHAR(32) NOT NULL DEFAULT 'undergraduate',
    majors TEXT[],
    stem_majors TEXT[],
    academic_year VARCHAR(16) DEFAULT '2026-27',
    tuition_usd NUMERIC(12,2) DEFAULT 0,
    living_usd NUMERIC(12,2) DEFAULT 0,
    fees_usd NUMERIC(12,2) DEFAULT 0,
    other_expenses_usd NUMERIC(12,2) DEFAULT 0,
    min_gpa4 NUMERIC(4,2),
    min_ielts NUMERIC(3,1),
    min_toefl NUMERIC(5,1),
    min_duolingo NUMERIC(5,1),
    avg_gpa4 NUMERIC(4,2),
    sat25 INT,
    sat75 INT,
    sat_policy VARCHAR(32) DEFAULT 'optional',
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
    retention_rate_1yr NUMERIC(5,2),
    grad_rate_6yr NUMERIC(5,2),
    notes VARCHAR(2000),
    source_url VARCHAR(512),
    retrieved_at DATE DEFAULT CURRENT_DATE,
    created_at TIMESTAMPTZ NOT NULL DEFAULT CURRENT_TIMESTAMP,
    updated_at TIMESTAMPTZ NOT NULL DEFAULT CURRENT_TIMESTAMP,
    CONSTRAINT uq_uni_offering_level_p1 UNIQUE(university_id, study_level)
);

CREATE TRIGGER trg_university_offerings_updated_at BEFORE UPDATE ON app.university_offerings
FOR EACH ROW EXECUTE FUNCTION app.update_timestamp();

-- 10. analysis_results
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

-- 11. recommendations
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

-- 12. app_settings
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

-- 13. audit_logs
CREATE TABLE IF NOT EXISTS app.audit_logs (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    actor_user_id UUID REFERENCES app.users(id) ON DELETE SET NULL,
    action VARCHAR(64) NOT NULL,
    entity_type VARCHAR(64),
    entity_id VARCHAR(64),
    detail_json JSONB,
    ip_address VARCHAR(64),
    user_agent VARCHAR(512),
    created_at TIMESTAMPTZ NOT NULL DEFAULT CURRENT_TIMESTAMP,
    updated_at TIMESTAMPTZ NOT NULL DEFAULT CURRENT_TIMESTAMP
);

CREATE TRIGGER trg_audit_logs_updated_at BEFORE UPDATE ON app.audit_logs
FOR EACH ROW EXECUTE FUNCTION app.update_timestamp();
