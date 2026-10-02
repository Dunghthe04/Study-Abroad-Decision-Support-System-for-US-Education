from typing import Literal

from pydantic import BaseModel, ConfigDict, Field

from app.core.study_levels import StudyLevel


class ChatMessage(BaseModel):
    role: Literal["system", "user", "assistant"]
    content: str = Field(min_length=1, max_length=8000)


class ChatRequest(BaseModel):
    model_config = ConfigDict(populate_by_name=True)

    messages: list[ChatMessage] = Field(min_length=1)
    session_id: str | None = Field(default=None, alias="sessionId")
    # Level the user is asking about (from profile or UI). None = not chosen -> search all levels.
    study_level: StudyLevel | None = Field(default=None, alias="studyLevel")


class Citation(BaseModel):
    title: str
    url: str | None = None
    snippet: str | None = None


class ChatResponse(BaseModel):
    answer: str
    citations: list[Citation] = []
    disclaimer: str | None = None
