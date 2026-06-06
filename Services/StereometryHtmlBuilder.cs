namespace StudyAssistant.Services;

public static class StereometryHtmlBuilder
{
    public static string Build(string sceneJson)
    {
        var safeJson = sceneJson.Replace("</", "<\\/");
        return HtmlTemplate.Replace("__SCENE_JSON__", safeJson);
    }

    // SCENE JSON contract:
    //   vertices — { "A":[x,y,z], ... }
    //   edges    — [["A","B"], ...]
    //   faces    — [{ pts, color, opacity, label }]
    //   helpers  — [{ kind:"dashed"|"line"|"dot"|"arc"|"right_angle", ...fields }]
    //   angles   — [{ face, intersection?, value, color }]
    //   given    — string | string[]   (optional; shown in legend bottom section)
    //   camera   — { rotX, rotY, zoom }
    private const string HtmlTemplate = """
        <!DOCTYPE html>
        <html lang="bg">
        <head>
        <meta charset="UTF-8">
        <meta name="viewport" content="width=device-width,initial-scale=1">
        <title>Schooly 3D</title>
        <style>
          * { margin:0; padding:0; box-sizing:border-box; }
          body { background:#0d0f12; overflow:hidden; position:relative; font-family:'DM Mono',monospace; }
          canvas { display:block; width:100%; height:100%; cursor:grab; }
          canvas.grabbing { cursor:grabbing; }
        </style>
        </head>
        <body>
        <script src="https://cdn.jsdelivr.net/npm/three@0.128.0/build/three.min.js"></script>
        <script>
        (function () {
          const SCENE = __SCENE_JSON__;

          // Per-face colour palette (index 0–4, repeating)
          const PAL = [
            { border:'#1D9E75', bg:'#1D9E7522', text:'#5DCAA5' },
            { border:'#7F77DD', bg:'#7F77DD22', text:'#AFA9EC' },
            { border:'#D4537E', bg:'#D4537E22', text:'#ED93B1' },
            { border:'#D4A017', bg:'#D4A01722', text:'#E8C96B' },
            { border:'#3A9AD4', bg:'#3A9AD422', text:'#7AC4EC' },
          ];

          // ── Vertex map & coordinate normalisation ────────────────────
          const V = {};
          for (const [k, p] of Object.entries(SCENE.vertices || {}))
            V[k] = new THREE.Vector3(p[0], p[1], p[2]);

          const vList = Object.values(V);

          // Shift so base sits at y=0 and footprint is centred at xz origin.
          // Because V holds THREE.Vector3 references, every face/edge/helper
          // that reads from V automatically sees the shifted coordinates.
          const minY = vList.reduce((m, v) => Math.min(m, v.y), Infinity);
          const xzCx = vList.reduce((s, v) => s + v.x, 0) / vList.length;
          const xzCz = vList.reduce((s, v) => s + v.z, 0) / vList.length;
          vList.forEach(v => { v.x -= xzCx; v.y -= minY; v.z -= xzCz; });

          // Centroid after shift — this is the true rotation centre.
          const rotCx = vList.reduce((s, v) => s + v.x, 0) / vList.length;
          const rotCy = vList.reduce((s, v) => s + v.y, 0) / vList.length;
          const rotCz = vList.reduce((s, v) => s + v.z, 0) / vList.length;
          const modelMidY = rotCy;

          let maxDist = 1;
          vList.forEach(v => {
            const d = Math.sqrt((v.x-rotCx)**2 + (v.y-rotCy)**2 + (v.z-rotCz)**2);
            if (d > maxDist) maxDist = d;
          });

          // ── Renderer ────────────────────────────────────────────────
          const renderer = new THREE.WebGLRenderer({ antialias: true });
          renderer.setClearColor(0x0d0f12);
          renderer.setPixelRatio(Math.min(devicePixelRatio, 2));
          document.body.appendChild(renderer.domElement);

          const scene = new THREE.Scene();
          scene.background = new THREE.Color(0x0d0f12);

          const camera = new THREE.PerspectiveCamera(40, 1, 0.01, 2000);

          // ── Lighting ────────────────────────────────────────────────
          scene.add(new THREE.AmbientLight(0xffffff, 0.55));
          const keyLight = new THREE.DirectionalLight(0xffffff, 0.8);
          keyLight.position.set(10, 20, 8);
          scene.add(keyLight);
          const fillLight = new THREE.DirectionalLight(0x4466ff, 0.15);
          fillLight.position.set(-10, -5, -10);
          scene.add(fillLight);

          // ── Pivot group ─────────────────────────────────────────────
          const pivot = new THREE.Group();
          pivot.position.set(-rotCx, -rotCy, -rotCz);
          scene.add(pivot);

          // Subtle grid
          scene.add(new THREE.GridHelper(Math.max(20, maxDist * 5), 12, 0x141618, 0x111315));

          // ── Skeleton edges ──────────────────────────────────────────
          const edgeMat = new THREE.LineBasicMaterial({ color: 0x888780 });
          for (const [a, b] of (SCENE.edges || [])) {
            if (!V[a] || !V[b]) continue;
            pivot.add(new THREE.Line(
              new THREE.BufferGeometry().setFromPoints([V[a].clone(), V[b].clone()]),
              edgeMat
            ));
          }

          // ── Face groups ─────────────────────────────────────────────
          const faceGroups = [];
          let soloIdx = -1;
          const faces  = SCENE.faces  || [];
          const angles = SCENE.angles || [];

          for (let fi = 0; fi < faces.length; fi++) {
            const face = faces[fi];
            const pal  = PAL[fi % PAL.length];
            const faceColor = face.color || pal.border;
            const grp  = new THREE.Group();
            const pts  = (face.pts || []).map(p => V[p]).filter(Boolean);

            if (pts.length >= 3) {
              const pos = [];
              for (let i = 1; i < pts.length - 1; i++)
                pos.push(...pts[0].toArray(), ...pts[i].toArray(), ...pts[i+1].toArray());
              const fg = new THREE.BufferGeometry();
              fg.setAttribute('position', new THREE.Float32BufferAttribute(pos, 3));
              fg.computeVertexNormals();
              grp.add(new THREE.Mesh(fg, new THREE.MeshPhongMaterial({
                color:       new THREE.Color(faceColor),
                transparent: true,
                opacity:     face.opacity !== undefined ? face.opacity : 0.38,
                side:        THREE.DoubleSide,
                depthWrite:  false,
                shininess:   30,
              })));

              // Highlighted dihedral edge in face colour
              grp.add(new THREE.Line(
                new THREE.BufferGeometry().setFromPoints([...pts, pts[0]]),
                new THREE.LineBasicMaterial({ color: faceColor })
              ));

              if (face.label) {
                const cen = new THREE.Vector3();
                pts.forEach(p => cen.add(p));
                cen.divideScalar(pts.length);
                const sp = makeTextSprite(face.label, faceColor);
                sp.position.copy(cen);
                grp.add(sp);
              }
            }

            pivot.add(grp);
            faceGroups.push({ group: grp, face, pal, faceColor });
          }

          // ── Helpers ─────────────────────────────────────────────────
          for (const h of (SCENE.helpers || [])) {
            switch (h.kind) {

              case 'dashed': {
                if (!V[h.from] || !V[h.to]) break;
                const geo = new THREE.BufferGeometry().setFromPoints([V[h.from].clone(), V[h.to].clone()]);
                const mat = new THREE.LineDashedMaterial({ color: h.color || 0xffffff, dashSize: 0.3, gapSize: 0.15 });
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
                  new THREE.SphereGeometry(0.15, 8, 6),
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
                  const sp = makeTextSprite(h.label, h.color || '#ffffff');
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

          // ── Vertex label sprites ─────────────────────────────────────
          for (const [label, pos] of Object.entries(SCENE.vertices || {})) {
            const sp = makeVertexSprite(label);
            sp.position.set(pos[0], pos[1], pos[2]);
            pivot.add(sp);
          }

          // ── Sprite factories ─────────────────────────────────────────
          function makeVertexSprite(text) {
            const sz = 64;
            const cv = document.createElement('canvas');
            cv.width = cv.height = sz;
            const cx = cv.getContext('2d');
            cx.fillStyle = 'rgba(13,15,18,0.85)';
            cx.beginPath(); cx.arc(sz/2, sz/2, 26, 0, Math.PI*2); cx.fill();
            cx.strokeStyle = 'rgba(200,220,255,0.45)';
            cx.lineWidth = 1.5; cx.stroke();
            cx.fillStyle = '#e8eaf0';
            cx.font = 'bold 24px monospace';
            cx.textAlign = 'center'; cx.textBaseline = 'middle';
            cx.fillText(text, sz/2, sz/2);
            const sp = new THREE.Sprite(new THREE.SpriteMaterial({
              map: new THREE.CanvasTexture(cv), depthTest: false, sizeAttenuation: true,
            }));
            sp.scale.set(1.05, 0.62, 1);
            return sp;
          }

          function makeTextSprite(text, color) {
            const cw = 140, ch = 50;
            const cv = document.createElement('canvas');
            cv.width = cw; cv.height = ch;
            const cx = cv.getContext('2d');
            cx.fillStyle = 'rgba(13,15,18,0.72)';
            cx.fillRect(2, 2, cw-4, ch-4);
            cx.fillStyle = color || '#ffffff';
            cx.font = 'bold 24px monospace';
            cx.textAlign = 'center'; cx.textBaseline = 'middle';
            cx.fillText(text, cw/2, ch/2);
            const sp = new THREE.Sprite(new THREE.SpriteMaterial({
              map: new THREE.CanvasTexture(cv), depthTest: false, sizeAttenuation: true,
            }));
            sp.scale.set(1.05, 0.62, 1);
            return sp;
          }

          // ── Camera / drag state ──────────────────────────────────────
          const camCfg  = SCENE.camera || {};
          const initRotX = camCfg.rotX !== undefined ? camCfg.rotX : -0.35;
          const initRotY = camCfg.rotY !== undefined ? camCfg.rotY :  0.55;
          const initZoom = camCfg.zoom !== undefined ? camCfg.zoom :  1.0;
          let rotX = initRotX, rotY = initRotY, zoom = initZoom;
          const baseD = 24;

          function resetView() { rotX = initRotX; rotY = initRotY; zoom = initZoom; }

          let drag = false, lx = 0, ly = 0;
          const el = renderer.domElement;

          el.addEventListener('mousedown', e => {
            drag = true; lx = e.clientX; ly = e.clientY;
            el.classList.add('grabbing');
          });
          el.addEventListener('mousemove', e => {
            if (!drag) return;
            rotY += (e.clientX - lx) * 0.007;
            rotX += (e.clientY - ly) * 0.007;
            rotX  = Math.max(-1.3, Math.min(1.3, rotX));
            lx = e.clientX; ly = e.clientY;
          });
          el.addEventListener('mouseup',    () => { drag = false; el.classList.remove('grabbing'); });
          el.addEventListener('mouseleave', () => { drag = false; el.classList.remove('grabbing'); });
          el.addEventListener('wheel', e => {
            zoom *= e.deltaY > 0 ? 1.1 : 0.91;
            zoom  = Math.max(0.3, Math.min(3.5, zoom));
            e.preventDefault();
          }, { passive: false });

          let ltx = 0, lty = 0;
          el.addEventListener('touchstart', e => {
            ltx = e.touches[0].clientX; lty = e.touches[0].clientY;
            e.preventDefault();
          }, { passive: false });
          el.addEventListener('touchmove', e => {
            rotY += (e.touches[0].clientX - ltx) * 0.009;
            rotX += (e.touches[0].clientY - lty) * 0.009;
            rotX  = Math.max(-1.3, Math.min(1.3, rotX));
            ltx = e.touches[0].clientX; lty = e.touches[0].clientY;
            e.preventDefault();
          }, { passive: false });

          // ── Face toggle buttons (top-left) ───────────────────────────
          {
            const bar = document.createElement('div');
            bar.style.cssText = 'position:absolute;top:10px;left:10px;display:flex;gap:6px;flex-wrap:wrap;';
            document.body.appendChild(bar);

            const neutralCss = `font-size:10px;padding:4px 9px;border-radius:6px;border:0.5px solid #5F5E5A;background:#44444122;color:#B4B2A9;cursor:pointer;font-family:'DM Mono',monospace;`;

            const allBtn = document.createElement('button');
            allBtn.textContent = 'all';
            allBtn.style.cssText = neutralCss;
            bar.appendChild(allBtn);

            const resetBtn = document.createElement('button');
            resetBtn.textContent = 'reset';
            resetBtn.style.cssText = neutralCss;
            resetBtn.addEventListener('click', resetView);
            bar.appendChild(resetBtn);

            const faceBtns = [];

            function updateOpacities() {
              faceBtns.forEach((b, i) => {
                b.style.opacity = (soloIdx === -1 || i === soloIdx) ? '1' : '0.4';
              });
            }

            allBtn.addEventListener('click', () => {
              soloIdx = -1;
              faceGroups.forEach(fg => fg.group.visible = true);
              updateOpacities();
            });

            faceGroups.forEach(({ face, pal }, idx) => {
              const ang   = angles.find(a => a.face === face.label);
              const label = face.label
                ? (ang ? `${face.label} \u2014 ${ang.value}` : face.label)
                : `face${idx}`;
              const btn = document.createElement('button');
              btn.textContent = label;
              btn.style.cssText = `font-size:10px;padding:4px 9px;border-radius:6px;border:0.5px solid ${pal.border};background:${pal.bg};color:${pal.text};cursor:pointer;font-family:'DM Mono',monospace;`;
              btn.addEventListener('click', () => {
                soloIdx = soloIdx === idx ? -1 : idx;
                faceGroups.forEach((f, i) => f.group.visible = soloIdx === -1 || i === soloIdx);
                updateOpacities();
              });
              bar.appendChild(btn);
              faceBtns.push(btn);
            });
          }

          // ── Legend (top-right) ───────────────────────────────────────
          {
            const leg = document.createElement('div');
            leg.style.cssText = [
              'position:absolute;top:10px;right:10px',
              'background:#141417dd',
              'border:0.5px solid #2e2e38',
              'border-radius:8px',
              'padding:10px 14px',
              'font-size:11px',
              "font-family:'DM Mono',monospace",
              'line-height:2.1',
              'min-width:180px',
            ].join(';');

            const hdr = document.createElement('div');
            hdr.style.cssText = 'color:#5a5a6a;font-size:9px;letter-spacing:0.08em;margin-bottom:3px;';
            hdr.textContent = '\u0414\u0418\u0425\u0415\u0414\u0420\u0410\u041b\u041d\u0418 \u044a\u0413\u041b\u0418';
            leg.appendChild(hdr);

            angles.forEach((a, i) => {
              const pal = PAL[i % PAL.length];
              const row = document.createElement('div');

              const swatch = document.createElement('span');
              swatch.style.color = pal.border;
              swatch.textContent = '\u25a0';

              const label = document.createElement('span');
              label.style.cssText = 'color:#9898a8;margin-left:5px;';
              label.textContent = a.intersection ? `${a.face} \u2229 ${a.intersection}` : a.face;

              const val = document.createElement('span');
              val.style.cssText = 'color:#f0f0f4;margin-left:6px;font-weight:500;';
              val.textContent = a.value;

              row.appendChild(swatch);
              row.appendChild(label);
              row.appendChild(val);
              leg.appendChild(row);
            });

            const given = SCENE.given;
            if (given) {
              const sep = document.createElement('div');
              sep.style.cssText = 'border-top:0.5px solid #2e2e38;margin-top:6px;padding-top:6px;color:#5a5a6a;font-size:10px;line-height:1.6;white-space:pre-line;';
              sep.textContent = Array.isArray(given) ? given.join('\n') : given;
              leg.appendChild(sep);
            }

            document.body.appendChild(leg);
          }

          // ── Hint (bottom-left) ───────────────────────────────────────
          {
            const hint = document.createElement('div');
            hint.style.cssText = "position:absolute;bottom:10px;left:10px;font-size:10px;color:#444441;font-family:'DM Mono',monospace;pointer-events:none;user-select:none;";
            hint.textContent = '\u0432\u043b\u0430\u0447\u0438 \u0437\u0430 \u0432\u044a\u0440\u0442\u0435\u043d\u0435 \u00b7 scroll \u0437\u0430 zoom';
            document.body.appendChild(hint);
          }

          // ── ResizeObserver ───────────────────────────────────────────
          function onResize() {
            const w = innerWidth, h = innerHeight;
            renderer.setSize(w, h, false);
            camera.aspect = w / h;
            camera.updateProjectionMatrix();
          }
          new ResizeObserver(onResize).observe(document.documentElement);
          onResize();

          // ── Render loop ──────────────────────────────────────────────
          (function loop() {
            requestAnimationFrame(loop);
            pivot.rotation.x = rotX;
            pivot.rotation.y = rotY;
            const d = baseD * zoom;
            camera.position.set(d * 0.7, d * 0.5, d * 1.0);
            camera.lookAt(0, modelMidY, 0);
            renderer.render(scene, camera);
          })();
        })();
        </script>
        </body>
        </html>
        """;
}
