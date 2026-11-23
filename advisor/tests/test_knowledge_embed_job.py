"""Tạo vector ở nền cho kho kiến thức: backend gọi sau khi moderator import. Dùng hồ chứa giả, không đụng DB thật."""

import asyncio
import hashlib
from contextlib import contextmanager

import pytest
from fastapi.testclient import TestClient

from app.api.deps import get_embed_job
from app.main import create_app
from app.services.knowledge_embedding import KnowledgeEmbedJob, question_hash
from app.services.ollama import OllamaError


class FakeConn:
    def __init__(self, store: "FakeStore") -> None:
        self._store = store
        self.staged: list[dict] = []

    def execute(self, sql, params=None):
        if "SELECT id, question" not in sql:
            self.staged.append(params)
        return self  # fetchall bên dưới

    def fetchall(self):
        return [(r["id"], r["question"], r["hash"], r["has_vector"]) for r in self._store.rows]


class FakeStore:
    """Bảng giả: mỗi dòng có id, question, hash, has_vector; ghi vào chỉ có hiệu lực khi thoát khối with (commit)."""

    def __init__(self, questions: list[str]) -> None:
        self.rows = [
            {"id": i, "question": q, "hash": None, "has_vector": False}
            for i, q in enumerate(questions, start=1)
        ]
        self.fail_commit = False

    @contextmanager
    def connection(self):
        conn = FakeConn(self)
        yield conn
        if self.fail_commit:
            raise RuntimeError("DB lỗi")
        for params in conn.staged:
            for row in self.rows:
                # giống câu UPDATE thật: chỉ ghi khi câu hỏi chưa bị sửa
                if row["id"] == params["id"] and row["question"] == params["question"]:
                    row["has_vector"], row["hash"] = True, params["hash"]


class FakeEmbedder:
    def __init__(self, fail_times: int = 0) -> None:
        self.asked: list[list[str]] = []
        self.fail_times = fail_times
        self.gate: asyncio.Event | None = None  # đặt để giữ lần chạy lại giữa chừng

    async def embed(self, texts):
        if self.gate is not None:
            await self.gate.wait()
        if self.fail_times > 0:
            self.fail_times -= 1
            raise OllamaError("429")
        self.asked.append(list(texts))
        return [[b / 255 for b in hashlib.sha256(t.encode()).digest()] * 32 for t in texts]


def make_job(store, embedder) -> KnowledgeEmbedJob:
    return KnowledgeEmbedJob(store, embedder, batch=2, retries=3, retry_wait=0)


async def finish(job: KnowledgeEmbedJob) -> None:
    await asyncio.wait_for(job._task, timeout=5)  # noqa: SLF001


async def test_creates_vectors_for_rows_that_have_none_in_batches():
    store, embedder = FakeStore(["A?", "B?", "C?"]), FakeEmbedder()
    job = make_job(store, embedder)

    assert job.start() is True
    await finish(job)

    assert embedder.asked == [["A?", "B?"], ["C?"]]
    assert all(r["has_vector"] and r["hash"] == question_hash(r["question"]) for r in store.rows)
    assert (job.done, job.error, job.running) == (3, None, False)


async def test_rows_that_already_have_a_matching_vector_are_skipped():
    store, embedder = FakeStore(["A?", "B?"]), FakeEmbedder()
    store.rows[0].update(has_vector=True, hash=question_hash("A?"))
    job = make_job(store, embedder)

    assert await job.count_pending() == 1
    job.start()
    await finish(job)

    assert embedder.asked == [["B?"]]


async def test_a_second_start_while_running_does_not_run_twice():
    store, embedder = FakeStore(["A?"]), FakeEmbedder()
    embedder.gate = asyncio.Event()
    job = make_job(store, embedder)

    assert job.start() is True
    await asyncio.sleep(0)
    assert job.running is True
    assert job.start() is False

    embedder.gate.set()
    await finish(job)
    assert embedder.asked == [["A?"]]


async def test_rate_limit_is_retried_then_succeeds():
    store, embedder = FakeStore(["A?"]), FakeEmbedder(fail_times=2)
    job = make_job(store, embedder)

    job.start()
    await finish(job)

    assert store.rows[0]["has_vector"] is True
    assert job.error is None


async def test_embedding_that_keeps_failing_is_reported_without_details_and_job_can_restart():
    store, embedder = FakeStore(["A?"]), FakeEmbedder(fail_times=99)
    job = make_job(store, embedder)

    job.start()
    await finish(job)

    assert job.error == "Không tạo được vector, xem log của advisor."
    assert store.rows[0]["has_vector"] is False
    embedder.fail_times = 0
    assert job.start() is True  # lần sau chạy lại được, không kẹt ở trạng thái lỗi
    await finish(job)
    assert store.rows[0]["has_vector"] is True and job.error is None


async def test_a_question_edited_during_the_run_does_not_get_the_old_vector():
    store, embedder = FakeStore(["A?"]), FakeEmbedder()
    embedder.gate = asyncio.Event()
    job = make_job(store, embedder)
    job.start()
    await asyncio.sleep(0.05)  # để việc nạp danh sách dòng thiếu vector chạy xong, rồi mới sửa câu hỏi
    store.rows[0]["question"] = "A đã đổi?"  # moderator sửa câu hỏi trong lúc đang tạo vector
    embedder.gate.set()
    await finish(job)

    assert store.rows[0]["has_vector"] is False  # lần sau mới tạo cho câu mới
    assert await job.count_pending() == 1


async def test_db_failure_while_saving_is_reported_not_raised():
    store, embedder = FakeStore(["A?"]), FakeEmbedder()
    store.fail_commit = True
    job = make_job(store, embedder)

    job.start()
    await finish(job)

    assert job.error is not None and job.done == 0


# --- API ---


class StubJob:
    def __init__(self, pending: int, running: bool = False) -> None:
        self._pending, self.running = pending, running
        self.done, self.error, self.started = 0, None, 0

    async def count_pending(self) -> int:
        return self._pending

    def start(self) -> bool:
        if self.running:
            return False
        self.started += 1
        self.running = True
        return True


def client_with(job) -> TestClient:
    app = create_app()  # không chạy lifespan nên không cần Ollama, DB
    app.dependency_overrides[get_embed_job] = lambda: job
    return TestClient(app)


def test_embed_missing_starts_the_job_and_answers_in_camel_case():
    job = StubJob(pending=494)

    body = client_with(job).post("/api/v1/knowledge/embed-missing").json()

    assert body == {"pending": 494, "running": True, "done": 0, "error": None, "started": True}
    assert job.started == 1


def test_embed_missing_with_nothing_to_do_does_not_start():
    job = StubJob(pending=0)

    body = client_with(job).post("/api/v1/knowledge/embed-missing").json()

    assert body["started"] is False and job.started == 0


def test_embed_missing_while_running_reports_not_started():
    job = StubJob(pending=10, running=True)

    body = client_with(job).post("/api/v1/knowledge/embed-missing").json()

    assert body["started"] is False and body["running"] is True and job.started == 0


def test_embed_status_reports_progress():
    job = StubJob(pending=3, running=True)
    job.done = 7

    body = client_with(job).get("/api/v1/knowledge/embed-status").json()

    assert body == {"pending": 3, "running": True, "done": 7, "error": None}


def test_without_a_database_the_embed_routes_answer_503():
    app = create_app()  # app.state.embed_job chưa có: giống advisor chạy không có DB

    res = TestClient(app).post("/api/v1/knowledge/embed-missing")

    assert res.status_code == 503


@pytest.mark.parametrize(
    ("path", "method"),
    [("/api/v1/knowledge/embed-missing", "post"), ("/api/v1/knowledge/embed-status", "get")],
)
def test_embed_routes_need_the_api_key_when_one_is_configured(monkeypatch, path, method):
    from app.core import config

    monkeypatch.setenv("API_KEY", "bi-mat")
    config.get_settings.cache_clear()
    try:
        res = getattr(client_with(StubJob(0)), method)(path)
    finally:
        monkeypatch.delenv("API_KEY")
        config.get_settings.cache_clear()

    assert res.status_code == 401
