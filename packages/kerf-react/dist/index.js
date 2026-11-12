var Mt = Object.defineProperty;
var Nt = (i, t, e) => t in i ? Mt(i, t, { enumerable: !0, configurable: !0, writable: !0, value: e }) : i[t] = e;
var T = (i, t, e) => Nt(i, typeof t != "symbol" ? t + "" : t, e);
import { jsx as ht } from "react/jsx-runtime";
import { forwardRef as Ft, useRef as j, useImperativeHandle as bt, useEffect as Z, useState as ut, useCallback as L } from "react";
var M = Uint8Array, R = Uint16Array, Ht = Int32Array, vt = new M([
  0,
  0,
  0,
  0,
  0,
  0,
  0,
  0,
  1,
  1,
  1,
  1,
  2,
  2,
  2,
  2,
  3,
  3,
  3,
  3,
  4,
  4,
  4,
  4,
  5,
  5,
  5,
  5,
  0,
  /* unused */
  0,
  0,
  /* impossible */
  0
]), gt = new M([
  0,
  0,
  0,
  0,
  1,
  1,
  2,
  2,
  3,
  3,
  4,
  4,
  5,
  5,
  6,
  6,
  7,
  7,
  8,
  8,
  9,
  9,
  10,
  10,
  11,
  11,
  12,
  12,
  13,
  13,
  /* unused */
  0,
  0
]), Vt = new M([16, 17, 18, 0, 8, 7, 9, 6, 10, 5, 11, 4, 12, 3, 13, 2, 14, 1, 15]), dt = function(i, t) {
  for (var e = new R(31), r = 0; r < 31; ++r)
    e[r] = t += 1 << i[r - 1];
  for (var n = new Ht(e[30]), r = 1; r < 30; ++r)
    for (var s = e[r]; s < e[r + 1]; ++s)
      n[s] = s - e[r] << 5 | r;
  return { b: e, r: n };
}, yt = dt(vt, 2), mt = yt.b, At = yt.r;
mt[28] = 258, At[258] = 28;
var Wt = dt(gt, 0), Lt = Wt.b, _ = new R(32768);
for (var m = 0; m < 32768; ++m) {
  var W = (m & 43690) >> 1 | (m & 21845) << 1;
  W = (W & 52428) >> 2 | (W & 13107) << 2, W = (W & 61680) >> 4 | (W & 3855) << 4, _[m] = ((W & 65280) >> 8 | (W & 255) << 8) >> 1;
}
var E = function(i, t, e) {
  for (var r = i.length, n = 0, s = new R(t); n < r; ++n)
    i[n] && ++s[i[n] - 1];
  var o = new R(t);
  for (n = 1; n < t; ++n)
    o[n] = o[n - 1] + s[n - 1] << 1;
  var a;
  if (e) {
    a = new R(1 << t);
    var l = 15 - t;
    for (n = 0; n < r; ++n)
      if (i[n])
        for (var c = n << 4 | i[n], g = t - i[n], f = o[i[n] - 1]++ << g, h = f | (1 << g) - 1; f <= h; ++f)
          a[_[f] >> l] = c;
  } else
    for (a = new R(r), n = 0; n < r; ++n)
      i[n] && (a[n] = _[o[i[n] - 1]++] >> 15 - i[n]);
  return a;
}, O = new M(288);
for (var m = 0; m < 144; ++m)
  O[m] = 8;
for (var m = 144; m < 256; ++m)
  O[m] = 9;
for (var m = 256; m < 280; ++m)
  O[m] = 7;
for (var m = 280; m < 288; ++m)
  O[m] = 8;
var pt = new M(32);
for (var m = 0; m < 32; ++m)
  pt[m] = 5;
var Xt = /* @__PURE__ */ E(O, 9, 1), Yt = /* @__PURE__ */ E(pt, 5, 1), G = function(i) {
  for (var t = i[0], e = 1; e < i.length; ++e)
    i[e] > t && (t = i[e]);
  return t;
}, b = function(i, t, e) {
  var r = t / 8 | 0;
  return (i[r] | i[r + 1] << 8) >> (t & 7) & e;
}, J = function(i, t) {
  var e = t / 8 | 0;
  return (i[e] | i[e + 1] << 8 | i[e + 2] << 16) >> (t & 7);
}, Rt = function(i) {
  return (i + 7) / 8 | 0;
}, et = function(i, t, e) {
  return (t == null || t < 0) && (t = 0), (e == null || e > i.length) && (e = i.length), new M(i.subarray(t, e));
}, Bt = [
  "unexpected EOF",
  "invalid block type",
  "invalid length/literal",
  "invalid distance",
  "stream finished",
  "no stream handler",
  ,
  // determined by compression function
  "no callback",
  "invalid UTF-8 data",
  "extra field too long",
  "date not in range 1980-2099",
  "filename too long",
  "stream finishing",
  "invalid zip data"
  // determined by unknown compression method
], x = function(i, t, e) {
  var r = new Error(t || Bt[i]);
  if (r.code = i, Error.captureStackTrace && Error.captureStackTrace(r, x), !e)
    throw r;
  return r;
}, Kt = function(i, t, e, r) {
  var n = i.length, s = r ? r.length : 0;
  if (!n || t.f && !t.l)
    return e || new M(0);
  var o = !e, a = o || t.i != 2, l = t.i;
  o && (e = new M(n * 3));
  var c = function(ct) {
    var lt = e.length;
    if (ct > lt) {
      var ft = new M(Math.max(lt * 2, ct));
      ft.set(e), e = ft;
    }
  }, g = t.f || 0, f = t.p || 0, h = t.b || 0, u = t.l, v = t.d, d = t.m, y = t.n, C = n * 8;
  do {
    if (!u) {
      g = b(i, f, 1);
      var k = b(i, f + 1, 3);
      if (f += 3, k)
        if (k == 1)
          u = Xt, v = Yt, d = 9, y = 5;
        else if (k == 2) {
          var V = b(i, f, 31) + 257, P = b(i, f + 10, 15) + 4, w = V + b(i, f + 5, 31) + 1;
          f += 14;
          for (var B = new M(w), z = new M(19), N = 0; N < P; ++N)
            z[Vt[N]] = b(i, f + N * 3, 7);
          f += P * 3;
          for (var it = G(z), kt = (1 << it) - 1, wt = E(z, it, 1), N = 0; N < w; ) {
            var rt = wt[b(i, f, kt)];
            f += rt & 15;
            var p = rt >> 4;
            if (p < 16)
              B[N++] = p;
            else {
              var X = 0, D = 0;
              for (p == 16 ? (D = 3 + b(i, f, 3), f += 2, X = B[N - 1]) : p == 17 ? (D = 3 + b(i, f, 7), f += 3) : p == 18 && (D = 11 + b(i, f, 127), f += 7); D--; )
                B[N++] = X;
            }
          }
          var nt = B.subarray(0, V), A = B.subarray(V);
          d = G(nt), y = G(A), u = E(nt, d, 1), v = E(A, y, 1);
        } else
          x(1);
      else {
        var p = Rt(f) + 4, S = i[p - 4] | i[p - 3] << 8, I = p + S;
        if (I > n) {
          l && x(0);
          break;
        }
        a && c(h + S), e.set(i.subarray(p, I), h), t.b = h += S, t.p = f = I * 8, t.f = g;
        continue;
      }
      if (f > C) {
        l && x(0);
        break;
      }
    }
    a && c(h + 131072);
    for (var Pt = (1 << d) - 1, It = (1 << y) - 1, U = f; ; U = f) {
      var X = u[J(i, f) & Pt], Y = X >> 4;
      if (f += X & 15, f > C) {
        l && x(0);
        break;
      }
      if (X || x(2), Y < 256)
        e[h++] = Y;
      else if (Y == 256) {
        U = f, u = null;
        break;
      } else {
        var at = Y - 254;
        if (Y > 264) {
          var N = Y - 257, K = vt[N];
          at = b(i, f, (1 << K) - 1) + mt[N], f += K;
        }
        var $ = v[J(i, f) & It], q = $ >> 4;
        $ || x(3), f += $ & 15;
        var A = Lt[q];
        if (q > 3) {
          var K = gt[q];
          A += J(i, f) & (1 << K) - 1, f += K;
        }
        if (f > C) {
          l && x(0);
          break;
        }
        a && c(h + 131072);
        var st = h + at;
        if (h < A) {
          var ot = s - A, xt = Math.min(A, st);
          for (ot + h < 0 && x(3); h < xt; ++h)
            e[h] = r[ot + h];
        }
        for (; h < st; ++h)
          e[h] = e[h - A];
      }
    }
    t.l = u, t.p = U, t.b = h, t.f = g, u && (g = 1, t.m = d, t.d = v, t.n = y);
  } while (!g);
  return h != e.length && o ? et(e, 0, h) : e.subarray(0, h);
}, Et = /* @__PURE__ */ new M(0), H = function(i, t) {
  return i[t] | i[t + 1] << 8;
}, F = function(i, t) {
  return (i[t] | i[t + 1] << 8 | i[t + 2] << 16 | i[t + 3] << 24) >>> 0;
}, Q = function(i, t) {
  return F(i, t) + F(i, t + 4) * 4294967296;
};
function Ot(i, t) {
  return Kt(i, { i: 2 }, t && t.out, t && t.dictionary);
}
var tt = typeof TextDecoder < "u" && /* @__PURE__ */ new TextDecoder(), Dt = 0;
try {
  tt.decode(Et, { stream: !0 }), Dt = 1;
} catch {
}
var jt = function(i) {
  for (var t = "", e = 0; ; ) {
    var r = i[e++], n = (r > 127) + (r > 223) + (r > 239);
    if (e + n > i.length)
      return { s: t, r: et(i, e - 1) };
    n ? n == 3 ? (r = ((r & 15) << 18 | (i[e++] & 63) << 12 | (i[e++] & 63) << 6 | i[e++] & 63) - 65536, t += String.fromCharCode(55296 | r >> 10, 56320 | r & 1023)) : n & 1 ? t += String.fromCharCode((r & 31) << 6 | i[e++] & 63) : t += String.fromCharCode((r & 15) << 12 | (i[e++] & 63) << 6 | i[e++] & 63) : t += String.fromCharCode(r);
  }
};
function zt(i, t) {
  if (t) {
    for (var e = "", r = 0; r < i.length; r += 16384)
      e += String.fromCharCode.apply(null, i.subarray(r, r + 16384));
    return e;
  } else {
    if (tt)
      return tt.decode(i);
    var n = jt(i), s = n.s, e = n.r;
    return e.length && x(8), s;
  }
}
var Ut = function(i, t) {
  return t + 30 + H(i, t + 26) + H(i, t + 28);
}, $t = function(i, t, e) {
  var r = H(i, t + 28), n = H(i, t + 30), s = zt(i.subarray(t + 46, t + 46 + r), !(H(i, t + 8) & 2048)), o = t + 46 + r, a = qt(i, o, n, e, F(i, t + 20), F(i, t + 24), F(i, t + 42)), l = a[0], c = a[1], g = a[2];
  return [H(i, t + 10), l, c, s, o + n + H(i, t + 32), g];
}, qt = function(i, t, e, r, n, s, o) {
  var a = n == 4294967295, l = s == 4294967295, c = o == 4294967295, g = t + e, f = a + l + c;
  if (r && f) {
    for (; t + 4 < g; t += 4 + H(i, t + 2))
      if (H(i, t) == 1)
        return [
          a ? Q(i, t + 4 + 8 * l) : n,
          l ? Q(i, t + 4) : s,
          c ? Q(i, t + 4 + 8 * (l + a)) : o,
          1
        ];
    r < 2 && x(13);
  }
  return [n, s, o, 0];
};
function Gt(i, t) {
  for (var e = {}, r = i.length - 22; F(i, r) != 101010256; --r)
    (!r || i.length - r > 65558) && x(13);
  var n = H(i, r + 8);
  if (!n)
    return {};
  var s = F(i, r + 16), o = F(i, r - 20) == 117853008;
  if (o) {
    var a = F(i, r - 12);
    o = F(i, a) == 101075792, o && (n = F(i, a + 32), s = F(i, a + 48));
  }
  for (var l = 0; l < n; ++l) {
    var c = $t(i, s, o), g = c[0], f = c[1], h = c[2], u = c[3], v = c[4], d = c[5], y = Ut(i, d);
    s = v, g ? g == 8 ? e[u] = Ot(i.subarray(y, y + f), { out: new M(h) }) : x(14, "unknown compression type " + g) : e[u] = et(i, y, y + f);
  }
  return e;
}
function St(i, t, e, r, n) {
  if (i <= 0) return 0;
  if (i >= 1) return 1;
  let s = 0, o = 1, a = 0.5;
  for (let l = 0; l < 16; l++) {
    a = 0.5 * (s + o);
    const c = 3 * (1 - a) * (1 - a) * a * t + 3 * (1 - a) * a * a * r + a * a * a;
    if (Math.abs(c - i) < 1e-4) break;
    c < i ? s = a : o = a;
  }
  return 3 * (1 - a) * (1 - a) * a * e + 3 * (1 - a) * a * a * n + a * a * a;
}
function Jt(i, t) {
  if (!i || !i.Keys || i.Keys.length === 0) return 0;
  const e = i.Keys;
  if (t <= e[0].Time) return e[0].Value;
  if (t >= e[e.length - 1].Time) return e[e.length - 1].Value;
  for (let r = 0; r < e.length - 1; r++) {
    const n = e[r], s = e[r + 1];
    if (t >= n.Time && t <= s.Time) {
      const o = (t - n.Time) / Math.max(1e-4, s.Time - n.Time);
      if (n.Interp === 0) return n.Value;
      if (n.Interp === 1) return n.Value + (s.Value - n.Value) * o;
      const a = St(o, n.X1 ?? 0.25, n.Y1 ?? 0.1, n.X2 ?? 0.25, n.Y2 ?? 1);
      return n.Value + (s.Value - n.Value) * a;
    }
  }
  return e[0].Value;
}
async function Ct(i) {
  const t = await fetch(i);
  if (!t.ok)
    throw new Error(`Failed to load .kfx package from ${i}: HTTP ${t.status}`);
  const e = await t.arrayBuffer();
  return Qt(e);
}
function Qt(i) {
  const t = new Uint8Array(i), e = Gt(t), r = (l, c = !0) => {
    const g = e[l];
    if (!g) {
      if (c) throw new Error(`Missing expected file in .kfx container: ${l}`);
      return null;
    }
    const f = new TextDecoder("utf-8").decode(g);
    return JSON.parse(f);
  }, n = r("manifest.json"), s = r("scene.json"), o = r("animations.json") ?? [], a = r("state-machines.json", !1);
  return {
    manifest: n,
    scene: s,
    animations: o,
    stateMachine: a
  };
}
class Tt {
  constructor(t) {
    T(this, "pkg");
    T(this, "isPlaying", !0);
    T(this, "currentTime", 0);
    T(this, "playbackSpeed", 1);
    // State Machine State
    T(this, "inputs", {});
    T(this, "currentState", null);
    T(this, "activeClip", null);
    // State Machine Transition State
    T(this, "isTransitioning", !1);
    T(this, "transitionFromState", null);
    T(this, "transitionToState", null);
    T(this, "transitionTime", 0);
    T(this, "transitionDuration", 0.25);
    T(this, "transitionMix", 0);
    T(this, "activeTransition", null);
    // Listeners & Pointer Hover Tracking
    T(this, "hoveredLayers", /* @__PURE__ */ new Set());
    // Change Callback
    T(this, "onStateChange");
    T(this, "onInputChange");
    this.pkg = t, this.init();
  }
  init() {
    var t, e, r, n;
    if ((t = this.pkg.stateMachine) != null && t.Inputs)
      for (const s of this.pkg.stateMachine.Inputs)
        this.inputs[s.Name] = s.InitialValue ?? s.Value ?? 0;
    if ((n = (r = (e = this.pkg.stateMachine) == null ? void 0 : e.Layers) == null ? void 0 : r[0]) != null && n.States) {
      const s = this.pkg.stateMachine.Layers[0].States, o = s.find((c) => c.Kind === 0 || c.Name.toLowerCase() === "entry") ?? s[0];
      this.currentState = o;
      const l = (this.pkg.stateMachine.Layers[0].Transitions ?? []).find((c) => c.FromState === o.Id);
      if (l) {
        const c = s.find((g) => g.Id === l.ToState);
        c && (this.currentState = c);
      }
      this.updateActiveClip();
    } else this.pkg.animations.length > 0 && (this.activeClip = this.pkg.animations[0]);
  }
  updateActiveClip() {
    this.currentState && (this.currentState.ClipIndex >= 0 && this.currentState.ClipIndex < this.pkg.animations.length ? this.activeClip = this.pkg.animations[this.currentState.ClipIndex] : this.currentState.ClipName ? this.activeClip = this.pkg.animations.find((t) => {
      var e;
      return t.Name === ((e = this.currentState) == null ? void 0 : e.ClipName);
    }) ?? null : this.pkg.animations.length > 0 && (this.activeClip = this.pkg.animations[0]));
  }
  // Input Control API
  setBool(t, e) {
    var n;
    const r = e ? 1 : 0;
    this.inputs[t] !== r && (this.inputs[t] = r, (n = this.onInputChange) == null || n.call(this, t, r), this.evaluateStateTransitions());
  }
  setNumber(t, e) {
    var r;
    this.inputs[t] !== e && (this.inputs[t] = e, (r = this.onInputChange) == null || r.call(this, t, e), this.evaluateStateTransitions());
  }
  fireTrigger(t) {
    var e;
    this.inputs[t] = 1, (e = this.onInputChange) == null || e.call(this, t, 1), this.evaluateStateTransitions(), setTimeout(() => {
      var r;
      this.inputs[t] = 0, (r = this.onInputChange) == null || r.call(this, t, 0);
    }, 16);
  }
  // Pointer Interaction
  handlePointerMove(t, e) {
    var r;
    if ((r = this.pkg.scene) != null && r.Layers)
      for (const n of this.pkg.scene.Layers) {
        const s = t >= n.X && t <= n.X + (n.Width || 100) && e >= n.Y && e <= n.Y + (n.Height || 100), o = n.Id, a = this.hoveredLayers.has(o);
        s && !a ? (this.hoveredLayers.add(o), this.dispatchLayerEvent(
          n,
          3
          /* PointerEnter */
        )) : !s && a && (this.hoveredLayers.delete(o), this.dispatchLayerEvent(
          n,
          4
          /* PointerExit */
        ));
      }
  }
  handlePointerLeave() {
    var t, e;
    for (const r of Array.from(this.hoveredLayers)) {
      this.hoveredLayers.delete(r);
      const n = (e = (t = this.pkg.scene) == null ? void 0 : t.Layers) == null ? void 0 : e.find((s) => s.Id === r);
      n && this.dispatchLayerEvent(
        n,
        4
        /* PointerExit */
      );
    }
  }
  dispatchLayerEvent(t, e) {
    var n;
    const r = ((n = this.pkg.stateMachine) == null ? void 0 : n.Listeners) ?? [];
    for (const s of r)
      if ((s.TargetLayerId === t.Id || t.NumericId && s.TargetNodeId === t.NumericId) && (s.Kind === e || e === 4 && s.Kind === 3)) {
        for (const o of s.Actions)
          if (o.PropertyName) {
            const a = e === 4 ? 0 : o.Parameter ?? 1;
            this.setNumber(o.PropertyName, a);
          }
      }
  }
  // State Transition Evaluation
  evaluateStateTransitions() {
    var n, s;
    if (!((s = (n = this.pkg.stateMachine) == null ? void 0 : n.Layers) != null && s[0])) return;
    const t = this.pkg.stateMachine.Layers[0], e = t.States, r = t.Transitions ?? [];
    if (this.currentState)
      for (const o of r) {
        if (o.FromState !== this.currentState.Id && o.FromState !== 1)
          continue;
        let a = !0;
        const l = o.ConditionGroups ?? [], c = o.Conditions ?? [];
        if (l.length > 0)
          for (const g of l) {
            for (const f of g.Conditions)
              if (!this.testCondition(f)) {
                a = !1;
                break;
              }
            if (!a) break;
          }
        else if (c.length > 0) {
          for (const g of c)
            if (!this.testCondition(g)) {
              a = !1;
              break;
            }
        }
        if (a) {
          const g = e.find((f) => f.Id === o.ToState);
          if (g && g.Id !== this.currentState.Id) {
            this.startTransition(o, g);
            break;
          }
        }
      }
  }
  testCondition(t) {
    const e = this.inputs[t.PropertyName] ?? 0, r = t.Value;
    switch (t.Op) {
      case 0:
        return e === r;
      case 1:
        return e !== r;
      case 2:
        return e < r;
      case 3:
        return e <= r;
      case 4:
        return e > r;
      case 5:
        return e >= r;
      default:
        return e === r;
    }
  }
  startTransition(t, e) {
    var r, n, s;
    t.Duration <= 0 ? (this.currentState = e, this.updateActiveClip(), this.isTransitioning = !1, (r = this.onStateChange) == null || r.call(this, e.Name)) : (this.isTransitioning = !0, this.transitionFromState = this.currentState, this.transitionToState = e, this.activeTransition = t, this.transitionTime = 0, this.transitionDuration = t.Duration, this.transitionMix = 0, (s = this.onStateChange) == null || s.call(this, `${(n = this.transitionFromState) == null ? void 0 : n.Name} → ${e.Name}`));
  }
  // Animation Update Loop
  advance(t) {
    var n, s;
    if (!this.isPlaying) return;
    const e = t * this.playbackSpeed, r = ((n = this.activeClip) == null ? void 0 : n.Duration) ?? 2;
    if (r > 0 && (this.currentTime = (this.currentTime + e) % r), this.isTransitioning && this.transitionToState) {
      this.transitionTime += e;
      const o = Math.min(1, this.transitionTime / Math.max(1e-3, this.transitionDuration)), a = this.activeTransition;
      this.transitionMix = St(
        o,
        (a == null ? void 0 : a.X1) ?? 0.42,
        (a == null ? void 0 : a.Y1) ?? 0,
        (a == null ? void 0 : a.X2) ?? 0.58,
        (a == null ? void 0 : a.Y2) ?? 1
      ), o >= 1 && (this.currentState = this.transitionToState, this.updateActiveClip(), this.isTransitioning = !1, this.transitionFromState = null, this.transitionToState = null, this.activeTransition = null, this.transitionMix = 0, (s = this.onStateChange) == null || s.call(this, this.currentState.Name));
    }
  }
  // Pose Computation & Blending
  computePose() {
    const t = {};
    if (!this.activeClip) return t;
    if (this.evaluateClipIntoPose(this.activeClip, this.currentTime, t), this.isTransitioning && this.transitionToState) {
      const e = this.transitionToState.ClipIndex, r = e >= 0 && e < this.pkg.animations.length ? this.pkg.animations[e] : null;
      if (r) {
        const n = {};
        this.evaluateClipIntoPose(r, this.currentTime % (r.Duration || 2), n);
        const s = this.transitionMix;
        for (const o of Object.keys(n)) {
          t[o] || (t[o] = {});
          const a = t[o], l = n[o];
          l.Width !== void 0 && a.Width !== void 0 ? a.Width = a.Width * (1 - s) + l.Width * s : l.Width !== void 0 && (a.Width = l.Width), l.Height !== void 0 && a.Height !== void 0 ? a.Height = a.Height * (1 - s) + l.Height * s : l.Height !== void 0 && (a.Height = l.Height), l.VisualWidth !== void 0 && a.VisualWidth !== void 0 && (a.VisualWidth = a.VisualWidth * (1 - s) + l.VisualWidth * s), l.VisualHeight !== void 0 && a.VisualHeight !== void 0 && (a.VisualHeight = a.VisualHeight * (1 - s) + l.VisualHeight * s), l.ScaleX !== void 0 && a.ScaleX !== void 0 && (a.ScaleX = a.ScaleX * (1 - s) + l.ScaleX * s), l.ScaleY !== void 0 && a.ScaleY !== void 0 && (a.ScaleY = a.ScaleY * (1 - s) + l.ScaleY * s), l.Rotation !== void 0 && a.Rotation !== void 0 && (a.Rotation = a.Rotation * (1 - s) + l.Rotation * s), l.Opacity !== void 0 && a.Opacity !== void 0 && (a.Opacity = a.Opacity * (1 - s) + l.Opacity * s);
        }
      }
    }
    return t;
  }
  evaluateClipIntoPose(t, e, r) {
    if (t != null && t.Tracks)
      for (const n of t.Tracks) {
        if (n.IsActive === !1) continue;
        const s = n.TargetNodeId ?? String(n.NodeId);
        r[s] || (r[s] = {});
        const o = Jt(n, e), a = (n.PropertyPath || "").toLowerCase();
        a === "scalex" || n.Prop === 12 ? r[s].ScaleX = o : a === "scaley" || n.Prop === 13 ? r[s].ScaleY = o : a === "width" || n.Prop === 3 ? r[s].Width = o : a === "height" || n.Prop === 4 ? r[s].Height = o : a === "visualwidth" || n.Prop === 16 ? r[s].VisualWidth = o : a === "visualheight" || n.Prop === 17 ? r[s].VisualHeight = o : a === "rotation" || n.Prop === 5 ? r[s].Rotation = o : a === "opacity" || n.Prop === 7 ? r[s].Opacity = o : a === "x" || a === "translationx" || n.Prop === 1 ? r[s].X = o : (a === "y" || a === "translationy" || n.Prop === 2) && (r[s].Y = o);
      }
  }
  // Canvas Render Execution
  renderToCanvas(t) {
    const e = t.getContext("2d");
    if (!e) return;
    const r = this.pkg.scene, n = r.Width || 1920, s = r.Height || 1080, o = typeof window < "u" && window.devicePixelRatio || 1, a = t.clientWidth || 800, l = t.clientHeight || 500;
    (t.width !== a * o || t.height !== l * o) && (t.width = a * o, t.height = l * o), e.save(), e.scale(o, o), e.fillStyle = r.BackgroundColor || "#1E1E22", e.fillRect(0, 0, a, l);
    const c = Math.min(a / n, l / s), g = (a - n * c) * 0.5, f = (l - s * c) * 0.5;
    e.translate(g, f), e.scale(c, c);
    const h = this.computePose();
    for (const u of r.Layers) {
      if (u.IsVisible === !1) continue;
      e.save();
      const v = h[u.Id] ?? (u.NumericId ? h[String(u.NumericId)] : null) ?? {}, d = v.VisualWidth ?? v.Width ?? u.Width ?? 100, y = v.VisualHeight ?? v.Height ?? u.Height ?? 100, C = v.ScaleX ?? u.ScaleX ?? 1, k = v.ScaleY ?? u.ScaleY ?? 1, S = (v.Rotation ?? u.Rotation ?? 0) * Math.PI / 180, I = Math.max(0, Math.min(1, v.Opacity ?? u.Opacity ?? 1));
      e.globalAlpha = I;
      const V = u.X + d * 0.5, P = u.Y + y * 0.5;
      e.translate(V, P), e.rotate(S), e.scale(C, k), e.translate(-V, -P), u.Fills && u.Fills.length > 0 && u.Fills[0].ColorHex ? e.fillStyle = u.Fills[0].ColorHex : e.fillStyle = "#203BFF";
      const w = u.CornerRadius ?? 0;
      w > 0 && typeof e.roundRect == "function" ? (e.beginPath(), e.roundRect(u.X, u.Y, d, y, w), e.fill()) : e.fillRect(u.X, u.Y, d, y), e.restore();
    }
    e.restore();
  }
  getArtboardCoords(t, e, r) {
    const n = t.getBoundingClientRect(), s = n.width, o = n.height, a = this.pkg.scene.Width || 1920, l = this.pkg.scene.Height || 1080, c = Math.min(s / a, o / l), g = (s - a * c) * 0.5, f = (o - l * c) * 0.5, h = e - n.left, u = r - n.top, v = (h - g) / c, d = (u - f) / c;
    return { x: v, y: d };
  }
}
const Zt = Ft(({
  src: i,
  autoplay: t = !0,
  inputs: e,
  style: r,
  className: n,
  onLoad: s,
  onStateChange: o
}, a) => {
  const l = j(null), c = j(null);
  bt(a, () => ({
    setBool: (h, u) => {
      var v;
      return (v = c.current) == null ? void 0 : v.setBool(h, u);
    },
    setNumber: (h, u) => {
      var v;
      return (v = c.current) == null ? void 0 : v.setNumber(h, u);
    },
    fireTrigger: (h) => {
      var u;
      return (u = c.current) == null ? void 0 : u.fireTrigger(h);
    },
    play: () => {
      c.current && (c.current.isPlaying = !0);
    },
    pause: () => {
      c.current && (c.current.isPlaying = !1);
    },
    reset: () => {
      c.current && (c.current.currentTime = 0);
    },
    getPlayer: () => c.current
  })), Z(() => {
    if (!(!c.current || !e))
      for (const [h, u] of Object.entries(e))
        typeof u == "boolean" ? c.current.setBool(h, u) : typeof u == "number" && c.current.setNumber(h, u);
  }, [e]), Z(() => {
    let h = !1, u, v = performance.now();
    async function d() {
      try {
        const y = await Ct(i);
        if (h) return;
        const C = new Tt(y);
        if (C.isPlaying = t, o && (C.onStateChange = o), e)
          for (const [p, S] of Object.entries(e))
            typeof S == "boolean" ? C.setBool(p, S) : typeof S == "number" && C.setNumber(p, S);
        c.current = C, s == null || s(y);
        const k = (p) => {
          const S = (p - v) / 1e3;
          v = p, c.current && l.current && (c.current.advance(S), c.current.renderToCanvas(l.current)), u = requestAnimationFrame(k);
        };
        u = requestAnimationFrame(k);
      } catch (y) {
        console.error(`[Kerf] Error loading "${i}":`, y);
      }
    }
    return d(), () => {
      h = !0, cancelAnimationFrame(u), c.current = null;
    };
  }, [i, t, o, s]);
  const g = (h) => {
    const u = c.current, v = l.current;
    if (u && v) {
      const d = u.getArtboardCoords(v, h.clientX, h.clientY);
      u.handlePointerMove(d.x, d.y);
    }
  }, f = () => {
    var h;
    (h = c.current) == null || h.handlePointerLeave();
  };
  return /* @__PURE__ */ ht(
    "canvas",
    {
      ref: l,
      className: n,
      style: {
        width: "100%",
        height: "100%",
        display: "block",
        ...r
      },
      onPointerMove: g,
      onPointerLeave: f
    }
  );
});
Zt.displayName = "Kerf";
function ie(i) {
  const t = j(null), e = j(null), [r, n] = ut(!1), [s, o] = ut(""), a = L((v, d) => {
    var y;
    (y = e.current) == null || y.setBool(v, d);
  }, []), l = L((v, d) => {
    var y;
    (y = e.current) == null || y.setNumber(v, d);
  }, []), c = L((v) => {
    var d;
    (d = e.current) == null || d.fireTrigger(v);
  }, []), g = L(() => {
    e.current && (e.current.isPlaying = !0);
  }, []), f = L(() => {
    e.current && (e.current.isPlaying = !1);
  }, []), h = L(() => {
    e.current && (e.current.currentTime = 0);
  }, []);
  return Z(() => {
    let v = !1, d, y = performance.now();
    async function C() {
      var k, p;
      try {
        const S = await Ct(i.src);
        if (v) return;
        const I = new Tt(S);
        if (I.isPlaying = i.autoplay ?? !0, I.onStateChange = (P) => {
          var w;
          o(P), (w = i.onStateChange) == null || w.call(i, P);
        }, i.inputs)
          for (const [P, w] of Object.entries(i.inputs))
            typeof w == "boolean" ? I.setBool(P, w) : typeof w == "number" && I.setNumber(P, w);
        e.current = I, n(!0), (k = I.currentState) != null && k.Name && o(I.currentState.Name), (p = i.onLoad) == null || p.call(i, S);
        const V = (P) => {
          const w = (P - y) / 1e3;
          y = P, e.current && t.current && (e.current.advance(w), e.current.renderToCanvas(t.current)), d = requestAnimationFrame(V);
        };
        d = requestAnimationFrame(V);
      } catch (S) {
        console.error("[useKerf] Error loading:", S);
      }
    }
    return C(), () => {
      v = !0, cancelAnimationFrame(d), e.current = null;
    };
  }, [i.src, i.autoplay]), {
    KerfComponent: L((v) => {
      const d = (C) => {
        const k = e.current, p = t.current;
        if (k && p) {
          const S = k.getArtboardCoords(p, C.clientX, C.clientY);
          k.handlePointerMove(S.x, S.y);
        }
      }, y = () => {
        var C;
        (C = e.current) == null || C.handlePointerLeave();
      };
      return /* @__PURE__ */ ht(
        "canvas",
        {
          ref: t,
          className: v == null ? void 0 : v.className,
          style: {
            width: "100%",
            height: "100%",
            display: "block",
            ...v == null ? void 0 : v.style
          },
          onPointerMove: d,
          onPointerLeave: y
        }
      );
    }, []),
    canvasRef: t,
    player: e.current,
    isLoaded: r,
    activeState: s,
    setBool: a,
    setNumber: l,
    fireTrigger: c,
    play: g,
    pause: f,
    reset: h
  };
}
export {
  Zt as Kerf,
  Tt as KerfRuntimePlayer,
  St as cubicBezier,
  Jt as evaluateTrack,
  Qt as loadKfxFromArrayBuffer,
  Ct as loadKfxFromUrl,
  ie as useKerf
};
