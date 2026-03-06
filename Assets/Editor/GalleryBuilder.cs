#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;

/// <summary>
/// Procedurally builds the VR Art Gallery geometry.
/// Run via menu: Gallery > Build VR Gallery
///
/// Building specs:
///   100 ft x 100 ft floor (30.48m x 30.48m)
///   16 ft (4.877m) eave height on side walls
///   22 ft (6.706m) ridge height at center peak
///   Gable roof (ridge runs front-to-back along Z axis)
///   Front wall (+Z face) = gable end with large roll-up door opening
///   Back wall (-Z face) = gable end, no openings
///   Side walls (±X faces) = rectangular, no windows
///   4 skylights: 2 on left roof panel, 2 on right roof panel
///   7 wood trusses spaced ~5m apart (no interior columns)
///   White concrete walls, gray roof
/// </summary>
public static class GalleryBuilder
{
    // ── Dimensions (meters) ──────────────────────────────────────────────────
    const float FEET  = 0.3048f;
    const float W     = 100 * FEET;   // 30.48m  building width  (X axis)
    const float D     = 100 * FEET;   // 30.48m  building depth  (Z axis)
    const float EH    = 16  * FEET;   // 4.877m  eave / wall height
    const float RH    = 22  * FEET;   // 6.706m  ridge height
    const float WT    = 0.305f;       // wall thickness  (~12 in concrete panel)
    const float RT    = 0.18f;        // roof panel thickness

    // Roll-up door (front gable wall, centered)
    const float DOOR_W = 20 * FEET;   // 6.096m  (20 ft)
    const float DOOR_H = 14 * FEET;   // 4.267m  (14 ft)

    // Truss beam cross-section
    const float BW = 0.20f;   // beam width  (~8 in)
    const float BT = 0.30f;   // beam thickness (~12 in)

    // Derived (computed properties so const math stays readable)
    static float HW          => W  / 2f;
    static float HD          => D  / 2f;
    static float RISE        => RH - EH;
    static float SLOPE_LEN   => Mathf.Sqrt(HW * HW + RISE * RISE);
    static float SLOPE_DEG   => Mathf.Atan2(RISE, HW) * Mathf.Rad2Deg;

    // ── Entry point ──────────────────────────────────────────────────────────

    [MenuItem("Gallery/Add Exterior Ground")]
    static void AddExteriorGround()
    {
        var existing = GameObject.Find("Exterior Ground");
        if (existing != null)
        {
            if (!EditorUtility.DisplayDialog("Add Exterior Ground",
                    "A GameObject named 'Exterior Ground' already exists. Replace it?", "Replace", "Cancel"))
                return;
            Undo.DestroyObjectImmediate(existing);
        }

        var mat = MakeMaterial("Mat_Ground", new Color(0.35f, 0.38f, 0.30f)); // muted green-gray
        var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
        go.name = "Exterior Ground";
        go.transform.localPosition = new Vector3(0f, -0.05f, 0f);
        go.transform.localScale    = new Vector3(300f, 0.10f, 300f);
        SetMat(go, mat);
        Undo.RegisterCreatedObjectUndo(go, "Add Exterior Ground");
        Selection.activeGameObject = go;
        Debug.Log("[GalleryBuilder] Exterior ground added (300m x 300m).");
    }

    [MenuItem("Gallery/Add Floor")]
    static void AddFloor()
    {
        var existing = GameObject.Find("Floor");
        if (existing != null)
        {
            if (!EditorUtility.DisplayDialog("Add Floor",
                    "A GameObject named 'Floor' already exists. Replace it?", "Replace", "Cancel"))
                return;
            Undo.DestroyObjectImmediate(existing);
        }

        var floorMat = MakeMaterial("Mat_Floor", new Color(0.55f, 0.55f, 0.55f));
        var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
        go.name = "Floor";
        go.transform.localPosition = new Vector3(0, -0.05f, 0);
        go.transform.localScale    = new Vector3(W, 0.10f, D);
        SetMat(go, floorMat);
        Undo.RegisterCreatedObjectUndo(go, "Add Floor");
        Selection.activeGameObject = go;
        Debug.Log("[GalleryBuilder] Floor added.");
    }

    [MenuItem("Gallery/Build VR Gallery")]
    static void Build()
    {
        var existing = GameObject.Find("VR Gallery");
        if (existing != null)
        {
            if (!EditorUtility.DisplayDialog("Rebuild Gallery",
                    "Destroy the existing gallery and rebuild?", "Rebuild", "Cancel"))
                return;
            Undo.DestroyObjectImmediate(existing);
        }

        var root = new GameObject("VR Gallery");
        Undo.RegisterCreatedObjectUndo(root, "Build VR Gallery");

        // ── Materials ──
        var wallMat      = MakeMaterial("Mat_Wall",      new Color(0.93f, 0.93f, 0.93f)); // white concrete
        var roofMat      = MakeMaterial("Mat_Roof",      new Color(0.70f, 0.70f, 0.72f)); // gray
        var floorMat     = MakeMaterial("Mat_Floor",     new Color(0.55f, 0.55f, 0.55f)); // dark gray
        var woodMat      = MakeMaterial("Mat_Wood",      new Color(0.52f, 0.34f, 0.14f)); // warm brown
        var skylightMat  = MakeGlassMaterial("Mat_Skylight", new Color(0.70f, 0.88f, 1.00f, 0.25f));

        // ── Geometry ──
        BuildFloor(root, floorMat);
        BuildSideWalls(root, wallMat);
        BuildBackGableWall(root, wallMat);
        BuildFrontGableWall(root, wallMat);
        BuildRoofPanels(root, roofMat);
        BuildSkylights(root, roofMat, skylightMat);
        BuildTrusses(root, woodMat);
        BuildSunLights(root);

        Selection.activeGameObject = root;
        SceneView.FrameLastActiveSceneView();
        Debug.Log($"[GalleryBuilder] Gallery built. {W:F2}m × {D:F2}m, ridge = {RH:F2}m");
    }

    // ── Floor ────────────────────────────────────────────────────────────────

    static void BuildFloor(GameObject root, Material mat)
    {
        var go = Cube(root, "Floor");
        go.transform.localPosition = new Vector3(0, -0.05f, 0);
        go.transform.localScale    = new Vector3(W, 0.10f, D);
        SetMat(go, mat);
    }

    // ── Side walls (rectangular, ±X faces) ──────────────────────────────────

    static void BuildSideWalls(GameObject root, Material mat)
    {
        MakeSideWall(root, mat, -HW, "Wall_Left");
        MakeSideWall(root, mat, +HW, "Wall_Right");
    }

    static void MakeSideWall(GameObject root, Material mat, float x, string name)
    {
        var go = Cube(root, name);
        go.transform.localPosition = new Vector3(x, EH / 2f, 0f);
        go.transform.localScale    = new Vector3(WT, EH, D);
        SetMat(go, mat);
    }

    // ── Back gable wall (no door) ────────────────────────────────────────────

    static void BuildBackGableWall(GameObject root, Material mat)
    {
        float z = -HD;

        // Rectangular section (full width, eave height)
        var rect = Cube(root, "Wall_Back_Rect");
        rect.transform.localPosition = new Vector3(0f, EH / 2f, z);
        rect.transform.localScale    = new Vector3(W, EH, WT);
        SetMat(rect, mat);

        // Triangular gable prism above eave
        var gable = MakeGablePrism(root, "Wall_Back_Gable", mat);
        gable.transform.localPosition = new Vector3(0f, EH, z);
    }

    // ── Front gable wall (with roll-up door opening) ─────────────────────────

    static void BuildFrontGableWall(GameObject root, Material mat)
    {
        float z   = HD;
        float dw2 = DOOR_W / 2f;
        float hw2 = HW;

        // Left panel (left of door opening)
        float leftW = hw2 - dw2;
        if (leftW > 0.01f)
        {
            var lp = Cube(root, "Wall_Front_Left");
            lp.transform.localPosition = new Vector3(-(dw2 + leftW / 2f), EH / 2f, z);
            lp.transform.localScale    = new Vector3(leftW, EH, WT);
            SetMat(lp, mat);
        }

        // Right panel (right of door opening)
        float rightW = hw2 - dw2;
        if (rightW > 0.01f)
        {
            var rp = Cube(root, "Wall_Front_Right");
            rp.transform.localPosition = new Vector3(dw2 + rightW / 2f, EH / 2f, z);
            rp.transform.localScale    = new Vector3(rightW, EH, WT);
            SetMat(rp, mat);
        }

        // Header strip above door (door_height → eave_height)
        float headerH = EH - DOOR_H;
        if (headerH > 0.02f)
        {
            var hdr = Cube(root, "Wall_Front_Header");
            hdr.transform.localPosition = new Vector3(0f, DOOR_H + headerH / 2f, z);
            hdr.transform.localScale    = new Vector3(DOOR_W, headerH, WT);
            SetMat(hdr, mat);
        }

        // Triangular gable prism above eave
        var gable = MakeGablePrism(root, "Wall_Front_Gable", mat);
        gable.transform.localPosition = new Vector3(0f, EH, z);
        // Flip 180° so outward-facing normals face +Z (exterior)
        gable.transform.localRotation = Quaternion.Euler(0f, 180f, 0f);
    }

    // ── Triangular gable prism ───────────────────────────────────────────────

    static GameObject MakeGablePrism(GameObject root, string name, Material mat)
    {
        var go = new GameObject(name);
        go.transform.SetParent(root.transform, false);

        var mesh = BuildTriPrismMesh(W, RISE, WT);
        go.AddComponent<MeshFilter>().sharedMesh       = mesh;
        go.AddComponent<MeshRenderer>().sharedMaterial = mat;
        go.AddComponent<MeshCollider>().sharedMesh     = mesh;
        return go;
    }

    /// <summary>
    /// Isosceles triangular prism.
    /// Base runs along X from –baseW/2 to +baseW/2 at local Y = 0.
    /// Peak at (0, height, 0).
    /// Extruded from Z = –depth/2 to Z = +depth/2.
    /// Front face (Z = –depth/2) normals point –Z; back face point +Z.
    /// </summary>
    static Mesh BuildTriPrismMesh(float baseW, float height, float depth)
    {
        float hb = baseW  / 2f;
        float hd = depth  / 2f;

        // 6 corner vertices
        Vector3 fBL = new Vector3(-hb,    0f,      -hd);  // front bottom-left
        Vector3 fBR = new Vector3(+hb,    0f,      -hd);  // front bottom-right
        Vector3 fTP = new Vector3(  0f,   height,  -hd);  // front top-peak
        Vector3 bBL = new Vector3(-hb,    0f,      +hd);  // back  bottom-left
        Vector3 bBR = new Vector3(+hb,    0f,      +hd);  // back  bottom-right
        Vector3 bTP = new Vector3(  0f,   height,  +hd);  // back  top-peak

        // Each face uses its own duplicate vertices so RecalculateNormals works correctly.
        // Winding: clockwise when viewed from the face's outward normal direction.
        var verts = new Vector3[]
        {
            // Front face (normal –Z):  CW from front = fBL, fTP, fBR
            fBL, fTP, fBR,

            // Back face  (normal +Z):  CW from back  = bBL, bBR, bTP
            bBL, bBR, bTP,

            // Bottom face (normal –Y): bBL, fBL, fBR, bBR
            bBL, fBL, fBR, bBR,

            // Left slope  (normal upper-left):  fBL, bBL, bTP, fTP
            fBL, bBL, bTP, fTP,

            // Right slope (normal upper-right): fBR, fTP, bTP, bBR
            fBR, fTP, bTP, bBR,
        };

        var tris = new int[]
        {
            0,  1,  2,          // front tri
            3,  4,  5,          // back  tri
            6,  7,  8,          // bottom quad (tri 1)
            6,  8,  9,          // bottom quad (tri 2)
            10, 11, 12,         // left slope  (tri 1)
            10, 12, 13,         // left slope  (tri 2)
            14, 15, 16,         // right slope (tri 1)
            14, 16, 17,         // right slope (tri 2)
        };

        var mesh = new Mesh { name = "TriPrism" };
        mesh.vertices  = verts;
        mesh.triangles = tris;
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
        return mesh;
    }

    // ── Roof panels ──────────────────────────────────────────────────────────

    static void BuildRoofPanels(GameObject root, Material mat)
    {
        MakeRoofPanel(root, mat, false);  // left  (–X side)
        MakeRoofPanel(root, mat, true);   // right (+X side)
    }

    static void MakeRoofPanel(GameObject root, Material mat, bool isRight)
    {
        string name  = isRight ? "Roof_Right" : "Roof_Left";
        float  sign  = isRight ? 1f : -1f;
        float  angle = sign * (-SLOPE_DEG);   // left panel tilts +SLOPE_DEG, right tilts –SLOPE_DEG

        // Center point on the slope (midway between eave and ridge)
        float cosA = Mathf.Cos(SLOPE_DEG * Mathf.Deg2Rad);
        float sinA = Mathf.Sin(SLOPE_DEG * Mathf.Deg2Rad);

        // Eave at (±HW, EH); ridge at (0, RH). Panel center = midpoint of that hypotenuse.
        float cx = sign * (HW - (SLOPE_LEN / 2f) * cosA);
        float cy = EH  +         (SLOPE_LEN / 2f) * sinA;

        var go = Cube(root, name);
        go.transform.localPosition = new Vector3(cx, cy, 0f);
        go.transform.localScale    = new Vector3(SLOPE_LEN, RT, D + WT * 2f);
        go.transform.localRotation = Quaternion.Euler(0f, 0f, angle);
        SetMat(go, mat);
    }

    // ── Skylights (2 per roof panel) ─────────────────────────────────────────

    static void BuildSkylights(GameObject root, Material frameMat, Material glassMat)
    {
        // Z positions for the two skylights on each panel (1/3 and 2/3 of depth)
        float[] zPos = { -HD * 0.5f, HD * 0.5f };

        // Slope fractions along the panel from eave (0) toward ridge (1)
        float tSkylight = 0.40f;  // 40% up from eave

        float cosA = Mathf.Cos(SLOPE_DEG * Mathf.Deg2Rad);
        float sinA = Mathf.Sin(SLOPE_DEG * Mathf.Deg2Rad);

        // Skylight dimensions on the roof surface
        const float SL_ALONG = 2.5f;   // along-slope width  (~8 ft)
        const float SL_RIDGE = 4.0f;   // along-ridge length (~13 ft)
        const float FRAME_T  = 0.12f;  // skylight curb height

        for (int side = 0; side < 2; side++)
        {
            bool  isRight = side == 1;
            float sign    = isRight ? 1f : -1f;
            float panelAngle = sign * (-SLOPE_DEG);

            // World position of skylight center on the roof surface
            float offset = tSkylight * SLOPE_LEN;
            float cx     = sign * (HW - offset * cosA);
            float cy     = EH  +       offset * sinA;

            for (int k = 0; k < 2; k++)
            {
                float z     = zPos[k];
                string side_name = isRight ? "R" : "L";

                // Frame (slightly proud of roof surface — RT/2 pushes it above the panel center)
                var frame = Cube(root, $"Skylight_{side_name}{k + 1}_Frame");
                frame.transform.localPosition = new Vector3(cx, cy, z);
                frame.transform.localScale    = new Vector3(SL_ALONG + 0.2f, FRAME_T, SL_RIDGE + 0.2f);
                frame.transform.localRotation = Quaternion.Euler(0f, 0f, panelAngle);
                SetMat(frame, frameMat);

                // Glass pane (thin, transparent, same rotation)
                var glass = Cube(root, $"Skylight_{side_name}{k + 1}_Glass");
                // Offset slightly above frame surface along the roof normal
                float normalX = -sign * sinA;
                float normalY =          cosA;
                glass.transform.localPosition = new Vector3(
                    cx + normalX * FRAME_T,
                    cy + normalY * FRAME_T,
                    z);
                glass.transform.localScale    = new Vector3(SL_ALONG, 0.02f, SL_RIDGE);
                glass.transform.localRotation = Quaternion.Euler(0f, 0f, panelAngle);
                SetMat(glass, glassMat);
            }
        }
    }

    // ── Trusses ──────────────────────────────────────────────────────────────

    static void BuildTrusses(GameObject root, Material mat)
    {
        const int COUNT = 7;
        for (int i = 0; i < COUNT; i++)
        {
            float z = -HD + i * (D / (COUNT - 1));
            CreateTruss(root, mat, z, $"Truss_{i + 1:D2}");
        }
    }

    static void CreateTruss(GameObject root, Material mat, float z, string name)
    {
        var g = new GameObject(name);
        g.transform.SetParent(root.transform, false);

        // Chord and rafter endpoints
        Vector3 eaveL  = new Vector3(-HW,  EH,  z);
        Vector3 eaveR  = new Vector3(+HW,  EH,  z);
        Vector3 ridge  = new Vector3(  0f, RH,  z);
        Vector3 mid    = new Vector3(  0f, EH,  z);  // king-post foot

        // Queen-post attachment points at X = ±HW/2 on bottom chord and rafter
        float qx = HW / 2f;
        Vector3 queenBL = new Vector3(-qx, EH, z);
        Vector3 queenBR = new Vector3(+qx, EH, z);
        Vector3 queenRL = new Vector3(-qx, RafterY(-qx), z);
        Vector3 queenRR = new Vector3(+qx, RafterY(+qx), z);

        // Bottom chord
        AddBeam(g, mat, "BottomChord", eaveL, eaveR, BW, BT);

        // Left & right rafters
        AddBeam(g, mat, "Rafter_L", eaveL, ridge, BW, BT);
        AddBeam(g, mat, "Rafter_R", ridge, eaveR, BW, BT);

        // King post (center vertical)
        AddBeam(g, mat, "KingPost", mid, ridge, BW, BT);

        // Queen posts (vertical struts from bottom chord to rafter)
        AddBeam(g, mat, "QueenPost_L", queenBL, queenRL, BW * 0.85f, BT * 0.85f);
        AddBeam(g, mat, "QueenPost_R", queenBR, queenRR, BW * 0.85f, BT * 0.85f);

        // Diagonal braces: from king-post foot to queen-post tops (Fink truss pattern)
        AddBeam(g, mat, "Diag_L", mid, queenRL, BW * 0.7f, BT * 0.7f);
        AddBeam(g, mat, "Diag_R", mid, queenRR, BW * 0.7f, BT * 0.7f);
    }

    /// <summary>Y coordinate on the rafter at a given X position.</summary>
    static float RafterY(float x)
    {
        float t = (x + HW) / W;   // 0 at left eave, 1 at right eave
        if (t <= 0.5f)
            return EH + t * 2f * RISE;
        else
            return RH - (t - 0.5f) * 2f * RISE;
    }

    /// <summary>
    /// Creates a stretched cube as a beam from <paramref name="start"/> to <paramref name="end"/>.
    /// The cube's local Z axis is aligned along the beam direction.
    /// </summary>
    static void AddBeam(GameObject parent, Material mat, string name,
                        Vector3 start, Vector3 end, float width, float thickness)
    {
        Vector3 dir    = end - start;
        float   length = dir.magnitude;
        if (length < 0.001f) return;

        // Avoid gimbal lock when beam is vertical
        Vector3 up = (Mathf.Abs(Vector3.Dot(dir.normalized, Vector3.up)) > 0.99f)
            ? Vector3.right
            : Vector3.up;

        var go = Cube(parent, name);
        go.transform.localPosition = (start + end) * 0.5f;
        go.transform.localScale    = new Vector3(width, thickness, length);
        go.transform.localRotation = Quaternion.LookRotation(dir.normalized, up);
        SetMat(go, mat);
    }

    // ── Sunlight through skylights ────────────────────────────────────────────

    static void BuildSunLights(GameObject root)
    {
        // One spot light per skylight pointing downward to simulate daylight
        float cosA   = Mathf.Cos(SLOPE_DEG * Mathf.Deg2Rad);
        float sinA   = Mathf.Sin(SLOPE_DEG * Mathf.Deg2Rad);
        float tSkylightWorld = 0.40f * SLOPE_LEN;
        float[] zPos = { -HD * 0.5f, HD * 0.5f };

        for (int side = 0; side < 2; side++)
        {
            float sign = side == 1 ? 1f : -1f;
            float cx   = sign * (HW - tSkylightWorld * cosA);
            float cy   = EH  +       tSkylightWorld * sinA;

            for (int k = 0; k < 2; k++)
            {
                var lightGo = new GameObject($"SkylightLight_{(side == 1 ? "R" : "L")}{k + 1}");
                lightGo.transform.SetParent(root.transform, false);
                lightGo.transform.localPosition = new Vector3(cx, cy - 0.1f, zPos[k]);
                lightGo.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);

                var light = lightGo.AddComponent<Light>();
                light.type      = LightType.Spot;
                light.spotAngle = 80f;
                light.range     = RH + 2f;
                light.intensity = 1.5f;
                light.color     = new Color(1.0f, 0.97f, 0.88f);  // warm daylight
            }
        }
    }

    // ── Material helpers ─────────────────────────────────────────────────────

    static Material MakeMaterial(string matName, Color color)
    {
        var shader = Shader.Find("Universal Render Pipeline/Lit");
        if (shader == null) shader = Shader.Find("Standard");  // BIRP fallback

        var mat = new Material(shader) { name = matName };
        mat.SetColor("_BaseColor", color);
        mat.SetColor("_Color",     color);  // Standard shader compat
        return mat;
    }

    static Material MakeGlassMaterial(string matName, Color color)
    {
        var shader = Shader.Find("Universal Render Pipeline/Lit");
        if (shader == null) shader = Shader.Find("Standard");

        var mat = new Material(shader) { name = matName };
        mat.SetColor("_BaseColor", color);
        mat.SetColor("_Color",     color);

        // URP transparent surface
        mat.SetFloat("_Surface",  1f);   // 1 = Transparent
        mat.SetFloat("_Blend",    0f);   // Alpha blend
        mat.SetFloat("_AlphaClip", 0f);
        mat.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
        mat.SetOverrideTag("RenderType", "Transparent");
        mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");

        // Standard shader compat
        mat.SetFloat("_Mode", 3f);
        mat.EnableKeyword("_ALPHABLEND_ON");

        return mat;
    }

    // ── GameObject helpers ───────────────────────────────────────────────────

    static GameObject Cube(GameObject parent, string name)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
        go.name = name;
        go.transform.SetParent(parent.transform, false);
        return go;
    }

    static void SetMat(GameObject go, Material mat)
    {
        var mr = go.GetComponent<MeshRenderer>();
        if (mr != null) mr.sharedMaterial = mat;
    }
}
#endif
