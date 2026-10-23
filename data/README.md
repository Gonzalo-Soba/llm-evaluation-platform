# Datasets

Labeled datasets used to evaluate the AI modules.

## support-messages-v0.jsonl

Customer support messages in Spanish (Rioplatense, with voseo), labeled for the support message classifier. This is a draft v0 dataset.

- 40 messages: 10 per category.
- 29 `clear` and 11 `ambiguous`.
- Stratified split: for each category, 5 messages in `dev` and 5 in `test`, with ambiguous messages in both splits.

Labels follow [docs/labeling-guidelines.md](../docs/labeling-guidelines.md).

Clear messages were intentionally written to avoid obvious category keywords, so the mock classifier's accuracy on this dataset is not representative of how a keyword classifier would perform on real traffic.

### Format

[JSON Lines](https://jsonlines.org/), UTF-8: one JSON object per line.

```json
{"id": "msg-001", "text": "Quiero cambiar el correo con el que me registré porque ese ya no lo uso más.", "label": "account_management", "split": "test", "difficulty": "clear"}
```

### Fields

| Field | Type | Description |
| --- | --- | --- |
| `id` | string | Unique identifier, `msg-001` to `msg-040`. IDs are not grouped by category. |
| `text` | string | The customer message, as written: informal tone, typos and missing accents are intentional. |
| `label` | string | Expected category: `technical_support`, `billing`, `general_inquiry` or `account_management`. |
| `split` | string | `dev` (for iterating on prompts and models) or `test` (for final evaluation only). |
| `difficulty` | string | `clear` (one obvious category) or `ambiguous` (resolved with a boundary rule from the guidelines). |

### Provenance

Messages drafted with AI assistance and manually reviewed and relabeled by the author following the labeling guidelines.

The messages are fictional and contain no real personal data.
