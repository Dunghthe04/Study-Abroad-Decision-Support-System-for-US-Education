from fastapi.testclient import TestClient

from app.main import create_app
from app.services.centers import CenterFinder
from app.services.ollama import OllamaClient


def test_startup_creates_llm_client():
    # Có "with" thì TestClient mới chạy lifespan (không gọi Ollama, chỉ tạo client)
    with TestClient(create_app()) as client:
        assert isinstance(client.app.state.llm, OllamaClient)


def test_startup_registers_skills_from_build_handlers(monkeypatch):
    class FakeSkill:
        async def run(self, state, message):
            raise NotImplementedError

    seen = {}

    def fake_build_handlers(llm, model, knowledge=None, min_score=0.72, centers=None):
        seen["llm"], seen["model"], seen["knowledge"], seen["centers"] = llm, model, knowledge, centers
        return {"skill_ho_so": FakeSkill()}

    monkeypatch.setattr("app.main.build_handlers", fake_build_handlers)

    with TestClient(create_app()) as client:
        handlers = (
            client.app.state.chat_flow._handlers
        )  # kiểm tra nội bộ để chắc main.py đã nối danh sách đăng ký
        assert "skill_ho_so" in handlers  # skill đăng ký được gắn vào ChatFlow
        assert {"ngoai_le", "ket_thuc"} <= set(handlers)  # skill tạm vẫn còn
        assert seen["llm"] is client.app.state.llm  # skill nhận đúng llm dùng chung lúc khởi động
        assert seen["knowledge"] is None  # không có DATABASE_URL thì không có bộ tìm kiến thức
        assert isinstance(
            seen["centers"], CenterFinder
        )  # BACKEND_BASE_URL mặc định có nên bật gợi ý trung tâm (B6)


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
