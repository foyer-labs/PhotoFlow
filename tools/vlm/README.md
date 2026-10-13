# VLM conversion (level D, phase 5b)

Converts the original weights of **Qwen2.5-VL-7B-Instruct** (Apache-2.0, downloaded by the user from
<https://huggingface.co/Qwen/Qwen2.5-VL-7B-Instruct>) into the ONNX models of ONNX Runtime GenAI (int4 text decoder,
fp16 vision encoder, embedding) for CUDA. PhotoFlow runs it from Settings › Models with the Python found on the PC, in
a private virtual environment (decisions U2, U3 in `docs/plans/fase-5b.md`).

- `convert.py` — entry point: `python convert.py --weights <folder> --output <folder> --work <folder>`; prints
  `PF-PROGRESS <step>/<steps> <name>` lines and `PF-DONE` / `PF-ERROR <message>`.
- `requirements.txt` — pinned packages (CUDA 13 wheels).
- `recipe/` — Microsoft's Olive recipe for Qwen2.5-VL (MIT, `microsoft/olive-recipes@232aa122155f`,
  `Qwen-Qwen2.5-VL-3B-Instruct/builtin`): `optimize.py`, `user_script.py`, `codes/`, `cuda/*.json`. Changes in
  `user_script.py`: the model is read from `PF_QWEN_WEIGHTS` (the local folder) instead of the hub name; the size of
  the image features of the embedding export comes from the model (the recipe has the 3B value); the language weights,
  named `model.*` in the published checkpoint and `language_model.*` in the recipe's modeling code, are read under
  their checkpoint names (transformers would leave them at random with only a warning, and the embedding would be
  wrong). In `cuda/text.json` the text model is built with `exclude_embeds` (ONNX Runtime GenAI 0.17 needs the
  separate embedding model for Qwen2.5-VL).
- After the export `convert.py` checks that the embedding model gives the checkpoint's own vectors, and stops otherwise.
- `parity.py` — approximate parity (decision D5b-7, not shipped with the app): the converted model and the original
  PyTorch model caption the same photos, greedy; the report counts identical captions, tokens in common and times.

The files are only read from the weights folder; the output folder is created new.
