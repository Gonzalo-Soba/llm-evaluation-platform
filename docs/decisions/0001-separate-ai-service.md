# 0001. Run AI modules in a separate Python service and compute metrics in .NET

- Status: Accepted
- Date: 2026-10-22

## Context

The platform runs AI modules over labeled datasets and evaluates their outputs. The first module classifies customer support messages using an LLM.

Most LLM provider SDKs and AI tooling are Python-first. The orchestration side (datasets, evaluation runs, metrics, API) is a good fit for ASP.NET Core.

An evaluation is only trustworthy if the system under test cannot influence how it is scored. If the module that produces predictions also computed its own metrics, a bug or change in that module could silently distort the results.

## Decision

- AI modules run in a separate Python 3.12 + FastAPI service (`src/classifier-service`). It only receives text and returns a classification. It has no knowledge of datasets, labels, or metrics.
- The ASP.NET Core API (`src/api`) orchestrates evaluation runs: it loads datasets, calls the classifier service over HTTP, stores runs as JSON files, and computes all metrics.
- The classifier service provides a deterministic mock classifier alongside the LLM classifier, so the pipeline can be run and tested without calling an LLM provider.

## Consequences

- Clear separation between the system under test and the evaluator. Modules can be swapped or compared without touching metric code.
- Each side uses the ecosystem best suited to it: Python for AI integrations, .NET for orchestration and evaluation.
- Two services must be run, configured, and tested (pytest and xUnit), and they communicate through an HTTP contract that must be kept in sync.
- Network calls add latency and failure modes; the API must handle classifier errors and invalid outputs explicitly, counting them as errors rather than dropping them.
