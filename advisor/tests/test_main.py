from fastapi.testclient import TestClient

from app.main import create_app
from app.services.activity_reader import ActivityReader
from app.services.recommender import Recommender


def test_startup_creates_recommender():
    # Có "with" thì TestClient mới chạy lifespan (không gọi Ollama, chỉ tạo client)
    with TestClient(create_app()) as client:
        assert isinstance(client.app.state.recommender, Recommender)
        assert isinstance(client.app.state.activity_reader, ActivityReader)
