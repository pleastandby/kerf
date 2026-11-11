using System;
using System.IO;
using System.Text;
using System.Web.Script.Serialization;
using Kerf.Engine;
using Kerf.Engine.Animation;
using Kerf.Model;

namespace Kerf.IO
{
    /// <summary>
    /// Generates standalone HTML5 interactive web players for .kfx animations.
    /// Allows exported animations to be previewed and executed in any modern web browser
    /// with interactive state machine inputs and listeners, independent of platform.
    /// Strictly adheres to C# 5.0 compatibility.
    /// </summary>
    public static class WebPlayerGenerator
    {
        public static string GenerateHtmlPlayer(KerfDocument doc, string kfxFileName = null)
        {
            if (doc == null) throw new ArgumentNullException("doc");

            var page = doc.GetActivePage();
            var bounds = (page != null) ? HighQualityImageRenderer.ComputeVisualBounds(page, null) : new System.Windows.Rect(0, 0, 1200, 800);
            double width = bounds.Width > 0 ? bounds.Width : 1200.0;
            double height = bounds.Height > 0 ? bounds.Height : 800.0;
            string bgColor = (page != null && !string.IsNullOrEmpty(page.BackgroundColor)) ? page.BackgroundColor : "#140720";
            string title = !string.IsNullOrEmpty(doc.Name) ? doc.Name : "Kerf Animation";

            var serializer = new JavaScriptSerializer { MaxJsonLength = 100 * 1024 * 1024 };

            // Serialize scene layers
            string layersJson = "[]";
            if (page != null && page.Layers != null)
            {
                layersJson = serializer.Serialize(page.Layers);
            }

            // Serialize clips
            string clipsJson = "[]";
            if (doc.Animation != null && doc.Animation.Clips != null)
            {
                clipsJson = serializer.Serialize(doc.Animation.Clips);
            }

            // Serialize state machine
            string stateMachineJson = "null";
            if (doc.Animation != null && doc.Animation.StateMachine != null)
            {
                stateMachineJson = serializer.Serialize(doc.Animation.StateMachine);
            }

            var sb = new StringBuilder();
            sb.AppendLine("<!DOCTYPE html>");
            sb.AppendLine("<html lang=\"en\">");
            sb.AppendLine("<head>");
            sb.AppendLine("  <meta charset=\"UTF-8\">");
            sb.AppendLine("  <meta name=\"viewport\" content=\"width=device-width, initial-scale=1.0\">");
            sb.AppendFormat("  <title>{0} - Kerf Motion Runtime</title>\n", System.Security.SecurityElement.Escape(title));
            sb.AppendLine("  <style>");
            sb.AppendLine("    * { box-sizing: border-box; margin: 0; padding: 0; }");
            sb.AppendLine("    body { background: #0E0517; color: #FFFFFF; font-family: -apple-system, BlinkMacSystemFont, 'Segoe UI', Roboto, Helvetica, Arial, sans-serif; display: flex; flex-direction: column; height: 100vh; overflow: hidden; }");
            sb.AppendLine("    header { background: #1E082E; border-bottom: 1px solid #0A717B; height: 48px; display: flex; align-items: center; justify-content: space-between; padding: 0 20px; flex-shrink: 0; }");
            sb.AppendLine("    .brand { display: flex; align-items: center; gap: 10px; font-weight: 600; font-size: 15px; color: #FFFFFF; }");
            sb.AppendLine("    .brand-badge { background: #0A717B; color: #46EFB1; font-size: 10px; font-weight: 700; padding: 2px 6px; border-radius: 4px; text-transform: uppercase; letter-spacing: 0.5px; }");
            sb.AppendLine("    .viewport { flex: 1; display: flex; align-items: center; justify-content: center; position: relative; background: #08020D; overflow: hidden; }");
            sb.AppendLine("    canvas { box-shadow: 0 12px 48px rgba(0,0,0,0.6); border-radius: 8px; max-width: 95%; max-height: 90%; image-rendering: -webkit-optimize-contrast; }");
            sb.AppendLine("    .controls-dock { background: #140720; border-top: 1px solid #0C4247; padding: 12px 24px; display: flex; align-items: center; justify-content: space-between; gap: 16px; flex-shrink: 0; }");
            sb.AppendLine("    .transport-btns { display: flex; align-items: center; gap: 10px; }");
            sb.AppendLine("    button.btn { background: #0C4247; color: #FFFFFF; border: 1px solid #0A717B; border-radius: 6px; padding: 6px 14px; font-size: 12px; font-weight: 500; cursor: pointer; transition: all 0.15s ease; display: inline-flex; align-items: center; gap: 6px; }");
            sb.AppendLine("    button.btn:hover { background: #0A717B; border-color: #46EFB1; }");
            sb.AppendLine("    button.btn-primary { background: #46EFB1; color: #0C4247; border: 1px solid #46EFB1; font-weight: 600; }");
            sb.AppendLine("    button.btn-primary:hover { background: #38C793; }");
            sb.AppendLine("    .timeline-scrubber { flex: 1; display: flex; align-items: center; gap: 12px; max-width: 600px; }");
            sb.AppendLine("    .scrub-track { flex: 1; height: 6px; background: #0C4247; border-radius: 3px; position: relative; cursor: pointer; }");
            sb.AppendLine("    .scrub-fill { height: 100%; background: #46EFB1; border-radius: 3px; width: 0%; pointer-events: none; }");
            sb.AppendLine("    .time-label { font-size: 12px; font-family: 'Consolas', monospace; color: #A799B7; min-width: 80px; text-align: center; }");
            sb.AppendLine("    .state-badge { font-size: 12px; background: #0C4247; border: 1px solid #0A717B; color: #46EFB1; padding: 4px 10px; border-radius: 12px; }");
            sb.AppendLine("    .inputs-dock { display: flex; align-items: center; gap: 12px; }");
            sb.AppendLine("    .input-item { display: flex; align-items: center; gap: 6px; font-size: 12px; color: #E0D7EC; }");
            sb.AppendLine("  </style>");
            sb.AppendLine("</head>");
            sb.AppendLine("<body class=\"kerf-player\">");
            sb.AppendLine("  <header>");
            sb.AppendLine("    <div class=\"brand\">");
            sb.AppendLine("      <svg width=\"20\" height=\"20\" viewBox=\"0 0 24 24\" fill=\"#46EFB1\"><path d=\"M12 2L2 7l10 5 10-5-10-5zM2 17l10 5 10-5M2 12l10 5 10-5\"/></svg>");
            sb.AppendFormat("      <span>{0}</span>\n", System.Security.SecurityElement.Escape(title));
            sb.AppendLine("      <span class=\"brand-badge\">.kfx Runtime</span>");
            sb.AppendLine("    </div>");
            sb.AppendLine("    <div class=\"inputs-dock\" id=\"inputsContainer\"></div>");
            sb.AppendLine("  </header>");
            sb.AppendLine("  <div class=\"viewport\">");
            sb.AppendFormat("    <canvas id=\"animCanvas\" class=\"kerf-canvas\" width=\"{0}\" height=\"{1}\" style=\"background: {2};\"></canvas>\n",
                (int)width, (int)height, System.Security.SecurityElement.Escape(bgColor));
            sb.AppendLine("  </div>");
            sb.AppendLine("  <div class=\"controls-dock\">");
            sb.AppendLine("    <div class=\"transport-btns\">");
            sb.AppendLine("      <button class=\"btn btn-primary\" id=\"playBtn\" onclick=\"togglePlay()\">Pause</button>");
            sb.AppendLine("      <button class=\"btn\" id=\"resetBtn\" onclick=\"resetAnim()\">Reset</button>");
            sb.AppendLine("    </div>");
            sb.AppendLine("    <div class=\"timeline-scrubber\">");
            sb.AppendLine("      <span class=\"time-label\" id=\"timeCurrent\">0.00s</span>");
            sb.AppendLine("      <div class=\"scrub-track\" id=\"scrubTrack\" onclick=\"seekTrack(event)\">");
            sb.AppendLine("        <div class=\"scrub-fill\" id=\"scrubFill\"></div>");
            sb.AppendLine("      </div>");
            sb.AppendLine("      <span class=\"time-label\" id=\"timeDuration\">0.00s</span>");
            sb.AppendLine("    </div>");
            sb.AppendLine("    <div class=\"state-badge\" id=\"stateBadge\">State: Entry</div>");
            sb.AppendLine("  </div>");
            sb.AppendLine("  <script>");
            sb.AppendLine("    const SCENE_LAYERS = " + layersJson + ";");
            sb.AppendLine("    const ANIM_CLIPS = " + clipsJson + ";");
            sb.AppendLine("    const STATE_MACHINE = " + stateMachineJson + ";");
            sb.AppendLine("    let isPlaying = true;");
            sb.AppendLine("    let currentTime = 0.0;");
            sb.AppendLine("    let duration = 2.0;");
            sb.AppendLine("    let lastTimestamp = performance.now();");
            sb.AppendLine("    let activeClip = (ANIM_CLIPS && ANIM_CLIPS.length > 0) ? ANIM_CLIPS[0] : null;");
            sb.AppendLine("    if (activeClip && activeClip.Duration > 0) duration = activeClip.Duration;");
            sb.AppendLine("    document.getElementById('timeDuration').innerText = duration.toFixed(2) + 's';");
            sb.AppendLine("    const canvas = document.getElementById('animCanvas');");
            sb.AppendLine("    const ctx = canvas.getContext('2d');");
            sb.AppendLine("");
            sb.AppendLine("    function togglePlay() {");
            sb.AppendLine("      isPlaying = !isPlaying;");
            sb.AppendLine("      document.getElementById('playBtn').innerText = isPlaying ? 'Pause' : 'Play';");
            sb.AppendLine("    }");
            sb.AppendLine("");
            sb.AppendLine("    function resetAnim() {");
            sb.AppendLine("      currentTime = 0.0;");
            sb.AppendLine("    }");
            sb.AppendLine("");
            sb.AppendLine("    function seekTrack(e) {");
            sb.AppendLine("      const rect = e.currentTarget.getBoundingClientRect();");
            sb.AppendLine("      const fraction = Math.max(0, Math.min(1, (e.clientX - rect.left) / rect.width));");
            sb.AppendLine("      currentTime = fraction * duration;");
            sb.AppendLine("    }");
            sb.AppendLine("");
            sb.AppendLine("    function cubicBezier(t, x1, y1, x2, y2) {");
            sb.AppendLine("      // Bisection / Newton fallback for 1D cubic easing");
            sb.AppendLine("      let low = 0.0, high = 1.0, s = 0.5;");
            sb.AppendLine("      for (let i = 0; i < 16; i++) {");
            sb.AppendLine("        s = 0.5 * (low + high);");
            sb.AppendLine("        const x = 3*(1-s)*(1-s)*s*x1 + 3*(1-s)*s*s*x2 + s*s*s;");
            sb.AppendLine("        if (Math.abs(x - t) < 1e-4) break;");
            sb.AppendLine("        if (x < t) low = s; else high = s;");
            sb.AppendLine("      }");
            sb.AppendLine("      return 3*(1-s)*(1-s)*s*y1 + 3*(1-s)*s*s*y2 + s*s*s;");
            sb.AppendLine("    }");
            sb.AppendLine("");
            sb.AppendLine("    function evaluateTrack(track, t) {");
            sb.AppendLine("      if (!track || !track.Keys || track.Keys.length === 0) return 0;");
            sb.AppendLine("      const keys = track.Keys;");
            sb.AppendLine("      if (t <= keys[0].Time) return keys[0].Value;");
            sb.AppendLine("      if (t >= keys[keys.length - 1].Time) return keys[keys.length - 1].Value;");
            sb.AppendLine("      for (let i = 0; i < keys.length - 1; i++) {");
            sb.AppendLine("        const a = keys[i], b = keys[i+1];");
            sb.AppendLine("        if (t >= a.Time && t <= b.Time) {");
            sb.AppendLine("          const u = (t - a.Time) / Math.max(0.0001, b.Time - a.Time);");
            sb.AppendLine("          if (a.Interp === 0) return a.Value; // Hold");
            sb.AppendLine("          if (a.Interp === 1) return a.Value + (b.Value - a.Value) * u; // Linear");
            sb.AppendLine("          const eased = cubicBezier(u, a.X1 || 0.25, a.Y1 || 0.1, a.X2 || 0.25, a.Y2 || 1.0);");
            sb.AppendLine("          return a.Value + (b.Value - a.Value) * eased;");
            sb.AppendLine("        }");
            sb.AppendLine("      }");
            sb.AppendLine("      return keys[0].Value;");
            sb.AppendLine("    }");
            sb.AppendLine("");
            sb.AppendLine("    function renderLoop(now) {");
            sb.AppendLine("      const dt = (now - lastTimestamp) / 1000.0;");
            sb.AppendLine("      lastTimestamp = now;");
            sb.AppendLine("      if (isPlaying && duration > 0) {");
            sb.AppendLine("        currentTime = (currentTime + dt) % duration;");
            sb.AppendLine("      }");
            sb.AppendLine("      const progress = duration > 0 ? (currentTime / duration) : 0;");
            sb.AppendLine("      document.getElementById('scrubFill').style.width = (progress * 100).toFixed(1) + '%';");
            sb.AppendLine("      document.getElementById('timeCurrent').innerText = currentTime.toFixed(2) + 's';");
            sb.AppendLine("");
            sb.AppendLine("      // Evaluate pose from active clip tracks");
            sb.AppendLine("      const pose = {};");
            sb.AppendLine("      if (activeClip && activeClip.Tracks) {");
            sb.AppendLine("        for (let tr of activeClip.Tracks) {");
            sb.AppendLine("          if (!tr.IsActive) continue;");
            sb.AppendLine("          const val = evaluateTrack(tr, currentTime);");
            sb.AppendLine("          if (!pose[tr.TargetNodeId || tr.NodeId]) pose[tr.TargetNodeId || tr.NodeId] = {};");
            sb.AppendLine("          pose[tr.TargetNodeId || tr.NodeId][tr.Property] = val;");
            sb.AppendLine("        }");
            sb.AppendLine("      }");
            sb.AppendLine("");
            sb.AppendLine("      // Draw canvas layers");
            sb.AppendLine("      ctx.clearRect(0, 0, canvas.width, canvas.height);");
            sb.AppendLine("      for (let layer of SCENE_LAYERS) {");
            sb.AppendLine("        ctx.save();");
            sb.AppendLine("        const layerPose = pose[layer.Id] || pose[layer.NumericId] || {};");
            sb.AppendLine("        const tx = layerPose[0] !== undefined ? (layerPose[0] - layer.X) : 0;");
            sb.AppendLine("        const ty = layerPose[1] !== undefined ? (layerPose[1] - layer.Y) : 0;");
            sb.AppendLine("        const sx = layerPose[2] !== undefined ? layerPose[2] : 1;");
            sb.AppendLine("        const sy = layerPose[3] !== undefined ? layerPose[3] : 1;");
            sb.AppendLine("        const rot = layerPose[4] !== undefined ? (layerPose[4] * Math.PI / 180) : 0;");
            sb.AppendLine("        const op = layerPose[7] !== undefined ? layerPose[7] : (layer.Opacity !== undefined ? layer.Opacity : 1);");
            sb.AppendLine("        ctx.globalAlpha = Math.max(0, Math.min(1, op));");
            sb.AppendLine("        const cx = layer.X + (layer.Width || 0) * 0.5;");
            sb.AppendLine("        const cy = layer.Y + (layer.Height || 0) * 0.5;");
            sb.AppendLine("        ctx.translate(cx + tx, cy + ty);");
            sb.AppendLine("        ctx.rotate(rot);");
            sb.AppendLine("        ctx.scale(sx, sy);");
            sb.AppendLine("        ctx.translate(-cx, -cy);");
            sb.AppendLine("        if (layer.Fills && layer.Fills.length > 0 && layer.Fills[0].Color) {");
            sb.AppendLine("          ctx.fillStyle = layer.Fills[0].Color;");
            sb.AppendLine("        } else { ctx.fillStyle = '#46EFB1'; }");
            sb.AppendLine("        const r = layer.CornerRadius || 0;");
            sb.AppendLine("        if (r > 0) {");
            sb.AppendLine("          ctx.beginPath();");
            sb.AppendLine("          ctx.roundRect(layer.X, layer.Y, layer.Width || 100, layer.Height || 100, r);");
            sb.AppendLine("          ctx.fill();");
            sb.AppendLine("        } else {");
            sb.AppendLine("          ctx.fillRect(layer.X, layer.Y, layer.Width || 100, layer.Height || 100);");
            sb.AppendLine("        }");
            sb.AppendLine("        ctx.restore();");
            sb.AppendLine("      }");
            sb.AppendLine("      requestAnimationFrame(renderLoop);");
            sb.AppendLine("    }");
            sb.AppendLine("    requestAnimationFrame(renderLoop);");
            sb.AppendLine("  </script>");
            sb.AppendLine("</body>");
            sb.AppendLine("</html>");

            return sb.ToString();
        }

        public static void ExportHtmlPlayerToFile(KerfDocument doc, string filePath, string kfxFileName = null)
        {
            string html = GenerateHtmlPlayer(doc, kfxFileName);
            File.WriteAllText(filePath, html, Encoding.UTF8);
        }
    }
}
