# PhotoFlow — Specifica di progetto

Oct 12, 2026 · @luca

## 1. Scopo e ambito

PhotoFlow (nome provvisorio) è un applicativo desktop C#/Avalonia che importa, seleziona, tagga e rende ricercabili foto con modelli AI locali, senza cloud. Uso personale, codice destinato alla pubblicazione open source (Apache 2.0).

### Obiettivi

| # | Obiettivo |
| --- | --- |
| O1 | Import da scheda SD con copia verificata, rinomina e backup su più destinazioni |
| O2 | Culling automatico: scarti tecnici, scelta della migliore in ogni raffica |
| O3 | Tagging automatico in italiano e inglese, con keyword gerarchiche |
| O4 | Riconoscimento volti con raggruppamento "Persona N" e nomi assegnati dall'utente |
| O5 | Numeri di gara e piloti in ambito motorsport (OCR + elenco iscritti) |
| O6 | Analisi in background di archivi esistenti (stima 50.000-60.000 foto, formati misti) |
| O7 | Consultazione: ricerca per tag, persona, data, evento, EXIF, linguaggio naturale, duplicati |
| O8 | Interoperabilità tramite XMP standard, senza formati proprietari chiusi |

### Fuori ambito

| Elemento | Motivo |
| --- | --- |
| Sviluppo RAW e fotoritocco | Resta a Photoshop o ad altro software |
| Analisi dei video | I video vengono copiati, non analizzati |
| Cloud, sincronizzazione online, telemetria | Tutto locale |
| Mappa GPS | Rinviato |
| Album, modalità portatile, panorami automatici | Backlog post-stabilità (sezione 17) |

### Principi guida

| Principio | Significato |
| --- | --- |
| Nessuna perdita | Nessuna cancellazione dalla sorgente, RAW mai modificati, nessuna eliminazione automatica |
| Le azioni dell'utente prevalgono | Decisioni e tag manuali, ovunque fatti, vincono sempre su quelli automatici |
| Il catalogo è la fonte primaria, XMP lo strato di interoperabilità | Dati ricchi nel catalogo, sempre esportati anche in formato standard |
| Generico, niente cablato | Hardware, percorsi, modelli, profili sono configurazione |
| Incertezza → revisione | Il sistema non decide da solo quando non è sicuro |
| Controllo delle modifiche | Ogni modifica a decisioni già prese passa da questo documento prima del codice |

## 2. Registro delle decisioni

Tutte le decisioni sotto sono confermate; ogni modifica richiede una valutazione d'impatto e l'aggiornamento di questo documento.

| # | Tema | Decisione | Motivazione |
| --- | --- | --- | --- |
| D1 | Linguaggio | C#/.NET puro con ONNX Runtime; Python solo come strumento di laboratorio (export, riferimenti di parità, benchmark) | Tutti i modelli necessari esistono in ONNX; distribuzione semplice; rilevamento hardware nativo |
| D2 | Interfaccia | Avalonia | Multipiattaforma (Windows prioritario, Mac/Linux possibili) |
| D3 | Ambito funzionale | Elaborazione + consultazione (opzione B) | Senza consultazione il valore resta chiuso nel catalogo; ricerca in linguaggio naturale |
| D4 | Sorgenti | SD, cartelle e archivi esistenti; formati RAW, JPG, PNG, WebP, TIFF, HEIC | Uso anche su archivi non NEF |
| D5 | Catalogo | Posizione configurabile: locale (default) o rete con protezioni; esportazione/importazione | Flessibilità senza rischi di corruzione non gestiti |
| D6 | Album | Rinviati al backlog | Costo basso se aggiunti dopo |
| D7 | Culling | Logica conservativa: scarto solo su segnale certo, incertezza → "da rivedere" | Uno scarto sbagliato costa più di uno mancato |
| D8 | Scelta del profilo | Manuale + suggerimento (C), con modalità automatica attivabile per job | Prevedibilità con aiuto nei casi misti |
| D9 | Lingua keyword XMP | Italiano e inglese (piatte); gerarchiche solo in italiano | Compatibilità e ricerca in entrambe le lingue |
| D10 | Lingua interfaccia | Italiano e inglese, configurabile (default: lingua di sistema) | — |
| D11 | Volti | Previsti da subito, configurabili (`Off` / `DetectOnly` / `Recognize`) | Servono già per gli occhi chiusi |
| D12 | Protezione volti | Cifratura delle sole impronte (C), chiave legata all'account o a password | Protegge il dato biometrico; perdita della chiave = ricalcolo, non perdita di dati |
| D13 | Sincronizzazione XMP | Modifiche esterne importate come manuali; tag manuali mai rimossi | Le azioni dell'utente prevalgono |
| D14 | Rigenerazione | Rianalisi selettiva con anteprima delle differenze e ritorno indietro | Aggiornare i modelli senza perdere il lavoro manuale |
| D15 | Rilevatore oggetti | D-FINE (S su CPU, M su GPU), RF-DETR come alternativa; YOLO Ultralytics escluso | Licenza Apache 2.0; nessun vincolo AGPL |
| D16 | Selezione modelli | Modello e taglia selezionabili manualmente per ogni funzione | Controllo e sperimentazione |
| D17 | Licenza progetto | Apache 2.0 | Coerente con le dipendenze, clausola brevetti |
| D18 | Equipaggi motorsport | Solo numero di vettura (+ team) nell'XMP; equipaggio associato nel catalogo | Nessun nome scritto se non certo |
| D19 | Modalità portatile | Backlog post-stabilità, con percorsi centralizzati in `IAppPaths` fin da subito | Riduce i tempi di build durante lo sviluppo |
| D20 | Avvio prima del set etichettato | Modalità prudente: scarti scritti solo come "da rivedere" | Culling non ancora validato |
| D21 | Politica XMP predefinita | EmbeddedForNonRaw per le librerie create da un import (incorporato in JPG/TIFF/PNG, sidecar per i RAW); SidecarOnly per le librerie create da un archivio esistente, con passaggio a EmbeddedForNonRaw solo su conferma esplicita che indica il numero di file coinvolti (aggiornata il 12 ott 2026) | Il visualizzatore di Windows ignora i sidecar; Photoshop/Camera Raw legge gli incorporati nei JPG e i sidecar nei RAW; gli originali di un archivio non si riscrivono senza consenso |
| D22 | Piano di sviluppo | Fasi 0-5 (sezione 13), con fase 0 di setup e laboratorio e fase 5 per rigenerazione completa, didascalie, esportazione catalogo e sincronizzazione esterna | Distribuisce tutte le funzioni definite in fasi chiudibili e utilizzabili |
| D23 | Passaggio a modalità normale | Si sblocca solo quando tutte e tre le metriche del culling sono entro obiettivo (intervallo di confidenza incluso) | Confermata in Design: evita di passare a rating -1 con metriche ancora incerte |
| D24 | Decisioni di Design | P1-P20 (sezione 17) confermate e vincolanti per l'implementazione | Emerse durante la progettazione delle schermate, coerenti con i principi della sezione 1 |

## 3. Architettura

Soluzione .NET a progetti separati: `Core` contiene dominio, interfacce e orchestrazione senza dipendenze esterne; tutto il resto si collega tramite interfacce.

### Struttura della soluzione

| Progetto | Responsabilità | Dipendenze principali |
| --- | --- | --- |
| `PhotoFlow.Core` | Entità, interfacce, orchestrazione della pipeline | Nessuna |
| `PhotoFlow.Sources` | Sorgenti (SD, cartella), rilevamento SD, ingest, copia verificata, rinomina | System.IO.Hashing (xxHash3) |
| `PhotoFlow.Imaging` | Gestori di formato (`IImageHandler`): anteprime, decodifica | exiftool, SkiaSharp, Magick.NET, LibRaw (binding da scegliere) |
| `PhotoFlow.Metadata` | Lettura EXIF, scrittura/lettura XMP, sincronizzazione | exiftool (`-stay_open`) |
| `PhotoFlow.Inference` | Selezione hardware, sessioni ONNX, registro modelli | ONNX Runtime, ONNX Runtime GenAI |
| `PhotoFlow.Analysis` | Analizzatori del culling, raggruppamenti, decisioni | OpenCvSharp |
| `PhotoFlow.Faces` | Impronte, confronto, clustering, cifratura impronte | HdbscanSharp |
| `PhotoFlow.Tagging` | CLIP zero-shot, RAM++, Florence-2, VLM, OCR, vocabolario | Florence2-Sharp, RapidOcrNet |
| `PhotoFlow.Data` | Catalogo, migrazioni, indice di ricerca (`ISearchIndex`) | Microsoft.Data.Sqlite, Dapper |
| `PhotoFlow.App` | Interfaccia MVVM, localizzazione IT/EN | Avalonia, CommunityToolkit.Mvvm |
| `PhotoFlow.Tests` | Unitari, parità, integrazione, scenari | xUnit |

### Pipeline

Le fasi per foto sono collegate da `System.Threading.Channels` limitati e partono mentre la copia è in corso; le barriere di lotto attendono l'intero insieme.

| # | Fase | Tipo | Note |
| --- | --- | --- | --- |
| 1 | Copia + verifica | Per foto | Saltata nei job `AnalyzeInPlace`; nessuna fase tocca un file non verificato |
| 2 | Estrazione anteprima | Per foto | Gestore scelto dal contenuto del file, non dall'estensione |
| 3 | Analizzatori del culling | Per foto, in parallelo | Esposizione, nitidezza, soggetto, volti/occhi, embedding |
| 4 | Raggruppamenti | Barriera di lotto | Raffiche, bracketing, timelapse, duplicati |
| 5 | Decisione proposta | Per gruppo | Sezione 6 |
| 6 | Tagging e OCR | Per gruppo | Solo foto tenute; il rappresentante propaga ai membri |
| 7 | Volti | Barriera di lotto | Confronto con i noti, poi clustering a blocchi |
| 8 | Revisione | Interfaccia | Conferma dell'utente |
| 9 | Scrittura XMP | Per foto | Solo dopo conferma, secondo la politica della libreria |

Tipi di job: `Import` (fasi 1-9), `AnalyzeInPlace` (2-9), `Reanalyze` (fasi selezionate, sezione 5).

### Interfacce chiave

```csharp
public interface ISource { IAsyncEnumerable<SourceItem> EnumerateAsync(CancellationToken ct); }
public interface IImageHandler { bool CanHandle(FileSignature sig); Task<PreviewImage> GetPreviewAsync(string path, int maxSize, CancellationToken ct); }
public interface IPhotoAnalyzer { string Id { get; } Task<AnalyzerResult> AnalyzeAsync(PhotoContext photo, SceneProfile profile, CancellationToken ct); }
public interface IBatchStage { Task RunAsync(JobContext job, IProgress<StageProgress> progress, CancellationToken ct); }
public interface ITagger { TaggingLevel Level { get; } Task<IReadOnlyList<TagResult>> TagAsync(PhotoContext photo, CancellationToken ct); }
public interface IInferenceProvider { HardwareInfo Detected { get; } IInferenceSession CreateSession(ModelDescriptor model, ExecutionTarget target); }
public interface IInferenceSession : IDisposable { Task<IReadOnlyList<NamedTensor>> RunAsync(IReadOnlyList<NamedTensor> inputs, CancellationToken ct); }
public interface IMetadataWriter { Task WriteAsync(Asset asset, MetadataChanges changes, WritePolicy policy, CancellationToken ct); }
public interface IObjectDetector { Task<IReadOnlyList<Detection>> DetectAsync(RgbImage image, CancellationToken ct); }
public interface IImageOperations { /* decodifica e ridimensionamento: isola SkiaSharp */ }
public interface ISearchIndex { /* FTS e ricerca vettoriale, isolati per un futuro PostgreSQL */ }
public interface IAppPaths { /* unico punto di risoluzione dei percorsi: abilita la modalità portatile */ }
```

Inferenza senza dipendenze in `Core` (decisione del 12 ott 2026): `NamedTensor` (nome, forma, dati `ReadOnlyMemory<T>`) ed `ExecutionTarget` (Auto/CPU/GPU) sono tipi di `Core`; solo `PhotoFlow.Inference` dipende da ONNX Runtime e converte i tensori senza copie. Gli adattatori dei modelli vivono nei progetti di dominio (`.Analysis`, `.Faces`, `.Tagging`) ed espongono interfacce per funzione (es. `IObjectDetector`, `IFaceAnalyzer`, `IImageEmbedder`) consumate dagli analizzatori. `Dispose` della sessione libera la memoria del modello (caricamento fase per fase).

### Moduli trasversali

| Modulo | Scelta |
| --- | --- |
| Hardware | Prova CUDA → DirectML → CoreML → CPU; micro-benchmark al primo avvio |
| VRAM | Modelli caricati e scaricati fase per fase (funziona con GPU da 4-6 GB) |
| Registro modelli | Manifest JSON: URL, SHA-256, dimensione, licenza, adattatore; download al primo uso |
| Profili | File JSON versionati |
| Percorsi | Tutti tramite `IAppPaths` |
| Localizzazione | File di risorse IT/EN separati dal codice |

## 4. Sorgenti, formati e metadati

L'import da SD è un caso particolare dell'analisi di una raccolta; i metadati si scrivono in XMP standard, unendo e mai sovrascrivendo.

### Ingest

| Funzione | Specifica |
| --- | --- |
| Rilevamento SD | Polling \~2 s dei volumi rimovibili con cartella `DCIM` (multipiattaforma) |
| Template percorsi | Configurabile, es. `{yyyy}/{yyyy-MM-dd}_{evento}/{yyyyMMdd}_{evento}_{seq:0000}.{ext}` |
| Copia | Una sola lettura della sorgente, scrittura parallela sulle destinazioni locali, hash calcolato in lettura |
| Verifica | Rilettura della destinazione e confronto xxHash3 |
| NAS | Destinazione secondaria asincrona con retry |
| Già importate | Riconosciute tramite hash + data di scatto: nessun duplicato al secondo import |
| RAW+JPEG | Trattati come una sola foto (`AssetPairs`) |
| Video | Copiati, non analizzati |

### Gestori di formato

| Formato | Anteprima | Libreria | Note |
| --- | --- | --- | --- |
| RAW (NEF, CR2/CR3, ARW, RAF, DNG…) | Anteprima incorporata | exiftool | Fallback: decodifica completa se l'anteprima è piccola |
| JPG, PNG, WebP | Decodifica diretta | SkiaSharp | Percorso veloce |
| TIFF | Decodifica con riduzione | Magick.NET | 16 bit, multipagina, file grandi |
| HEIC | Decodifica | Magick.NET (libheif) | Foto da smartphone |

### EXIF letti

`SubSecDateTimeOriginal` (ordine delle raffiche), ISO, tempo, diaframma, focale, obiettivo, compensazione dell'esposizione (bracketing), punto AF Nikon se disponibile (zona di nitidezza). La fonte della data (EXIF/file/sconosciuta) viene registrata.

### Politiche di scrittura (per libreria)

| Politica | Comportamento | Uso |
| --- | --- | --- |
| `SidecarOnly` | Sempre `.xmp` accanto all'originale | Originali intoccabili |
| `EmbeddedForNonRaw` | Incorporato in JPG/TIFF/PNG, sidecar per i RAW | Visibilità in Esplora risorse e Photoshop; copia `_original` configurabile |
| `CatalogOnly` | Solo catalogo, esportabile | Archivi in sola lettura |

Default per le librerie create da un import: `EmbeddedForNonRaw`, perché l'utente consulta con il visualizzatore di Windows (che ignora i sidecar) e modifica con Photoshop/Camera Raw. Default per le librerie create da un archivio esistente (`AnalyzeInPlace`): `SidecarOnly`; il passaggio a `EmbeddedForNonRaw` richiede conferma esplicita con il numero di file coinvolti e la copia `_original` attiva (D21).

### Campi XMP

| Dato | Campo |
| --- | --- |
| Scarto | `xmp:Rating = -1` (in modalità prudente: etichetta rossa) |
| Da rivedere | `xmp:Label = "Red"` |
| Keyword piatte | `dc:subject`, italiano + inglese |
| Keyword gerarchiche | `lr:hierarchicalSubject`, solo italiano |
| Volti | Regioni MWG con nome |
| Didascalia | `dc:description` (opzionale) |

Nomi di persone, eventi e piloti non si traducono. Tag "non rivisti": solo inglese.

### Sincronizzazione con modifiche esterne

| Situazione | Comportamento |
| --- | --- |
| XMP modificato da altri software | Modifiche importate come manuali |
| Conflitto su tag automatico | Vince la versione esterna |
| Conflitto su rating/etichetta | Vince la più recente, con log |
| Tag manuali del catalogo | Mai rimossi |
| Keyword e volti già presenti | Mantenuti; regioni volto nominate da altri software importabili nella galleria persone |

## 5. Modelli, inferenza e rigenerazione

Tutti i modelli girano in ONNX Runtime; ogni funzione ha un modello attivo selezionabile manualmente (modello e taglia), con default scelto in base all'hardware.

### Modelli per funzione

| Funzione | Default | Alternative | Peso indicativo | Disponibilità ONNX |
| --- | --- | --- | --- | --- |
| Rilevamento oggetti | D-FINE S (CPU) / M (GPU), pesi `obj2coco` fp32 | RF-DETR Nano-Large | 10-19M parametri | Conversioni `onnx-community` fissate per commit + SHA-256, verificate con test di parità contro il nostro export (`tools/lab`); riserva: nostro export su GitHub Releases |
| Volti (rilevamento, landmark, impronte) | InsightFace buffalo\_l | buffalo\_s; YuNet/SFace (Apache 2.0) | 326 MB | Nativo |
| Embedding (raffiche, ricerca, duplicati, zero-shot) | CLIP ViT-B/32 | — | 153 MB (quantizzato) / 606 MB | Disponibile (Xenova) |
| Tagger | RAM++ int8 | RAM++ fp32 | 873 MB / 1,86 GB | Export della community |
| Didascalie | Florence-2 base | Florence-2 large | \~0,5 / \~1,5 GB | Florence2-Sharp (NuGet) |
| VLM (livello D) | Qwen2.5-VL | — | 3-5 GB circa | ONNX Runtime GenAI (C#) |
| OCR | PaddleOCR PP-OCRv5 (latin) | Taglie server | \~15 MB | RapidOcrNet (NuGet) |
| Estetica | Predittore LAION su embedding CLIP | — | < 5 MB | Riscrivibile in poche righe |

Qwen3-VL è escluso finché ONNX Runtime GenAI non lo supporta nativamente.

### Livelli di tagging

| Livello | Contenuto | Default |
| --- | --- | --- |
| A | Zero-shot CLIP sul vocabolario personale | Attivo |
| B | RAM++ | Attivo |
| C | Didascalie Florence-2 | Opzionale |
| D | Didascalie VLM | Opzionale, solo GPU |

### Selezione hardware

| Aspetto | Comportamento |
| --- | --- |
| Ordine di prova | CUDA → DirectML → CoreML → CPU |
| Varianti di build | Windows: DirectML (default) e CUDA (opzionale); i due pacchetti non convivono |
| Primo avvio | Micro-benchmark su \~50 foto con stima dei tempi; su sola CPU propone un livello di tagging più leggero |
| Selezione manuale | Per funzione, con licenza, spazio, memoria stimata e ultimo benchmark mostrati; pulsante "benchmark" prima dell'attivazione |
| Compatibilità | Selezionabili solo modelli con un adattatore di input/output implementato |

### Stime di tempo (da validare con il benchmark)

| Scenario | GPU (RTX 3090) | Solo CPU (8 core) |
| --- | --- | --- |
| Import 1.000 scatti: culling + tagging A+B + volti | \~6-11 min | \~22-50 min |
| Archivio 50.000-60.000 foto: tagging A+B + volti | \~1,5-3 h | \~8-20 h |

### Rigenerazione (job `Reanalyze`)

| Aspetto | Specifica |
| --- | --- |
| Perimetro | Libreria, cartella, job, risultato di ricerca, singola foto |
| Cosa | Tag, didascalie, punteggi di culling, embedding, volti, OCR (selezionabili) |
| Modalità | "Solo modello più vecchio" (default) o "Forza tutto" |
| Protezioni | Tag manuali mai toccati; tag rimossi dall'utente restano soppressi; decisioni dell'utente mantenute; opzione per escludere job già revisionati |
| Nuovo modello volti | Nuove impronte su tutto; associazione ai vecchi volti per sovrapposizione dei riquadri; assegnazioni trasferite; clustering solo dei non associati |
| Nuovo modello embedding | Vecchio indice attivo fino al completamento del nuovo, poi scambio in un'unica operazione |
| Sicurezza | Anteprima delle differenze; risultati precedenti conservati fino a conferma; prova su campione prima dell'intero archivio |

## 6. Culling e profili di scena

Uno scarto avviene solo su un segnale ad alta affidabilità chiaramente oltre soglia; ogni incertezza produce "da rivedere".

### Raffiche

| Condizione | Regola |
| --- | --- |
| Con EXIF | Scatti entro N secondi (default 2 s) e similarità degli embedding sopra soglia |
| Senza EXIF | Solo similarità |
| Cambio di soggetto | Crollo della similarità spezza la raffica |

### Segnali per foto

| Segnale | Misura | Affidabilità |
| --- | --- | --- |
| Nitidezza | Varianza del Laplaciano sulla zona del profilo, dopo leggera sfocatura anti-rumore; assoluta + relativa nella raffica | Alta per il mosso grave |
| Esposizione | % alte luci/ombre perse nella zona del soggetto; soglie tolleranti (l'anteprima JPEG è più severa del RAW) | Media |
| Occhi | Apertura dagli 106 landmark, per volti rilevanti (≥ 40% del volto più grande); occhiali da sole = sconosciuto. Con YuNet/SFace (solo 5 landmark): sempre sconosciuto | Alta su volti grandi (solo buffalo\_l) |
| Soggetto | Presenza, dimensione, posizione (D-FINE) | Alta per auto e persone |
| Estetica | Predittore LAION | Bassa: solo spareggi |

### Decisione

1. Scarti tecnici: solo segnale ad alta affidabilità chiaramente oltre soglia; vicino alla soglia o incerto → "da rivedere".
2. Classifica nella raffica: punteggio composito con i pesi del profilo, normalizzato dentro la raffica.
3. Esito: la prima diventa "migliore"; se le prime due sono quasi a pari merito, entrambe "da rivedere".

| Esito | Significato | XMP |
| --- | --- | --- |
| Scarto | Problema tecnico certo | Rating -1 (modalità prudente: etichetta rossa) |
| Da rivedere | Incertezza o pari merito | Etichetta rossa |
| Migliore | Prima della raffica | Nessun rating forzato (opzionale 1 stella) |
| Simile | Valida ma non prima | Solo catalogo |
| Tenuta | Scatto singolo senza problemi | Nessuna scrittura |

### Sequenze speciali (mai trattate come raffiche)

| Sequenza | Riconoscimento | Comportamento |
| --- | --- | --- |
| Bracketing | Compensazione variabile a scatti ravvicinati | Gruppo "bracketing", nessuno "simile" |
| Timelapse | Intervallo regolare ripetuto | Gruppo "sequenza", nessuna classifica |
| Panorama | Contrassegno manuale (automatico nel backlog) | Nessuna classifica |

### Metriche obiettivo

| Metrica | Obiettivo |
| --- | --- |
| Scarti sbagliati | < 2% degli scarti |
| Scarti intercettati | > 70% |
| Quota "da rivedere" | < 20% |

### Apprendimento dalle correzioni

Ogni correzione è salvata in `Corrections` con i segnali dell'epoca; dopo un numero sufficiente di correzioni per profilo, l'applicativo suggerisce di spostare le soglie. Non cambia nulla da solo e non addestra modelli.

### Profili predefiniti

| Profilo | Zona nitidezza | Esposizione | Volti | Soggetto | OCR |
| --- | --- | --- | --- | --- | --- |
| Eventi | Punto AF → volti | Normale | `Recognize` | Persone | No |
| Ritratto | Occhi/volto | Normale | `Recognize` | Persone | No |
| Motorsport | Solo veicolo (panning ignorato) | Tollerante | `DetectOnly` | Auto, moto | Sì |
| Animali | Animale | Normale | `DetectOnly` | Classi COCO di animali | No |
| Paesaggio | Riquadri (il più nitido) | Normale | `Off` | — | No |
| Notturna/Aurora | Relativa nella raffica, nessuno scarto assoluto | Disattivata | `Off` | — | No |
| Archivio | Culling disattivato | — | `Recognize` | — | Opzionale |

### Pesi iniziali del punteggio composito (da tarare)

| Profilo | Nitidezza | Occhi | Esposizione | Soggetto | Estetica |
| --- | --- | --- | --- | --- | --- |
| Eventi | 35 | 30 | 15 | 10 | 10 |
| Ritratto | 30 | 45 | 15 | 0 | 10 |
| Motorsport | 50 | 0 | 15 | 30 | 5 |
| Animali | 45 | 0 | 15 | 30 | 10 |
| Paesaggio | 45 | 0 | 35 | 0 | 20 |
| Notturna/Aurora | 80 | 0 | 0 | 0 | 20 |

### Scelta del profilo

| Modalità | Comportamento |
| --- | --- |
| Manuale + suggerimento (default) | Profilo principale scelto all'avvio; i gruppi che sembrano di altro tipo vengono segnalati nella revisione |
| Automatica (per job) | CLIP assegna il profilo per gruppo; profilo di riserva obbligatorio sotto soglia di confidenza, con segnalazione; profilo e confidenza mostrati nel riepilogo |
| Personalizzazione | Duplica e modifica un profilo; i predefiniti restano intatti; cambio profilo per gruppo con rianalisi |

## 7. Volti

Il riconoscimento raggruppa i volti ignoti come "Persona N" e l'utente assegna i nomi una volta sola; le impronte biometriche sono cifrate.

### Modalità

| Modalità | Cosa fa |
| --- | --- |
| `Off` | Nessuna analisi dei volti |
| `DetectOnly` | Volti e occhi chiusi, senza impronte |
| `Recognize` | Impronte, riconoscimento dei noti, clustering degli ignoti |

Impostabile globalmente e sovrascrivibile per profilo.

### Configurazione

```json
"faces": {
  "mode": "Recognize",
  "modelPack": "buffalo_l",
  "minFaceSizePx": 48,
  "detectionConfidence": 0.6,
  "matchThreshold": 0.45,
  "clusterMinSize": 3,
  "writeRegionsToXmp": true,
  "writeNamesAsKeywords": true
}
```

| Parametro | Nota |
| --- | --- |
| `modelPack` | buffalo\_l (default), buffalo\_s; buffalo\_sc escluso (senza landmark, inutile per gli occhi); YuNet/SFace come alternativa con licenza permissiva, **senza rilevamento degli occhi chiusi** (occhi sempre "sconosciuto", decisione del 12 ott 2026) |
| `matchThreshold` | Valore iniziale da tarare sui dati reali |
| `clusterMinSize` | Sotto soglia i volti restano "non assegnati" |

### Flusso

1. Rilevamento (condiviso con il culling) e impronte.
2. Confronto con `PersonPrototypes` delle persone note.
3. Clustering HDBSCAN solo dei non assegnati, a blocchi (per anno o cartella), con ricerca per vicini approssimata: con \~100.000 volti la matrice completa delle distanze non sta in memoria.
4. Revisione: rinomina, unisci, separa, ignora; gruppi ordinati per dimensione.
5. Scrittura: regioni MWG e `Persone|Nome` come keyword.

### Regole di qualità

| Regola | Motivo |
| --- | --- |
| Soglie che preferiscono dividere i gruppi | Unire è un clic, separare è un lavoro |
| Campioni aggiornati per persona | Il volto dei bambini cambia negli anni |
| Eliminare una persona cancella tutte le sue impronte | Nessun residuo |

### Protezione delle impronte

| Aspetto | Specifica |
| --- | --- |
| Cosa è cifrato | Solo i vettori delle impronte (cifratura applicativa con le librerie standard .NET) |
| Chiave | Default: account utente del sistema (Windows; portachiavi su Mac/Linux); alternativa: password |
| Chiave non disponibile | Richiesta della password, oppure ricalcolo delle impronte; le assegnazioni restano (in chiaro, legate alla posizione del volto) |
| Esportazione | Impronte cifrate con password di esportazione, oppure escluse e ricalcolate sulla destinazione |

### Licenza

buffalo\_l è solo per ricerca non commerciale: mai incluso nel repository, scaricato al primo uso con accettazione esplicita della licenza.

## 8. Tagging e OCR motorsport

Ogni tag ha un identificativo interno stabile in inglese ed etichette per lingua; ogni assegnazione registra fonte e confidenza.

### Fonti dei tag

| Fonte | Produce | Lingua di origine |
| --- | --- | --- |
| RAM++ | Tag generici (4.585 possibili) | Inglese |
| CLIP zero-shot | Tag personalizzati (descrizione inglese per il modello + etichetta italiana) | Definita dall'utente |
| Job | Nome dell'evento | Utente |
| Volti | `Persone\|Nome` | Utente (nomi mai tradotti) |
| OCR | `Motorsport\|Numero N`, `Motorsport\|Pilota\|Nome`, `Motorsport\|Team\|Nome` (pilota e team solo da elenco iscritti; con equipaggio solo numero e team, D18) | — (non tradotti) |
| Manuale | Tag dell'utente | Utente |

I dati EXIF non diventano keyword: sono già ricercabili nel catalogo.

### Vocabolario e traduzione

| Livello | Contenuto | Trattamento |
| --- | --- | --- |
| Nucleo | \~500 tag più frequenti nell'archivio dell'utente | Tradotti con un LLM e rivisti dall'utente |
| Resto | Tag rari | Traduzione automatica, "non rivisti": scritti solo in inglese finché non rivisti |
| Lista nera | Tag inutili (es. "image", "photo") | Mai scritti |

Categorie gerarchiche: `Persone`, `Luoghi`, `Natura`, `Veicoli`, `Eventi`, `Animali`, tramite tabella di mappatura sul nucleo.

### Soglie

| Fonte | Soglia |
| --- | --- |
| RAM++ | Soglie ufficiali per tag + regolatore globale |
| CLIP | Soglia per tag + margine minimo sul secondo candidato |

### Provenienza

| Regola | Motivo |
| --- | --- |
| Fonte e confidenza registrate | Filtri e rimozioni mirate |
| Tag manuali mai rimossi da una rianalisi | Lavoro dell'utente protetto |
| Tag automatici rimossi dall'utente → soppressi | Non vengono reinseriti |

### Didascalie

Florence-2 produce solo inglese; Qwen2.5-VL può produrre italiano ma solo con GPU. Scrittura in `dc:description` opzionale.

### OCR motorsport

1. D-FINE rileva auto/moto.
2. OCR solo sul ritaglio di ciascun veicolo.
3. Accettati solo i numeri presenti nell'elenco iscritti.
4. Nelle raffiche il numero si propaga a maggioranza.

| Caso | Gestione |
| --- | --- |
| Numeri estranei (sponsor, targhe) | Ignorati se non in elenco |
| Numero parziale, confusione 6/9 o 1/7 | Candidati multipli → "da rivedere" |
| Più auto nella foto | Tag per ciascuna; soggetto principale evidenziato |
| Vettura con un pilota | XMP: numero + pilota (+ team) |
| Vettura con equipaggio | XMP: solo numero (+ team); equipaggio associato nel catalogo, ricercabile e segnalato come "equipaggio" |
| Senza elenco iscritti | Solo il numero |

Elenco iscritti: import CSV/Excel (numero, pilota/i, team, categoria, vettura) o inserimento manuale; riutilizzabile su più eventi. Correzione rapida dalla vista "Numeri" della revisione.

## 9. Catalogo e modello dati

Catalogo SQLite, foto identificate dall'hash del contenuto: tag, volti e decisioni seguono il file anche se spostato o rinominato.

### Posizione del catalogo

| Elemento | Posizione |
| --- | --- |
| Catalogo | Configurabile: locale (default) o percorso di rete |
| Miniature e cache | Sempre locali (\~2 GB per 60.000 foto), ricostruibili |
| Profili e impostazioni | File JSON nella cartella dati utente |

| Catalogo in rete: protezione | Comportamento |
| --- | --- |
| Modalità journal | Rollback automatico su percorso di rete (WAL non funziona su filesystem di rete) |
| Un PC alla volta | File di lock; apertura rifiutata con messaggio chiaro |
| Backup | A ogni chiusura e programmato, N versioni |
| Integrità | Controllo all'apertura; ripristino proposto dall'ultimo backup |

Esportazione/importazione: file unico (es. `.pfcatalog`) con database, galleria persone e configurazione delle librerie; rimappatura dei percorsi all'importazione.

### Tabelle

| Area | Tabella | Campi principali |
| --- | --- | --- |
| Librerie | `Libraries` | Nome, percorso radice, politica XMP, sola lettura, ultima scansione |
| Foto | `Assets` | Hash rapido (dimensione + primi/ultimi 64 KB), hash completo (facoltativo, in background), formato, gestore, dimensioni, data di scatto + fonte, EXIF principali, stato |
|  | `AssetLocations` | Asset, libreria, percorso relativo, ultima volta visto |
|  | `AssetPairs` | Gemelli RAW+JPEG |
| Lavori | `Jobs` | Tipo, sorgente, evento, profilo/automatico + riserva, stato, priorità, istantanea delle impostazioni |
|  | `JobItems` | Avanzamento per foto e fase, errori (punto di ripresa) |
| Modelli | `Models` | Nome, versione, SHA-256, licenza, funzione, stato (in prova/attivo/ritirato) |
| Analisi | `AnalyzerResults` | Asset, analizzatore, versione modello, profilo, punteggi |
|  | `Embeddings` | Asset, modello, vettore (BLOB) |
| Gruppi | `Groups` | Tipo, profilo assegnato e confidenza, origine |
|  | `GroupMembers` | Gruppo, asset, posizione, punteggio |
| Decisioni | `Decisions` | Esito automatico e motivazioni, esito utente, rating, etichetta |
|  | `Corrections` | Correzioni dell'utente con i segnali dell'epoca |
| Tag | `Tags` | Identificativo, categoria, fonte, rivisto, lista nera |
|  | `TagLabels` | Tag, lingua, etichetta |
|  | `AssetTags` | Asset, tag, fonte, confidenza, versione modello, stato (attivo/soppresso) |
|  | `CustomTags` | Descrizione CLIP, soglia, margine |
| Volti | `Faces` | Asset, riquadro, landmark, confidenza, occhi, impronta cifrata (facoltativa), persona, origine assegnazione, somiglianza |
|  | `Persons` | Nome, ignorata, volto di copertina |
|  | `PersonPrototypes` | Impronte rappresentative (cifrate) |
| Testi | `Captions` | Asset, lingua, testo, modello |
|  | `OcrResults` | Asset, riquadro, testo, confidenza |
|  | `EntryLists` | Evento, numero, pilota/i, team, categoria, vettura |
| Sincronizzazione | `XmpSync` | Ultima scrittura, firma del file XMP, scrittura in sospeso |
| Sistema | `UndoLog`, `SchemaVersion` | Operazioni annullabili; versione schema |

I risultati automatici conservano la versione del modello che li ha prodotti (rianalisi, anteprima differenze, ritorno indietro).

### Scelte tecniche

| Tema | Scelta |
| --- | --- |
| Ricerca testuale | SQLite FTS5 su tag (IT+EN), persone, eventi, didascalie, percorsi |
| Ricerca per similarità | Vettori in memoria (\~60-120 MB per 60.000 foto), confronto diretto |
| Migrazioni | Script SQL numerati, solo in avanti, backup automatico prima di ogni migrazione |
| Futuro PostgreSQL | FTS e vettori dietro `ISearchIndex`; resto SQL standard |

## 10. Requisiti non funzionali

Le invarianti di sicurezza dei dati non sono configurabili e vanno coperte da test automatici.

### Invarianti

| # | Invariante |
| --- | --- |
| I1 | Nessuna scrittura né cancellazione sulla sorgente di import (sorgente dei job `Import`; i file di una libreria seguono la sua politica XMP, vedi D21) |
| I2 | RAW mai modificati (solo sidecar) |
| I3 | Nessuna fase successiva su un file non verificato |
| I4 | Nessun file eliminato automaticamente; gli scarti si spostano solo su richiesta |
| I5 | Tag e decisioni manuali mai sovrascritti da processi automatici |

### Affidabilità

| Requisito | Specifica |
| --- | --- |
| Errori su singoli file | Non fermano il job; motivo registrato; nuovo tentativo possibile |
| File corrotti | Elencati in un report, mai spostati o modificati |
| Crash | Report locale; al riavvio proposta di riprendere i job interrotti |
| Ripresa | Avanzamento salvato per foto: si perde al massimo l'ultimo blocco |
| Annulla | Ogni modifica di rating, tag o persona è annullabile |

### Archivi grandi (fase 1b)

| Requisito | Specifica |
| --- | --- |
| Priorità | Modalità a bassa priorità con limiti di CPU/GPU/memoria |
| Ordine | Per cartella o dalla più recente |
| Stima del tempo | Dal benchmark iniziale, corretta durante l'esecuzione |
| Sorgenti lente | Coda limitata: rete e GPU non si bloccano a vicenda |
| Identità | Hash rapido al primo passaggio, hash completo in background |

### Log e privacy

| Requisito | Specifica |
| --- | --- |
| Log | File locali a rotazione, livello configurabile, log per job |
| Telemetria | Nessuna |
| Rete | Solo download modelli e controllo aggiornamenti, entrambi disattivabili; funzionamento completamente offline dopo il setup |

### Prestazioni (obiettivi)

| Area | Obiettivo |
| --- | --- |
| Interfaccia | Nessuna operazione bloccante; risposta ai comandi < 100 ms |
| Griglia | Scorrimento fluido con 60.000 miniature |
| Ricerca | < 1 s su 60.000 foto |
| Duplicati | Confronto embedding su 60.000 foto: secondi su GPU, minuti su CPU |

## 11. Installazione e aggiornamenti

Pacchetto self-contained con Velopack, modelli scaricati al primo uso; durante lo sviluppo si esegue dalla cartella di build e l'installer si genera solo alle versioni di riferimento.

### Pacchetto

| Aspetto | Specifica |
| --- | --- |
| Runtime .NET | Incluso (self-contained) |
| Installer e aggiornamenti | Velopack; feed come file statici su qualsiasi server HTTP |
| Varianti | Windows: DirectML (default), CUDA (opzionale); Mac: CoreML; Linux: CPU/CUDA |
| Firma del codice | Nessuna per ora (avviso SmartScreen al primo avvio) |
| exiftool | Windows: eseguibile incluso; Mac/Linux: script Perl incluso; percorso personalizzabile |

### Modelli

| Aspetto | Specifica |
| --- | --- |
| Distribuzione | Download al primo uso dal manifest, verifica SHA-256 |
| Offline | Importazione di un pacchetto modelli da cartella |
| Posizione | Configurabile |
| Licenze | Mostrate prima del download; non commerciali contrassegnate |
| Aggiornamenti | Separati dall'applicativo: flusso prova → rianalisi → conferma |

| Configurazione | Modelli | Spazio indicativo |
| --- | --- | --- |
| Fase 1 | D-FINE, buffalo\_l | \~0,4 GB |
| + raffiche e tagging A+B | + CLIP, RAM++ int8 | \~1,5-2 GB |
| + didascalie e OCR | + Florence-2, PaddleOCR | \~3-3,5 GB |
| + VLM | + Qwen2.5-VL | +3-5 GB |

### Dati sul disco

| Contenuto | Posizione predefinita | Alla disinstallazione |
| --- | --- | --- |
| Applicativo | Cartella di installazione | Rimosso |
| Configurazione, profili, vocabolario | Cartella dati utente | Conservati salvo scelta |
| Catalogo | Configurabile | Conservato salvo scelta |
| Modelli | Configurabile | Conservati salvo scelta |
| Miniature e cache | Cartella dati utente | Rimosse |

### Aggiornamenti dell'applicativo

Controllo all'avvio in background; installazione solo dopo conferma e mai durante un job; migrazione del catalogo con backup preventivo; versione precedente disponibile per il ripristino.

### Requisiti indicativi

| Risorsa | Minimo | Consigliato |
| --- | --- | --- |
| Sistema | Windows 10/11 x64 | Windows 11 |
| RAM | 8 GB | 16 GB o più |
| GPU | Nessuna | GPU DirectX 12; NVIDIA per la build CUDA |
| Disco | \~1 GB + modelli + miniature | SSD per catalogo e miniature |

## 12. Brief per Claude Design

Obiettivo: sistema visivo e 11 schermate di un'applicazione desktop per fotografi, da implementare poi in Avalonia (non web). Prima il sistema visivo, poi le schermate.

### Vincoli

| Vincolo | Dettaglio |
| --- | --- |
| Piattaforma | Desktop (Windows prioritario), finestra ridimensionabile, larghezza minima \~1280 px |
| Implementazione | Avalonia: evitare effetti esclusivamente web; segnalare soluzioni che richiedono componenti personalizzati |
| Tema | Scuro con grigi neutri di default (non falsano i colori delle foto); tema chiaro disponibile |
| Lingue | Italiano e inglese: prevedere testi più lunghi del 30% senza rompere il layout |
| Contenuto protagonista | Le foto: interfaccia sobria, cromia minima attorno alle immagini |
| Tastiera | Ogni azione frequente ha una scorciatoia visibile |

### Sistema visivo da definire

Palette (con colori semantici per gli esiti: scarto, da rivedere, migliore, simile, tenuta), tipografia, spaziature, griglia, componenti (miniatura con stato, chip tag, chip persona, barra di avanzamento per fase, filtri a faccette, pannello dettagli, dialoghi di conferma).

### Schermate

| # | Schermata | Contenuto | Note di design |
| --- | --- | --- | --- |
| 1 | Home | Librerie, job attivi, avviso "scheda rilevata", azioni rapide | Punto di partenza, poco testo |
| 2 | Import (procedura guidata) | Sorgente → evento, profilo (o automatico + riserva), destinazioni, volti, politica XMP → avvio | 3 passi, impostazioni ricordate |
| 3 | Job | Avanzamento per fase, stima tempo, pausa/ripresa, priorità, log | Più job in parallelo |
| 4 | Revisione | Griglia per raffiche, filtri per esito, confronto 2-4 affiancati con zoom sincronizzato, cambio profilo per gruppo, vista "Numeri", conferma e scrittura XMP | Schermata principale: velocità da tastiera |
| 5 | Persone | Gruppi ordinati per dimensione; rinomina, unisci, separa, ignora; pagina persona | Volti ritagliati, selezione multipla |
| 6 | Consultazione | Ricerca libera + filtri (tag, persona, data, evento, EXIF, rating, esito), griglia, pannello dettagli con tag modificabili e volti evidenziati, "Apri in Photoshop", "Apri con app predefinita", "Mostra in Esplora risorse" | Ricerca in linguaggio naturale nello stesso campo |
| 7 | Duplicati | Gruppi esatti e quasi-duplicati, scelta di quale tenere | Confronto affiancato |
| 8 | Librerie | Radici, politica XMP, sola lettura, nuova scansione, rimappatura percorsi |  |
| 9 | Vocabolario | Revisione traduzioni del nucleo, lista nera, tag CLIP personalizzati, mappatura categorie | Revisione rapida a righe |
| 10 | Profili | Elenco, duplica, modifica pesi e soglie | Predefiniti non modificabili |
| 11 | Impostazioni | Hardware e benchmark, modelli (per funzione, con licenza e spazio), catalogo (posizione, backup, esporta/importa, cifratura impronte), lingua, template percorsi, exiftool, modalità prudente/normale | Sezioni a schede |

Modalità aggiuntiva della schermata 4: **etichettatura** (sezione 14), con un tasto per foto, verdetto nascosto ("alla cieca"), sessioni da \~100 foto e pannello metriche.

### Scorciatoie della revisione

| Tasto | Azione |
| --- | --- |
| Frecce | Navigazione |
| `P` / `X` | Tieni / scarta |
| `1`-`5` | Stelle |
| `C` | Confronta |
| `Z` | Zoom 100% |
| `Ctrl+Z` | Annulla |

Etichettatura: `1` buona, `2` mossa, `3` occhi chiusi, `4` esposizione, `5` altro, `Spazio` salta.

### Flussi da rappresentare

| Flusso | Percorso |
| --- | --- |
| Import dopo un evento | Home (scheda rilevata) → Import → Job → Revisione → Persone (solo nuovi gruppi) |
| Primo passaggio sull'archivio | Librerie → Job in background → Persone (gruppi più grandi) → Vocabolario |
| Ricerca quotidiana | Consultazione → dettaglio → Apri in Photoshop |

Output atteso: sistema visivo, una tavola per schermata (stato tipico + stato vuoto + stato di errore dove rilevante), note su componenti non standard per Avalonia.

## 12bis. Esito del design (v0.1, 12 ott 2026)

Design ha prodotto il sistema visivo e le 11 schermate in cinque tavole HTML, da conservare in `docs/design/` del repository; questa sezione riporta i valori vincolanti per l'implementazione.

### Tavole

| File | Tavole | Contenuto |
| --- | --- | --- |
| `PhotoFlow_Sistema_Visivo_dc.html` | 01-08 | Colori, tipografia, spaziature, struttura finestra, componenti, tema chiaro, testi IT/EN, note Avalonia |
| `PhotoFlow_Revisione_dc.html` | 2a-2h | Griglia per raffiche (interattiva), Confronto, Numeri, Etichettatura, attesa, vuoto, conferma XMP, errore parziale |
| `PhotoFlow_Home_Import_Job_dc.html` | 3a-3i | Home (scheda rilevata, primo avvio, ripresa dopo crash), Import passi 1-3, Job (parallelo, vuoto, pausa per destinazione scollegata) |
| `PhotoFlow_Persone_Consultazione_Duplicati_dc.html` | 4a-4g | Persone (nuovi gruppi, pagina persona, vuoto, raggruppamento in corso, chiave non disponibile), Consultazione (ricerca mista, nessun risultato, indice in costruzione), Duplicati |
| `PhotoFlow_Librerie_Vocabolario_Profili_Impostazioni_dc.html` | 5a-5j | Librerie e rimappatura, Vocabolario (nucleo, personalizzati), Profili, Impostazioni (Hardware, Modelli, Catalogo, Generali, Culling) |

### Colori · tema scuro (default) e chiaro

| Ruolo | Scuro | Chiaro |
| --- | --- | --- |
| Visore | #0E0E0E | #2A2A2A (resta scuro) |
| Area griglia | #151515 | #DCDCDC |
| Finestra, barra laterale | #1B1B1B | #F2F2F2 |
| Pannelli | #222222 | #FAFAFA |
| Controlli | #2B2B2B | #E8E8E8 |
| Hover, premuto | #363636 | #DEDEDE |
| Bordo / bordo forte | #303030 / #4A4A4A | #D4D4D4 / #ABABAB |
| Testo primario / secondario / terziario | #EDEDED / #A6A6A6 / #808080 | #1A1A1A / #4F4F4F / #6B6B6B |
| Accento (selezione, focus, link) | #5E9EEA | #1F6FD1 |
| Azione primaria | #E6E6E6 (neutro) | #1A1A1A |

### Esiti del culling

| Esito | Scuro | Chiaro | Glifo | Miniatura |
| --- | --- | --- | --- | --- |
| Scarto | #EB5A50 | #C8372D | ✕ | Foto attenuata al 35% |
| Da rivedere | #E9B13C | #A86F00 | ? | Cornice tratteggiata 2 px |
| Migliore | #4CC38A | #1E8A55 | ★ | Cornice piena 2 px |
| Simile | #9C9AE0 | #5F5BB8 | ≈ | Solo badge |
| Tenuta | #3A3A3A | #D4D4D4 | ✓ | Badge neutro al passaggio o con filtro |

Ogni esito è riconoscibile anche senza colore (glifo + trattamento). Stati di sistema (informazione, avviso, errore) con forme distinte, mai usati come esito sulle miniature.

### Tipografia

IBM Plex Sans + IBM Plex Mono (OFL, incorporati come risorse), cifre tabulari ovunque cambino numeri.

| Stile | Dimensione/interlinea · peso |
| --- | --- |
| Titolo pagina | 24/32 · 600 |
| Titolo sezione | 18/24 · 600 |
| Sottotitolo | 15/22 · 500 |
| Corpo (base) | 13/20 · 400 |
| Etichetta | 12/16 · 500 |
| Dati (mono) | 12/16 · 400 |
| Micro (mono) | 11/14 · 500 |

Formati localizzati: 1.248 · 2,8 GB · 12 ott 2026 (IT); 1,248 · 2.8 GB · Oct 12, 2026 (EN).

### Spaziature, raggi, elevazione, movimento

| Elemento | Valore |
| --- | --- |
| Scala | Base 4: 4 · 8 · 12 · 16 · 24 · 32 · 48 |
| Raggi | 2 foto · 4 controlli · 8 dialoghi · cerchio per le persone |
| Elevazione | Due livelli; separazione con tono e bordo 1 px; un'unica ombra per dialoghi, menu, tooltip: 0 8 24 #80000000 |
| Animazioni | Solo transizioni ≤ 120 ms su opacità e colore; nessuno shimmer, nessuna sfocatura |

### Struttura della finestra

| Area | Dimensione |
| --- | --- |
| Barra del titolo (con "Cerca ovunque" Ctrl K) | 32 |
| Navigazione | 184, comprimibile a 48 (automatica a 1280 px) |
| Barra strumenti | 40 |
| Pannello dettagli | 300 (Tab per chiuderlo) |
| Barra di stato | 24, con i tasti del contesto corrente |
| Miniature | Celle quadrate S 120 · M 168 · L 240, gap 4, Ctrl +/− |
| Pagine impostazioni | Colonna max 760, etichetta sopra il controllo |

Testi elastici: pulsanti e chip crescono col testo (minimo 72 px); le barre strumenti spostano le voci in "Altro", mai a capo; nomi file e percorsi troncati al centro con tooltip.

### Componenti chiave

| Componente | Regole |
| --- | --- |
| Miniatura | In alto a sinistra esito + "M" se manuale; in alto a destra posizione nella raffica o selezione; in basso a sinistra stelle o nome file; in basso a destra motivo breve, RAW+JPG, XMP in sospeso. Anello blu = cursore |
| Chip tag | Pieno = manuale; contorno + confidenza = automatico; "Categoria ›" = gerarchico; tratteggio + EN = non rivisto; barrato = soppresso |
| Chip persona | Nome certo pieno; proposta sotto soglia tratteggiata amber con somiglianza |
| Barra fasi | 9 segmenti: completata, in corso, partita in anticipo, richiede l'utente |
| Ricerca | Campo unico a gettoni (tag, persone, eventi) + testo residuo = linguaggio naturale; tipo sempre dichiarato |
| Dialoghi | Titolo = domanda con numero esatto; primario ripete il verbo; azioni pesanti con contorno rosso, mai default su Invio |
| Stati | Vuoto con azione; attesa con celle statiche e contatore in barra di stato; errore come banner: causa, conseguenza, azione |

### Mappa Avalonia

| Elemento | Tipo | Soluzione |
| --- | --- | --- |
| Tema e token | Standard | ResourceDictionary con ThemeVariant su FluentTheme; niente Acrylic/Mica |
| Griglia miniature 60.000 | Personalizzato | ItemsRepeater con layout a righe virtualizzato; intestazioni di gruppo sticky; navigazione ↑↓ calcolata dal layout |
| Cornice tratteggiata | Template | Rectangle con StrokeDashArray |
| Confronto sincronizzato | Personalizzato | 2-4 ZoomBorder (PanAndZoom) con matrice condivisa; decodifica a tasselli solo al 100% |
| Ricerca a gettoni | Personalizzato | TextBox + WrapPanel di chip + Popup |
| Cursore a due maniglie (ISO) | Personalizzato | Nessun RangeSlider nativo |
| Selezione a rettangolo (Persone) | Personalizzato | Su ItemsRepeater |
| Vocabolario | Standard | DataGrid (pacchetto Avalonia) virtualizzato |
| Log del job | Standard | ListBox virtualizzato o AvaloniaEdit in sola lettura |
| Barra del titolo | Standard | ExtendClientAreaToDecorationsHint con pulsanti di sistema |
| Tasti | Standard | KeyBindings per vista; barra di stato generata dalla stessa tabella dei comandi |
| Dialoghi | Standard | OverlayLayer/DialogHost, velo nero 60% senza sfocatura |
| Notifica di sistema in background | Personalizzato, facoltativo | Integrazione di piattaforma |

### Scorciatoie globali e per vista

| Contesto | Tasti |
| --- | --- |
| Globali | Ctrl 1-7 sezioni · Ctrl K cerca · Ctrl I importa cartella · Ctrl , impostazioni · Ctrl Z annulla |
| Revisione | G/C/N/L viste · ← → ↑ ↓ · P tieni · X scarta · 1-5 stelle · Z 100% · F adatta · S sincronizza · Tab pannello · Maiusc R rianalizza gruppo · Ctrl R riprova XMP · Ctrl ↵ conferma e scrivi |
| Numeri | 0-9 digita numero · ↵ conferma e avanti · Canc nessun numero |
| Etichettatura | 1 buona · 2 mossa · 3 occhi chiusi · 4 esposizione · 5 altro · Spazio salta |
| Persone | F2 rinomina · M unisci · I ignora · S separa · Canc "non è lei/lui" |
| Consultazione | Ctrl E Photoshop · Ctrl ↵ app predefinita · Ctrl Maiusc E Esplora risorse · T tag · S trova simili |
| Import | ↵ avanti · Alt ← indietro · Ctrl ↵ avvia subito con le impostazioni ricordate |

Le cifre cambiano significato per vista (stelle, numero, etichetta) e la barra di stato lo dichiara sempre.

## 13. Guida per Claude Code

Sviluppo per fasi, ognuna chiusa solo quando tutti i suoi criteri di accettazione sono soddisfatti; questa specifica vive nel repository in Markdown ed è il riferimento per ogni sessione.

### Regole di lavoro

| Regola | Dettaglio |
| --- | --- |
| Fonte di verità | Questo documento; una modifica a una decisione si propone, non si applica in silenzio |
| Invarianti prima | I test delle invarianti (sezione 10) si scrivono prima del codice che le riguarda |
| Percorsi | Solo tramite `IAppPaths` |
| Dipendenze | Ogni libreria esterna dietro un'interfaccia di `Core` |
| Modelli | Mai nel repository; solo manifest con URL e SHA-256 |
| Dati personali | Foto di test fuori dalla cartella del repository; controllo pre-commit che blocca immagini non autorizzate |
| Avalonia vs design | Se una soluzione del design non è realizzabile fedelmente, segnalarlo invece di approssimare |
| Build | Esecuzione dalla cartella di build in sviluppo; pacchetto Velopack solo alle versioni di riferimento |

### Fasi

| Fase | Contenuto | Dipende da |
| --- | --- | --- |
| 0 | Repository, struttura della soluzione, CI, script Python di benchmark ed export ONNX (D-FINE, verifica RAM++), test di parità di base | — |
| 1 | Sorgenti SD + cartella; gestori RAW/JPG; ingest con copia verificata; esposizione/nitidezza; rilevamento volti e occhi; politiche XMP; catalogo base; procedura di import e riepilogo con griglia; avanzamento per foto (`JobItems`) e ripresa dei job di import dopo chiusura forzata | 0 |
| 1b | Gestori TIFF/PNG/HEIC; estensione della ripresa ai job d'archivio in background (`AnalyzeInPlace`), più job in parallelo, pausa/ripresa; bassa priorità; hash rapido/completo | 1 |
| 2 | Embedding CLIP; raffiche e sequenze speciali; schermata Revisione completa; riconoscimento volti, clustering, schermata Persone; cifratura impronte; modalità etichettatura | 1 |
| 3 | Tagging A+B; vocabolario e traduzioni; Consultazione con ricerca FTS e linguaggio naturale; Duplicati | 2 |
| 4 | OCR, elenco iscritti, profilo Motorsport, vista "Numeri" | 3 |
| 5 | Rigenerazione completa con anteprima differenze; didascalie (C/D); esportazione/importazione catalogo; sincronizzazione XMP esterna | 3 |

Le parti senza interfaccia della fase 1 (ingest, metadati, analisi, catalogo) possono partire in parallelo al lavoro di Design.

### Primissima attività della fase 1

Verifica sui primi NEF della Z5 II: risoluzione dell'anteprima incorporata, decodificabilità dei NEF High Efficiency, disponibilità del punto AF tramite exiftool.

### Criteri di accettazione

**Fase 0**

- [ ] Soluzione compilabile con tutti i progetti e le interfacce di `Core`
- [ ] Benchmark Python eseguibile su una cartella di foto, con report dei tempi per fase
- [ ] D-FINE esportato in ONNX con test di parità superato

**Fase 1**

- [ ] Import di 1.000 NEF: zero file persi, tutti verificati
- [ ] Secondo import della stessa scheda: nessun duplicato
- [ ] Chiusura forzata a metà: ripresa dal punto di interruzione
- [ ] SD in sola lettura: nessun tentativo di scrittura o cancellazione (hash sorgente invariati)
- [ ] RAW invariati dopo la scrittura XMP
- [ ] XMP riletto correttamente da exiftool; checklist manuale in Esplora risorse e Photoshop/Camera Raw superata
- [ ] Set di foto mosse/occhi chiusi: percentuale di intercettazione misurata e riportata

**Fase 1b**

- [ ] Analisi di 10.000 foto miste interrotta e ripresa senza ripetere il lavoro
- [ ] File con estensione sbagliata gestiti dal gestore corretto
- [ ] File corrotti nel report, intatti

**Fase 2**

- [ ] Bracketing e timelapse mai classificati come raffiche nel set di test
- [ ] Revisione interamente utilizzabile da tastiera
- [ ] Chiave delle impronte rimossa: ricalcolo riuscito con assegnazioni preservate
- [ ] Metriche del culling calcolate dal set etichettato con margine di incertezza

**Fase 3**

- [ ] Ricerca < 1 s su 60.000 foto
- [ ] Keyword IT+EN e gerarchiche lette da Bridge
- [ ] Rianalisi: tag manuali e soppressi preservati

**Fase 4**

- [ ] Numeri riconosciuti solo se presenti in elenco; casi ambigui in "da rivedere"
- [ ] Equipaggi: solo numero nell'XMP

**Fase 5**

- [ ] Cambio modello volti: assegnazioni trasferite, nessuna persona persa
- [ ] Esportazione e importazione su altro percorso con rimappatura riuscita
- [ ] Modifiche XMP fatte in Bridge importate come manuali

## 14. Strategia di test

Sei livelli di test; quelli che usano foto personali girano solo in locale, e il repository pubblico funziona anche senza.

### Livelli

| Livello | Verifica | Esempi |
| --- | --- | --- |
| Unitari | Logica pura | Regole di decisione, raffiche, template percorsi, unione XMP |
| Parità | C# = modello originale Python | Embedding: somiglianza ≥ 0,999; tag: stesso insieme alle soglie ufficiali; riquadri: su due livelli (vedi sotto) |
| Integrazione | Componenti reali | exiftool, SQLite, "SD" simulata da cartella |
| Scenari | Condizioni avverse | 1.000 file; processo terminato a metà; NAS che sparisce; SD in sola lettura |
| Qualità | Metriche del culling (sezione 6) | Dal set etichettato |
| Prestazioni | Velocità per fase su GPU e CPU | Regressione oltre soglia rispetto alla versione precedente |

### Parità dei rilevatori (decisione del 12 ott 2026)

| Livello | Ingresso | Criterio |
| --- | --- | --- |
| Tensori | Tensore preparato da Python (float32 + JSON delle forme), identico per C# | Uscite grezze: \|Δ\| ≤ 1e-4 sui riquadri, ≤ 1e-3 sui logit (da confermare sul primo campione) |
| Rilevamenti | Immagine con pre-elaborazione C# | Abbinamento greedy per classe sopra soglia (0,5): ogni coppia IoU ≥ 0,95 e \|Δ punteggio\| ≤ 0,02; rilevamenti senza corrispettivo ammessi solo entro ±0,05 dalla soglia |

Riferimento: modello originale PyTorch con il preprocessore ufficiale. Immagini e riferimenti fuori dal repository (`PHOTOFLOW_TESTDATA`), report per immagine.

### Test delle invarianti

| Invariante | Verifica |
| --- | --- |
| Nessuna scrittura sulla sorgente | Hash di tutti i file prima e dopo ogni job |
| Nessuna cancellazione | Conteggio e confronto dei file |
| RAW mai modificati | Hash dei RAW prima e dopo la scrittura XMP |
| Nessuna fase prima della verifica | Iniezione di errori nella copia |

### Dati di test

| Set | Contenuto | Dove vive |
| --- | --- | --- |
| Formati | RAW di varie marche, TIFF 16 bit e multipagina, HEIC, PNG, file corrotti, estensioni sbagliate, foto senza EXIF | Repository pubblico (solo file con licenza libera verificata) |
| Subset d'archivio | 1.000-2.000 foto varie dell'archivio esistente, non etichettate: benchmark, formati, frequenza tag, comportamento clustering | Solo locale |
| Qualità | Foto etichettate dall'utente (obiettivo 300-500) | Solo locale |
| Riferimenti di parità | Output dei modelli originali | Generati in locale, versionati con il modello |

### Set di qualità ed etichettatura

Il set etichettato arriverà dopo l'inizio dell'uso: fino ad allora vale la modalità prudente (D20). Le correzioni in revisione alimentano il set ma sono distorte (le foto non controllate non producono etichette), quindi serve anche etichettatura esplicita.

| Funzione della modalità etichettatura | Specifica |
| --- | --- |
| Un tasto per foto | `1` buona, `2` mossa, `3` occhi chiusi, `4` esposizione, `5` altro, `Spazio` salta |
| Raffiche | Clic sulla migliore + conferma |
| Selezione intelligente | Prima le foto più utili: vicine alle soglie, incerte, profili diversi |
| Sessioni brevi | \~100 foto (\~15 min), interrompibili |
| Alla cieca | Verdetto del sistema nascosto (default per il set di misura) |
| Pannello metriche | Valori con margine di incertezza |

### Compatibilità XMP

Scrittura → rilettura con exiftool a ogni build; checklist manuale in Esplora risorse, Bridge, Photoshop/Camera Raw (e Lightroom se adottato) a ogni versione di riferimento.

## 15. Inventario licenze

Il progetto è Apache 2.0; l'unico componente non commerciale (buffalo\_l) è opzionale, separato e scaricato con accettazione esplicita.

### Librerie

| Componente | Licenza | Stato |
| --- | --- | --- |
| .NET, Avalonia, CommunityToolkit.Mvvm, SkiaSharp | MIT | Verificato |
| ONNX Runtime, ONNX Runtime GenAI | MIT | Verificato |
| Microsoft.Data.Sqlite / SQLite | MIT / pubblico dominio | Verificato |
| Dapper, Serilog, xUnit, OpenCvSharp | Apache 2.0 | Da conoscenza |
| Magick.NET | Apache 2.0; decoder HEIC (libheif, libde265) LGPL, usati come librerie dinamiche | Da verificare |
| exiftool | Licenza di Perl (Artistic/GPL), ridistribuibile | Da conoscenza |
| Velopack | MIT | Da verificare |
| [Florence2-Sharp](https://docs.curiosity.ai/florence2-sharp/) | MIT | Verificato |
| [RapidOcrNet](https://www.nuget.org/packages/rapidocrnet) | Codice: da verificare; modelli PaddleOCR: Apache 2.0 | Parziale |
| [HdbscanSharp](https://www.nuget.org/packages/HdbscanSharp) | Da verificare | Aperto |
| IBM Plex Sans / Plex Mono (font) | SIL Open Font License, incorporabile | Da design |
| PanAndZoom (Avalonia) | MIT | Da design, da verificare |
| Avalonia DataGrid, AvaloniaEdit (opzionale) | MIT | Da verificare |
| VirtualizingWrapPanel di terze parti (alternativa a ItemsRepeater) | Da verificare | Aperto, solo se adottato |

### Modelli

| Modello | Licenza | Uso commerciale |
| --- | --- | --- |
| [D-FINE](https://pypi.org/project/easydetect/) | Apache 2.0 (codice e pesi) | Sì |
| [RF-DETR](https://pypi.org/project/rfdetr-onnx/) Nano-Large | Apache 2.0 (XLarge/2XLarge: PML 1.0, esclusi) | Sì |
| [CLIP](https://huggingface.co/Xenova/clip-vit-base-patch32) | MIT | Sì |
| [RAM++ (ONNX int8)](https://huggingface.co/anakhiu/ram-plus-onnx-int8) | Apache 2.0 | Sì |
| [Florence-2](https://huggingface.co/onnx-community/Florence-2-large) | MIT | Sì |
| PaddleOCR | Apache 2.0 | Sì |
| Qwen2.5-VL | Varia per dimensione | Da verificare |
| Predittore estetico LAION | Da verificare | Da verificare |
| [InsightFace buffalo\_l](https://pypi.org/project/insightface/) | Solo ricerca non commerciale | No |
| YuNet (OpenCV Zoo) | MIT (LICENSE in opencv\_zoo, verificato il 12 ott 2026) | Sì |
| SFace (OpenCV Zoo) | Apache 2.0 (LICENSE in opencv\_zoo, verificato il 12 ott 2026) | Sì |
| YOLO Ultralytics | AGPL-3.0 | Escluso dal progetto |

## 16. Registro dei rischi

Il rischio principale è l'allargamento continuo dell'ambito; questa specifica e il controllo delle modifiche sono la mitigazione.

| # | Rischio | Prob. | Impatto | Mitigazione |
| --- | --- | --- | --- | --- |
| R1 | Allargamento continuo dell'ambito | Alta | Alto | Fasi con criteri di chiusura; nuove idee nel backlog salvo che cambino le fondamenta |
| R2 | Culling poco affidabile | Media | Alto | Modalità prudente, metriche misurate, invarianti |
| R3 | Anteprime Z5 II non a piena risoluzione o NEF High Efficiency non decodificabili | Media | Medio | Verifica come prima attività della fase 1; fallback di decodifica |
| R4 | Export ONNX non fedele (RAM++, D-FINE) | Bassa | Medio | Test di parità; alternative CLIP zero-shot e RF-DETR |
| R5 | Futuro di DirectML (spinta verso Windows ML) | Media | Medio | `IInferenceProvider`; build CUDA e CPU |
| R6 | Librerie piccole abbandonate | Media | Medio-basso | Interfacce; sostituzione o fork |
| R7 | Corruzione del catalogo | Bassa | Alto | Rollback in rete, lock, backup, integrità |
| R8 | Prestazioni insufficienti su sola CPU | Media | Basso | Livelli di tagging, esecuzione notturna |
| R9 | Foto personali nel repository pubblico | Bassa | Alto | Dati fuori dal repository, controllo pre-commit |
| R10 | Incompatibilità XMP con i software | Media | Medio | Politiche configurabili, checklist manuale |
| R11 | Errori del riconoscimento facciale | Media | Medio | Soglie che preferiscono dividere, revisione, campioni aggiornati |
| R12 | Cambio di licenza di un modello | Bassa | Medio | Versioni fissate con SHA-256, licenza registrata |
| R13 | exiftool bloccato o lento | Bassa | Medio | `-stay_open`, controllo di salute, riavvio automatico |
| R14 | Durata del progetto con impegno serale | Alta | Medio | Ogni fase produce un applicativo utilizzabile |

## 17. Backlog e punti da verificare

Decisioni emerse in Design (confermate), funzioni rinviate a dopo la stabilizzazione e verifiche tecniche da fare durante lo sviluppo.

### Decisioni emerse in Design (confermate il 12 ott 2026)

| # | Area | Decisione proposta | Nota |
| --- | --- | --- | --- |
| P1 | Revisione | P su una "Migliore" la conferma; su qualsiasi altra la rende "Tenuta" manuale. P nel Confronto = "questa è la migliore": le altre valide diventano Simili | Semantica di P diversa tra Griglia e Confronto |
| P2 | Revisione | Durante l'analisi P/X/stelle sono già accettati e prevalgono sulla proposta successiva | Coerente con "le azioni dell'utente prevalgono" |
| P3 | Revisione | Riaprire un job confermato è consentito: le modifiche riscrivono solo gli XMP interessati |  |
| P4 | XMP | Errore di scrittura (es. cartella in sola lettura): decisioni salvate nel catalogo, scrittura in sospeso con nuovo tentativo all'avvio, oppure "solo catalogo per questi" | Gli errori non bloccano il resto |
| P5 | Duplicati | Le copie non tenute si spostano in `_duplicati` nella stessa libreria, mai eliminate; gruppi "non duplicati" esclusi in seguito | Non applicabile a librerie in sola lettura |
| P6 | Revisione | Azione "Sposta scarti…" in una cartella dedicata, su richiesta esplicita | Coerente con I4 |
| P7 | Job | L'import ha precedenza sulla GPU: i job d'archivio rallentano mentre un import è in corso | Nuova regola di scheduling |
| P8 | Job | Destinazione scollegata durante la copia: job in pausa, con "Riprendi" o "Continua senza" quella destinazione |  |
| P9 | Import | Opzione "Tutte, incluse le già importate" oltre a "Solo nuove" (default) | Regola: le già importate presenti e integre nella destinazione (hash uguale) non vengono ricopiate; quelle mancanti o diverse vengono ricopiate e verificate. Mai sovrascritture |
| P10 | Import | Data dell'evento modificabile, proposta dagli scatti |  |
| P11 | Primo avvio | Procedura in 3 passi: hardware e benchmark, modelli con scelta della licenza volti (buffalo\_l o YuNet/SFace), prima libreria | La scelta YuNet/SFace dichiara che gli occhi chiusi non vengono rilevati |
| P12 | Persone | Campioni di riferimento per anno, rinnovati automaticamente con i volti confermati; ordinamento "prima i meno simili" nella pagina persona |  |
| P13 | Persone | Chiave impronte non disponibile: galleria consultabile, riconoscimento sospeso fino a password o ricalcolo | Coerente con D12 |
| P14 | Consultazione | Persone e tag sono filtri esatti; il testo libero ordina per significato; azione "Trova simili" |  |
| P15 | Vocabolario | Il nucleo si forma dopo almeno 1.000 foto con tag |  |
| P16 | Tagging | Didascalie VLM (livello D) disponibili solo con GPU ≥ 8 GB |  |
| P17 | Profili | Parametri esposti: scarto per mosso sotto soglia, intervallo massimo raffica (2,0 s), similarità minima (0,90), pari merito entro 0,02 | Valori iniziali da tarare |
| P18 | Impostazioni | Cambio lingua applicato al riavvio della finestra (non a caldo) | Semplifica la localizzazione |
| P19 | Catalogo | Estensione del file catalogo `.pfdb` |  |
| P20 | Import | Variabili del template percorsi: `{yyyy}` `{MM}` `{evento}` `{seq}` `{camera}` `{nomeoriginale}` `{ext}`; caratteri non validi sostituiti con "-" |  |

### Backlog post-stabilità

| Voce | Origine |
| --- | --- |
| Album (ed eventuale esportazione come keyword `Album\|Nome`) | D6 |
| Modalità portatile | D19 |
| Riconoscimento automatico dei panorami | Sezione 6 |
| Firma del codice | Sezione 11 |
| Mappa GPS | Sezione 1 |
| Catalogo su PostgreSQL (homelab) per uso multi-PC simultaneo | Sezione 9 |
| Cambio di politica XMP di una libreria: unione dei sidecar esistenti negli incorporati (o viceversa) | D21, fase 5 (sincronizzazione) |

### Verifiche tecniche

- [ ] Risoluzione dell'anteprima incorporata nei NEF della Z5 II
- [ ] Decodifica dei NEF High Efficiency (LibRaw) e scelta del binding .NET di LibRaw
- [ ] Punto AF Nikon leggibile con exiftool per la Z5 II
- [ ] Lettura dei sidecar/incorporati in Esplora risorse, Photoshop/Camera Raw, Bridge, Lightroom
- [ ] Stato di Velopack e di DirectML rispetto a Windows ML
- [ ] Licenze aperte: HdbscanSharp, codice RapidOcrNet, Qwen2.5-VL, predittore estetico, decoder HEIC in Magick.NET
- [ ] Perl disponibile sulle versioni recenti di macOS
- [ ] Riconoscimento di monoposto e prototipi da parte di D-FINE (COCO) sulle foto in pista
- [ ] Qualità di YuNet/SFace rispetto a buffalo\_l
- [ ] Tempi reali con il benchmark (sostituiscono le stime della sezione 5)

### Fonti consultate

[RAM++ ONNX int8](https://huggingface.co/anakhiu/ram-plus-onnx-int8) · [RAM++ ONNX export](https://huggingface.co/benjaminjonard/ram-plus-onnx) · [Florence-2 ONNX](https://huggingface.co/onnx-community/Florence-2-large) · [Florence2-Sharp](https://docs.curiosity.ai/florence2-sharp/) · [InsightFace](https://pypi.org/project/insightface/) · [InsightFace model zoo](https://github.com/deepinsight/insightface/blob/e68b8b076fb11e9f75c87244d234385f0f318181/model_zoo/README.md) · [ONNX Runtime GenAI multimodale C#](https://mintlify.wiki/microsoft/onnxruntime-genai/examples/csharp/multimodal) · [Qwen3-VL in ORT GenAI](https://github.com/microsoft/onnxruntime-genai/issues/1989) · [RapidOcrNet](https://www.nuget.org/packages/rapidocrnet) · [HdbscanSharp](https://www.nuget.org/packages/HdbscanSharp) · [CLIP ONNX](https://huggingface.co/Xenova/clip-vit-base-patch32) · [SQLite over a network](https://sqlite.org/useovernet.html) · [RF-DETR](https://pypi.org/project/rfdetr-onnx/) · [D-FINE (easydetect)](https://pypi.org/project/easydetect/) · [D-FINE vs YOLO11](https://playground.roboflow.com/models/compare/d-fine-vs-yolo11)
