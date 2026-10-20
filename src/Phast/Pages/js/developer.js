// phast://settings — "Advanced / developer mode" section (C# side: Bridge/DeveloperBridge.cs).
(function () {
  const $ = (id) => document.getElementById(id);
  const esc = (s) => String(s ?? "").replace(/[&<>"']/g, (c) => ({ "&": "&amp;", "<": "&lt;", ">": "&gt;", '"': "&quot;", "'": "&#39;" }[c]));
  const mb = (bytes) => (bytes / 1048576).toFixed(0) + " MB";

  let settings = null;
  let monitorOn = false;

  // Engine flags offered with an explanation (they are just added to / removed from the flags text).
  const FLAGS = [
    ["--renderer-process-limit=4", "Limitar los procesos de páginas a 4", "Menos memoria con muchas pestañas; si una web se cuelga puede arrastrar a otras."],
    ["--enable-low-end-device-mode", "Modo de equipo de gama baja", "El motor ahorra memoria de forma agresiva (útil con 4 GB de RAM o menos)."],
    ["--disable-background-timer-throttling", "No frenar las pestañas en segundo plano", "Los temporizadores de las webs ocultas siguen a toda velocidad (gasta más CPU)."],
    ["--disable-renderer-backgrounding", "Misma prioridad para las pestañas ocultas", "Útil para música o directos en otra pestaña; gasta algo más."],
    ["--disable-smooth-scrolling", "Desplazamiento sin suavizado", "La rueda del ratón mueve la página al instante."],
    ["--autoplay-policy=no-user-gesture-required", "Permitir reproducción automática con sonido", "Los vídeos pueden empezar a sonar solos."],
    ["--disable-quic", "Desactivar QUIC (HTTP/3)", "Arregla problemas de conexión con algunas redes, proxies o VPN."],
    ["--force-device-scale-factor=1", "Ignorar el escalado de Windows en las páginas", "Las webs se ven al 100% aunque Windows use 125% o 150%."],
    ["--js-flags=--max-old-space-size=512", "Limitar la memoria de JavaScript (512 MB por página)", "Evita que una web descontrolada consuma toda la RAM."],
  ];

  const USER_AGENTS = [
    ["", "Predeterminado (el de Phast)"],
    ["Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/140.0.0.0 Safari/537.36", "Chrome en Windows"],
    ["Mozilla/5.0 (Linux; Android 14; Pixel 8) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/140.0.0.0 Mobile Safari/537.36", "Chrome en Android (móvil)"],
    ["Mozilla/5.0 (iPhone; CPU iPhone OS 18_0 like Mac OS X) AppleWebKit/605.1.15 (KHTML, like Gecko) Version/18.0 Mobile/15E148 Safari/604.1", "Safari en iPhone"],
    ["Mozilla/5.0 (Windows NT 10.0; Win64; x64; rv:140.0) Gecko/20100101 Firefox/140.0", "Firefox en Windows"],
  ];

  // ------------------------------------------------------------ settings → panels
  phast.on("settings", (msg) => {
    settings = msg.settings;
    $("devPanels").hidden = !settings.developerMode;
    if (!settings.developerMode) { stopMonitor(); return; }
    if (document.activeElement !== $("customRules")) $("customRules").value = settings.customFilterRules || "";
    if (document.activeElement !== $("customLists")) $("customLists").value = (settings.customFilterLists || []).join("\n");
    if (document.activeElement !== $("flagsText")) $("flagsText").value = settings.browserFlags || "";
    renderFlags();
    renderSites();
    phast.send("dev.info");
    phast.send("shortcuts.get");
    startMonitorWhenVisible();
  });

  phast.on("devInfo", (msg) => {
    $("restartBanner").hidden = !msg.restartNeeded;
    $("dataFolder").textContent = msg.dataFolder;
  });

  // ------------------------------------------------------------ monitor
  const observer = new IntersectionObserver((entries) => {
    for (const e of entries) e.isIntersecting ? startMonitor() : stopMonitor();
  });
  function startMonitorWhenVisible() { observer.observe($("monitorRows").closest(".card")); }
  function startMonitor() { if (!monitorOn && settings?.developerMode) { monitorOn = true; phast.send("monitor.start"); } }
  function stopMonitor() { if (monitorOn) { monitorOn = false; phast.send("monitor.stop"); } }
  document.addEventListener("visibilitychange", () => (document.hidden ? stopMonitor() : startMonitorWhenVisible()));

  phast.on("monitor", (m) => {
    const loaded = m.tabs.filter((t) => t.pid).length;
    $("monitorSummary").textContent =
      `${m.tabs.length} pestañas (${loaded} cargadas) · memoria total de Phast y su motor: ${mb(m.total)} · ventana de Phast: ${mb(m.phast.memory)}`;
    $("monitorRows").innerHTML = m.tabs.map((t) => `
      <tr class="${t.active ? "active" : ""}">
        <td class="title" title="${esc(t.url)}">${esc(t.title)}</td>
        <td>${esc(t.state)}</td>
        <td>${t.pid ? mb(t.memory) + (t.shared > 1 ? ` <span class="muted" title="Proceso compartido con otras pestañas del mismo sitio">(×${t.shared})</span>` : "") : "—"}</td>
        <td>${t.pid ? t.cpu.toFixed(1) + " %" : "—"}</td>
        <td>${t.blocked ? `<button class="linkish" data-tab="blocked" data-id="${t.id}">${t.blocked}</button>` : "0"}</td>
        <td>
          <button class="mini" data-tab="activate" data-id="${t.id}" title="Ir a la pestaña">Ir</button>
          ${t.pid && !t.active ? `<button class="mini" data-tab="sleep" data-id="${t.id}" title="Liberar su memoria">Dormir</button>` : ""}
          <button class="mini" data-tab="devtools" data-id="${t.id}" title="Herramientas para desarrolladores">&lt;/&gt;</button>
          <button class="mini" data-tab="close" data-id="${t.id}" title="Cerrar">✕</button>
        </td>
      </tr>`).join("");
    $("processRows").innerHTML = m.processes.map((p) =>
      `<tr><td>${esc(p.kind)}</td><td>PID ${p.pid}</td><td>${mb(p.memory)}</td><td>${p.cpu.toFixed(1)} %</td></tr>`).join("");
  });
  $("monitorRows").addEventListener("click", (e) => {
    const b = e.target.closest("[data-tab]");
    if (b) phast.send("tab." + b.dataset.tab, { id: b.dataset.id });
  });
  phast.on("blockedList", (m) => {
    $("blockedTitle").textContent = `Bloqueado en "${m.title}" (${m.urls.length})`;
    $("blockedList").textContent = m.urls.join("\n") || "Nada";
    $("blockedDialog").showModal();
  });

  // ------------------------------------------------------------ shortcuts
  let waitingFor = null;
  phast.on("shortcuts", (m) => {
    $("shortcutRows").innerHTML = m.items.map((s) => `
      <div class="shortcut-row">
        <span class="name">${esc(s.name)}</span>
        <span>${s.shortcuts.length ? s.shortcuts.map((k) => `<span class="kbd ${s.custom ? "custom" : ""}">${esc(k)}</span>`).join("") : '<span class="muted">sin atajo</span>'}</span>
        <button class="mini" data-sc="change" data-id="${s.id}">Cambiar</button>
        <button class="mini" data-sc="clear" data-id="${s.id}" title="Dejar sin atajo">Quitar</button>
        ${s.custom ? `<button class="mini" data-sc="reset" data-id="${s.id}" title="Volver al atajo de serie">Restablecer</button>` : ""}
      </div>`).join("");
  });
  $("shortcutRows").addEventListener("click", (e) => {
    const b = e.target.closest("[data-sc]");
    if (!b) return;
    const id = b.dataset.id;
    if (b.dataset.sc === "change") {
      waitingFor = id;
      b.closest(".shortcut-row").querySelector("span:nth-child(2)").innerHTML = '<span class="kbd waiting">Pulsa la combinación…</span>';
      phast.send("shortcuts.capture", { command: id });
      b.blur();
    }
    if (b.dataset.sc === "clear") phast.send("shortcuts.set", { command: id, shortcut: "" });
    if (b.dataset.sc === "reset") phast.send("shortcuts.reset", { command: id });
  });

  // Keys without Ctrl/Alt (e.g. "K" or "F9") reach the page instead of the window: translate them here.
  const KEY_NAMES = { ArrowLeft: "Left", ArrowRight: "Right", ArrowUp: "Up", ArrowDown: "Down", " ": "Space", Enter: "Return",
    Escape: "Escape", Backspace: "Back", Delete: "Delete", Insert: "Insert", Home: "Home", End: "End", PageUp: "PageUp", PageDown: "Next", Tab: "Tab" };
  document.addEventListener("keydown", (e) => {
    if (!waitingFor || ["Control", "Shift", "Alt", "Meta"].includes(e.key)) return;
    e.preventDefault();
    if (e.key === "Escape" && !e.ctrlKey && !e.altKey && !e.shiftKey) { finishCapture(null); return; }
    let key = KEY_NAMES[e.key] || (/^Key[A-Z]$/.test(e.code) ? e.code.slice(3) : /^Digit\d$/.test(e.code) ? e.code.slice(5) : /^F\d+$/.test(e.key) ? e.key : null);
    if (!key) return;
    finishCapture([e.ctrlKey && "Ctrl", e.altKey && "Alt", e.shiftKey && "Shift", key].filter(Boolean).join("+"));
  }, true);
  phast.on("shortcutCaptured", (m) => finishCapture(m.shortcut));
  function finishCapture(shortcut) {
    const id = waitingFor;
    waitingFor = null;
    if (id && shortcut) phast.send("shortcuts.set", { command: id, shortcut });
    else phast.send("shortcuts.get");
  }

  // ------------------------------------------------------------ advanced ad blocking
  function addRule(rule) {
    const t = $("customRules");
    t.value = (t.value.trim() ? t.value.trim() + "\n" : "") + rule;
  }
  const domain = () => $("domainInput").value.trim().replace(/^https?:\/\//, "").replace(/\/.*$/, "").toLowerCase();

  // ------------------------------------------------------------ per-site tweaks
  function renderSites() {
    const sites = settings.siteOverrides || {};
    const dark = settings.darkSites || [];
    $("siteRows").innerHTML = Object.keys(sites).sort().map((site) => {
      const o = sites[site];
      return `<div class="card site-card" data-site="${esc(site)}">
        <div class="group-head"><strong style="flex:1">${esc(site)}</strong>
          <button class="btn" data-site-action="save">Guardar</button>
          <button class="btn" data-site-action="delete">Quitar</button></div>
        <div class="grid">
          <span>JavaScript</span><label><input type="checkbox" data-f="js-off" ${o.javaScriptDisabled ? "checked" : ""}> Desactivado en esta web</label>
          <span>Modo oscuro</span><label><input type="checkbox" data-f="dark" ${dark.includes(site) ? "checked" : ""}> Forzar modo oscuro</label>
          <span>User agent</span><div><select data-f="ua-preset">${USER_AGENTS.map(([v, n]) =>
            `<option value="${esc(v)}" ${v === o.userAgent ? "selected" : ""}>${esc(n)}</option>`).join("")}
            ${o.userAgent && !USER_AGENTS.some(([v]) => v === o.userAgent) ? `<option value="${esc(o.userAgent)}" selected>Personalizado</option>` : ""}</select>
            <input data-f="ua" value="${esc(o.userAgent)}" placeholder="O escribe uno a mano" style="margin-top:6px"></div>
          <span>Mi CSS</span><textarea data-f="css" class="code" spellcheck="false" placeholder="body { font-size: 18px; }">${esc(o.css)}</textarea>
          <span>Mi JavaScript</span><textarea data-f="jsCode" class="code" spellcheck="false" placeholder="console.log('hola');">${esc(o.js)}</textarea>
        </div></div>`;
    }).join("");
  }
  $("siteRows").addEventListener("change", (e) => {
    if (e.target.dataset.f === "ua-preset") e.target.closest(".site-card").querySelector('[data-f="ua"]').value = e.target.value;
  });
  $("siteRows").addEventListener("click", (e) => {
    const b = e.target.closest("[data-site-action]");
    if (!b) return;
    const card = b.closest(".site-card"), site = card.dataset.site;
    if (b.dataset.siteAction === "delete") { phast.send("site.set", { site, value: null }); return; }
    const f = (name) => card.querySelector(`[data-f="${name}"]`);
    phast.send("site.set", { site, value: {
      javaScriptDisabled: f("js-off").checked, userAgent: f("ua").value.trim(), css: f("css").value, js: f("jsCode").value } });
    const dark = new Set(settings.darkSites || []);
    if (f("dark").checked !== dark.has(site)) {
      f("dark").checked ? dark.add(site) : dark.delete(site);
      phast.send("settings.set", { key: "darkSites", value: [...dark] });
    }
    toast("Guardado. Se aplica al recargar la web.");
  });

  // ------------------------------------------------------------ flags
  const flagTokens = () => $("flagsText").value.split(/\s+/).filter(Boolean);
  function renderFlags() {
    const on = new Set(flagTokens());
    $("flagRows").innerHTML = FLAGS.map(([flag, name, help]) => `
      <div class="row"><div class="label">${esc(name)}<small>${esc(help)} <code>${esc(flag)}</code></small></div>
      <label class="toggle"><input type="checkbox" data-flag="${esc(flag)}" ${on.has(flag) ? "checked" : ""}><span></span></label></div>`).join("");
  }
  $("flagRows").addEventListener("change", (e) => {
    const flag = e.target.dataset.flag;
    if (!flag) return;
    const tokens = flagTokens().filter((t) => t !== flag);
    if (e.target.checked) tokens.push(flag);
    $("flagsText").value = tokens.join(" ");
    phast.send("settings.set", { key: "browserFlags", value: $("flagsText").value });
  });

  // ------------------------------------------------------------ buttons
  document.getElementById("devPanels").addEventListener("click", (e) => {
    const action = e.target.closest("[data-dev]")?.dataset.dev;
    switch (action) {
      case "restart": phast.send("dev.restart"); break;
      case "export": phast.send("dev.export"); break;
      case "import": phast.send("dev.import"); break;
      case "openData": phast.send("dev.openData"); break;
      case "resetShortcuts": phast.send("shortcuts.reset", { command: "" }); break;
      case "blockDomain": if (domain()) addRule(`||${domain()}^`); break;
      case "allowDomain": if (domain()) addRule(`@@||${domain()}^$document`); break;
      case "saveFilters":
        phast.send("adblock.setCustom", { rules: $("customRules").value, lists: $("customLists").value.split("\n") });
        toast("Filtros guardados. Se aplican en las páginas que abras a partir de ahora.");
        break;
      case "saveFlags":
        phast.send("settings.set", { key: "browserFlags", value: $("flagsText").value.trim() });
        renderFlags();
        break;
      case "addSite": {
        const site = $("siteInput").value.trim().replace(/^https?:\/\//, "").replace(/\/.*$/, "").replace(/^www\./, "").toLowerCase();
        if (site) phast.send("site.set", { site, value: { javaScriptDisabled: false, userAgent: "", css: "", js: "" } });
        $("siteInput").value = "";
        break;
      }
    }
  });

  let toastTimer;
  function toast(text) {
    const t = $("toast");
    t.textContent = text;
    t.classList.add("show");
    clearTimeout(toastTimer);
    toastTimer = setTimeout(() => t.classList.remove("show"), 3500);
  }
  phast.on("toast", (m) => toast(m.text));
})();
