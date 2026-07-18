# Bundled ONNX embedding assets (all-MiniLM-L6-v2)

Used by `OnnxEmbeddingProvider` when Settings leave model/vocab paths empty.

| File | Source | Tracked in Git |
|------|--------|----------------|
| `vocab.txt` | [sentence-transformers/all-MiniLM-L6-v2](https://huggingface.co/sentence-transformers/all-MiniLM-L6-v2) | Yes |
| `model.onnx` | same repo, `onnx/model.onnx` (~90 MB) | No (downloaded at build) |

At runtime, assets are preferred from `{BaseDirectory}/onnx/`. If missing
(self-contained single-file publish), they are extracted once to
`%APPDATA%/IsaacAgent/onnx/` from embedded resources.

License: Apache-2.0 (see model card on Hugging Face).

`IsaacAgent.Rag.csproj` downloads `model.onnx` during build if missing.
CI caches `src/IsaacAgent.Rag/Resources/onnx/model.onnx`.
