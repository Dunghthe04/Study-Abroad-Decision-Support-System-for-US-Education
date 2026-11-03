-- Bo sung cot tren bang da tao boi EF (truoc khi chay 02-full-schema-base.sql).
-- Tranh loi CREATE INDEX / seed khi CREATE TABLE IF NOT EXISTS bi bo qua.

ALTER TABLE app.users ADD COLUMN IF NOT EXISTS date_of_birth DATE;
ALTER TABLE app.users ADD COLUMN IF NOT EXISTS is_birthdate_locked BOOLEAN DEFAULT FALSE;
ALTER TABLE app.users ALTER COLUMN created_at SET DEFAULT CURRENT_TIMESTAMP;

ALTER TABLE app.app_settings ADD COLUMN IF NOT EXISTS category VARCHAR(64);
ALTER TABLE app.app_settings ALTER COLUMN created_at SET DEFAULT CURRENT_TIMESTAMP;

ALTER TABLE app.scholarships ADD COLUMN IF NOT EXISTS description VARCHAR(2000);
ALTER TABLE app.scholarships ADD COLUMN IF NOT EXISTS requirements_json JSONB;
ALTER TABLE app.scholarships ALTER COLUMN is_active SET DEFAULT TRUE;

ALTER TABLE app.knowledge_documents ALTER COLUMN study_level SET DEFAULT 'general';

ALTER TABLE app.study_centers ADD COLUMN IF NOT EXISTS state VARCHAR(64);
ALTER TABLE app.study_centers ADD COLUMN IF NOT EXISTS min_fee_usd NUMERIC(12,2) DEFAULT 0;
ALTER TABLE app.study_centers ADD COLUMN IF NOT EXISTS max_fee_usd NUMERIC(12,2) DEFAULT 0;
ALTER TABLE app.study_centers ADD COLUMN IF NOT EXISTS published_fee_notes VARCHAR(1000);
ALTER TABLE app.study_centers ADD COLUMN IF NOT EXISTS rating_avg NUMERIC(3,2) DEFAULT 0;
ALTER TABLE app.study_centers ADD COLUMN IF NOT EXISTS review_count INT DEFAULT 0;
ALTER TABLE app.study_centers ALTER COLUMN study_levels SET DEFAULT '{}'::text[];
ALTER TABLE app.study_centers ALTER COLUMN services SET DEFAULT '{}'::text[];

-- EF dung numeric(5,4) cho phan tram CDS (0-100); seed Excel dung 72.00 khong phai 0.72.
ALTER TABLE app.university_offerings
    ALTER COLUMN sat_submit_pct TYPE NUMERIC(5,2),
    ALTER COLUMN gpa_pct_4_0 TYPE NUMERIC(5,2),
    ALTER COLUMN gpa_pct_375_399 TYPE NUMERIC(5,2),
    ALTER COLUMN gpa_pct_350_374 TYPE NUMERIC(5,2),
    ALTER COLUMN gpa_pct_325_349 TYPE NUMERIC(5,2),
    ALTER COLUMN gpa_pct_300_324 TYPE NUMERIC(5,2),
    ALTER COLUMN gpa_pct_250_299 TYPE NUMERIC(5,2),
    ALTER COLUMN gpa_pct_below_250 TYPE NUMERIC(5,2),
    ALTER COLUMN retention_rate_1yr TYPE NUMERIC(5,2),
    ALTER COLUMN grad_rate_6yr TYPE NUMERIC(5,2);

-- EF thuong khong dat DEFAULT cho created_at; seed SQL can mac dinh.
DO $$
DECLARE
    bang RECORD;
BEGIN
    FOR bang IN
        SELECT c.table_name
        FROM information_schema.columns c
        WHERE c.table_schema = 'app'
          AND c.column_name = 'created_at'
          AND c.is_nullable = 'NO'
    LOOP
        EXECUTE format(
            'ALTER TABLE app.%I ALTER COLUMN created_at SET DEFAULT CURRENT_TIMESTAMP',
            bang.table_name
        );
    END LOOP;
END $$;
