from functools import lru_cache

from pydantic_settings import BaseSettings, SettingsConfigDict


class Settings(BaseSettings):
    model_config = SettingsConfigDict(env_file=".env", env_file_encoding="utf-8", extra="ignore")

    app_env: str = "development"
    log_level: str = "INFO"

    # Shared secret the .NET API sends in X-Api-Key. Empty = no check (local dev only).
    api_key: str = ""

    database_url: str = "postgresql://studyabroad:studyabroad@localhost:5432/studyabroad"

    ollama_base_url: str = "http://localhost:11434"
    llm_model: str = "qwen3:8b"
    embedding_model: str = "bge-m3"
    embedding_dim: int = 1024
    llm_temperature: float = 0.2
    llm_timeout_seconds: float = 120.0

    rag_top_k: int = 5
    rag_candidate_k: int = 20


@lru_cache
def get_settings() -> Settings:
    return Settings()
