# Report di fine fase 0 — Setup e laboratorio

Data: 2026-10-12 · Branch: `phase/0-setup` · Piano: [`docs/plans/fase-0.md`](../plans/fase-0.md)

## Checklist dei criteri di accettazione

| # | Criterio | Esito | Evidenza |
|---|---|---|---|
| 1 | Soluzione compilabile con tutti i progetti e le interfacce di `Core` | ✅ Superato | `dotnet build` 0 avvisi / 0 errori (avvisi = errori); CI verde su `windows-latest` e `ubuntu-latest`; `ArchitectureTests` (confini dei progetti, Core senza dipendenze) |
| 2 | Benchmark Python eseguibile su una cartella di foto, con report dei tempi per fase | ✅ Superato (script) · ⏳ misura ufficiale | `tools/lab/benchmark.py` eseguito su 8 immagini COCO: report in `PHOTOFLOW_TESTDATA/lab/benchmark/20261012-181304/`. **Misura sulle foto reali (NEF Z5 II + JPG) da eseguire**: servono le foto dell'utente |
| 3 | D-FINE esportato in ONNX con test di parità superato | ✅ Export · ✅ livello rilevamenti · ⚠️ livello tensori (tolleranza logit da confermare) | `export_dfine.py` (S e M, opset 17); `DFineParityTests`: rilevamenti 73/73 abbinati per S e M, IoU min 1,000, Δ punteggio < 1e-4; tensori: riquadri ≤ 8,8e-5 (≤ 1e-4 ✅), logit fino a 8,5e-3 (> 1e-3 provvisorio ❌). Vedi proposta T1 |
| 4 | Hook pre-commit dimostrato con un tentativo di commit di un'immagine | ✅ Superato | Tentativo reale nel repository: `DSC_0001.jpg` (estensione) e `appunti.txt` (firma JPEG) bloccati, exit code 1. `PreCommitHookTests` (8 casi, CI Windows + Linux); job CI `hygiene` su tutti i file tracciati |

## Cosa è stato fatto

| Area | Contenuto |
|---|---|
| Repository | `.gitignore`, `.gitattributes`, `LICENSE` (Apache-2.0 canonica), `NOTICE`, `README`, `global.json` (SDK 10.0.401), gestione centralizzata dei pacchetti, SPDX imposto in build (IDE0073) |
| Core | Interfacce di §3 (+ `IInferenceSession`, `IObjectDetector`, `IImageOperations`), tipi di supporto, entità di §9, `DefaultAppPaths` |
| Imaging | Decodifica SkiaSharp; ricampionatore **bit-exact con Pillow** (test con valori di riferimento) |
| Inference | ONNX Runtime 1.31 CPU, tensori senza copia, creazione sessione asincrona |
| Analysis | Adattatore D-FINE (contratto HF) |
| App | Guscio Avalonia 12.1.4: token §12bis Dark/Light, IBM Plex incorporati, barra titolo estesa, navigazione 184/48 (auto a 1280), barra di stato e `KeyBindings` dalla stessa tabella dei comandi, IT/EN, DI, log Serilog via `IAppPaths` |
| Test | 119 test (111 in CI + 8 di parità locali, saltati senza `PHOTOFLOW_TESTDATA`) |
| CI | GitHub Actions: controllo immagini + build/test Windows e Linux, azioni fissate per SHA |
| Laboratorio | uv, torch 2.14.1 cu130, transformers 4.55.4, onnxruntime-gpu 1.31: `fetch_models`, `export_dfine`, `parity_refs`, `benchmark`, `verify_ramplus` |
| Manifest | 7 modelli con URL fissati, SHA-256, dimensioni, licenze verificate alla fonte |

## Misure preliminari (8 immagini COCO, non rappresentative)

| Fase | CUDA (RTX 3090) ms/foto | CPU (24 thread) ms/foto |
|---|---|---|
| D-FINE S / M | 12,7 / 15,7 | 103 / 182 |
| Volti buffalo_l (1,9 volti/foto) | 33,4 | 134 |
| CLIP immagine (quantizzato) | 20,7 | 17,4 |
| RAM++ int8 | 528 | 614 |
| Stima 1.000 foto (anteprima + rilevamento + volti + CLIP + RAM++) | 10,0 min (spec: 6-11) | 14,5 min (spec: 22-50, 8 core) |

L'anteprima (3,7 ms) è misurata su JPEG piccoli: sui NEF da 24 MP sarà molto più alta.

## Verifica RAM++ (§13 "verifica RAM++", 8 immagini COCO)

ONNX int8 della community vs RAM++ originale PyTorch, stesso tensore d'ingresso: insiemi identici 1/8, Jaccard medio 0,895, max |Δ probabilità| 0,078; la int8 **perde** 1-2 tag per immagine, non ne aggiunge. Liste di tag e soglie coerenti con l'originale. **Non soddisfa** il criterio di parità di §14 ("stesso insieme alle soglie ufficiali"): vedi N6.

## Proposte di modifica alla specifica (da approvare)

| # | Proposta | Motivo |
|---|---|---|
| T1 | §14 parità rilevatori, livello tensori: tolleranza su **punteggi (sigmoide) ≤ 1e-3** al posto dei logit ≤ 1e-3; riquadri ≤ 2e-4 | Lo scarto osservato è tra ONNX Runtime e PyTorch (C# = Python ORT); logit fino a 8,5e-3 solo su query a punteggio ~0; punteggi ≤ 1,7e-4; riquadri ≤ 8,8e-5, margine stretto su 1e-4 |
| L1 | §15: Velopack **MIT** (1.2.161), PanAndZoom **MIT** con ID pacchetto `PanAndZoom` ≥ 12.1.1, HdbscanSharp **MIT** (inattivo da 05/2025), RapidOcrNet codice **Apache-2.0** (4.2.0), Magick.NET 15.0.0 Apache-2.0 con libheif/libde265 **LGPL collegate staticamente** in Magick.Native, DirectML e Windows ML runtime **proprietari Microsoft ridistribuibili a condizioni** | Verifiche 0.6 |
| L2 | §15: CLIP — nota sulla scheda del modello (uso "deployed" fuori ambito) | N1 |
| R1 | §16 nuovo rischio: **NEF High Efficiency non decodificabili** da nessuna LibRaw pubblica (0.22.2); Z5 II non nella lista camere; LibRaw annuncia il decoder HE "nel prossimo snapshot, autunno 2026". Fallback: anteprima incorporata via exiftool, stato "RAW HE: solo anteprima" | Conferma il rischio R3 per la parte di decodifica |
| R2 | §16: **una sola distribuzione di ONNX Runtime per processo**: DirectML NuGet fermo a 1.24.4, Windows ML con ORT 1.27.1, RapidOcrNet richiede ORT ≥ 1.29 | Conflitto da risolvere prima della fase 1 (selezione hardware) e della fase 4 (OCR) |
| R3 | §5/§11: valutare **Windows ML** al posto della variante DirectML (DirectML "sustained engineering"); provider di vendor (TensorRT-RTX ecc.) solo da Windows 11 24H2 | N2; decisione per la fase 1 |
| R4 | §7/§16: HdbscanSharp è O(n²): non adatto a ~100.000 volti; serve HDBSCAN su grafo k-NN dietro interfaccia | Fase 2 |
| R5 | §3: binding LibRaw: P/Invoke sottile sui binari ufficiali 0.22.x (Sdcb.LibRaw fermo a 0.21.1, senza correzioni di sicurezza 0.22.1) | Fase 1 |
| N5 | §5: RAM++ int8 e CLIP quantizzato non accelerano su GPU (quantizzazione dinamica eseguita in CPU): su GPU usare fp32/fp16 | Benchmark |
| N6 | §5/§14: la RAM++ int8 della community non supera la parità: in fase 3 usare fp32 (export `CannotFindObject/RAM_ONNX`) o un export nostro verificato | verify_ramplus |

## Verifiche manuali per l'utente

1. **App**: `dotnet run --project src/PhotoFlow.App` — controllare barra del titolo con pulsanti di sistema nativi, trascinamento e doppio clic per massimizzare, navigazione compressa sotto 1280 px e pulsante di compressione, Ctrl 1-7 / Ctrl K / Ctrl , ; tema chiaro con `-- --theme light`.
2. **Foto di test** in `E:\AI\PhotoFlowTestData\photos\`: 30-50 varie per la parità, ~200 per il benchmark (NEF Z5 II + JPG). Poi rieseguo `parity_refs.py`, `DFineParityTests`, `benchmark.py`, `verify_ramplus.py` sul set reale.
3. **Hook su un altro clone**: dopo il clone, `git config core.hooksPath .githooks`.
