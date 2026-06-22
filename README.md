- An LLM API endpoint (OpenAI-compatible or Ollama)

### Configuration

Set environment variables or create a config file:

```bash
# Environment variables — ISAAC_AGENT_API_KEY is required to use env-based config.
# ENDPOINT and MODEL fall back to defaults if omitted.
set ISAAC_AGENT_API_KEY=your-api-key
set ISAAC_AGENT_ENDPOINT=https://api.minimax.chat/v1
set ISAAC_AGENT_MODEL=abab6.5s-chat

# Or for Ollama
set ISAAC_AGENT_ENDPOINT=http://localhost:11434
set ISAAC_AGENT_MODEL=llama3
```

Or place a `config.json` in `%APPDATA%\IsaacAgent\`. The API key is
encrypted with DPAPI (CurrentUser scope) and stored in `EncryptedApiKey`:

```json
{
  "ProviderType": "OpenAICompatible",
  "Endpoint": "https://api.minimax.chat/v1",
  "Model": "abab6.5s-chat",
  "EncryptedApiKey": "<base64 DPAPI ciphertext>"
}
```

The config file is normally managed by the Settings UI; you rarely need
to edit it by hand. If you do, leave `EncryptedApiKey` empty and set the
key via environment variable or the Settings dialog.

### Build & Run
