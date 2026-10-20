// phast://settings — every control with data-setting="<jsonName>" is bound to that option automatically.
(function () {
  const controls = () => document.querySelectorAll("[data-setting]");

  // ---- receive settings from the browser and fill the controls
  phast.on("settings", (msg) => {
    const engines = document.getElementById("engines");
    if (engines.options.length === 0) {
      for (const e of msg.engines) engines.add(new Option(e.name, e.id));
    }
    for (const el of controls()) {
      const value = msg.settings[el.dataset.setting];
      if (el.type === "checkbox") el.checked = !!value;
      else if (el.type === "radio") el.checked = el.value === value;
      else el.value = value;
    }
    renderDarkSites(msg.settings);
    document.getElementById("version").textContent = msg.version || "";
    document.getElementById("engineVersion").textContent = msg.engineVersion || "";
  });

  // ---- dark mode: sites with dark mode (or excluded from "all sites")
  function renderDarkSites(s) {
    const all = s.darkAllSites;
    const list = all ? s.darkExcludedSites : s.darkSites;
    document.getElementById("darkListTitle").textContent = all ? "Webs sin modo oscuro" : "Webs con modo oscuro";
    const box = document.getElementById("darkSites");
    const safe = (t) => String(t).replace(/[&<>"']/g, (c) => ({ "&": "&amp;", "<": "&lt;", ">": "&gt;", '"': "&quot;", "'": "&#39;" }[c]));
    box.innerHTML = list.length === 0
      ? `<div class="row"><div class="label"><small>${all ? "Ninguna: todas las webs se ven en oscuro." : "Ninguna todavía. Pulsa la luna de la barra en una web para oscurecerla."}</small></div></div>`
      : list.map((d) => `<div class="row"><div class="label">${safe(d)}</div><button class="btn" data-dark-remove="${safe(d)}">Quitar</button></div>`).join("");
    box.querySelectorAll("[data-dark-remove]").forEach((b) =>
      b.addEventListener("click", () => phast.send("dark.remove", { site: b.dataset.darkRemove })));
  }

  // ---- send changes back
  for (const el of controls()) {
    el.addEventListener("change", () => {
      const value = el.type === "checkbox" ? el.checked : el.value;
      phast.send("settings.set", { key: el.dataset.setting, value });
    });
  }

  // ---- left navigation: highlight the section in view
  const links = [...document.querySelectorAll("#nav a")];
  const highlight = () => {
    let current = links[0];
    for (const a of links) {
      const s = document.querySelector(a.getAttribute("href"));
      if (s && s.getBoundingClientRect().top < 120) current = a;
    }
    links.forEach((a) => a.classList.toggle("active", a === current));
  };
  window.addEventListener("scroll", highlight, { passive: true });
  highlight();

  // ---- search box: show only rows/sections that match
  document.getElementById("filter").addEventListener("input", (e) => {
    const q = e.target.value.trim().toLowerCase();
    let any = false;
    for (const section of document.querySelectorAll("main section")) {
      const rows = section.querySelectorAll(".row, .radio, .about");
      let sectionHit = section.querySelector("h2").textContent.toLowerCase().includes(q);
      let rowHit = false;
      rows.forEach((r) => {
        const hit = !q || sectionHit || r.textContent.toLowerCase().includes(q);
        r.classList.toggle("hidden", !hit);
        r.style.display = hit ? "" : "none";
        rowHit = rowHit || hit;
      });
      section.style.display = rowHit ? "" : "none";
      any = any || rowHit;
    }
    document.getElementById("noResults").style.display = any ? "none" : "block";
  });

  // ---- bookmarks: import / export
  let toastTimer;
  function toast(text) {
    const t = document.getElementById("toast");
    t.textContent = text;
    t.classList.add("show");
    clearTimeout(toastTimer);
    toastTimer = setTimeout(() => t.classList.remove("show"), 4000);
  }

  const importSelect = document.getElementById("importSource");
  const importBtn = document.getElementById("importBtn");
  phast.on("bookmarkSources", (msg) => {
    importSelect.innerHTML = "";
    for (const s of msg.sources) importSelect.add(new Option(s.label, s.id));
    if (msg.sources.length === 0) importSelect.add(new Option("No se ha encontrado ningún navegador", ""));
    importBtn.disabled = msg.sources.length === 0;
  });
  importBtn.addEventListener("click", () => {
    if (!importSelect.value) return;
    importBtn.disabled = true;
    phast.send("bookmarks.importBrowser", { id: importSelect.value });
  });
  phast.on("bookmarksImported", (msg) => {
    importBtn.disabled = false;
    toast(msg.count > 0 ? `Se han importado ${msg.count} marcadores ${msg.detail}` : msg.detail || "No se ha importado nada");
  });
  phast.on("bookmarksExported", (msg) => toast(`Se han exportado ${msg.count} marcadores`));
  document.getElementById("importHtmlBtn").addEventListener("click", () => phast.send("bookmarks.importHtml"));
  document.getElementById("exportHtmlBtn").addEventListener("click", () => phast.send("bookmarks.exportHtml"));
  document.getElementById("managerBtn").addEventListener("click", () => phast.send("openPage", { url: "phast://bookmarks" }));

  // ---- ad blocking: lists, status, allowlist
  const esc = (s) => String(s ?? "").replace(/[&<>"']/g, (c) => ({ "&": "&amp;", "<": "&lt;", ">": "&gt;", '"': "&quot;", "'": "&#39;" }[c]));
  phast.on("adblockStatus", (msg) => {
    const summary = document.getElementById("adblockSummary");
    summary.textContent = msg.updating ? "Descargando listas..."
      : msg.ready ? `${msg.networkRules.toLocaleString("es")} reglas activas · preparadas en ${msg.buildMs} ms · se actualizan solas cada 4 días`
      : "Las listas aún no se han descargado";
    if (msg.twitchVersion)
      document.getElementById("twitchInfo").textContent = `Usa vaft ${msg.twitchVersion}, de TwitchAdSolutions (licencia MIT)`;

    document.getElementById("filterLists").innerHTML = msg.lists.map((l) => `
      <div class="row">
        <div class="label">${esc(l.name)}<small>${esc(l.description)} · ${l.updated ? "actualizada el " + esc(l.updated) : "sin descargar"}</small></div>
        <label class="toggle"><input type="checkbox" data-list="${esc(l.id)}" ${l.enabled ? "checked" : ""}><span></span></label>
      </div>`).join("");
    document.querySelectorAll("[data-list]").forEach((el) =>
      el.addEventListener("change", () => phast.send("adblock.setList", { id: el.dataset.list, enabled: el.checked })));

    const allow = document.getElementById("allowlist");
    allow.innerHTML = msg.allowlist.length === 0
      ? `<div class="row"><div class="label"><small>Ninguno. Puedes desactivar el bloqueo en una web desde el escudo de la barra de herramientas.</small></div></div>`
      : msg.allowlist.map((h) => `<div class="row"><div class="label">${esc(h)}</div><button class="btn" data-unallow="${esc(h)}">Volver a bloquear</button></div>`).join("");
    allow.querySelectorAll("[data-unallow]").forEach((b) =>
      b.addEventListener("click", () => phast.send("adblock.unallow", { host: b.dataset.unallow })));
  });
  document.getElementById("adblockUpdate").addEventListener("click", () => phast.send("adblock.update"));

  phast.send("settings.get");
  phast.send("bookmarks.sources");
  phast.send("adblock.status");
})();
