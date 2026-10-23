from enum import StrEnum
from typing import Annotated, Literal

from pydantic import BaseModel, Field, StringConstraints


class Category(StrEnum):
    TECHNICAL_SUPPORT = "technical_support"
    BILLING = "billing"
    GENERAL_INQUIRY = "general_inquiry"
    ACCOUNT_MANAGEMENT = "account_management"
    # Only used in responses, when the classifier output is not a valid category.
    INVALID = "invalid"


class ClassifierConfig(BaseModel):
    provider: Literal["mock"]
    model: str | None = None
    prompt_version: str | None = None
    temperature: float = Field(default=0.0, ge=0.0, le=2.0)


class ClassifyRequest(BaseModel):
    text: Annotated[str, StringConstraints(strip_whitespace=True, min_length=1, max_length=2000)]
    config: ClassifierConfig


class ClassificationResult(BaseModel):
    category: Category
    valid: bool
    raw_output: str
    latency_ms: int
    input_tokens: int | None = None
    output_tokens: int | None = None
