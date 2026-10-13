// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.ML.OnnxRuntimeGenAI;

// Level D captions (phase 5b, decision U4): Qwen2.5-VL-7B (ONNX Runtime GenAI, CUDA) in a process of its own.
// Protocol, one JSON object per line (UTF-8):
//   start:    PhotoFlow.Vlm --model <folder>   → {"type":"ready"} or {"type":"error","message":...}
//   request:  {"id":1,"image":"<jpeg>","prompt":"...","maxTokens":128}
//   answer:   {"id":1,"text":"...","tokens":n,"ms":t} or {"id":1,"error":"..."}
//   "quit" or end of input closes the process.
Console.InputEncoding = Encoding.UTF8;
Console.OutputEncoding = new UTF8Encoding(false);

// Held for the whole process: its disposal shuts the library down after every model is released (otherwise the
// process crashes on exit).
using var oga = new OgaHandle();
var modelPath = args.SkipWhile(a => a != "--model").Skip(1).FirstOrDefault();
if (modelPath is null || !Directory.Exists(modelPath))
{
    Write(new JsonObject { ["type"] = "error", ["message"] = "Usage: PhotoFlow.Vlm --model <folder>" });
    return 2;
}

Model model;
Tokenizer tokenizer;
MultiModalProcessor processor;
try
{
    model = new Model(modelPath);
    tokenizer = new Tokenizer(model);
    processor = new MultiModalProcessor(model);
}
catch (Exception e) when (e is OnnxRuntimeGenAIException or IOException or DllNotFoundException or BadImageFormatException)
{
    Write(new JsonObject { ["type"] = "error", ["message"] = e.Message });
    return 3;
}

Write(new JsonObject { ["type"] = "ready" });
string? line;
while ((line = Console.ReadLine()) is not null && line != "quit")
{
    long id = 0;
    try
    {
        var request = JsonNode.Parse(line)!;
        id = request["id"]!.GetValue<long>();
        var image = request["image"]!.GetValue<string>();
        var prompt = request["prompt"]!.GetValue<string>();
        var maxTokens = request["maxTokens"]?.GetValue<int>() ?? 128;
        var clock = Stopwatch.StartNew();
        var (text, tokens) = Caption(model, tokenizer, processor, image, prompt, maxTokens);
        Write(new JsonObject { ["id"] = id, ["text"] = text, ["tokens"] = tokens, ["ms"] = clock.ElapsedMilliseconds });
    }
    catch (Exception e) when (e is OnnxRuntimeGenAIException or IOException or JsonException or InvalidOperationException or NullReferenceException or FormatException)
    {
        Write(new JsonObject { ["id"] = id, ["error"] = e.Message });
    }
}

processor.Dispose();
tokenizer.Dispose();
model.Dispose();
return 0;

// Greedy generation on one image with Qwen2.5-VL's chat format. The KV cache is sized for MaxLength tokens: an image of
// at most 1024 px is at most ~1,370 image tokens, plus the prompt and the caption.
static (string Text, int Tokens) Caption(Model model, Tokenizer tokenizer, MultiModalProcessor processor, string imagePath, string prompt, int maxTokens)
{
    var chat = "<|im_start|>system\nYou are a helpful assistant.<|im_end|>\n"
        + "<|im_start|>user\n<|vision_start|><|image_pad|><|vision_end|>" + prompt + "<|im_end|>\n"
        + "<|im_start|>assistant\n";
    using var images = Images.Load([imagePath]);
    using var inputs = processor.ProcessImages(chat, images);
    using var parameters = new GeneratorParams(model);
    parameters.SetSearchOption("do_sample", false);
    parameters.SetSearchOption("max_length", MaxLength);
    using var generator = new Generator(model, parameters);
    generator.SetInputs(inputs);
    using var stream = tokenizer.CreateStream();
    var text = new StringBuilder();
    var count = 0;
    while (count < maxTokens)
    {
        generator.GenerateNextToken();
        if (generator.IsDone())
        {
            break;
        }

        text.Append(stream.Decode(generator.GetNextTokens()[0]));
        count++;
    }

    return (text.ToString().Trim(), count);
}

static void Write(JsonObject message)
{
    Console.Out.WriteLine(message.ToJsonString());
    Console.Out.Flush();
}

internal static partial class Program
{
    private const int MaxLength = 2048;
}
