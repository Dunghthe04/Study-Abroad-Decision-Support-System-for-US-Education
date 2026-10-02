from fastapi import Request

from app.services.advisor import AdvisorService


def get_advisor(request: Request) -> AdvisorService:
    return request.app.state.advisor
