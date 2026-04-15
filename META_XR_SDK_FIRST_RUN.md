# Meta XR SDK — primeira abertura GUI (one-time, ~5 min)

Este passo é necessário **uma vez** no Windows. Depois disso, todo build é headless via `unity-ssh.sh`.

## Pré-requisito

`ProjectBootstrap.Run` já foi executado e o `Packages/manifest.json` já tem `com.meta.xr.sdk.all`.

## Passos

1. Abra **Unity Hub** no Windows
2. Clique em **Open** e selecione `C:\Dev\vr-voice-claude-unity`
3. Aguarde o Editor abrir (~30-60s — Meta XR SDK é ~500MB)
4. Quando aparecer o popup **"Importing Package"** do Meta XR SDK, clique **Import All**
5. Aguarde o **Project Setup Tool** abrir automaticamente (popup com lista de "Outstanding Issues")
6. Clique **Fix All** no topo
7. Se aparecer popup adicional pedindo reload da scene ou aplicação de mudanças, aceite (**Apply All**)
8. **File > Save** (Ctrl+S) — salva a `Main.unity` modificada
9. Feche o Editor (File > Exit)

## Validação

Depois disso, rode no Linux:

```bash
./apps/vr-voice-claude/tools/unity-ssh.sh build
./apps/vr-voice-claude/tools/unity-ssh.sh deploy
```

Espera-se: APK gerado, deployado, e ao abrir no Quest 3 você vê o quarto real (passthrough), não o Home space do Meta.

## Se algo der errado

- **"Failed to fetch Meta XR SDK"**: confira internet do Windows e retry o `Open` no Hub
- **Project Setup Tool não abre**: menu **Edit > Project Settings > Meta XR > Project Setup Tool**
- **Editor crash durante import**: confira se você tem ≥4GB livres em `C:\` (Meta XR cache fica em `C:\Users\guibr\AppData\Local\Unity`)
