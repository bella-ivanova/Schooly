namespace StudyAssistant.Services;

public static class StereometryHtmlBuilder
{
    public static string Build(string sceneJson)
    {
        // Prevent </script> in JSON values from closing the script tag early.
        var safeJson = sceneJson.Replace("</", "<\\/");
        return HtmlTemplate.Replace("__SCENE_JSON__", safeJson);
    }

    // ══════════════════════════════════════════════════════════════════════════
    // HTML TEMPLATE
    // Uses Three.js r128 (no OrbitControls — rotation is handled manually via
    // a pivot THREE.Group so the model spins and the camera stays fixed).
    // __SCENE_JSON__ is replaced by the extracted JSON before writing to disk.
    //
    // SCENE JSON contract:
    //   vertices — { "A":[x,y,z], ... }
    //   edges    — [["A","B"], ...]
    //   faces    — [{ pts, color, opacity, label }]
    //   helpers  — [{ kind:"dashed"|"line"|"dot"|"arc"|"right_angle", ...fields }]
    //   angles   — [{ face, value, color }]   ← drives legend + toggle buttons
    //   camera   — { rotX, rotY, zoom }
    // ══════════════════════════════════════════════════════════════════════════
    private const string HtmlTemplate = """
        <!DOCTYPE html>
        <html lang="en">
        <head>
        <meta charset="UTF-8">
        <title>Schooly 3D</title>
        <style>
          * { margin:0; padding:0; box-sizing:border-box; }
          body { background:#1a1a2e; overflow:hidden; }
        </style>
        </head>
        <body>
        <script src="https://cdn.jsdelivr.net/npm/three@0.128.0/build/three.min.js"></script>
        <script>
        (function () {
          const SCENE = __SCENE_JSON__;

          // ── Vertex map ──────────────────────────────────────────────────────
          const V = {};
          for (const [k, p] of Object.entries(SCENE.vertices || {}))
            V[k] = new THREE.Vector3(p[0], p[1], p[2]);

          const vList = Object.values(V);
          const centroid = new THREE.Vector3();
          vList.forEach(v => centroid.add(v));
          if (vList.length) centroid.divideScalar(vList.length);

          let maxDist = 1;
          vList.forEach(v => { const d = v.distanceTo(centroid); if (d > maxDist) maxDist = d; });
          const baseDist = Math.max(maxDist * 3.8, 6);

          // ── Renderer ────────────────────────────────────────────────────────
          const renderer = new THREE.WebGLRenderer({ antialias: true });
          renderer.setSize(innerWidth, innerHeight);
          renderer.setPixelRatio(Math.min(devicePixelRatio, 2));
          document.body.appendChild(renderer.domElement);

          const scene = new THREE.Scene();
          scene.background = new THREE.Color(0x1a1a2e);

          const camera = new THREE.PerspectiveCamera(40, innerWidth / innerHeight, 0.01, 2000);

          // ── Lighting ────────────────────────────────────────────────────────
          scene.add(new THREE.AmbientLight(0xaaccff, 0.8));
          const sun = new THREE.DirectionalLight(0xffffff, 0.9);
          sun.position.set(5, 10, 7); scene.add(sun);
          const fill = new THREE.DirectionalLight(0x334488, 0.4);
          fill.position.set(-5, -3, -5); scene.add(fill);

          // ── Pivot group ─────────────────────────────────────────────────────
          // All geometry lives inside pivot. Setting pivot.position = -centroid
          // ensures the model always rotates around its own centre regardless
          // of the coordinate origin the LLM chose.
          const pivot = new THREE.Group();
          pivot.position.copy(centroid).negate();
          scene.add(pivot);

          // Grid (world-space, decorative)
          scene.add(new THREE.GridHelper(Math.max(20, maxDist * 5), 12, 0x2a2a4a, 0x1e1e36));

          // ── Skeleton edges ──────────────────────────────────────────────────
          const edgeMat = new THREE.LineBasicMaterial({ color: 0x888780 });
          for (const [a, b] of (SCENE.edges || [])) {
            if (!V[a] || !V[b]) continue;
            pivot.add(new THREE.Line(
              new THREE.BufferGeometry().setFromPoints([V[a].clone(), V[b].clone()]),
              edgeMat
            ));
          }

          // ── Face groups ─────────────────────────────────────────────────────
          const faceGroups = [];
          let soloIdx = -1;

          for (const face of (SCENE.faces || [])) {
            const grp = new THREE.Group();
            const pts = (face.pts || []).map(p => V[p]).filter(Boolean);

            if (pts.length >= 3) {
              // Fan-triangulated mesh
              const pos = [];
              for (let i = 1; i < pts.length - 1; i++)
                pos.push(...pts[0].toArray(), ...pts[i].toArray(), ...pts[i+1].toArray());
              const fg = new THREE.BufferGeometry();
              fg.setAttribute('position', new THREE.Float32BufferAttribute(pos, 3));
              fg.computeVertexNormals();
              grp.add(new THREE.Mesh(fg, new THREE.MeshPhongMaterial({
                color:       new THREE.Color(face.color || '#4488ff'),
                transparent: true,
                opacity:     face.opacity !== undefined ? face.opacity : 0.38,
                side:        THREE.DoubleSide,
                depthWrite:  false,
              })));

              // Edge-highlight loop in face colour
              grp.add(new THREE.Line(
                new THREE.BufferGeometry().setFromPoints([...pts, pts[0]]),
                new THREE.LineBasicMaterial({ color: face.color || '#4488ff' })
              ));

              // Face label sprite at centroid
              if (face.label) {
                const cen = new THREE.Vector3();
                pts.forEach(p => cen.add(p));
                cen.divideScalar(pts.length);
                const sp = makeTextSprite(face.label, face.color || '#ffffff');
                sp.position.copy(cen);
                grp.add(sp);
              }
            }

            pivot.add(grp);
            faceGroups.push({ group: grp, face });
          }

          // ── Helpers ─────────────────────────────────────────────────────────
          for (const h of (SCENE.helpers || [])) {
            switch (h.kind) {

              case 'dashed': {
                if (!V[h.from] || !V[h.to]) break;
                const geo = new THREE.BufferGeometry().setFromPoints([V[h.from].clone(), V[h.to].clone()]);
                const mat = new THREE.LineDashedMaterial({ color: h.color || 0xffffff, dashSize: 0.25, gapSize: 0.12 });
                const ln  = new THREE.Line(geo, mat);
                ln.computeLineDistances();
                pivot.add(ln);
                break;
              }

              case 'line': {
                if (!V[h.from] || !V[h.to]) break;
                pivot.add(new THREE.Line(
                  new THREE.BufferGeometry().setFromPoints([V[h.from].clone(), V[h.to].clone()]),
                  new THREE.LineBasicMaterial({ color: h.color || 0xffffff })
                ));
                break;
              }

              case 'dot': {
                if (!V[h.at]) break;
                const dot = new THREE.Mesh(
                  new THREE.SphereGeometry(Math.max(0.08, maxDist * 0.04), 8, 6),
                  new THREE.MeshBasicMaterial({ color: h.color || 0xffffff })
                );
                dot.position.copy(V[h.at]);
                pivot.add(dot);
                break;
              }

              case 'arc': {
                if (!V[h.center] || !V[h.from] || !V[h.to]) break;
                const cen = V[h.center].clone();
                const d1  = V[h.from].clone().sub(cen).normalize();
                const d2  = V[h.to].clone().sub(cen).normalize();
                const r   = h.radius !== undefined ? h.radius : maxDist * 0.18;
                const arcPts = [];
                for (let i = 0; i <= 32; i++) {
                  const dir = d1.clone().lerp(d2, i / 32).normalize();
                  arcPts.push(cen.clone().add(dir.multiplyScalar(r)));
                }
                pivot.add(new THREE.Line(
                  new THREE.BufferGeometry().setFromPoints(arcPts),
                  new THREE.LineBasicMaterial({ color: h.color || 0xffffff })
                ));
                if (h.label) {
                  const midDir = d1.clone().lerp(d2, 0.5).normalize();
                  const sp = makeTextSprite(h.label, h.color || '#ffffff', 0.65);
                  sp.position.copy(cen.clone().add(midDir.multiplyScalar(r * 1.75)));
                  pivot.add(sp);
                }
                break;
              }

              case 'right_angle': {
                if (!V[h.corner] || !V[h.d1] || !V[h.d2]) break;
                const corner = V[h.corner].clone();
                const sz = h.size !== undefined ? h.size : Math.max(0.2, maxDist * 0.07);
                const e1 = V[h.d1].clone().sub(corner).normalize().multiplyScalar(sz);
                const e2 = V[h.d2].clone().sub(corner).normalize().multiplyScalar(sz);
                pivot.add(new THREE.Line(
                  new THREE.BufferGeometry().setFromPoints([
                    corner.clone().add(e1),
                    corner.clone().add(e1).add(e2),
                    corner.clone().add(e2),
                  ]),
                  new THREE.LineBasicMaterial({ color: h.color || 0xffffff })
                ));
                break;
              }
            }
          }

          // ── Vertex label sprites ────────────────────────────────────────────
          for (const [label, pos] of Object.entries(SCENE.vertices || {})) {
            const sp = makeVertexSprite(label);
            sp.position.set(pos[0], pos[1], pos[2]);
            pivot.add(sp);
          }

          // ── Sprite factories ────────────────────────────────────────────────
          function makeVertexSprite(text) {
            const sz = 64;
            const cv = document.createElement('canvas');
            cv.width = cv.height = sz;
            const cx = cv.getContext('2d');
            cx.fillStyle = 'rgba(10,10,30,0.82)';
            cx.beginPath(); cx.arc(sz/2, sz/2, 26, 0, Math.PI*2); cx.fill();
            cx.strokeStyle = 'rgba(140,200,255,0.65)';
            cx.lineWidth = 1.5; cx.stroke();
            cx.fillStyle = '#e0eeff';
            cx.font = 'bold 28px monospace';
            cx.textAlign = 'center'; cx.textBaseline = 'middle';
            cx.fillText(text, sz/2, sz/2);
            const sp = new THREE.Sprite(new THREE.SpriteMaterial({
              map: new THREE.CanvasTexture(cv), depthTest: false, sizeAttenuation: true,
            }));
            const s = Math.max(0.4, maxDist * 0.22);
            sp.scale.set(s, s, 1);
            return sp;
          }

          function makeTextSprite(text, color, mul) {
            const cw = 140, ch = 50;
            const cv = document.createElement('canvas');
            cv.width = cw; cv.height = ch;
            const cx = cv.getContext('2d');
            cx.fillStyle = 'rgba(10,10,30,0.72)';
            cx.fillRect(2, 2, cw-4, ch-4);
            cx.fillStyle = color || '#ffffff';
            cx.font = 'bold 22px sans-serif';
            cx.textAlign = 'center'; cx.textBaseline = 'middle';
            cx.fillText(text, cw/2, ch/2);
            const sp = new THREE.Sprite(new THREE.SpriteMaterial({
              map: new THREE.CanvasTexture(cv), depthTest: false, sizeAttenuation: true,
            }));
            const s = (mul || 1) * Math.max(0.5, maxDist * 0.3);
            sp.scale.set(s * 2.1, s * 0.75, 1);
            return sp;
          }

          // ── Drag / zoom state ───────────────────────────────────────────────
          const camCfg = SCENE.camera || {};
          let rotX = camCfg.rotX !== undefined ? camCfg.rotX : -0.35;
          let rotY = camCfg.rotY !== undefined ? camCfg.rotY :  0.55;
          let zoom = camCfg.zoom !== undefined ? camCfg.zoom :  1.0;
          let drag = false, lx = 0, ly = 0;

          const el = renderer.domElement;
          el.addEventListener('mousedown',  e => { drag=true; lx=e.clientX; ly=e.clientY; });
          el.addEventListener('mousemove',  e => {
            if (!drag) return;
            rotY += (e.clientX - lx) * 0.007; rotX += (e.clientY - ly) * 0.007;
            lx = e.clientX; ly = e.clientY;
          });
          el.addEventListener('mouseup',    () => drag = false);
          el.addEventListener('mouseleave', () => drag = false);
          el.addEventListener('wheel', e => {
            zoom *= e.deltaY > 0 ? 0.93 : 1.07;
            zoom = Math.max(0.1, Math.min(10, zoom));
            e.preventDefault();
          }, { passive: false });

          let ltx = 0, lty = 0;
          el.addEventListener('touchstart', e => { ltx=e.touches[0].clientX; lty=e.touches[0].clientY; e.preventDefault(); }, { passive: false });
          el.addEventListener('touchmove',  e => {
            rotY += (e.touches[0].clientX - ltx) * 0.007; rotX += (e.touches[0].clientY - lty) * 0.007;
            ltx = e.touches[0].clientX; lty = e.touches[0].clientY; e.preventDefault();
          }, { passive: false });

          // ── Face toggle buttons ─────────────────────────────────────────────
          if (faceGroups.length > 0) {
            const bar = document.createElement('div');
            bar.style.cssText = 'position:fixed;bottom:18px;left:50%;transform:translateX(-50%);display:flex;gap:8px;flex-wrap:wrap;justify-content:center;';
            document.body.appendChild(bar);

            function makeBtn(label, color, onClick) {
              const b = document.createElement('button');
              b.textContent = label;
              b.style.cssText = `background:${color};color:#fff;border:none;padding:7px 16px;border-radius:7px;cursor:pointer;font:13px sans-serif;opacity:0.88;`;
              b.addEventListener('click', onClick);
              bar.appendChild(b);
              return b;
            }

            makeBtn('всички', '#3a3a5a', () => {
              soloIdx = -1;
              faceGroups.forEach(fg => fg.group.visible = true);
            });

            faceGroups.forEach((fg, idx) => {
              const ang   = (SCENE.angles || []).find(a => a.face === fg.face.label);
              const color = ang ? ang.color : (fg.face.color || '#3a7bd5');
              makeBtn(fg.face.label || `face${idx}`, color, () => {
                soloIdx = soloIdx === idx ? -1 : idx;
                faceGroups.forEach((f, i) => f.group.visible = soloIdx === -1 || i === soloIdx);
              });
            });
          }

          // ── Legend (top-right) ──────────────────────────────────────────────
          const angles = SCENE.angles || [];
          if (angles.length > 0) {
            const leg = document.createElement('div');
            leg.style.cssText = 'position:fixed;top:12px;right:14px;background:rgba(18,18,44,0.88);padding:10px 16px;border-radius:10px;font-family:sans-serif;font-size:13px;color:#aac;line-height:2;min-width:190px;';
            leg.innerHTML = angles.map(a =>
              `<div><span style="color:${a.color};font-size:16px">■</span>&nbsp;${a.face}&nbsp;<span style="color:#eef;font-weight:bold">${a.value}</span></div>`
            ).join('');
            document.body.appendChild(leg);
          }

          // ── Hint ────────────────────────────────────────────────────────────
          const hint = document.createElement('div');
          hint.style.cssText = 'position:fixed;bottom:52px;left:50%;transform:translateX(-50%);color:rgba(180,190,220,0.38);font:12px sans-serif;pointer-events:none;user-select:none;';
          hint.textContent = 'Drag to rotate · Scroll to zoom';
          document.body.appendChild(hint);

          // ── Resize ──────────────────────────────────────────────────────────
          window.addEventListener('resize', () => {
            camera.aspect = innerWidth / innerHeight;
            camera.updateProjectionMatrix();
            renderer.setSize(innerWidth, innerHeight);
          });

          // ── Render loop ─────────────────────────────────────────────────────
          (function loop() {
            requestAnimationFrame(loop);
            pivot.rotation.x = rotX;
            pivot.rotation.y = rotY;
            const dist = baseDist / zoom;
            camera.position.set(dist * 0.65, dist * 0.55, dist * 0.9);
            camera.lookAt(0, 0, 0);
            renderer.render(scene, camera);
          })();
        })();
        </script>
        </body>
        </html>
        """;
}
