
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
