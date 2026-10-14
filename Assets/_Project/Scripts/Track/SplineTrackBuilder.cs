using System.Collections.Generic;
using UnityEngine;

namespace QuickChecks.Track
{
    /// <summary>
    /// Takes a TrackDefinition and builds the runtime track GameObjects:
    ///   - Visual track surface (sliced sprite along the spline)
    ///   - Inner + outer boundary colliders (chain of BoxCollider2Ds)
    ///   - Checkpoints (trigger colliders at regular intervals)
    ///   - Finish line (trigger collider at t=0)
    ///
    /// Usage:
    ///   var builder = gameObject.AddComponent&lt;SplineTrackBuilder&gt;();
    ///   builder.Build(trackDefinition);
    /// </summary>
    public class SplineTrackBuilder : MonoBehaviour
    {
        [Header("Build Settings")]
        [Tooltip("Number of segments to subdivide the spline into when building boundary colliders. " +
                 "Higher = smoother curves but more colliders. 60 is a good default for 8-track games.")]
        [SerializeField] private int boundarySegmentCount = 60;

        [Tooltip("Number of checkpoints to place along the track. 6-12 is good per track.")]
        [SerializeField] private int checkpointCount = 8;

        [Header("Visual")]
        [SerializeField] private Color trackColor = new Color(0.106f, 0.165f, 0.165f, 1f);  // #1B2A2A
        [SerializeField] private Color borderColor = new Color(0.247f, 0.878f, 0.760f, 1f); // #3FE0C2
        [SerializeField] private Color finishLineColor = new Color(0.247f, 0.878f, 0.760f, 0.6f);
        [SerializeField] private int trackSortingOrder = 0;
        [SerializeField] private int borderSortingOrder = 1;
        [SerializeField] private int finishLineSortingOrder = 2;

        /// <summary>
        /// Builds the entire track as child GameObjects of this transform.
        /// Clears any existing children first.
        /// </summary>
        public TrackRuntimeData Build(TrackDefinition trackDefinition)
        {
            if (trackDefinition == null)
            {
                Debug.LogError("[SplineTrackBuilder] TrackDefinition is null!");
                return null;
            }

            if (!trackDefinition.Validate(out string error))
            {
                Debug.LogError($"[SplineTrackBuilder] Track '{trackDefinition.trackId}' is invalid: {error}");
                return null;
            }

            // Clear existing children
            for (int i = transform.childCount - 1; i >= 0; i--)
            {
                DestroyImmediate(transform.GetChild(i).gameObject);
            }

            var points = trackDefinition.splinePoints;
            bool closedLoop = true; // Tracks are closed loops for lap racing
            float halfWidth = trackDefinition.trackWidth * 0.5f;

            // Build visual track surface
            BuildTrackSurface(points, halfWidth, closedLoop);

            // Build inner + outer boundary colliders
            BuildBoundaries(points, halfWidth, closedLoop);

            // Build checkpoints
            var checkpoints = BuildCheckpoints(points, halfWidth, closedLoop, trackDefinition);

            // Build direction arrows (Week 6 polish — make track direction obvious)
            BuildDirectionArrows(points, halfWidth, closedLoop);

            // Build finish line
            var finishLine = BuildFinishLine(points, halfWidth, closedLoop);

            // Build power-up pickups (Week 5)
            BuildPowerUpPickups(points, halfWidth, closedLoop, trackDefinition);

            var data = new TrackRuntimeData
            {
                checkpoints = checkpoints,
                finishLine = finishLine,
                trackDefinition = trackDefinition
            };

            Debug.Log($"[SplineTrackBuilder] Built track '{trackDefinition.trackId}': " +
                      $"{BezierSpline.SegmentCount(points.Length, closedLoop)} segments, " +
                      $"{checkpoints.Count} checkpoints, " +
                      $"{BezierSpline.ApproximateLength(points, closedLoop):F1} units long.");
            return data;
        }

        private void BuildTrackSurface(IReadOnlyList<Vector2> points, float halfWidth, bool closedLoop)
        {
            var trackGo = new GameObject("TrackSurface");
            trackGo.transform.SetParent(transform, false);

            // Sample centerline + boundaries
            BezierSpline.SampleBoundaries(points, halfWidth, boundarySegmentCount, closedLoop,
                out var leftBoundary, out var rightBoundary);

            var meshFilter = trackGo.AddComponent<MeshFilter>();
            var meshRenderer = trackGo.AddComponent<MeshRenderer>();

            var mesh = new Mesh { name = "TrackSurface" };
            int vertexCount = boundarySegmentCount * 2;
            var vertices = new Vector3[vertexCount];
            var uvs = new Vector2[vertexCount];
            // 2 triangles per segment, 3 vertices per triangle = 6 indices per segment
            // For closed loop, we add one more segment to close the loop (last -> first)
            int segmentCount = closedLoop ? boundarySegmentCount : boundarySegmentCount - 1;
            var triangles = new int[segmentCount * 6];

            // Place vertices at z=1 (in front of camera at z=-10).
            // SpriteRenderer sortOrders handle 2D layering; mesh z is just to ensure visibility.
            for (int i = 0; i < boundarySegmentCount; i++)
            {
                vertices[i * 2]     = new Vector3(leftBoundary[i].x,  leftBoundary[i].y,  1f);
                vertices[i * 2 + 1] = new Vector3(rightBoundary[i].x, rightBoundary[i].y, 1f);
                uvs[i * 2]     = new Vector2(0, (float)i / (boundarySegmentCount - 1));
                uvs[i * 2 + 1] = new Vector2(1, (float)i / (boundarySegmentCount - 1));
            }

            // Build triangles. Winding: counter-clockwise when viewed from -Z (camera side)
            // so triangles face the camera. In Unity 2D, this means vertex order: left[i], right[i], left[i+1].
            int tri = 0;
            for (int i = 0; i < segmentCount; i++)
            {
                int next = (i + 1) % boundarySegmentCount;
                int v0 = i * 2;          // left[i]
                int v1 = i * 2 + 1;        // right[i]
                int v2 = next * 2;          // left[next]
                int v3 = next * 2 + 1;      // right[next]
                // Triangle 1: v0, v2, v1 (CCW from -Z)
                triangles[tri++] = v0;
                triangles[tri++] = v2;
                triangles[tri++] = v1;
                // Triangle 2: v1, v2, v3
                triangles[tri++] = v1;
                triangles[tri++] = v2;
                triangles[tri++] = v3;
            }

            mesh.vertices = vertices;
            mesh.uv = uvs;
            mesh.triangles = triangles;
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            meshFilter.sharedMesh = mesh;

            // URP Unlit shader works for both URP and fallback to Sprites/Default for built-in pipeline.
            var shader = Shader.Find("Universal Render Pipeline/Unlit");
            if (shader == null) shader = Shader.Find("Unlit/Color");
            if (shader == null) shader = Shader.Find("Sprites/Default");
            var mat = new Material(shader);
            mat.color = trackColor;
            meshRenderer.sharedMaterial = mat;
            meshRenderer.sortingOrder = trackSortingOrder;

            Debug.Log($"[SplineTrackBuilder] TrackSurface built: {vertexCount} vertices, " +
                      $"{segmentCount} quads, color={trackColor}.");
        }

        private void BuildBoundaries(IReadOnlyList<Vector2> points, float halfWidth, bool closedLoop)
        {
            var innerGo = new GameObject("Boundary_Inner");
            innerGo.transform.SetParent(transform, false);
            var outerGo = new GameObject("Boundary_Outer");
            outerGo.transform.SetParent(transform, false);

            // Sample boundaries
            BezierSpline.SampleBoundaries(points, halfWidth, boundarySegmentCount, closedLoop,
                out var leftBoundary, out var rightBoundary);

            // For each consecutive pair of points on each boundary, create a thin BoxCollider2D.
            // This forms a chain of colliders that approximates the curve.
            float colliderThickness = 0.2f;

            for (int i = 0; i < boundarySegmentCount - 1; i++)
            {
                // Inner boundary (left)
                CreateBoundarySegment(innerGo.transform, $"Inner_{i}",
                    leftBoundary[i], leftBoundary[i + 1], colliderThickness, borderColor);
                // Outer boundary (right)
                CreateBoundarySegment(outerGo.transform, $"Outer_{i}",
                    rightBoundary[i], rightBoundary[i + 1], colliderThickness, borderColor);
            }
        }

        private void CreateBoundarySegment(Transform parent, string name, Vector2 p1, Vector2 p2, float thickness, Color color)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);

            Vector2 mid = (p1 + p2) * 0.5f;
            Vector2 delta = p2 - p1;
            float length = delta.magnitude;
            float angle = Mathf.Atan2(delta.y, delta.x) * Mathf.Rad2Deg;

            go.transform.position = mid;
            go.transform.rotation = Quaternion.Euler(0, 0, angle);
            go.transform.localScale = Vector3.one;

            var col = go.AddComponent<BoxCollider2D>();
            col.size = new Vector2(length, thickness);

            // Optional: tiny sprite to make boundary visible (debug)
            // Skip in production — track border is drawn by track surface mesh
        }

        private List<Checkpoint> BuildCheckpoints(IReadOnlyList<Vector2> points, float halfWidth, bool closedLoop, TrackDefinition trackDefinition)
        {
            var container = new GameObject("Checkpoints");
            container.transform.SetParent(transform, false);

            var checkpoints = new List<Checkpoint>(checkpointCount);
            float trackWidth = trackDefinition.trackWidth;

            for (int i = 0; i < checkpointCount; i++)
            {
                // Place checkpoints evenly between t=0 and t=1, EXCLUDING both endpoints.
                // t=0 is the finish line (handled separately).
                // t=1 wraps to t=0 on closed loops (same position as finish line).
                // So we use: t = (i + 1) / (checkpointCount + 1)
                // For 8 checkpoints: t = 1/9, 2/9, 3/9, 4/9, 5/9, 6/9, 7/9, 8/9
                // This avoids placing a checkpoint at the finish line position.
                float t = (float)(i + 1) / (checkpointCount + 1);
                Vector2 center = BezierSpline.EvaluateSpline(points, t, closedLoop);
                Vector2 tangent = BezierSpline.EvaluateSplineTangent(points, t, closedLoop);
                Vector2 normal = new Vector2(-tangent.y, tangent.x);

                float angle = Mathf.Atan2(tangent.y, tangent.x) * Mathf.Rad2Deg;

                var go = new GameObject($"Checkpoint_{i}");
                go.transform.SetParent(container.transform, false);
                go.transform.position = center;
                go.transform.rotation = Quaternion.Euler(0, 0, angle);
                go.transform.localScale = Vector3.one;

                var col = go.AddComponent<BoxCollider2D>();
                col.size = new Vector2(0.5f, trackWidth);  // Thin in tangent direction, full track width perpendicular
                col.isTrigger = true;

                var checkpoint = go.AddComponent<Checkpoint>();
                checkpoint.index = i;
                checkpoint.requiredCount = checkpointCount;

                Debug.Log($"[SplineTrackBuilder] Checkpoint {i} at t={t:F3} -> world pos ({center.x:F1}, {center.y:F1})");

                // Visible debug sprite — color cycles through hue so checkpoints are distinguishable.
                // Index 0 = green, index 1 = yellow, index 2 = orange, etc.
                var sr = go.AddComponent<SpriteRenderer>();
                float hue = (float)i / checkpointCount;
                sr.color = Color.HSVToRGB(hue, 0.6f, 1f);  // Visible but not glaring
                sr.sprite = CreateSquareSprite();
                sr.drawMode = SpriteDrawMode.Sliced;
                sr.size = new Vector2(0.5f, trackWidth);
                sr.sortingOrder = 5;  // Above track surface

                // Optional: add a TextMeshPro component to show index above the checkpoint.
                // For prototype, the hue cycling + the [Checkpoint N] log is enough.

                checkpoints.Add(checkpoint);
            }

            return checkpoints;
        }

        private GameObject BuildFinishLine(IReadOnlyList<Vector2> points, float halfWidth, bool closedLoop)
        {
            Vector2 center = BezierSpline.EvaluateSpline(points, 0f, closedLoop);
            Vector2 tangent = BezierSpline.EvaluateSplineTangent(points, 0f, closedLoop);
            float angle = Mathf.Atan2(tangent.y, tangent.x) * Mathf.Rad2Deg;
            float trackWidth = halfWidth * 2f;

            var go = new GameObject("FinishLine");
            go.transform.SetParent(transform, false);
            go.transform.position = center;
            go.transform.rotation = Quaternion.Euler(0, 0, angle);
            go.transform.localScale = Vector3.one;

            var col = go.AddComponent<BoxCollider2D>();
            col.size = new Vector2(0.8f, trackWidth);  // Thicker (0.8 vs 0.5) for visibility
            col.isTrigger = true;

            try { go.tag = "FinishLine"; }
            catch (System.Exception) { /* tag not defined */ }

            // Visual: bright cyan-green, more opaque than before (was 0.4 alpha — easy to miss)
            var sr = go.AddComponent<SpriteRenderer>();
            sr.color = new Color(0.247f, 0.878f, 0.760f, 0.9f);  // Bright cyan-green, 90% alpha
            sr.sprite = CreateSquareSprite();
            sr.drawMode = SpriteDrawMode.Sliced;
            sr.size = new Vector2(0.8f, trackWidth);
            sr.sortingOrder = finishLineSortingOrder;

            // Add "START / FINISH" text label next to the line for clarity
            CreateFinishLineLabel(go, center, tangent);

            return go;
        }

        private void CreateFinishLineLabel(GameObject parent, Vector2 center, Vector2 tangent)
        {
            var labelGo = new GameObject("FinishLineLabel");
            labelGo.transform.SetParent(parent.transform, false);

            // Position the label just past the finish line in the direction of travel
            Vector2 labelOffset = tangent.normalized * 3f;
            labelGo.transform.position = new Vector3(center.x + labelOffset.x, center.y + labelOffset.y, -0.5f);

            // Rotate label to face along the tangent direction
            float angle = Mathf.Atan2(tangent.y, tangent.x) * Mathf.Rad2Deg;
            labelGo.transform.rotation = Quaternion.Euler(0, 0, angle);

            // Try TextMeshPro (3D world-space text), fall back gracefully
            try
            {
                var text = labelGo.AddComponent<TMPro.TextMeshPro>();
                text.text = "START / FINISH";
                text.fontSize = 4;
                text.alignment = TMPro.TextAlignmentOptions.Center;
                text.color = new Color(0.247f, 0.878f, 0.760f, 1f);  // bright cyan
                text.sortingOrder = 10;
            }
            catch (System.Exception ex)
            {
                Debug.LogWarning("[SplineTrackBuilder] Failed to create finish line label: " + ex.Message);
            }
        }

        /// <summary>
        /// Places direction arrows along the track centerline at regular intervals.
        /// Arrows point in the direction of travel (along the spline tangent).
        /// Helps players see which way the track goes, especially on closed loops.
        /// </summary>
        private void BuildDirectionArrows(IReadOnlyList<Vector2> points, float halfWidth, bool closedLoop)
        {
            var container = new GameObject("DirectionArrows");
            container.transform.SetParent(transform, false);

            int arrowCount = 8;  // 8 arrows evenly spaced around the track
            int placed = 0;

            for (int i = 0; i < arrowCount; i++)
            {
                float t = (float)i / arrowCount;
                Vector2 center = BezierSpline.EvaluateSpline(points, t, closedLoop);
                Vector2 tangent = BezierSpline.EvaluateSplineTangent(points, t, closedLoop);
                float angle = Mathf.Atan2(tangent.y, tangent.x) * Mathf.Rad2Deg;

                var go = new GameObject($"Arrow_{i}");
                go.transform.SetParent(container.transform, false);
                go.transform.position = new Vector3(center.x, center.y, 0.5f);  // slightly in front of track surface
                go.transform.rotation = Quaternion.Euler(0, 0, angle);
                go.transform.localScale = Vector3.one;

                // Visual: triangle pointing right (will be rotated to match tangent)
                var sr = go.AddComponent<SpriteRenderer>();
                sr.color = new Color(0.247f, 0.878f, 0.760f, 0.5f);  // cyan, 50% alpha (subtle)
                sr.sprite = CreateTriangleSprite();
                sr.drawMode = SpriteDrawMode.Sliced;
                sr.size = new Vector2(1f, 1f);
                sr.sortingOrder = 3;  // Above track surface (0), below checkpoints (5)

                placed++;
            }

            Debug.Log($"[SplineTrackBuilder] Placed {placed} direction arrows.");
        }

        private Sprite _cachedTriangleSprite;
        private Sprite CreateTriangleSprite()
        {
            if (_cachedTriangleSprite == null)
            {
                // Create a simple right-pointing triangle texture
                const int size = 64;
                var texture = new Texture2D(size, size);
                var pixels = new Color32[size * size];
                // Default: transparent
                var transparent = new Color32(0, 0, 0, 0);
                var white = new Color32(255, 255, 255, 255);
                for (int i = 0; i < pixels.Length; i++) pixels[i] = transparent;

                // Draw a right-pointing triangle: apex at right, base at left
                for (int y = 0; y < size; y++)
                {
                    for (int x = 0; x < size; x++)
                    {
                        // Triangle vertices: (8, 8), (8, 55), (55, 32) — apex at right
                        float dy1 = y - 8;
                        float dy2 = y - 55;
                        float dx = x - 8;
                        float apexDy = y - 32;

                        // Inside triangle if: x >= 8 AND between the two slanted edges
                        if (x >= 8 && x <= 55)
                        {
                            // Left edge: y = 8 to apex y = 32, slope = (32-8)/(55-8) = 0.51
                            // Right edge: y = 55 to apex y = 32, slope = (32-55)/(55-8) = -0.49
                            float progress = (x - 8f) / (55f - 8f);  // 0 at left, 1 at apex
                            float topY = 8f + (32f - 8f) * progress;
                            float botY = 55f + (32f - 55f) * progress;
                            if (y >= topY && y <= botY)
                            {
                                pixels[y * size + x] = white;
                            }
                        }
                    }
                }
                texture.SetPixels32(pixels);
                texture.Apply();
                texture.filterMode = FilterMode.Point;
                _cachedTriangleSprite = Sprite.Create(texture, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), size);
                _cachedTriangleSprite.name = "DirectionArrowTriangle";
            }
            return _cachedTriangleSprite;
        }

        private void BuildPowerUpPickups(IReadOnlyList<Vector2> points, float halfWidth, bool closedLoop, TrackDefinition trackDefinition)
        {
            if (trackDefinition.powerUpSpawns == null || trackDefinition.powerUpSpawns.Length == 0)
            {
                Debug.Log("[SplineTrackBuilder] No power-up spawns defined for this track.");
                return;
            }

            var container = new GameObject("PowerUps");
            container.transform.SetParent(transform, false);

            int placed = 0;
            foreach (var spawn in trackDefinition.powerUpSpawns)
            {
                if (spawn.powerUp == null)
                {
                    Debug.LogWarning($"[SplineTrackBuilder] Power-up spawn at t={spawn.t} has no powerUp assigned. Skipping.");
                    continue;
                }

                // Compute position: along spline at t, offset by lateralOffset along the normal
                Vector2 center = BezierSpline.EvaluateSpline(points, spawn.t, closedLoop);
                Vector2 tangent = BezierSpline.EvaluateSplineTangent(points, spawn.t, closedLoop);
                Vector2 normal = new Vector2(-tangent.y, tangent.x);
                Vector2 spawnPos = center + normal * spawn.lateralOffset;

                var go = new GameObject($"PowerUp_{spawn.powerUp.powerUpId}_t{spawn.t:F2}");
                go.transform.SetParent(container.transform, false);
                go.transform.position = spawnPos;
                go.transform.localScale = Vector3.one;

                // Add visual sprite (colored circle would be nice; for prototype use square)
                var sr = go.AddComponent<SpriteRenderer>();
                sr.color = spawn.powerUp.effectColor;
                sr.sprite = CreateSquareSprite();
                sr.drawMode = SpriteDrawMode.Sliced;
                sr.size = new Vector2(1f, 1f);  // 1x1 unit pickup
                sr.sortingOrder = 8;  // Above track surface (0), above checkpoints (5)

                // Add trigger collider
                var col = go.AddComponent<CircleCollider2D>();
                col.radius = 0.6f;  // Pickup radius
                col.isTrigger = true;

                // Add PowerUpPickup component
                var pickup = go.AddComponent<Racing.PowerUpPickup>();
                pickup.powerUp = spawn.powerUp;
                pickup.oneTimeUse = spawn.oneTimeUse;

                placed++;
            }

            Debug.Log($"[SplineTrackBuilder] Placed {placed} power-up pickups.");
        }

        private Sprite _cachedSquareSprite;
        private Sprite CreateSquareSprite()
        {
            if (_cachedSquareSprite == null)
            {
                const int size = 64;
                var texture = new Texture2D(size, size);
                var pixels = texture.GetPixels();
                for (int i = 0; i < pixels.Length; i++) pixels[i] = Color.white;
                texture.SetPixels(pixels);
                texture.Apply();
                texture.filterMode = FilterMode.Point;
                _cachedSquareSprite = Sprite.Create(
                    texture,
                    new Rect(0, 0, size, size),
                    new Vector2(0.5f, 0.5f),
                    size
                );
                _cachedSquareSprite.name = "SplineTrackBuilderSquare";
            }
            return _cachedSquareSprite;
        }
    }

    /// <summary>
    /// Runtime references to the built track elements. Returned by SplineTrackBuilder.Build()
    /// so callers can wire up checkpoint triggers, finish line detection, etc.
    /// </summary>
    public class TrackRuntimeData
    {
        public List<Checkpoint> checkpoints;
        public GameObject finishLine;
        public TrackDefinition trackDefinition;
    }
}
