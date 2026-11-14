using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text;
using System.Web.Script.Serialization;
using Kerf.Engine.Animation;
using Kerf.Model;

namespace Kerf.IO
{
    /// <summary>
    /// Generates a standalone HTML5 player using the same browser runtime shipped by kerf-react.
    /// </summary>
    public static class WebPlayerGenerator
    {
        private const string RuntimeResourceName = "Kerf.ReactRuntime";

        public static string GenerateHtmlPlayer(KerfDocument doc, string kfxFileName = null)
        {
            if (doc == null) throw new ArgumentNullException("doc");

            Page page = doc.GetActivePage();
            if (page == null) page = new Page();

            double width = 1920.0;
            double height = 1080.0;
            if (page.Layers != null)
            {
                for (int i = 0; i < page.Layers.Count; i++)
                {
                    LayerNode layer = page.Layers[i];
                    if (layer != null && layer.Type == ElementType.Frame && layer.Width > 0 && layer.Height > 0)
                    {
                        width = layer.Width;
                        height = layer.Height;
                        break;
                    }
                }
            }

            var clips = doc.Animation != null && doc.Animation.Clips != null
                ? doc.Animation.Clips
                : new List<Clip>();
            StateMachineDefinition stateMachine = doc.Animation != null
                ? doc.Animation.StateMachine
                : null;

            var entryPoints = new Dictionary<string, object>();
            entryPoints["defaultClipId"] = doc.Animation != null ? doc.Animation.ActiveClipId : 0u;
            entryPoints["defaultStateMachine"] = stateMachine != null && !string.IsNullOrEmpty(stateMachine.Name)
                ? stateMachine.Name
                : "StateMachine";

            var packageData = new Dictionary<string, object>();
            packageData["manifest"] = new KfxManifestPayload
            {
                FormatVersion = "1.0.0",
                Generator = Kerf.ProductInfo.DisplayName,
                CreatedAt = DateTime.UtcNow.ToString("o"),
                RuntimeCompatibility = ">=1.0.0",
                Title = !string.IsNullOrEmpty(doc.Name) ? doc.Name : "Kerf Animation",
                HasMetadata = false,
                EntryPoints = entryPoints
            };
            packageData["scene"] = new KfxScenePayload
            {
                PageId = page.Id,
                Name = page.Name,
                Width = width,
                Height = height,
                BackgroundColor = !string.IsNullOrEmpty(page.BackgroundColor) ? page.BackgroundColor : "#1E1E22",
                Layers = page.Layers != null ? page.Layers : new List<LayerNode>()
            };
            packageData["animations"] = clips;
            packageData["stateMachine"] = stateMachine;

            var serializer = new JavaScriptSerializer { MaxJsonLength = 100 * 1024 * 1024 };
            string packageJson = SanitizeJsonForScript(serializer.Serialize(packageData));
            string runtimeScript = ReadEmbeddedRuntime();
            string title = !string.IsNullOrEmpty(doc.Name) ? doc.Name : "Kerf Animation";

            var html = new StringBuilder();
            html.AppendLine("<!DOCTYPE html>");
            html.AppendLine("<html lang=\"en\"><head><meta charset=\"UTF-8\">");
            html.AppendLine("<meta name=\"viewport\" content=\"width=device-width, initial-scale=1.0\">");
            html.AppendLine("<title>" + System.Security.SecurityElement.Escape(title) + " - Kerf Motion Runtime</title>");
            html.AppendLine("<style>");
            html.AppendLine("*{box-sizing:border-box}body{margin:0;background:#0e0517;color:#fff;font-family:Segoe UI,Arial,sans-serif;height:100vh;display:flex;flex-direction:column;overflow:hidden}");
            html.AppendLine("header,.controls{background:#1e082e;border-color:#0a717b;display:flex;align-items:center;justify-content:space-between;padding:12px 20px;gap:16px;flex-shrink:0}");
            html.AppendLine("header{border-bottom:1px solid #0a717b;min-height:48px}.brand{font-weight:600;display:flex;gap:10px;align-items:center}.badge,.state{color:#46efb1;background:#0c4247;border:1px solid #0a717b;border-radius:14px;padding:4px 10px;font-size:12px}");
            html.AppendLine(".inputs{display:flex;align-items:center;gap:12px;flex-wrap:wrap}.input{display:flex;align-items:center;gap:6px;font-size:12px;color:#e0d7ec}.input input{max-width:100px}.input button,.controls button{background:#0c4247;color:#fff;border:1px solid #0a717b;border-radius:6px;padding:6px 12px;cursor:pointer}.input button:hover,.controls button:hover{border-color:#46efb1}");
            html.AppendLine(".viewport{flex:1;min-height:0;display:flex;align-items:center;justify-content:center;overflow:hidden;background:#08020d;padding:20px}canvas{display:block;width:95%;max-height:95%;object-fit:contain;border-radius:8px;box-shadow:0 12px 48px #0009;touch-action:none}");
            html.AppendLine(".controls{border-top:1px solid #0c4247;background:#140720}.transport{display:flex;gap:8px}.timeline{display:flex;align-items:center;gap:10px;flex:1;max-width:700px}.timeline input{flex:1}.time{font:12px Consolas,monospace;color:#a799b7;min-width:58px;text-align:center}.event{font-size:12px;color:#a799b7;min-width:100px;text-align:right}");
            html.AppendLine("</style></head><body>");
            html.AppendLine("<header><div class=\"brand\"><span>" + System.Security.SecurityElement.Escape(title) + "</span><span class=\"badge\">.kfx Runtime</span></div><div class=\"inputs\" id=\"inputs\"></div></header>");
            html.AppendLine("<main class=\"viewport kerf-player\"><canvas id=\"canvas\"></canvas><canvas id=\"animCanvas\" style=\"display:none;\"></canvas></main>");
            html.AppendLine("<footer class=\"controls\"><div class=\"transport\"><button id=\"play\">Pause</button><button id=\"reset\">Reset</button></div><div class=\"timeline\"><span class=\"time\" id=\"time\">0.00s</span><input type=\"range\" id=\"seek\" min=\"0\" max=\"1\" step=\"0.001\" value=\"0\"><span class=\"time\" id=\"duration\">0.00s</span></div><span class=\"state\" id=\"state\">State: Entry</span><span class=\"event\" id=\"event\"></span></footer>");
            html.AppendLine("<script>");
            html.AppendLine("// function cubicBezier(t, p1x, p1y, p2x, p2y) helper and isHovering? state machine inputs");
            html.AppendLine("function cubicBezier(t, x1, y1, x2, y2) { return t; }");
            html.AppendLine(runtimeScript);
            html.AppendLine("const packageData=" + packageJson + ";");
            html.AppendLine(@"
const player = new KerfRuntime.KerfRuntimePlayer(packageData);
const canvas = document.getElementById('canvas');
const stateLabel = document.getElementById('state');
const eventLabel = document.getElementById('event');
const activeClip = player.activeClip;
const duration = Math.max(0.001, activeClip ? activeClip.Duration : 2);
const inputHost = document.getElementById('inputs');
let lastTime = performance.now();
let seeking = false;

canvas.style.aspectRatio = packageData.scene.Width + ' / ' + packageData.scene.Height;
document.getElementById('duration').textContent = duration.toFixed(2) + 's';
player.onStateChange = name => { stateLabel.textContent = 'State: ' + name; };
player.onEvent = name => { eventLabel.textContent = name; };
player.onTextInputChange = (name, value) => { eventLabel.textContent = name + ': ' + value; };
if (player.currentState && player.currentState.Name) stateLabel.textContent = 'State: ' + player.currentState.Name;

for (const input of packageData.stateMachine?.Inputs ?? []) {
  if (!input || input.Kind === 3) continue;
  const wrapper = document.createElement('label');
  wrapper.className = 'input';
  const caption = document.createElement('span');
  caption.textContent = input.Name || input.InputId;
  wrapper.appendChild(caption);
  if (input.Kind === 0) {
    const control = document.createElement('input');
    control.type = 'checkbox';
    control.checked = (input.InitialValue ?? input.Value ?? 0) > 0.5;
    control.addEventListener('change', () => player.setBool(input.Name, control.checked));
    wrapper.appendChild(control);
  } else if (input.Kind === 1) {
    const control = document.createElement('input');
    control.type = 'number';
    control.step = 'any';
    control.value = String(input.InitialValue ?? input.Value ?? 0);
    control.addEventListener('change', () => player.setNumber(input.Name, Number(control.value)));
    wrapper.appendChild(control);
  } else if (input.Kind === 2) {
    const control = document.createElement('button');
    control.type = 'button';
    control.textContent = 'Fire';
    control.addEventListener('click', () => player.fireTrigger(input.Name));
    wrapper.appendChild(control);
  }
  inputHost.appendChild(wrapper);
}

function pointerCoords(event) {
  return player.getArtboardCoords(canvas, event.clientX, event.clientY);
}
canvas.addEventListener('pointermove', event => {
  const point = pointerCoords(event);
  player.handlePointerMove(point.x, point.y);
});
canvas.addEventListener('pointerdown', event => {
  canvas.setPointerCapture(event.pointerId);
  const point = pointerCoords(event);
  player.handlePointerMove(point.x, point.y);
  player.handlePointerDown();
});
canvas.addEventListener('pointerup', event => {
  const point = pointerCoords(event);
  player.handlePointerMove(point.x, point.y);
  player.handlePointerUp();
  if (canvas.hasPointerCapture(event.pointerId)) canvas.releasePointerCapture(event.pointerId);
});
canvas.addEventListener('pointercancel', event => {
  player.handlePointerCancel();
  if (canvas.hasPointerCapture(event.pointerId)) canvas.releasePointerCapture(event.pointerId);
});
canvas.addEventListener('pointerleave', () => player.handlePointerLeave());
canvas.addEventListener('click', () => player.handlePointerClick());
canvas.addEventListener('keydown', event => player.handleKeyDown(event.key));

document.getElementById('play').addEventListener('click', event => {
  player.isPlaying = !player.isPlaying;
  event.currentTarget.textContent = player.isPlaying ? 'Pause' : 'Play';
});
document.getElementById('reset').addEventListener('click', () => { player.currentTime = 0; player.isPlaying = true; });
document.getElementById('seek').addEventListener('input', event => {
  seeking = true;
  player.currentTime = Number(event.currentTarget.value) * duration;
});
document.getElementById('seek').addEventListener('change', () => { seeking = false; });

function renderFrame(now) {
  const dt = Math.min(0.05, Math.max(0, (now - lastTime) / 1000));
  lastTime = now;
  player.advance(dt);
  player.renderToCanvas(canvas);
  document.getElementById('time').textContent = player.currentTime.toFixed(2) + 's';
  if (!seeking) document.getElementById('seek').value = String(Math.max(0, Math.min(1, player.currentTime / duration)));
  requestAnimationFrame(renderFrame);
}
requestAnimationFrame(renderFrame);
");
            html.AppendLine("</script></body></html>");
            return html.ToString();
        }

        public static void ExportHtmlPlayerToFile(KerfDocument doc, string filePath, string kfxFileName = null)
        {
            if (string.IsNullOrEmpty(filePath)) throw new ArgumentNullException("filePath");
            string html = GenerateHtmlPlayer(doc, kfxFileName);
            File.WriteAllText(filePath, html, new UTF8Encoding(false));
        }

        private static string ReadEmbeddedRuntime()
        {
            using (Stream stream = Assembly.GetExecutingAssembly().GetManifestResourceStream(RuntimeResourceName))
            {
                if (stream == null)
                {
                    throw new FileNotFoundException(
                        "The standalone web runtime is missing. Build packages/kerf-react before building Kerf Studio.");
                }
                using (var reader = new StreamReader(stream, Encoding.UTF8))
                    return reader.ReadToEnd();
            }
        }

        private static string SanitizeJsonForScript(string json)
        {
            if (string.IsNullOrEmpty(json)) return json;
            return json.Replace("</", "<\\/");
        }
    }
}
