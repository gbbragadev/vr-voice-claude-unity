# vr-voice-claude-unity

Unity 6 LTS project for the Quest 3 native voice-AI app (Phase 2 of vr-voice-claude).

Companion to the backend at `https://voice-api.gbbragadev.com` (see parent monorepo `vr-business/apps/vr-voice-claude/server/`).

## Folder ownership

- **Linux side (srv01-bc) edits:** `Assets/Scripts/`, `Assets/Editor/`, `Assets/Plugins/Android/`, `tools/`, `.gitignore`, `README.md`
- **Windows side (Unity editor) edits:** `Assets/Scenes/`, `Assets/Resources/secrets.txt` (gitignored), `Packages/manifest.json`, `ProjectSettings/`

Never commit cross-owner files — subdirs are disjoint, so merges never collide.

## Quick reference

- Wake word: Porcupine built-in `jarvis`
- Backend: `https://voice-api.gbbragadev.com` (FastAPI, Whisper large-v3, Claude Sonnet 4.6, ElevenLabs Fernanda pt-BR)
- Target: Quest 3 / Quest 3S, Android 10+, ARM64, Vulkan
- Package: `com.gbbraga.vrvoiceclaude`
