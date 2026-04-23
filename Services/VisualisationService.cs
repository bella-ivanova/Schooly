using System.Diagnostics;
using System.Text.RegularExpressions;

namespace StudyAssistant.Services;

public static class VisualisationService
{
    private static string? _lastHtmlPath;

    // ── Public instruction appended to the RAG prompt for stereometry questions ──
    public const string GeomInstruction =
        "At the end of your response, append a JSON block wrapped in <GEOM> and </GEOM> tags " +
        "describing the 3D shape or spatial relationship in the problem. Do not explain the JSON. " +
        "The JSON format is:\n" +
        "{\n" +
        "  \"shape\": \"cube\" | \"cuboid\" | \"pyramid\" | \"cone\" | \"cylinder\" | \"sphere\" | \"tetrahedron\" | \"prism\" | \"custom\",\n" +
        "  \"dimensions\": { ... shape-specific numeric dimensions ... },\n" +
        "  \"labels\": [ { \"text\": \"A\", \"position\": [x, y, z] }, ... ],\n" +
        "  \"relationships\": [ { \"type\": \"perpendicular\" | \"parallel\" | \"skew\" | \"intersection\", \"elements\": [\"label1\", \"label2\"] }, ... ]\n" +
        "}\n" +
        "For a cube with side 5: {\"shape\":\"cube\",\"dimensions\":{\"side\":5},\"labels\":[{\"text\":\"A\",\"position\":[0,0,0]},{\"text\":\"B\",\"position\":[5,0,0]},{\"text\":\"C\",\"position\":[5,5,0]},{\"text\":\"D\",\"position\":[0,5,0]},{\"text\":\"E\",\"position\":[0,0,5]},{\"text\":\"F\",\"position\":[5,0,5]},{\"text\":\"G\",\"position\":[5,5,5]},{\"text\":\"H\",\"position\":[0,5,5]}]}\n" +
        "Only include labels and relationships if they are relevant to the problem.";

    // ── Extracts the JSON between <GEOM> and </GEOM> tags ──────────────────────
    public static string? ExtractGeomJson(string llmResponse)
    {
        var match = Regex.Match(llmResponse, @"<GEOM>([\s\S]*?)</GEOM>", RegexOptions.IgnoreCase);
        return match.Success ? match.Groups[1].Value.Trim() : null;
    }

    // ── Writes the temp HTML file and opens it in the default browser ──────────
    public static void ShowVisualisation(string geomJson)
    {
        // Prevent </script> injection from closing the script tag early
        var safeJson = geomJson.Replace("</", "<\\/");

        var html = HtmlTemplate.Replace("__GEOM_JSON__", safeJson);
        var path = Path.Combine(
            Path.GetTempPath(),
            $"schooly_vis_{DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()}.html"
        );

        File.WriteAllText(path, html);
        TempFileManager.RegisterTempFile(path);
        _lastHtmlPath = path;

        Process.Start(new ProcessStartInfo(path) { UseShellExecute = true });
    }

    // ── Re-opens the last visualisation (used by /visualise command) ───────────
    public static void ShowLast()
    {
        if (_lastHtmlPath != null && File.Exists(_lastHtmlPath))
            Process.Start(new ProcessStartInfo(_lastHtmlPath) { UseShellExecute = true });
        else
            Console.WriteLine("[No visualisation available yet.]");
    }

    // ══════════════════════════════════════════════════════════════════════════
    // HTML TEMPLATE
    // Uses Three.js r128 + OrbitControls from jsdelivr (cdnjs does not host the examples folder).
    // __GEOM_JSON__ is replaced by the extracted JSON before writing to disk.
    // ══════════════════════════════════════════════════════════════════════════
    private const string HtmlTemplate = """
        <!DOCTYPE html>
        <html lang="en">
        <head>
        <meta charset="UTF-8">
        <title>Schooly 3D Visualiser</title>
        <style>
          * { margin: 0; padding: 0; box-sizing: border-box; }
          body { background: #1a1a2e; overflow: hidden; }
          #hint {
            position: fixed; bottom: 16px; left: 50%;
            transform: translateX(-50%);
            color: rgba(180,190,220,0.45);
            font-family: sans-serif; font-size: 13px;
            pointer-events: none; user-select: none;
          }
        </style>
        </head>
        <body>
        <div id="hint">Drag to rotate &nbsp;·&nbsp; Scroll to zoom &nbsp;·&nbsp; Right-drag to pan</div>

        <script src="https://cdn.jsdelivr.net/npm/three@0.128.0/build/three.min.js"></script>
        <script src="https://cdn.jsdelivr.net/npm/three@0.128.0/examples/js/controls/OrbitControls.js"></script>
        <script>
        (function () {
          const GEOM = __GEOM_JSON__;

          // ── Renderer ────────────────────────────────────────────────────────
          const renderer = new THREE.WebGLRenderer({ antialias: true });
          renderer.setSize(innerWidth, innerHeight);
          renderer.setPixelRatio(Math.min(devicePixelRatio, 2));
          document.body.appendChild(renderer.domElement);

          // ── Scene ───────────────────────────────────────────────────────────
          const scene = new THREE.Scene();
          scene.background = new THREE.Color(0x1a1a2e);

          // ── Camera ──────────────────────────────────────────────────────────
          const camera = new THREE.PerspectiveCamera(45, innerWidth / innerHeight, 0.01, 2000);

          // ── Controls ────────────────────────────────────────────────────────
          const controls = new THREE.OrbitControls(camera, renderer.domElement);
          controls.enableDamping  = true;
          controls.dampingFactor  = 0.06;
          controls.minDistance    = 0.5;
          controls.maxDistance    = 500;

          // ── Lighting ────────────────────────────────────────────────────────
          scene.add(new THREE.AmbientLight(0xaaccff, 0.9));
          const sun = new THREE.DirectionalLight(0xffffff, 0.85);
          sun.position.set(5, 10, 7);
          scene.add(sun);
          const fill = new THREE.DirectionalLight(0x334488, 0.35);
          fill.position.set(-5, -3, -5);
          scene.add(fill);

          // ── Geometry builder ────────────────────────────────────────────────
          function buildGeometry(shape, dim) {
            const d = dim || {};
            switch ((shape || '').toLowerCase()) {
              case 'cube':
                return new THREE.BoxGeometry(d.side || 1, d.side || 1, d.side || 1);
              case 'cuboid':
              case 'rectangular prism':
              case 'parallelepiped':
                return new THREE.BoxGeometry(
                  d.width  || d.w || d.a || 1,
                  d.height || d.h || d.b || 1,
                  d.depth  || d.d || d.c || 1
                );
              case 'sphere':
                return new THREE.SphereGeometry(d.radius || d.r || 1, 48, 32);
              case 'cone':
                return new THREE.ConeGeometry(d.radius || d.r || 1, d.height || d.h || 2, 64);
              case 'cylinder':
                return new THREE.CylinderGeometry(
                  d.radius || d.r || 1,
                  d.radius || d.r || 1,
                  d.height || d.h || 2,
                  64
                );
              case 'pyramid': {
                // Square pyramid: ConeGeometry with 4 radial segments.
                // BoxGeometry side a → circumradius = a * sqrt(2) / 2
                const base = d.base || d.a || d.side || 2;
                return new THREE.ConeGeometry(
                  (base / 2) * Math.SQRT2,
                  d.height || d.h || 2,
                  4
                );
              }
              case 'tetrahedron': {
                // Edge length a → circumradius R = a * sqrt(6) / 4
                const edge = d.edge || d.a || d.side || 2;
                return new THREE.TetrahedronGeometry(edge * Math.sqrt(6) / 4);
              }
              case 'prism': {
                const sides = d.sides || d.n || 6;
                const r = d.radius || d.r || (d.base
                  ? d.base / (2 * Math.sin(Math.PI / sides))
                  : 1);
                return new THREE.CylinderGeometry(r, r, d.height || d.h || 2, sides);
              }
              default:
                return new THREE.BoxGeometry(2, 2, 2);
            }
          }

          // ── Build main shape ─────────────────────────────────────────────────
          const geometry = buildGeometry(GEOM.shape, GEOM.dimensions);
          geometry.computeBoundingSphere();
          const R = geometry.boundingSphere.radius || 1;

          // Translucent solid fill
          scene.add(new THREE.Mesh(geometry, new THREE.MeshPhongMaterial({
            color:       0x3a7bd5,
            transparent: true,
            opacity:     0.22,
            side:        THREE.DoubleSide,
            depthWrite:  false,
          })));

          // Crisp wireframe edges
          scene.add(new THREE.LineSegments(
            new THREE.WireframeGeometry(geometry),
            new THREE.LineBasicMaterial({ color: 0x6aadff })
          ));

          // ── Camera initial position (isometric-ish) ─────────────────────────
          const dist = R * 3.8;
          camera.position.set(dist * 0.85, dist * 0.7, dist * 0.85);
          camera.lookAt(0, 0, 0);
          controls.update();

          // ── Subtle reference grid ────────────────────────────────────────────
          const gridSz = Math.max(10, R * 4);
          scene.add(new THREE.GridHelper(gridSz, 10, 0x2a2a4a, 0x212136));

          // ── Label sprite factory ─────────────────────────────────────────────
          function makeLabel(text) {
            const cw = 96, ch = 48;
            const canvas = document.createElement('canvas');
            canvas.width = cw; canvas.height = ch;
            const ctx = canvas.getContext('2d');

            // Background pill
            ctx.fillStyle = 'rgba(18,18,44,0.8)';
            ctx.fillRect(3, 3, cw - 6, ch - 6);
            ctx.strokeStyle = 'rgba(100,160,255,0.55)';
            ctx.lineWidth = 1.5;
            ctx.strokeRect(3, 3, cw - 6, ch - 6);

            // Text
            ctx.fillStyle   = '#d8ecff';
            ctx.font        = 'bold 26px Arial, sans-serif';
            ctx.textAlign   = 'center';
            ctx.textBaseline = 'middle';
            ctx.fillText(text, cw / 2, ch / 2);

            const sprite = new THREE.Sprite(new THREE.SpriteMaterial({
              map:        new THREE.CanvasTexture(canvas),
              depthTest:  false,
              sizeAttenuation: true,
            }));
            sprite.scale.set(R * 0.55, R * 0.28, 1);
            return sprite;
          }

          // ── Place vertex labels ──────────────────────────────────────────────
          // Label positions from the LLM use an arbitrary origin (e.g. [0,0,0]).
          // We compute their bounding-box centroid and subtract it so labels align
          // with the geometry, which Three.js always centres at the world origin.
          const labelMap = {};   // label text → THREE.Vector3 (centred)

          if (GEOM.labels && GEOM.labels.length > 0) {
            const pts = GEOM.labels.map(function(l) { return l.position; });
            const xs = pts.map(function(p) { return p[0]; });
            const ys = pts.map(function(p) { return p[1]; });
            const zs = pts.map(function(p) { return p[2]; });
            const cx = (Math.min.apply(null,xs) + Math.max.apply(null,xs)) / 2;
            const cy = (Math.min.apply(null,ys) + Math.max.apply(null,ys)) / 2;
            const cz = (Math.min.apply(null,zs) + Math.max.apply(null,zs)) / 2;

            for (var i = 0; i < GEOM.labels.length; i++) {
              var lbl = GEOM.labels[i];
              var v = new THREE.Vector3(
                lbl.position[0] - cx,
                lbl.position[1] - cy,
                lbl.position[2] - cz
              );
              labelMap[lbl.text] = v;

              var sprite = makeLabel(lbl.text);
              // Nudge the label slightly outward from the shape centre
              var nudge = v.clone().normalize().multiplyScalar(R * 0.22);
              sprite.position.copy(v).add(nudge);
              scene.add(sprite);
            }
          }

          // ── Relationship lines ───────────────────────────────────────────────
          var REL_COLORS = {
            perpendicular: 0xff4444,
            parallel:      0x44aaff,
            skew:          0xff8800,
            intersection:  0xffee00,
          };

          // Resolve a label name to a position.
          // Supports single labels ("A") and edge names ("AB" → midpoint of A and B).
          function resolvePos(name) {
            if (labelMap[name]) return labelMap[name].clone();
            if (name.length === 2) {
              var a = labelMap[name[0]], b = labelMap[name[1]];
              if (a && b) return a.clone().add(b).multiplyScalar(0.5);
            }
            return null;
          }

          var legendItems = [];

          if (GEOM.relationships) {
            for (var ri = 0; ri < GEOM.relationships.length; ri++) {
              var rel    = GEOM.relationships[ri];
              var color  = REL_COLORS[rel.type] !== undefined ? REL_COLORS[rel.type] : 0xffffff;
              var elems  = rel.elements || [];

              if (elems.length >= 2) {
                var p1 = resolvePos(elems[0]);
                var p2 = resolvePos(elems[1]);
                if (p1 && p2) {
                  var geo = new THREE.BufferGeometry().setFromPoints([p1, p2]);
                  scene.add(new THREE.Line(geo, new THREE.LineBasicMaterial({ color: color })));
                }
              }

              // Collect unique types for the legend
              var alreadyInLegend = false;
              for (var li = 0; li < legendItems.length; li++) {
                if (legendItems[li].type === rel.type) { alreadyInLegend = true; break; }
              }
              if (!alreadyInLegend) legendItems.push({ type: rel.type, color: color });
            }
          }

          // ── Relationship legend (top-right) ──────────────────────────────────
          if (legendItems.length > 0) {
            var leg = document.createElement('div');
            leg.style.cssText =
              'position:fixed;top:12px;right:14px;background:rgba(18,18,44,0.85);' +
              'padding:8px 14px;border-radius:8px;font-family:sans-serif;' +
              'font-size:13px;color:#aac;line-height:1.7;';
            var legHtml = '';
            for (var k = 0; k < legendItems.length; k++) {
              var hex = '#' + legendItems[k].color.toString(16).padStart(6,'0');
              legHtml += '<div><span style="color:' + hex + ';font-size:16px">&#9632;</span> ' +
                         legendItems[k].type + '</div>';
            }
            leg.innerHTML = legHtml;
            document.body.appendChild(leg);
          }

          // ── Shape + dimensions badge (top-left) ──────────────────────────────
          var badge = document.createElement('div');
          badge.style.cssText =
            'position:fixed;top:12px;left:14px;background:rgba(18,18,44,0.85);' +
            'padding:6px 14px;border-radius:8px;font-family:sans-serif;' +
            'font-size:14px;color:#88bbff;';
          var shapeName = (GEOM.shape || 'shape').replace(/\b\w/g, function(c) { return c.toUpperCase(); });
          var dimParts = [];
          if (GEOM.dimensions) {
            var dimKeys = Object.keys(GEOM.dimensions);
            for (var di = 0; di < dimKeys.length; di++) {
              dimParts.push(dimKeys[di] + ' = ' + GEOM.dimensions[dimKeys[di]]);
            }
          }
          badge.textContent = dimParts.length > 0
            ? shapeName + '  (' + dimParts.join(', ') + ')'
            : shapeName;
          document.body.appendChild(badge);

          // ── Resize handler ───────────────────────────────────────────────────
          window.addEventListener('resize', function() {
            camera.aspect = innerWidth / innerHeight;
            camera.updateProjectionMatrix();
            renderer.setSize(innerWidth, innerHeight);
          });

          // ── Render loop ──────────────────────────────────────────────────────
          (function loop() {
            requestAnimationFrame(loop);
            controls.update();
            renderer.render(scene, camera);
          })();
        })();
        </script>
        </body>
        </html>
        """;
}
