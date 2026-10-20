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
    document.getElementById("version").textContent = msg.version || "";
    document.getElementById("engineVersion").textContent = msg.engineVersion || "";
  });

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

  phast.send("settings.get");
  phast.send("bookmarks.sources");
})();
