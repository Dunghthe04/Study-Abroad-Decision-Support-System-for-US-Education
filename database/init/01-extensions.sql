-- Runs once, when the Postgres volume is first created.
CREATE EXTENSION IF NOT EXISTS vector;
CREATE SCHEMA IF NOT EXISTS app;      -- business data, owned by the .NET API (EF Core migrations)
CREATE SCHEMA IF NOT EXISTS advisor;  -- RAG knowledge base, owned by the advisor service
