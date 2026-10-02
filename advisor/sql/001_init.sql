-- Knowledge base for RAG. Owned by the advisor service; the .NET API uses the "app" schema.
CREATE EXTENSION IF NOT EXISTS vector;
CREATE SCHEMA IF NOT EXISTS advisor;

CREATE TABLE IF NOT EXISTS advisor.documents (
    id           BIGSERIAL PRIMARY KEY,
    source_url   TEXT,
    title        TEXT NOT NULL,
    doc_type     TEXT NOT NULL,                 -- visa | ho_so | chi_phi | hoc_bong | ...
    study_level  TEXT NOT NULL DEFAULT 'general',      -- secondary | community_college | undergraduate | master | phd | general
    chunk_index  INT  NOT NULL DEFAULT 0,
    content      TEXT NOT NULL,
    embedding    vector(1024) NOT NULL,         -- bge-m3
    content_tsv  tsvector GENERATED ALWAYS AS (to_tsvector('simple', content)) STORED,
    metadata     JSONB NOT NULL DEFAULT '{}'::jsonb,
    retrieved_at DATE,
    created_at   TIMESTAMPTZ NOT NULL DEFAULT now(),
    UNIQUE (source_url, chunk_index)
);

CREATE INDEX IF NOT EXISTS ix_documents_embedding ON advisor.documents USING hnsw (embedding vector_cosine_ops);
CREATE INDEX IF NOT EXISTS ix_documents_tsv ON advisor.documents USING gin (content_tsv);
CREATE INDEX IF NOT EXISTS ix_documents_level_type ON advisor.documents (study_level, doc_type);
