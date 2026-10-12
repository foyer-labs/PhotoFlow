// SPDX-License-Identifier: Apache-2.0

using PhotoFlow.Core.Catalog;
using PhotoFlow.Core.Imaging;

namespace PhotoFlow.Core.Analysis;

/// <summary>Face detection with keypoints and, when the model has them, eye contours (decision A3: interface per function).</summary>
public interface IFaceDetector
{
    string ModelVersion { get; }

    /// <summary>True when the model provides eye contours (buffalo_l); false for 5-point models (YuNet: eyes always unknown, A5).</summary>
    bool ProvidesEyeContours { get; }

    Task<IReadOnlyList<DetectedFace>> DetectAsync(RgbImage image, CancellationToken ct);
}

/// <summary>Box in pixels of the analyzed image.</summary>
public readonly record struct PixelBox(float X1, float Y1, float X2, float Y2)
{
    public float Width => X2 - X1;

    public float Height => Y2 - Y1;

    public BoundingBox ToNormalized(int imageWidth, int imageHeight) =>
        new(X1 / imageWidth, Y1 / imageHeight, X2 / imageWidth, Y2 / imageHeight);
}

/// <summary>
/// A detected face, coordinates in pixels of the analyzed image. <paramref name="Keypoints"/>: left eye, right eye, nose,
/// left and right mouth corner ("left" = smaller x in the image). <paramref name="Landmarks"/>: the model's dense landmarks.
/// </summary>
public sealed record DetectedFace(
    PixelBox Box,
    float Score,
    IReadOnlyList<Point2> Keypoints,
    IReadOnlyList<Point2>? Landmarks,
    EyeContour? LeftEye,
    EyeContour? RightEye);

/// <summary>
/// Eye outline from dense landmarks, model independent: <see cref="Points"/> holds 8 points in the order
/// corner A, lower lid ×3 (from A to B), corner B, upper lid ×3 (from B to A).
/// </summary>
public sealed record EyeContour(IReadOnlyList<Point2> Points)
{
    /// <summary>Eye aspect ratio: mean distance between facing lid points divided by the corner distance.</summary>
    public double AspectRatio
    {
        get
        {
            var p = Points;
            var width = Distance(p[0], p[4]);
            if (width <= 0)
            {
                return 0;
            }

            return (Distance(p[1], p[7]) + Distance(p[2], p[6]) + Distance(p[3], p[5])) / (3 * width);
        }
    }

    /// <summary>Corner-to-corner width in pixels.</summary>
    public double Width => Distance(Points[0], Points[4]);

    private static double Distance(Point2 a, Point2 b) => Math.Sqrt(((a.X - b.X) * (a.X - b.X)) + ((a.Y - b.Y) * (a.Y - b.Y)));
}
