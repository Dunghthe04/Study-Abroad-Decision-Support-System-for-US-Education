import logging
from contextlib import asynccontextmanager

from fastapi import FastAPI

from app.api.routes import chat, health
from app.core import db
from app.core.config import get_settings
from app.rag.retriever import Retriever
from app.services.advisor import AdvisorService
from app.services.ollama import OllamaClient


@asynccontextmanager
async def lifespan(app: FastAPI):
    settings = get_settings()
    logging.basicConfig(level=settings.log_level)

    await db.run_schema_sql()
    pool = await db.open_pool()
    llm = OllamaClient(settings)

    app.state.pool = pool
    app.state.llm = llm
    app.state.advisor = AdvisorService(llm, Retriever(pool, settings.rag_candidate_k, settings.rag_top_k))
    yield
    await llm.aclose()
    await db.close_pool()


def create_app() -> FastAPI:
    settings = get_settings()
    app = FastAPI(
        title="Study Abroad Advisor Service",
        version="0.1.0",
        lifespan=lifespan,
        docs_url="/docs" if settings.app_env != "production" else None,
        redoc_url=None,
    )
    app.include_router(health.router)
    app.include_router(chat.router)
    return app


app = create_app()
