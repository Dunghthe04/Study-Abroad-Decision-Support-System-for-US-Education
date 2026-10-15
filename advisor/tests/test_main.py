from fastapi.testclient import TestClient

from app.main import create_app
from app.services.ollama import OllamaClient


def test_startup_creates_llm_client():
    # Có "with" thì TestClient mới chạy lifespan (không gọi Ollama, chỉ tạo client)
    with TestClient(create_app()) as client:
        assert isinstance(client.app.state.llm, OllamaClient)
