// phast://bookmarks — bookmark manager. Data comes from the browser (Bridge/BookmarksBridge.cs).
(function () {
  const FOLDER_SVG = '<svg class="fav" viewBox="0 0 24 24"><path fill="currentColor" d="M10 4H4c-1.1 0-2 .9-2 2v12c0 1.1.9 2 2 2h16c1.1 0 2-.9 2-2V8c0-1.1-.9-2-2-2h-8l-2-2z"/></svg>';
  const GLOBE_SVG = '<svg class="fav" viewBox="0 0 24 24"><path fill="currentColor" opacity=".7" d="M12 2a10 10 0 1 0 0 20 10 10 0 0 0 0-20zm6.93 6h-2.95a15.7 15.7 0 0 0-1.38-3.56A8.03 8.03 0 0 1 18.93 8zM12 4.04c.83 1.2 1.48 2.53 1.91 3.96h-3.82c.43-1.43 1.08-2.76 1.91-3.96zM4.26 14a8.2 8.2 0 0 1 0-4h3.38a16.5 16.5 0 0 0 0 4H4.26zm.81 2h2.95c.32 1.25.78 2.45 1.38 3.56A7.99 7.99 0 0 1 5.07 16zM8.02 8H5.07a7.99 7.99 0 0 1 4.33-3.56A15.7 15.7 0 0 0 8.02 8zM12 19.96c-.83-1.2-1.48-2.53-1.91-3.96h3.82c-.43 1.43-1.08 2.76-1.91 3.96zM14.34 14H9.66a14.7 14.7 0 0 1 0-4h4.68a14.7 14.7 0 0 1 0 4zm.25 5.56c.6-1.11 1.06-2.31 1.38-3.56h2.95a8.03 8.03 0 0 1-4.33 3.56zM16.36 14a16.5 16.5 0 0 0 0-4h3.38a8.2 8.2 0 0 1 0 4h-3.38z"/></svg>';
  const DOTS_SVG = '<svg width="18" height="18" viewBox="0 0 24 24"><circle cx="12" cy="5" r="2"/><circle cx="12" cy="12" r="2"/><circle cx="12" cy="19" r="2"/></svg>';

  let data = null;           // { bar, other, icons }
  let sources = [];          // browsers we can import from
  let selected = "bar";      // selected folder id
  const expanded = new Set(["bar", "other"]);
  let query = "";

  const $ = (id) => document.getElementById(id);
  const esc = (s) => String(s ?? "").replace(/[&<>"']/g, (c) => ({ "&": "&amp;", "<": "&lt;", ">": "&gt;", '"': "&quot;", "'": "&#39;" }[c]));

  // ------------------------------------------------------------ tree helpers
  const roots = () => [data.bar, data.other];
  function walk(node, fn, parent = null) { fn(node, parent); (node.children || []).forEach((c) => walk(c, fn, node)); }
  function find(id) { let r = null; roots().forEach((n) => walk(n, (x) => { if (x.id === id) r = x; })); return r; }
  function parentOf(id) { let r = null; roots().forEach((n) => walk(n, (x, p) => { if (x.id === id) r = p; })); return r; }
  function pathTo(id) { const p = []; let n = find(id); while (n) { p.unshift(n); n = parentOf(n.id); } return p; }
  const isFolder = (n) => Array.isArray(n.children);
  function hostOf(url) { try { return new URL(url).hostname.toLowerCase(); } catch { return ""; } }
  function prettyUrl(url) { return (url || "").replace(/^https?:\/\/(www\.)?/, "").replace(/\/$/, ""); }
  function icon(n) {
    if (isFolder(n)) return FOLDER_SVG;
    const src = data.icons[hostOf(n.url)];
    return src ? `<img class="fav" src="${src}" alt="">` : GLOBE_SVG;
  }

  // ------------------------------------------------------------ rendering
  function render() {
    if (!find(selected)) selected = "bar";
    renderTree();
    renderList();
  }

  function renderTree() {
    const out = [];
    const add = (n, depth) => {
      const subfolders = n.children.filter(isFolder);
      const open = expanded.has(n.id);
      out.push(`<div class="tree-item ${n.id === selected && !query ? "selected" : ""}" data-id="${n.id}" style="padding-left:${12 + depth * 20}px">
        <span class="twisty" data-twisty="${n.id}">${subfolders.length ? (open ? "▾" : "▸") : ""}</span>
        ${FOLDER_SVG}<span class="name">${esc(n.title)}</span></div>`);
      if (open) subfolders.forEach((c) => add(c, depth + 1));
    };
    roots().forEach((r) => add(r, 0));
    $("tree").innerHTML = out.join("");
  }

  function renderList() {
    let items;
    if (query) {
      const words = query.toLowerCase().split(/\s+/).filter(Boolean);
      items = [];
      roots().forEach((r) => walk(r, (n) => {
        if (n.url && words.every((w) => (n.title + " " + n.url).toLowerCase().includes(w))) items.push(n);
      }));
      $("path").textContent = `${items.length} resultado(s) para "${query}"`;
    } else {
      const folder = find(selected);
      items = folder.children;
      $("path").textContent = pathTo(selected).map((n) => n.title).join("  ›  ");
    }

    if (items.length === 0) {
      $("list").innerHTML = `<div class="empty">${query ? "No se han encontrado marcadores" : "Esta carpeta está vacía"}</div>`;
      return;
    }
    $("list").innerHTML = items.map((n) => `
      <a class="row" draggable="${query ? "false" : "true"}" data-id="${n.id}"
         href="${isFolder(n) ? "#" : esc(n.url)}" title="${esc(n.url || n.title)}">
        ${icon(n)}
        <span class="title">${esc(n.title || n.url)}</span>
        <span class="url">${isFolder(n) ? "" : esc(prettyUrl(n.url))}</span>
        <button class="icon-btn" data-menu="${n.id}" title="Más acciones">${DOTS_SVG}</button>
      </a>`).join("");
  }

  // ------------------------------------------------------------ menus
  const menu = $("menu");
  function openMenu(anchor, entries) {
    menu.innerHTML = entries.map((e, i) =>
      e === "-" ? "<hr>" : e.label ? `<div class="label">${esc(e.label)}</div>` : `<button data-i="${i}">${esc(e.text)}</button>`).join("");
    menu.querySelectorAll("button").forEach((b) => b.addEventListener("click", () => { closeMenu(); entries[b.dataset.i].run(); }));
    menu.classList.add("open");
    const r = anchor.getBoundingClientRect();
    const w = menu.offsetWidth, h = menu.offsetHeight;
    menu.style.left = Math.max(8, Math.min(r.right - w, innerWidth - w - 8)) + "px";
    menu.style.top = (r.bottom + h + 8 > innerHeight ? Math.max(8, r.top - h) : r.bottom) + "px";
  }
  function closeMenu() { menu.classList.remove("open"); }
  document.addEventListener("click", (e) => { if (!menu.contains(e.target) && !e.target.closest("[data-menu],#mainMenuBtn")) closeMenu(); });
  document.addEventListener("keydown", (e) => { if (e.key === "Escape") closeMenu(); });

  function itemMenu(n, anchor) {
    const entries = isFolder(n) ? [
      { text: "Abrir todo", run: () => phast.send("bookmarks.openAll", { id: n.id }) },
      { text: "Cambiar nombre", run: () => editDialog(n) },
      "-",
      { text: "Eliminar", run: () => phast.send("bookmarks.remove", { id: n.id }) },
    ] : [
      { text: "Abrir en una pestaña nueva", run: () => window.open(n.url, "_blank") },
      { text: "Editar", run: () => editDialog(n) },
      { text: "Copiar URL", run: () => navigator.clipboard.writeText(n.url).then(() => toast("URL copiada")) },
      "-",
      { text: "Eliminar", run: () => phast.send("bookmarks.remove", { id: n.id }) },
    ];
    openMenu(anchor, entries);
  }

  $("mainMenuBtn").addEventListener("click", (e) => {
    const target = query ? "bar" : selected;
    const entries = [
      { text: "Añadir marcador nuevo", run: () => editDialog(null, target, false) },
      { text: "Añadir carpeta nueva", run: () => editDialog(null, target, true) },
      "-",
      { label: "Importar" },
      ...sources.map((s) => ({ text: s.label, run: () => phast.send("bookmarks.importBrowser", { id: s.id }) })),
      { text: "Desde un archivo HTML...", run: () => phast.send("bookmarks.importHtml") },
      "-",
      { text: "Exportar marcadores...", run: () => phast.send("bookmarks.exportHtml") },
    ];
    openMenu(e.currentTarget, entries);
  });

  // ------------------------------------------------------------ edit dialog
  let dialogAction = null;
  function editDialog(node, parentId, folder) {
    const isNew = !node;
    const asFolder = node ? isFolder(node) : folder;
    $("dlgTitle").textContent = isNew ? (asFolder ? "Añadir carpeta" : "Añadir marcador") : (asFolder ? "Cambiar nombre" : "Editar marcador");
    $("dlgName").value = node ? node.title : (asFolder ? "Nueva carpeta" : "");
    $("dlgUrl").value = node && !asFolder ? node.url : "";
    $("dlgUrlBox").style.display = asFolder ? "none" : "";
    dialogAction = () => {
      const title = $("dlgName").value.trim(), url = $("dlgUrl").value.trim();
      if (isNew && asFolder) phast.send("bookmarks.addFolder", { parentId, title });
      else if (isNew) { if (url) phast.send("bookmarks.add", { parentId, title, url }); }
      else if (asFolder) phast.send("bookmarks.update", { id: node.id, title });
      else phast.send("bookmarks.update", { id: node.id, title, url });
    };
    $("dialog").showModal();
    $("dlgName").select();
  }
  $("dlgForm").addEventListener("submit", () => { dialogAction && dialogAction(); });
  $("dlgCancel").addEventListener("click", () => $("dialog").close());

  // ------------------------------------------------------------ toast
  let toastTimer;
  function toast(text) {
    const t = $("toast");
    t.textContent = text;
    t.classList.add("show");
    clearTimeout(toastTimer);
    toastTimer = setTimeout(() => t.classList.remove("show"), 3500);
  }

  // ------------------------------------------------------------ events
  $("tree").addEventListener("click", (e) => {
    const tw = e.target.closest("[data-twisty]");
    if (tw && tw.textContent.trim()) {
      const id = tw.dataset.twisty;
      expanded.has(id) ? expanded.delete(id) : expanded.add(id);
      renderTree();
      return;
    }
    const item = e.target.closest(".tree-item");
    if (!item) return;
    selectFolder(item.dataset.id);
  });

  function selectFolder(id) {
    selected = id;
    query = "";
    $("search").value = "";
    pathTo(id).forEach((n) => expanded.add(n.id));
    history.replaceState(null, "", "#folder=" + id);
    render();
  }

  $("list").addEventListener("click", (e) => {
    const btn = e.target.closest("[data-menu]");
    if (btn) { e.preventDefault(); itemMenu(find(btn.dataset.menu), btn); return; }
    const row = e.target.closest(".row");
    if (row && isFolder(find(row.dataset.id))) { e.preventDefault(); selectFolder(row.dataset.id); }
  });

  $("search").addEventListener("input", (e) => { query = e.target.value.trim(); render(); });

  // ------------------------------------------------------------ drag & drop (reorder / move to folder)
  let dragId = null;
  const clearDrop = () => document.querySelectorAll(".drop-before,.drop-after,.drop-into,.drop").forEach((el) => el.classList.remove("drop-before", "drop-after", "drop-into", "drop"));

  $("list").addEventListener("dragstart", (e) => {
    const row = e.target.closest(".row");
    if (!row) return;
    dragId = row.dataset.id;
    e.dataTransfer.effectAllowed = "move";
    e.dataTransfer.setData("text/plain", dragId);
  });
  $("list").addEventListener("dragover", (e) => {
    const row = e.target.closest(".row");
    if (!dragId || !row || row.dataset.id === dragId) return;
    e.preventDefault();
    clearDrop();
    const r = row.getBoundingClientRect(), y = (e.clientY - r.top) / r.height;
    const folder = isFolder(find(row.dataset.id));
    row.classList.add(folder && y > 0.25 && y < 0.75 ? "drop-into" : y < 0.5 ? "drop-before" : "drop-after");
  });
  $("list").addEventListener("drop", (e) => {
    const row = e.target.closest(".row");
    if (!dragId || !row) return;
    e.preventDefault();
    const target = row.dataset.id;
    if (row.classList.contains("drop-into")) phast.send("bookmarks.move", { id: dragId, parentId: target, index: 1e6 });
    else {
      const siblings = find(selected).children.map((n) => n.id);
      const index = siblings.indexOf(target) + (row.classList.contains("drop-after") ? 1 : 0);
      phast.send("bookmarks.move", { id: dragId, parentId: selected, index });
    }
    clearDrop();
  });
  $("tree").addEventListener("dragover", (e) => {
    const item = e.target.closest(".tree-item");
    if (!dragId || !item) return;
    e.preventDefault();
    clearDrop();
    item.classList.add("drop");
  });
  $("tree").addEventListener("drop", (e) => {
    const item = e.target.closest(".tree-item");
    if (!dragId || !item) return;
    e.preventDefault();
    phast.send("bookmarks.move", { id: dragId, parentId: item.dataset.id, index: 1e6 });
    clearDrop();
  });
  document.addEventListener("dragend", () => { dragId = null; clearDrop(); });

  // ------------------------------------------------------------ messages from the browser
  phast.on("bookmarks", (msg) => { data = msg; render(); });
  phast.on("bookmarkSources", (msg) => { sources = msg.sources; });
  phast.on("bookmarksImported", (msg) => toast(msg.count > 0 ? `Se han importado ${msg.count} marcadores ${msg.detail}` : msg.detail || "No se ha importado nada"));
  phast.on("bookmarksExported", (msg) => toast(`Se han exportado ${msg.count} marcadores`));

  const m = location.hash.match(/folder=([\w-]+)/);
  if (m) selected = m[1];
  phast.send("bookmarks.get");
  phast.send("bookmarks.sources");
})();
