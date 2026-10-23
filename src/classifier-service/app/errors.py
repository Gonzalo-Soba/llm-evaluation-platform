class ProviderError(Exception):
    """The LLM provider failed (not the model's answer). Mapped to HTTP 502.

    Messages must never include the API key or request headers.
    """

    status_code = 502


class ProviderUnavailableError(ProviderError):
    """The provider is temporarily unavailable (rate limit, timeout, connection) or not configured. Mapped to HTTP 503."""

    status_code = 503
