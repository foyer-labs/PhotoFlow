# Third-party notices

PhotoFlow is licensed under the Apache License 2.0 (see `LICENSE` and `NOTICE`). This file lists the third-party
components it uses, with their licenses. Versions are those of `Directory.Packages.props` and `build/ExifTool.targets`
at the time of writing; the review of R19 (statically linked LGPL libraries) is required before the first distributed
version (spec §16).

## Bundled with the application

| Component | Version | License | Notes |
| --- | --- | --- | --- |
| Avalonia, Avalonia.Desktop, Avalonia.Themes.Fluent | 12.1.4 | MIT | User interface |
| CommunityToolkit.Mvvm | 8.4.2 | MIT | View models |
| Microsoft.Extensions.DependencyInjection, Microsoft.Extensions.Logging | 10.0.12 | MIT | Composition, logging |
| Serilog, Serilog.Extensions.Logging, Serilog.Sinks.File | 4.4.0, 10.0.0, 7.0.0 | Apache-2.0 | Log files |
| SkiaSharp (Skia) | 3.119.4 | MIT (Skia: BSD-3-Clause) | JPEG decoding, thumbnails |
| Magick.NET-Q8-AnyCPU (ImageMagick) | 15.0.0 | Apache-2.0 (ImageMagick License) | Fallback RAW decoding. Magick.Native links **statically** LibRaw (LGPL-2.1 or CDDL-1.0), libheif and libde265 (LGPL-3.0): their sources are available from the upstream projects and on request; relinking terms to be reviewed (R19) |
| Microsoft.ML.OnnxRuntime.Gpu | 1.31.0 | MIT | Inference (CPU and CUDA execution providers) |
| Microsoft.ML.OnnxRuntimeGenAI.Cuda | 0.17.1 | MIT | Level D captions, only in the separate `PhotoFlow.Vlm` process (phase 5b, exception U4) |
| Olive recipe for Qwen2.5-VL (`microsoft/olive-recipes@232aa122155f`) | — | MIT (`tools/vlm/recipe/LICENSE`); `codes/modeling_qwen2_5_vl.py`: Apache-2.0 (Qwen Team, Hugging Face) | Conversion scripts shipped as `vlm/`, run with the Python of the user's PC |
| Microsoft.Data.Sqlite (SQLitePCLRaw, SQLite) | 10.0.12 | MIT (SQLitePCLRaw: Apache-2.0; SQLite: public domain) | Catalog |
| Dapper | 2.1.89 | Apache-2.0 | Catalog queries |
| System.IO.Hashing | 10.0.12 | MIT | xxHash3 verified copy |
| System.Security.Cryptography.ProtectedData | 10.0.12 | MIT | DPAPI wrapping of the face fingerprint key (Windows) |
| ExifTool by Phil Harvey | 13.59 | Artistic License or GPL (Perl terms) | Separate, unmodified executable shipped next to the application (`tools/exiftool`), run as an external process; the Windows build includes its own Perl runtime under the same terms |
| IBM Plex Sans, IBM Plex Mono | — | SIL Open Font License 1.1 | `src/PhotoFlow.App/Assets/Fonts/OFL.txt` |

## Downloaded at first use (never bundled)

| Component | License | Conditions |
| --- | --- | --- |
| NVIDIA CUDA runtime libraries (cudart, cuBLAS, cuFFT, cuRAND, NVRTC, nvJitLink) and cuDNN — the "GPU pack" (`models/gpu-runtime.json`) | NVIDIA CUDA Toolkit EULA, NVIDIA cuDNN Software License Agreement | Downloaded from NVIDIA's redistribution archives only after the user accepts the licenses in the first run; verified with SHA-256; kept unmodified with their `LICENSE.txt`; used only by PhotoFlow and only on NVIDIA GPUs |
| Python packages of the level D conversion (`tools/vlm/requirements.txt`: PyTorch, transformers, ONNX, ONNX Runtime, ONNX Runtime GenAI, Olive) | BSD-3-Clause, Apache-2.0, MIT | Installed by `pip` from PyPI and the PyTorch index into a private environment of PhotoFlow, only when the user starts the conversion; never bundled |
| Qwen2.5-VL-7B-Instruct original weights | Apache-2.0 | Downloaded by the user from Hugging Face; PhotoFlow verifies them (SHA-256) and converts them on the PC |
| Machine-learning models (`models/manifest.json`) | Per model, shown before download (Apache-2.0, MIT; InsightFace buffalo_l: non-commercial research only) | Downloaded with SHA-256 verification; licenses that require it (buffalo_l) are accepted explicitly before the download |

## Development only (not distributed)

xUnit v3 (Apache-2.0), Microsoft.NET.Test.Sdk (MIT), Avalonia.Headless.XUnit (MIT); the Python lab in `tools/lab`
(its own `pyproject.toml`, outputs outside the repository).
