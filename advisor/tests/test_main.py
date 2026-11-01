from fastapi.testclient import TestClient

from app.main import create_app
from app.schemas.slm import INTENTS
from app.services.centers import CenterFinder
from app.services.ollama import OllamaClient
from app.services.slm_classifier import OllamaSlm


def test_startup_creates_llm_client():
    # Có "with" thì TestClient mới chạy lifespan (không gọi Ollama, chỉ tạo client)
    with TestClient(create_app()) as client:
        assert isinstance(client.app.state.llm, OllamaClient)


def test_startup_builds_the_chat_flow_from_the_slm_and_the_branches(monkeypatch):
    class FakeSkill:
        async def run(self, state, message):
            raise NotImplementedError

    seen = {}

    def fake_build_handlers(llm, model, knowledge=None, min_score=0.72, centers=None):
        seen["llm"], seen["model"], seen["knowledge"], seen["centers"] = llm, model, knowledge, centers
        return {"skill_ho_so": FakeSkill()}

    monkeypatch.setattr("app.main.build_handlers", fake_build_handlers)

    with TestClient(create_app()) as client:
        flow = client.app.state.chat_flow  # kiểm tra nội bộ để chắc main.py đã nối SLM, nhánh và skill
        assert set(flow._branches) == set(INTENTS)  # đủ 6 nhánh theo loại câu hỏi SLM
        assert isinstance(flow._slm, OllamaSlm)
        assert seen["llm"] is client.app.state.llm  # skill nhận đúng llm dùng chung lúc khởi động
        assert seen["knowledge"] is None  # không có DATABASE_URL thì không có bộ tìm kiến thức
        # BACKEND_BASE_URL mặc định có nên bật gợi ý trung tâm
        assert isinstance(seen["centers"], CenterFinder)


def test_startup_has_no_db_pool_when_no_database_is_configured():
    with TestClient(create_app()) as client:
        assert (
            client.app.state.db_pool is None
        )  # DATABASE_URL trống (conftest) nên chat chạy không kho kiến thức


def test_startup_opens_and_closes_the_pool_it_gets(monkeypatch):
    closed = []

    class FakePool:
        def close(self):
            closed.append(True)

    def fake_open_pool(database_url):
        return FakePool()

    monkeypatch.setattr("app.main.open_pool", fake_open_pool)

    with TestClient(create_app()) as client:
        assert isinstance(client.app.state.db_pool, FakePool)

    assert closed == [True]  # tắt dịch vụ thì đóng hồ chứa kết nối
