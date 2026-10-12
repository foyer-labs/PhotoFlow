# PhotoFlow — Registro di avanzamento

Memoria del progetto tra una sessione e l'altra. Aggiornato a fine sessione.

## Stato

| Voce | Valore |
|---|---|
| Fase corrente | 0 — Setup e laboratorio (non ancora avviata: in attesa di conferma) |
| Branch | nessuno (repository git inizializzato localmente, nessun commit) |
| Remoto | https://github.com/foyer-labs/PhotoFlow.git (pubblico, vuoto) |
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
| exiftool | non installato | Serve al benchmark (anteprime RAW) |
| `PHOTOFLOW_TESTDATA` | `E:\AI\PhotoFlowTestData` | Variabile utente impostata il 2026-10-12 (i processi già aperti non la vedono finché non vengono riavviati) |

## Attività completate

### Sessione 1 — 2026-10-12
- Lettura di CLAUDE.md, docs/spec.md (intero), panoramica delle tavole in docs/design/.
- Creato questo registro.
- `git init` locale con identità Foyer Labs e remoto `origin`; nessun commit.
- Installato .NET SDK 10.0.401 (winget).
- Risolti con l'utente i punti A1-A11 (vedi decisioni); spec.md aggiornata in §2 (D21), §3, §4, §5, §6, §7, §8, §10 (I1), §13, §14, §17 (P11, backlog).

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
| 2026-10-12 | A1 — Tavole rinominate come in §12bis (`PhotoFlow_*_dc.html`); specifica invariata |
| 2026-10-12 | A10 — Ricostruite le celle troncate di §8 (Volti: `Persone\|Nome`; OCR: `Motorsport\|Numero N`, `Motorsport\|Pilota\|Nome`, `Motorsport\|Team\|Nome`) e §17 (Album → D6), con pipe protetti |

## Problemi aperti

Nessuno.

## Verifiche manuali in attesa (per l'utente)

Nessuna.

## Prossimo passo

Piano della fase 0 scritto in `docs/plans/fase-0.md`, in attesa di approvazione → sviluppo su `phase/0-setup`.
