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
    if (document.activeElement !== document.getElementById("startupPages"))
      document.getElementById("startupPages").value = (msg.settings.startupPages || []).join("\n");
    renderGroups(msg.settings.pageGroups);
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

  // ---- page groups editor
  const GROUP_COLORS = [
    ["grey", "#5F6368", "#DADCE0"], ["blue", "#1A73E8", "#8AB4F8"], ["red", "#D93025", "#F28B82"],
    ["yellow", "#E8A600", "#FDD663"], ["green", "#188038", "#81C995"], ["pink", "#D01884", "#FF8BCB"],
    ["purple", "#A142F4", "#C58AF9"], ["cyan", "#007B83", "#78D9EC"], ["orange", "#FA903E", "#FCAD70"],
  ];
  const darkScheme = () => matchMedia("(prefers-color-scheme: dark)").matches;
  const colorHex = (id) => (GROUP_COLORS.find((c) => c[0] === id) || GROUP_COLORS[1])[darkScheme() ? 2 : 1];
  let groups = [];
  let lastSentGroups = "";
  let saveTimer = 0;

  function saveGroups() {
    clearTimeout(saveTimer);
    saveTimer = setTimeout(() => {
      lastSentGroups = JSON.stringify(groups);
      phast.send("settings.set", { key: "pageGroups", value: groups });
    }, 350);
  }

  function renderGroups(fromBrowser) {
    // Don't redraw (and lose the cursor) when the update is just the echo of our own change.
    if (fromBrowser && JSON.stringify(fromBrowser) === lastSentGroups) return;
    groups = fromBrowser || groups;
    const box = document.getElementById("pageGroups");
    box.innerHTML = groups.map((g, i) => `
      <div class="card group-card" data-i="${i}">
        <div class="group-head">
          <input value="${esc(g.name)}" placeholder="Nombre del grupo" data-field="name" aria-label="Nombre del grupo">
          <button class="btn" data-action="open" ${g.urls.length ? "" : "disabled"}>Abrir</button>
          <button class="btn" data-action="delete">Eliminar</button>
        </div>
        <div class="swatches">${GROUP_COLORS.map((c) =>
          `<button class="swatch ${c[0] === g.color ? "selected" : ""}" data-color="${c[0]}" title="${c[0]}" style="background:${colorHex(c[0])}"></button>`).join("")}</div>
        <textarea data-field="urls" placeholder="Una web por línea, por ejemplo:&#10;gmail.com&#10;https://calendar.google.com">${esc(g.urls.join("\n"))}</textarea>
        <div class="group-foot">
          <span class="muted">${g.urls.length} ${g.urls.length === 1 ? "página" : "páginas"}</span>
          <button class="btn" data-action="tabs">Añadir las pestañas abiertas</button>
        </div>
      </div>`).join("");
  }

  const pageGroupsBox = document.getElementById("pageGroups");
  pageGroupsBox.addEventListener("input", (e) => {
    const card = e.target.closest(".group-card");
    if (!card) return;
    const g = groups[+card.dataset.i];
    if (e.target.dataset.field === "name") g.name = e.target.value;
    if (e.target.dataset.field === "urls") {
      g.urls = e.target.value.split("\n").map((u) => u.trim()).filter(Boolean);
      card.querySelector(".muted").textContent = `${g.urls.length} ${g.urls.length === 1 ? "página" : "páginas"}`;
      card.querySelector('[data-action="open"]').disabled = g.urls.length === 0;
    }
    saveGroups();
  });
  pageGroupsBox.addEventListener("click", (e) => {
    const card = e.target.closest(".group-card");
    if (!card) return;
    const i = +card.dataset.i, g = groups[i];
    if (e.target.dataset.color) { g.color = e.target.dataset.color; saveGroups(); renderGroups(); }
    const action = e.target.dataset.action;
    if (action === "delete") { groups.splice(i, 1); saveGroups(); renderGroups(); }
    if (action === "open") phast.send("groups.open", { id: g.id });
    if (action === "tabs") { pendingTabsFor = g.id; phast.send("groups.currentTabs"); }
  });
  let pendingTabsFor = null;
  phast.on("currentTabs", (msg) => {
    const g = groups.find((x) => x.id === pendingTabsFor);
    if (!g) return;
    for (const u of msg.urls) if (!g.urls.includes(u)) g.urls.push(u);
    saveGroups();
    renderGroups();
  });
  document.getElementById("addGroup").addEventListener("click", () => {
    const used = groups.map((g) => g.color);
    const color = (GROUP_COLORS.slice(1).find((c) => !used.includes(c[0])) || GROUP_COLORS[1])[0];
    groups.push({ id: Math.random().toString(36).slice(2, 10), name: "", color, urls: [] });
    saveGroups();
    renderGroups();
    pageGroupsBox.querySelector(".group-card:last-child input").focus();
  });

  // ---- send changes back
  for (const el of controls()) {
    el.addEventListener("change", () => {
      const value = el.type === "checkbox" ? el.checked : el.type === "number" ? Math.max(0, parseInt(el.value || "0", 10)) : el.value;
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

  // ---- startup pages, downloads, links to internal pages
  document.getElementById("startupPages").addEventListener("change", (e) => {
    const pages = e.target.value.split("\n").map((u) => u.trim()).filter(Boolean);
    phast.send("settings.set", { key: "startupPages", value: pages });
    if (pages.length) phast.send("settings.set", { key: "startup", value: "pages" });
  });
  phast.on("downloads", (m) => { document.getElementById("downloadFolder").textContent = m.folder; });
  document.getElementById("chooseFolder").addEventListener("click", () => phast.send("downloads.chooseFolder"));
  document.querySelectorAll("[data-open]").forEach((b) => b.addEventListener("click", () => phast.send("openPage", { url: b.dataset.open })));

  phast.send("downloads.get");
  phast.send("settings.get");
  phast.send("bookmarks.sources");
  phast.send("adblock.status");
})();
