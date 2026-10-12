# PhotoFlow — Registro di avanzamento

Memoria del progetto tra una sessione e l'altra. Aggiornato a fine sessione.

## Stato

| Voce | Valore |
|---|---|
| Fase corrente | 1 — Import, analisi base, XMP, catalogo: **piano approvato, sviluppo in corso** ([docs/plans/fase-1.md](plans/fase-1.md)). Fase 0 approvata e unita in `main` (PR #1) |
| Branch | `phase/1-import` |
| Remoto | https://github.com/foyer-labs/PhotoFlow.git (pubblico) |
| Autore dei commit | Foyer Labs <foyerlabs@gmail.com> (config locale del repository) |
| Ultimo aggiornamento | 2026-10-12 |

## Ambiente di sviluppo rilevato (2026-10-12)

| Strumento | Versione | Nota |
|---|---|---|
| .NET SDK | 10.0.401 (+ 9.0.318) | SDK 10 installato con winget il 2026-10-12 |
| Python | 3.12.9 | |
| uv | 0.11.28 | |
| git / gh | 2.51 / 2.101 | gh autenticato come foyer-labs |
| GPU | NVIDIA RTX 3090, 24 GB | Driver 610.88 (CUDA 12/13) |
| exiftool | 13.59 (solo laboratorio) | In `PHOTOFLOW_TESTDATA/tools/exiftool-13.59`, SHA-256 verificato |
| `PHOTOFLOW_TESTDATA` | `E:\AI\PhotoFlowTestData` | Variabile utente impostata il 2026-10-12 (i processi già aperti non la vedono finché non vengono riavviati) |

## Attività completate

### Sessione 1 — 2026-10-12
- Lettura di CLAUDE.md, docs/spec.md (intero), panoramica delle tavole in docs/design/.
- Creato questo registro.
- `git init` locale con identità Foyer Labs e remoto `origin`; nessun commit.
- Installato .NET SDK 10.0.401 (winget).
- Risolti con l'utente i punti A1-A11 (vedi decisioni); spec.md aggiornata in §2 (D21), §3, §4, §5, §6, §7, §8, §10 (I1), §13, §14, §17 (P11, backlog).
- Fase 0 sviluppata su phase/0-setup (7 commit): soluzione e Core, guscio Avalonia, CI, hook, manifest, laboratorio, parità D-FINE, benchmark, verifica RAM++, verifiche 0.6. Dettagli ed evidenze in [docs/reports/fase-0.md](reports/fase-0.md).
- Modelli scaricati e verificati in PHOTOFLOW_TESTDATA/models (buffalo_l con accettazione della licenza per uso di laboratorio).
- Immagini di sviluppo: 8 COCO val2017 in PHOTOFLOW_TESTDATA/photos-dev (solo per provare gli script).

## Decisioni prese con l'utente

| Data | Decisione |
|---|---|
| 2026-10-12 | Repository: github.com/foyer-labs/PhotoFlow; autore dei commit foyer-labs (Foyer Labs <foyerlabs@gmail.com>) |
| 2026-10-12 | A2 — .NET 10 LTS (`net10.0`), SDK fissato con `global.json` (`rollForward: latestFeature`) |
| 2026-10-12 | A3 — `IInferenceSession` + `NamedTensor` + `ExecutionTarget` in Core; solo `.Inference` dipende da ONNX Runtime; adattatori nei progetti di dominio con interfacce per funzione. spec.md §3 aggiornata |
| 2026-10-12 | A4 — D-FINE: manifest su `onnx-community/dfine_{s,m}_obj2coco-ONNX` (fp32), fissati per commit + SHA-256; `export_dfine.py` da `ustc-community` per la parità; riserva: nostro export su GitHub Releases (campo `mirrors`). spec.md §5 aggiornata |
| 2026-10-12 | A7 — Hook in `.githooks/pre-commit` (sh, senza dipendenze) attivato con `core.hooksPath`; stesso controllo in CI; rilevamento per estensione + firma binaria; ammessi `tests/data/public/` (ogni file in `LICENSES.md`) e i percorsi esatti in `.githooks/allowed-images.txt` (oggi solo l'icona dell'app); icone UI come geometrie XAML |
| 2026-10-12 | A8 — Parità dei rilevatori su due livelli (tensori con ingresso identico + rilevamenti abbinati per classe, IoU ≥ 0,95, \|Δ punteggio\| ≤ 0,02, tolleranza ±0,05 dalla soglia). spec.md §14 aggiornata |
| 2026-10-12 | A5 — Con YuNet/SFace gli occhi sono sempre "sconosciuto"; limite dichiarato nel primo avvio (P11). Nessun modello occhi aggiuntivo. spec.md §6, §7, P11 aggiornate. Nota: le fonti indicano YuNet MIT (non Apache 2.0): da confermare in 0.6 |
| 2026-10-12 | A6 — Librerie da archivio esistente: default `SidecarOnly`, passaggio a `EmbeddedForNonRaw` solo su conferma con numero di file e `_original`. D21, §4, I1 (§10) aggiornate; unione sidecar↔incorporati nel backlog (fase 5). CLAUDE.md I1 già coerente ("scheda SD o cartella sorgente") |
| 2026-10-12 | A9 — Ripresa: `JobItems` e ripresa dell'Import in fase 1 (Home 3c); job d'archivio in background, parallelo, pausa in 1b. spec.md §13 aggiornata. Da proporre nel piano fase 1: copia su `.pftmp` + rinomina dopo verifica, sovrascrittura del temporaneo alla ripresa (nessuna eliminazione, I4) |
| 2026-10-12 | A11 — `PROMPT_Claude_Code.md` resta solo locale: da inserire in `.gitignore` (fase 0.1), mai committato |
| 2026-10-12 | Piano fase 0 approvato (`docs/plans/fase-0.md`): Avalonia 12.1.4, xUnit v3 3.2.2 fissato, download del laboratorio (~7 GB) e push su GitHub autorizzati. spec §3 (+`IObjectDetector`, `IImageOperations`) e §15 (YuNet MIT) aggiornate |
| 2026-10-12 | `IInferenceProvider.CreateSessionAsync(model, target, ct)` asincrono (conformità a CLAUDE.md §4). spec §3 aggiornata |
| 2026-10-12 | Verifiche 0.6 applicate a spec.md: §15 licenze (Velopack, PanAndZoom, HdbscanSharp, RapidOcrNet, Magick.NET, DirectML/Windows ML, CLIP, RAM++), §16 rischi R15-R20, note di laboratorio in §5, §17 verifiche tecniche aggiornate. T1 (tolleranza parità tensori) rinviata: si decide dopo la misura sulle foto reali |
| 2026-10-12 | Nessun NEF della Z5 II disponibile fino a dopo l'implementazione: la "primissima attività della fase 1" (§13, verifica anteprima/HE/punto AF sui NEF Z5 II) va rinviata; proposta da fare nel piano della fase 1. Set di test reale: 240 file in `PHOTOFLOW_TESTDATA/photos` (178 NEF Nikon D40 lossy + D5100 lossless, 52 JPG di cui 49 Samsung SM-A500FU, 11 .xmp preesistenti, mai toccati) |
| 2026-10-12 | T1 — Parità livello tensori: query abbinate con assegnazione ottima prima del confronto; punteggi (sigmoide) ≤ 5e-3, riquadri ≤ 5e-4 (misurati 2,7e-3 e 1,5e-4 su 40 foto reali). spec.md §14 e `DFineParityTests` aggiornati |
| 2026-10-12 | Report fase 0 approvato; PR #1 unita in `main` |
| 2026-10-12 | F1-1..F1-7 — decisioni del piano della fase 1 (vedi `docs/plans/fase-1.md`): rinvio verifica Z5 II; CUDA + CPU (DirectML escluso per risultati errati); Magick.NET per RAW di riserva; analisi in C# con parità OpenCV; exiftool scaricato in build; layout completo con funzioni future disattivate; set qualità a cartelle. spec §3, §4, §5, §11, §13, §14, §15, §16, §17 e CLAUDE.md §3 aggiornati |
| 2026-10-12 | A1 — Tavole rinominate come in §12bis (`PhotoFlow_*_dc.html`); specifica invariata |
| 2026-10-12 | A10 — Ricostruite le celle troncate di §8 (Volti: `Persone\|Nome`; OCR: `Motorsport\|Numero N`, `Motorsport\|Pilota\|Nome`, `Motorsport\|Team\|Nome`) e §17 (Album → D6), con pipe protetti |

## Problemi aperti

| # | Tema | Sintesi |
|---|---|---|
| N5-N6 | Modelli quantizzati | RAM++ int8/CLIP quantizzato non accelerano su GPU; RAM++ int8 non supera la parità (52 JPG reali: 12/52 insiemi identici, Jaccard 0,866) — fase 3 |
| Z5 II | NEF Z5 II non disponibili | Prima attività della fase 1 (§13) da rinviare a dopo l'implementazione: proposta nel piano della fase 1 |
| N1-N4 | Note per fasi successive | CLIP scheda modello (fase 2), DirectML (0.6→R3), GridSample fp16 CUDA (fase 1), YuNet ingresso fisso (fase 1) |

## Verifiche manuali in attesa (per l'utente)

0. Preparare entro la tappa 1.10 il set etichettato: `E:\AI\PhotoFlowTestData\quality\buone` (≥ 100), `mosse` (30-50), `occhi_chiusi` (30-50).

1. ~~Avviare l'app~~ — fatto dall'utente il 2026-10-12: si apre e mostra la navigazione (comportamento atteso per la fase 0). Da controllare ancora, se possibile: pulsanti di sistema, trascinamento e doppio clic sulla barra del titolo, `-- --theme light`.
2. ~~Foto di test~~ — fornite il 2026-10-12 (240 file).

## Prossimo passo

Tappa 1.0 (test delle invarianti) → 1.1 catalogo → … secondo `docs/plans/fase-1.md` → pull request `phase/0-setup` → `main` → piano della fase 1.
