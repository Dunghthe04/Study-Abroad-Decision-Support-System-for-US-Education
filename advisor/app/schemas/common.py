"""Lớp dùng chung cho các schema trao đổi với .NET."""

from pydantic import BaseModel, ConfigDict
from pydantic.alias_generators import to_camel


class CamelModel(BaseModel):
    """Lớp cha: JSON dùng camelCase, code Python dùng snake_case."""

    model_config = ConfigDict(alias_generator=to_camel, populate_by_name=True)
