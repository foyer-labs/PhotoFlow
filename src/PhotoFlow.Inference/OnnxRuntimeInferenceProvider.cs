// SPDX-License-Identifier: Apache-2.0

using Microsoft.ML.OnnxRuntime;
using PhotoFlow.Core.Inference;

namespace PhotoFlow.Inference;

/// <summary>
/// ONNX Runtime backend. Phase 0: CPU execution provider only; GPU providers and hardware
/// selection (CUDA → DirectML → CoreML → CPU) arrive in phase 1.
/// </summary>
public sealed class OnnxRuntimeInferenceProvider : IInferenceProvider
{
    public OnnxRuntimeInferenceProvider()
    {
        Detected = new HardwareInfo([ExecutionBackend.Cpu], Environment.ProcessorCount, GpuName: null, GpuMemoryBytes: null);
    }

    public HardwareInfo Detected { get; }

    public Task<IInferenceSession> CreateSessionAsync(ModelDescriptor model, ExecutionTarget target, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(model);
        if (target == ExecutionTarget.Gpu)
        {
            throw new NotSupportedException("GPU execution is not available yet (planned for phase 1).");
        }

        if (!File.Exists(model.FilePath))
        {
            throw new FileNotFoundException($"Model file for '{model.Id}' not found.", model.FilePath);
        }

        return Task.Run<IInferenceSession>(
            () =>
            {
                ct.ThrowIfCancellationRequested();
                using var options = new SessionOptions
                {
                    GraphOptimizationLevel = GraphOptimizationLevel.ORT_ENABLE_ALL,
                    ExecutionMode = ExecutionMode.ORT_SEQUENTIAL,
                };
                var session = new InferenceSession(model.FilePath, options);
                return new OnnxRuntimeSession(model, ExecutionBackend.Cpu, session);
            },
            ct);
    }
}
